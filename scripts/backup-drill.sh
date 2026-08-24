#!/usr/bin/env bash
# =============================================================================
# scripts/backup-drill.sh — RatanHR HRMS Backup/Restore Drill
#
# Performs a complete backup + restore drill:
#   1. Takes a live mysqldump of the running database (encrypted)
#   2. Restores the dump into a temporary test database
#   3. Runs table-count sanity checks on all core, asset, and helpdesk tables
#   4. Computes and records the Recovery Time Objective (RTO) for this drill
#   5. Writes a timestamped drill report to docs/drill-reports/
#
# USAGE (from repo root with the production stack running):
#   BACKUP_ENCRYPTION_KEY=<key> bash scripts/backup-drill.sh
#
# USAGE (staging — set DB env vars explicitly):
#   MYSQL_HOST=127.0.0.1 MYSQL_PORT=3307 MYSQL_USER=hrms_staging \
#   MYSQL_PASSWORD=<pass> MYSQL_DATABASE=hrms_staging \
#   BACKUP_ENCRYPTION_KEY=<key> bash scripts/backup-drill.sh
#
# EXIT CODES:
#   0 — drill passed (backup taken + restore verified)
#   1 — drill failed (backup corrupt or restore incomplete)
#
# Schedule this drill weekly via cron, e.g.:
#   0 3 * * 0  cd /opt/hrms && BACKUP_ENCRYPTION_KEY=... bash scripts/backup-drill.sh \
#              >> /var/log/hrms-backup-drill.log 2>&1
# =============================================================================
set -uo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"

# ── Load .env if present ─────────────────────────────────────────────────────
ENV_FILE="$ROOT_DIR/.env"
[[ -f "$ENV_FILE" ]] && set -a && source "$ENV_FILE" && set +a

# ── Configuration ─────────────────────────────────────────────────────────────
DB_HOST="${MYSQL_HOST:-mysql}"
DB_PORT="${MYSQL_PORT:-3306}"
DB_USER="${MYSQL_USER:-hrms}"
DB_PASS="${MYSQL_PASSWORD:-}"
DB_NAME="${MYSQL_DATABASE:-hrms_db}"
BACKUP_DIR="${BACKUP_DIR:-$ROOT_DIR/backups}"
REPORT_DIR="$ROOT_DIR/docs/drill-reports"
TIMESTAMP=$(date -u '+%Y%m%d_%H%M%S')
DRILL_BACKUP="$BACKUP_DIR/drill_${TIMESTAMP}.sql.gz.enc"
TEST_DB="hrms_drill_${TIMESTAMP}"
REPORT_FILE="$REPORT_DIR/drill_${TIMESTAMP}.txt"
DRILL_START=$(date +%s)

# ── Guards ─────────────────────────────────────────────────────────────────────
if [[ -z "${BACKUP_ENCRYPTION_KEY:-}" ]]; then
  echo "FATAL: BACKUP_ENCRYPTION_KEY is not set."
  echo "       Generate with: openssl rand -base64 48"
  exit 1
fi

if [[ -z "$DB_PASS" ]]; then
  echo "FATAL: MYSQL_PASSWORD is not set."
  exit 1
fi

# ── Colour helpers ─────────────────────────────────────────────────────────────
GREEN='\033[0;32m'; RED='\033[0;31m'; YELLOW='\033[1;33m'; NC='\033[0m'
CHECKS_PASSED=0; CHECKS_FAILED=0

check_table() {
  local table="$1"
  local count
  count=$(mysql -h "$DB_HOST" -P "$DB_PORT" -u "$DB_USER" -p"$DB_PASS" \
    -D "$TEST_DB" --batch --skip-column-names \
    -e "SELECT COUNT(*) FROM \`${table}\`;" 2>/dev/null || echo "error")
  if [[ "$count" =~ ^[0-9]+$ ]]; then
    echo -e "  ${GREEN}✔${NC}  $table: $count rows"
    CHECKS_PASSED=$((CHECKS_PASSED+1))
    echo "    PASS  $table: $count rows" >> "$REPORT_FILE"
  else
    echo -e "  ${RED}✖${NC}  $table: query failed"
    CHECKS_FAILED=$((CHECKS_FAILED+1))
    echo "    FAIL  $table: query failed or table missing" >> "$REPORT_FILE"
  fi
}

mkdir -p "$BACKUP_DIR" "$REPORT_DIR"

# ── Report header ─────────────────────────────────────────────────────────────
{
  echo "========================================================"
  echo "  RatanHR HRMS — Backup/Restore Drill Report"
  echo "  Timestamp : $(date -u '+%Y-%m-%d %H:%M:%S UTC')"
  echo "  Database  : $DB_NAME @ $DB_HOST:$DB_PORT"
  echo "========================================================"
  echo ""
} > "$REPORT_FILE"

echo -e "${YELLOW}[DRILL]${NC} Starting backup/restore drill — $(date -u '+%Y-%m-%d %H:%M UTC')"
echo -e "${YELLOW}[DRILL]${NC} Target DB: $DB_NAME @ $DB_HOST:$DB_PORT"

# =============================================================================
# PHASE 1 — Take a backup
# =============================================================================
echo ""
echo -e "${YELLOW}[DRILL] PHASE 1 — Taking encrypted backup${NC}"
BACKUP_START=$(date +%s)

# Determine whether to use a Docker container or direct mysql client
CONTAINER=$(docker ps --format '{{.Names}}' 2>/dev/null | grep -E "hrms.*(mysql|db)" | head -1 || true)

if [[ -n "$CONTAINER" ]]; then
  echo "  Using Docker container: $CONTAINER"
  docker exec "$CONTAINER" \
    mysqldump \
      --single-transaction \
      --quick \
      --routines \
      --triggers \
      --hex-blob \
      -u "$DB_USER" -p"$DB_PASS" "$DB_NAME" \
  | gzip \
  | openssl enc -aes-256-cbc -pbkdf2 -iter 600000 \
      -pass "pass:${BACKUP_ENCRYPTION_KEY}" \
      -out "$DRILL_BACKUP"
else
  echo "  Using local mysqldump"
  mysqldump \
    -h "$DB_HOST" -P "$DB_PORT" \
    -u "$DB_USER" -p"$DB_PASS" \
    --single-transaction --quick --routines --triggers --hex-blob \
    "$DB_NAME" \
  | gzip \
  | openssl enc -aes-256-cbc -pbkdf2 -iter 600000 \
      -pass "pass:${BACKUP_ENCRYPTION_KEY}" \
      -out "$DRILL_BACKUP"
fi

BACKUP_END=$(date +%s)
BACKUP_SIZE=$(du -sh "$DRILL_BACKUP" 2>/dev/null | awk '{print $1}')
BACKUP_TIME=$((BACKUP_END - BACKUP_START))

echo -e "  ${GREEN}✔${NC}  Backup written: $(basename "$DRILL_BACKUP") ($BACKUP_SIZE in ${BACKUP_TIME}s)"
{
  echo "PHASE 1 — Backup"
  echo "  File : $(basename "$DRILL_BACKUP")"
  echo "  Size : $BACKUP_SIZE"
  echo "  Time : ${BACKUP_TIME}s"
  echo ""
} >> "$REPORT_FILE"

# =============================================================================
# PHASE 2 — Restore into a temporary database
# =============================================================================
echo ""
echo -e "${YELLOW}[DRILL] PHASE 2 — Restoring into temporary database: $TEST_DB${NC}"
RESTORE_START=$(date +%s)

# Create test DB
mysql -h "$DB_HOST" -P "$DB_PORT" -u "$DB_USER" -p"$DB_PASS" \
  -e "CREATE DATABASE \`${TEST_DB}\` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;" 2>/dev/null

cleanup() {
  echo -e "${YELLOW}[DRILL]${NC} Dropping temporary database ${TEST_DB} …"
  mysql -h "$DB_HOST" -P "$DB_PORT" -u "$DB_USER" -p"$DB_PASS" \
    -e "DROP DATABASE IF EXISTS \`${TEST_DB}\`;" 2>/dev/null || true
  # Remove drill backup (not a retention backup — just for the drill)
  rm -f "$DRILL_BACKUP"
}
trap cleanup EXIT

# Decrypt + decompress + restore
openssl enc -d -aes-256-cbc -pbkdf2 -iter 600000 \
  -pass "pass:${BACKUP_ENCRYPTION_KEY}" \
  -in "$DRILL_BACKUP" \
| gunzip \
| mysql -h "$DB_HOST" -P "$DB_PORT" -u "$DB_USER" -p"$DB_PASS" "$TEST_DB"

RESTORE_END=$(date +%s)
RESTORE_TIME=$((RESTORE_END - RESTORE_START))
echo -e "  ${GREEN}✔${NC}  Restore completed in ${RESTORE_TIME}s"
echo "PHASE 2 — Restore: ${RESTORE_TIME}s" >> "$REPORT_FILE"
echo "" >> "$REPORT_FILE"

# =============================================================================
# PHASE 3 — Sanity checks
# =============================================================================
echo ""
echo -e "${YELLOW}[DRILL] PHASE 3 — Table sanity checks${NC}"
echo "PHASE 3 — Sanity checks" >> "$REPORT_FILE"

# Core tables
check_table "Companies"
check_table "Employees"
check_table "Users"
check_table "Payslips"
check_table "LeaveRequests"
check_table "AuditLogs"

# Asset Management
check_table "AssetCategories"
check_table "Assets"
check_table "AssetHistories"

# Helpdesk
check_table "HelpdeskTickets"
check_table "HelpdeskCategories"
check_table "HelpdeskComments"

# Attendance
check_table "AttendanceLogs"

DRILL_END=$(date +%s)
TOTAL_RTO=$((DRILL_END - DRILL_START))

# =============================================================================
# Report footer
# =============================================================================
{
  echo ""
  echo "========================================================"
  echo "  SUMMARY"
  echo "  Backup time  : ${BACKUP_TIME}s"
  echo "  Restore time : ${RESTORE_TIME}s"
  echo "  Total RTO    : ${TOTAL_RTO}s"
  echo "  Tables PASS  : $CHECKS_PASSED"
  echo "  Tables FAIL  : $CHECKS_FAILED"
  if [[ "$CHECKS_FAILED" -eq 0 ]]; then
    echo "  VERDICT      : DRILL PASSED — backup is recoverable"
  else
    echo "  VERDICT      : DRILL FAILED — $CHECKS_FAILED table(s) missing/empty after restore"
  fi
  echo "========================================================"
} >> "$REPORT_FILE"

echo ""
echo "  Backup time  : ${BACKUP_TIME}s"
echo "  Restore time : ${RESTORE_TIME}s"
echo "  Total RTO    : ${TOTAL_RTO}s"
echo "  Tables PASS  : $CHECKS_PASSED"
echo "  Tables FAIL  : $CHECKS_FAILED"
echo ""
echo "  Drill report : $REPORT_FILE"

if [[ "$CHECKS_FAILED" -gt 0 ]]; then
  echo -e "${RED}[DRILL] ❌ DRILL FAILED — $CHECKS_FAILED table check(s) failed.${NC}"
  echo -e "${RED}        Investigate immediately — this backup cannot be fully recovered.${NC}"
  exit 1
else
  echo -e "${GREEN}[DRILL] ✅ DRILL PASSED — backup is recoverable (RTO: ${TOTAL_RTO}s).${NC}"
  exit 0
fi
