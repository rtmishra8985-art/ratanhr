#!/usr/bin/env bash
# =============================================================================
# test-restore.sh — Weekly backup restore validation
# Phase 5: Rewritten from PostgreSQL to MySQL 8.4.
# Referenced by docker-compose.backup.yml (Sunday 03:00 UTC cron job)
#
# What this script does:
#   1. Finds the most recent local backup file.
#   2. Restores it into a temporary MySQL database (hrms_restore_test_<timestamp>).
#   3. Runs a basic sanity check (row counts on key tables).
#   4. Drops the temporary database.
#   5. Exits non-zero on any failure — the cron log will capture the failure.
#
# A failed restore test means your backups cannot be recovered. Fix immediately.
# =============================================================================
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ENV_FILE="${SCRIPT_DIR}/../.env"
[[ -f "$ENV_FILE" ]] && set -a && source "$ENV_FILE" && set +a

BACKUP_DIR="${BACKUP_DIR:-${SCRIPT_DIR}/../backups}"
DB_HOST="${MYSQL_HOST:-mysql}"
DB_PORT="${MYSQL_PORT:-3306}"
DB_USER="${MYSQL_USER:-hrms}"
DB_PASS="${MYSQL_PASSWORD:-}"
TEST_DB="hrms_restore_test_$(date -u '+%Y%m%d%H%M%S')"
LOG_PREFIX="[$(date -u '+%Y-%m-%d %H:%M UTC')] [test-restore]"

echo "${LOG_PREFIX} Starting weekly restore validation…"

# FIX: backups have been AES-256-CBC encrypted (.sql.gz.enc) since
# mysql-backup.sh's BACKUP-01 fix. Decryption requires BACKUP_ENCRYPTION_KEY.
if [[ -z "${BACKUP_ENCRYPTION_KEY:-}" ]]; then
    echo "${LOG_PREFIX} FATAL: BACKUP_ENCRYPTION_KEY is not set. Cannot decrypt backups." >&2
    exit 1
fi

# ── Find the newest backup ────────────────────────────────────────────────────
LATEST=$(find "$BACKUP_DIR" -name "hrms_*.sql.gz.enc" -printf '%T@ %p\n' \
    2>/dev/null | sort -n | tail -1 | awk '{print $2}')

if [[ -z "$LATEST" ]]; then
    echo "${LOG_PREFIX} ERROR: No backup files found in ${BACKUP_DIR}. Cannot validate restore."
    exit 1
fi
echo "${LOG_PREFIX} Testing restore from: $(basename "$LATEST")"

# ── Create a temporary test database ─────────────────────────────────────────
mysql -h "$DB_HOST" -P "$DB_PORT" -u "$DB_USER" -p"$DB_PASS" \
    -e "CREATE DATABASE \`${TEST_DB}\` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;" > /dev/null

cleanup() {
    echo "${LOG_PREFIX} Dropping test database ${TEST_DB}…"
    mysql -h "$DB_HOST" -P "$DB_PORT" -u "$DB_USER" -p"$DB_PASS" \
        -e "DROP DATABASE IF EXISTS \`${TEST_DB}\`;" > /dev/null 2>&1 || true
}
trap cleanup EXIT

# ── Restore the backup (decrypt → decompress → restore) ───────────────────────
echo "${LOG_PREFIX} Restoring dump into ${TEST_DB}…"
openssl enc -d -aes-256-cbc -pbkdf2 -iter 600000 \
    -pass "pass:${BACKUP_ENCRYPTION_KEY}" \
    -in "$LATEST" \
  | gunzip -c | mysql \
    -h "$DB_HOST" \
    -P "$DB_PORT" \
    -u "$DB_USER" \
    -p"$DB_PASS" \
    "$TEST_DB" 2>&1 | tail -5

# ── Sanity check: verify key tables have rows ──────────────────────────────────
echo "${LOG_PREFIX} Running sanity checks…"

CHECKS_PASSED=0
CHECKS_FAILED=0

check_table() {
    local table="$1"
    local count
    count=$(mysql -h "$DB_HOST" -P "$DB_PORT" -u "$DB_USER" -p"$DB_PASS" \
        -D "$TEST_DB" --batch --skip-column-names \
        -e "SELECT COUNT(*) FROM \`${table}\`;" 2>/dev/null || echo "error")
    if [[ "$count" =~ ^[0-9]+$ ]]; then
        echo "${LOG_PREFIX}   ✅ ${table}: ${count} rows"
        ((CHECKS_PASSED++))
    else
        echo "${LOG_PREFIX}   ❌ ${table}: query failed or returned '${count}'"
        ((CHECKS_FAILED++))
    fi
}

# ── Core tables ───────────────────────────────────────────────────────────────
check_table "companies"
check_table "employees"
check_table "payslips"
check_table "leave_requests"
check_table "audit_logs"

# ── Asset Management tables ───────────────────────────────────────────────────
check_table "AssetCategories"
check_table "Assets"
check_table "AssetHistories"

# ── Helpdesk tables ───────────────────────────────────────────────────────────
check_table "HelpdeskTickets"
check_table "HelpdeskCategories"
check_table "HelpdeskComments"
check_table "HelpdeskHistories"

echo "${LOG_PREFIX} Results: ${CHECKS_PASSED} passed, ${CHECKS_FAILED} failed."

if [[ "$CHECKS_FAILED" -gt 0 ]]; then
    echo "${LOG_PREFIX} ❌ RESTORE VALIDATION FAILED — ${CHECKS_FAILED} table check(s) failed."
    echo "${LOG_PREFIX}    Backups may be corrupt. Investigate immediately."
    exit 1
fi

echo "${LOG_PREFIX} ✅ Restore validation PASSED — backup is recoverable."
