#!/usr/bin/env bash
# ─────────────────────────────────────────────────────────────────────────────
# mysql-backup.sh
# Phase 5: Renamed and rewritten from pg-backup.sh.
# FIX BACKUP-01: Encrypt dump with AES-256-CBC (PBKDF2, 600 000 iterations)
# so on-disk files match the .sql.gz.enc format documented in BackupGuide.md
# and referenced in docker-compose.yml and docker-compose.backup.yml.
# The BACKUP_ENCRYPTION_KEY env var is required; the script aborts if it is
# absent so an unencrypted backup is never silently written.
#
# Usage (manual):
#   BACKUP_ENCRYPTION_KEY=<key> ./scripts/mysql-backup.sh
#
# Usage (automated via cron — run as root or the docker user):
#   0 2 * * * /path/to/hrms/scripts/mysql-backup.sh >> /var/log/hrms-backup.log 2>&1
#
# Decrypt a backup:
#   openssl enc -d -aes-256-cbc -pbkdf2 -iter 600000 \
#     -pass pass:"$BACKUP_ENCRYPTION_KEY" \
#     -in hrms_YYYYMMDD_HHMMSS.sql.gz.enc | gunzip > hrms_restored.sql
# ─────────────────────────────────────────────────────────────────────────────
set -euo pipefail

# ── Configuration ─────────────────────────────────────────────────────────────
ENV_FILE="$(dirname "$0")/../.env"
[[ -f "$ENV_FILE" ]] && source "$ENV_FILE"

BACKUP_DIR="${BACKUP_DIR:-$(dirname "$0")/../backups}"
RETAIN_DAYS="${RETAIN_DAYS:-14}"
DB="${MYSQL_DATABASE:-hrms_db}"
USER="${MYSQL_USER:-hrms}"
MYSQL_HOST="${MYSQL_HOST:-mysql}"
CONTAINER="$(docker compose -f "$(dirname "$0")/../docker-compose.yml" ps -q mysql 2>/dev/null | head -1)"
TIMESTAMP=$(date -u '+%Y%m%d_%H%M%S')

# FIX BACKUP-01: Encrypted output file (.sql.gz.enc matches documented format)
BACKUP_FILE="${BACKUP_DIR}/hrms_${TIMESTAMP}.sql.gz.enc"

# ── Guard: encryption key is mandatory ────────────────────────────────────────
# Never produce an unencrypted backup. Fail fast so the operator knows
# immediately rather than discovering unencrypted files on disk.
if [[ -z "${BACKUP_ENCRYPTION_KEY:-}" ]]; then
  echo "[$(date -u '+%Y-%m-%d %H:%M UTC')] FATAL: BACKUP_ENCRYPTION_KEY is not set. Aborting backup." >&2
  echo "[$(date -u '+%Y-%m-%d %H:%M UTC')] Generate a key with: openssl rand -base64 48" >&2
  exit 1
fi

# ── Run ───────────────────────────────────────────────────────────────────────
mkdir -p "$BACKUP_DIR"

echo "[$(date -u '+%Y-%m-%d %H:%M UTC')] Starting encrypted backup → $BACKUP_FILE"

# Dump → gzip → AES-256-CBC encrypt (PBKDF2, 600 000 iterations)
# openssl enc is present in the amazon/aws-cli:2.17.0 image used by the backup
# service and in standard Linux distributions.
if [[ -n "$CONTAINER" ]]; then
  # Backup via running Docker container
  docker exec "$CONTAINER" \
    mysqldump -u "${USER}" -p"${MYSQL_PASSWORD}" "${DB}" \
  | gzip \
  | openssl enc -aes-256-cbc -pbkdf2 -iter 600000 \
      -pass pass:"${BACKUP_ENCRYPTION_KEY}" \
      -out "$BACKUP_FILE"
else
  # Fallback: direct mysqldump (requires mysql-client installed on host)
  mysqldump \
    -h "${MYSQL_HOST}" \
    -u "${USER}" \
    -p"${MYSQL_PASSWORD:-}" \
    "${DB}" \
  | gzip \
  | openssl enc -aes-256-cbc -pbkdf2 -iter 600000 \
      -pass pass:"${BACKUP_ENCRYPTION_KEY}" \
      -out "$BACKUP_FILE"
fi

SIZE=$(du -sh "$BACKUP_FILE" | cut -f1)
echo "[$(date -u '+%Y-%m-%d %H:%M UTC')] ✅ Encrypted backup complete — $SIZE written to $BACKUP_FILE"

# ── Prune old backups ─────────────────────────────────────────────────────────
# FIX BACKUP-01: Pattern updated from *.sql.gz to *.sql.gz.enc
PRUNED=$(find "$BACKUP_DIR" -name "hrms_*.sql.gz.enc" -mtime "+${RETAIN_DAYS}" -print -delete | wc -l)
[[ "$PRUNED" -gt 0 ]] && echo "[$(date -u '+%Y-%m-%d %H:%M UTC')] 🗑️  Pruned $PRUNED backup(s) older than ${RETAIN_DAYS} days."

echo "Done."
