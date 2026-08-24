#!/usr/bin/env bash
# =============================================================================
# backup-s3.sh — Off-site backup upload to S3 / S3-compatible storage
# Phase 5: Updated to call mysql-backup.sh instead of pg-backup.sh.
# Replaced POSTGRES_* variable references with MYSQL_* equivalents.
# Updated S3 prefix convention from hrms/postgres to hrms/mysql.
#
# What this script does:
#   1. Calls mysql-backup.sh to create a local mysqldump snapshot.
#   2. Uploads the snapshot to an S3 bucket (AWS S3, Backblaze B2, MinIO, etc.).
#   3. Verifies the upload succeeded by checking the remote object exists.
#   4. Prunes remote objects older than S3_RETAIN_DAYS (default 90 days).
#
# Prerequisites:
#   - aws CLI v2 installed: https://docs.aws.amazon.com/cli/latest/userguide/install-cliv2.html
#   - For non-AWS S3-compatible storage (Backblaze B2, MinIO, Cloudflare R2), set
#     AWS_ENDPOINT_URL in your .env (e.g. https://s3.us-west-001.backblazeb2.com).
#
# Required environment variables (set in .env):
#   S3_BUCKET                 Target bucket name, e.g. hrms-backups-prod
#   S3_PREFIX                 Key prefix, e.g. hrms/mysql (no trailing slash)
#   AWS_ACCESS_KEY_ID         IAM or B2 application key ID
#   AWS_SECRET_ACCESS_KEY     IAM secret key or B2 application key
#   AWS_DEFAULT_REGION        AWS region or "auto" for Backblaze/Cloudflare
#   S3_RETAIN_DAYS            Days to keep remote backups (default 90)
#
# Optional:
#   AWS_ENDPOINT_URL          Set for S3-compatible providers (Backblaze B2, MinIO, etc.)
#   BACKUP_DIR                Local backup directory (default: ../backups)
#
# Usage:
#   ./scripts/backup-s3.sh                    # interactive / cron
#
# Cron example (daily at 02:30 UTC — 30 min after mysql-backup.sh):
#   30 2 * * * /path/to/hrms/scripts/backup-s3.sh >> /var/log/hrms-backup-s3.log 2>&1
# =============================================================================
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ENV_FILE="${SCRIPT_DIR}/../.env"
[[ -f "$ENV_FILE" ]] && set -a && source "$ENV_FILE" && set +a

# ── Validate required variables ───────────────────────────────────────────────
MISSING=()
[[ -z "${S3_BUCKET:-}"             ]] && MISSING+=("S3_BUCKET")
[[ -z "${S3_PREFIX:-}"             ]] && MISSING+=("S3_PREFIX")
[[ -z "${AWS_ACCESS_KEY_ID:-}"     ]] && MISSING+=("AWS_ACCESS_KEY_ID")
[[ -z "${AWS_SECRET_ACCESS_KEY:-}" ]] && MISSING+=("AWS_SECRET_ACCESS_KEY")
[[ -z "${AWS_DEFAULT_REGION:-}"    ]] && MISSING+=("AWS_DEFAULT_REGION")
if [[ ${#MISSING[@]} -gt 0 ]]; then
    echo "ERROR: Missing required environment variables: ${MISSING[*]}"
    echo "       Set them in .env or as shell environment variables."
    exit 1
fi

# ── Configuration ─────────────────────────────────────────────────────────────
BACKUP_DIR="${BACKUP_DIR:-${SCRIPT_DIR}/../backups}"
S3_RETAIN_DAYS="${S3_RETAIN_DAYS:-90}"
TIMESTAMP=$(date -u '+%Y%m%d_%H%M%S')
LOG_PREFIX="[$(date -u '+%Y-%m-%d %H:%M UTC')]"

# Build aws CLI endpoint argument (empty for AWS; set for Backblaze/MinIO/R2)
ENDPOINT_ARG=""
if [[ -n "${AWS_ENDPOINT_URL:-}" ]]; then
    ENDPOINT_ARG="--endpoint-url ${AWS_ENDPOINT_URL}"
fi

# ── Step 1: Create a fresh local backup ───────────────────────────────────────
echo "${LOG_PREFIX} Running mysql-backup.sh to create local snapshot…"
bash "${SCRIPT_DIR}/mysql-backup.sh"

# Find the newest backup file (created by mysql-backup.sh)
LATEST_BACKUP=$(find "$BACKUP_DIR" -name "hrms_*.sql.gz.enc" -printf '%T@ %p\n' \
    | sort -n | tail -1 | awk '{print $2}')

if [[ -z "$LATEST_BACKUP" ]]; then
    echo "ERROR: No backup file found in ${BACKUP_DIR} after running mysql-backup.sh."
    exit 1
fi

BACKUP_FILENAME=$(basename "$LATEST_BACKUP")
S3_KEY="${S3_PREFIX}/${BACKUP_FILENAME}"
S3_URI="s3://${S3_BUCKET}/${S3_KEY}"

echo "${LOG_PREFIX} Uploading ${BACKUP_FILENAME} → ${S3_URI}"

# ── Step 2: Upload to S3 ──────────────────────────────────────────────────────
aws s3 cp "$LATEST_BACKUP" "$S3_URI" \
    ${ENDPOINT_ARG} \
    --storage-class STANDARD_IA \
    --sse AES256 \
    --no-progress

# ── Step 3: Verify upload ─────────────────────────────────────────────────────
echo "${LOG_PREFIX} Verifying upload…"
REMOTE_SIZE=$(aws s3api head-object \
    --bucket "$S3_BUCKET" \
    --key    "$S3_KEY" \
    ${ENDPOINT_ARG} \
    --query ContentLength \
    --output text 2>/dev/null || echo "0")

LOCAL_SIZE=$(stat -c%s "$LATEST_BACKUP")

if [[ "$REMOTE_SIZE" != "$LOCAL_SIZE" ]]; then
    echo "ERROR: Remote object size ${REMOTE_SIZE} does not match local file size ${LOCAL_SIZE}."
    echo "       The upload may be incomplete. Check S3 and retry."
    exit 1
fi

echo "${LOG_PREFIX} ✅ Upload verified — ${LOCAL_SIZE} bytes at ${S3_URI}"

# ── Step 4: Prune remote backups older than S3_RETAIN_DAYS ───────────────────
echo "${LOG_PREFIX} Pruning remote backups older than ${S3_RETAIN_DAYS} days…"

CUTOFF_DATE=$(date -u -d "${S3_RETAIN_DAYS} days ago" '+%Y-%m-%dT%H:%M:%SZ' 2>/dev/null \
    || date -u -v "-${S3_RETAIN_DAYS}d" '+%Y-%m-%dT%H:%M:%SZ')  # macOS fallback

PRUNED=0
while IFS= read -r KEY; do
    [[ -z "$KEY" ]] && continue
    aws s3 rm "s3://${S3_BUCKET}/${KEY}" ${ENDPOINT_ARG} --quiet
    echo "${LOG_PREFIX} 🗑️  Pruned remote: ${KEY}"
    ((PRUNED++))
done < <(
    aws s3api list-objects-v2 \
        --bucket "$S3_BUCKET" \
        --prefix "${S3_PREFIX}/" \
        ${ENDPOINT_ARG} \
        --query "Contents[?LastModified<='${CUTOFF_DATE}'].Key" \
        --output text 2>/dev/null | tr '\t' '\n' | grep -v '^None$' || true
)

echo "${LOG_PREFIX} Pruned ${PRUNED} remote backup(s) older than ${S3_RETAIN_DAYS} days."
echo "${LOG_PREFIX} Done."
