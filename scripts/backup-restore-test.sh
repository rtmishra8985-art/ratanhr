#!/usr/bin/env bash
# =============================================================================
# scripts/backup-restore-test.sh — RatanHR Backup & Restore Verification
#
# FIX BLOCKER-9: Provides a runnable, end-to-end backup-restore drill that
# satisfies the pre-go-live requirement for a tested restore procedure.
#
# What this script does:
#   1. Creates a backup of the source database using mysqldump + AES encryption
#   2. Restores the backup to a separate verification database
#   3. Compares row counts across all critical tables
#   4. Reports PASS or FAIL with evidence
#
# Usage (run against staging — never against production):
#   chmod +x scripts/backup-restore-test.sh
#   ./scripts/backup-restore-test.sh
#
# Required environment variables (set in .env or export before running):
#   MYSQL_HOST           — MySQL host (default: 127.0.0.1)
#   MYSQL_PORT           — MySQL port (default: 3306)
#   MYSQL_USER           — MySQL user with SELECT + CREATE DATABASE privileges
#   MYSQL_PASSWORD       — MySQL password
#   MYSQL_DATABASE       — Source database name (e.g. hrms_staging)
#   BACKUP_ENCRYPTION_KEY — AES-256 encryption key (same as BackupGuide.md)
#
# Exit codes:
#   0 — All checks passed (PASS)
#   1 — One or more checks failed (FAIL)
# =============================================================================

set -euo pipefail

MYSQL_HOST="${MYSQL_HOST:-127.0.0.1}"
MYSQL_PORT="${MYSQL_PORT:-3306}"
MYSQL_USER="${MYSQL_USER:?Must set MYSQL_USER}"
MYSQL_PASSWORD="${MYSQL_PASSWORD:?Must set MYSQL_PASSWORD}"
MYSQL_DATABASE="${MYSQL_DATABASE:?Must set MYSQL_DATABASE}"
BACKUP_ENCRYPTION_KEY="${BACKUP_ENCRYPTION_KEY:?Must set BACKUP_ENCRYPTION_KEY}"

RESTORE_DB="${MYSQL_DATABASE}_restore_test_$(date -u +%Y%m%d%H%M%S)"
BACKUP_FILE="/tmp/hrms_backup_drill_$(date -u +%Y%m%d%H%M%S).sql.gz.enc"
RESTORED_SQL="/tmp/hrms_restore_drill_$(date -u +%Y%m%d%H%M%S).sql"
PASS=0
FAIL=0

log()  { echo "[$(date -u +%H:%M:%S)] $*"; }
ok()   { echo "  ✅  $*"; ((PASS++)); }
fail() { echo "  ❌  $*"; ((FAIL++)); }

log "=== RatanHR Backup-Restore Drill ==="
log "Source DB   : ${MYSQL_DATABASE}@${MYSQL_HOST}:${MYSQL_PORT}"
log "Restore DB  : ${RESTORE_DB}"
log ""

# ── Step 1: Create encrypted backup ─────────────────────────────────────────
log "Step 1: Creating encrypted backup..."
mysqldump \
  -h "${MYSQL_HOST}" -P "${MYSQL_PORT}" \
  -u "${MYSQL_USER}" -p"${MYSQL_PASSWORD}" \
  --single-transaction \
  --set-gtid-purged=OFF \
  "${MYSQL_DATABASE}" \
  | gzip \
  | openssl enc -aes-256-cbc -pbkdf2 -iter 600000 \
      -pass "pass:${BACKUP_ENCRYPTION_KEY}" \
      -out "${BACKUP_FILE}"

if [[ -s "${BACKUP_FILE}" ]]; then
  BACKUP_BYTES=$(stat -c%s "${BACKUP_FILE}")
  ok "Backup created: ${BACKUP_FILE} (${BACKUP_BYTES} bytes)"
else
  fail "Backup file is empty or was not created"
  exit 1
fi

# ── Step 2: Decrypt and decompress ──────────────────────────────────────────
log "Step 2: Decrypting backup..."
openssl enc -d -aes-256-cbc -pbkdf2 -iter 600000 \
  -pass "pass:${BACKUP_ENCRYPTION_KEY}" \
  -in "${BACKUP_FILE}" \
  | gunzip > "${RESTORED_SQL}"

if [[ -s "${RESTORED_SQL}" ]]; then
  ok "Decryption successful: ${RESTORED_SQL}"
else
  fail "Decrypted file is empty"
  exit 1
fi

# ── Step 3: Create restore target database ───────────────────────────────────
log "Step 3: Creating restore target database '${RESTORE_DB}'..."
mysql -h "${MYSQL_HOST}" -P "${MYSQL_PORT}" \
  -u "${MYSQL_USER}" -p"${MYSQL_PASSWORD}" \
  -e "CREATE DATABASE \`${RESTORE_DB}\` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;"
ok "Database '${RESTORE_DB}' created"

# ── Step 4: Restore ──────────────────────────────────────────────────────────
log "Step 4: Restoring into '${RESTORE_DB}'..."
mysql -h "${MYSQL_HOST}" -P "${MYSQL_PORT}" \
  -u "${MYSQL_USER}" -p"${MYSQL_PASSWORD}" \
  "${RESTORE_DB}" < "${RESTORED_SQL}"
ok "Restore completed"

# ── Step 5: Row-count comparison ─────────────────────────────────────────────
log "Step 5: Comparing row counts..."

TABLES=(
  "Companies"
  "Employees"
  "Users"
  "payslips"
  "leave_requests"
  "attendance"
  "salary_structures"
  "payroll_locks"
  "refresh_tokens"
  "audit_logs"
)

ALL_MATCH=true
for TABLE in "${TABLES[@]}"; do
  SRC_COUNT=$(mysql -h "${MYSQL_HOST}" -P "${MYSQL_PORT}" \
    -u "${MYSQL_USER}" -p"${MYSQL_PASSWORD}" \
    -sNe "SELECT COUNT(*) FROM \`${MYSQL_DATABASE}\`.\`${TABLE}\` LIMIT 1;" 2>/dev/null || echo "N/A")
  RST_COUNT=$(mysql -h "${MYSQL_HOST}" -P "${MYSQL_PORT}" \
    -u "${MYSQL_USER}" -p"${MYSQL_PASSWORD}" \
    -sNe "SELECT COUNT(*) FROM \`${RESTORE_DB}\`.\`${TABLE}\` LIMIT 1;" 2>/dev/null || echo "N/A")

  if [[ "${SRC_COUNT}" == "${RST_COUNT}" ]]; then
    ok "${TABLE}: ${SRC_COUNT} rows — match"
  else
    fail "${TABLE}: source=${SRC_COUNT}, restored=${RST_COUNT} — MISMATCH"
    ALL_MATCH=false
  fi
done

# ── Step 6: Critical schema check ────────────────────────────────────────────
log "Step 6: Verifying critical indexes survive restore..."
IDX_CHECK=$(mysql -h "${MYSQL_HOST}" -P "${MYSQL_PORT}" \
  -u "${MYSQL_USER}" -p"${MYSQL_PASSWORD}" \
  -sNe "SELECT COUNT(*) FROM INFORMATION_SCHEMA.STATISTICS
        WHERE TABLE_SCHEMA='${RESTORE_DB}'
          AND INDEX_NAME IN (
            'ux_payslips_employee_month_year',
            'ux_attendance_employee_date'
          );" 2>/dev/null || echo "0")

if [[ "${IDX_CHECK}" == "2" ]]; then
  ok "Critical unique indexes present in restored DB (${IDX_CHECK}/2)"
else
  fail "Critical unique indexes missing in restored DB (found ${IDX_CHECK}/2)"
fi

# ── Step 7: Cleanup ──────────────────────────────────────────────────────────
log "Step 7: Cleaning up..."
mysql -h "${MYSQL_HOST}" -P "${MYSQL_PORT}" \
  -u "${MYSQL_USER}" -p"${MYSQL_PASSWORD}" \
  -e "DROP DATABASE IF EXISTS \`${RESTORE_DB}\`;"
rm -f "${BACKUP_FILE}" "${RESTORED_SQL}"
ok "Cleanup done"

# ── Final report ─────────────────────────────────────────────────────────────
log ""
log "=== Drill Result ==="
log "  PASS: ${PASS}"
log "  FAIL: ${FAIL}"
log ""

if [[ ${FAIL} -eq 0 ]]; then
  log "✅  BACKUP RESTORE DRILL PASSED — go-live backup readiness confirmed."
  exit 0
else
  log "❌  BACKUP RESTORE DRILL FAILED — resolve ${FAIL} issue(s) before go-live."
  exit 1
fi
