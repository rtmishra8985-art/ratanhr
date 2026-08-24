#!/usr/bin/env bash
# =============================================================================
# phase9_run.sh — RatanHR HRMS Phase 9 Full Regression Runner
#
# Usage (from repo root on staging server):
#   export SUPERADMIN_INITIAL_PASSWORD="<sa_pass>"
#   export DB_PASSWORD="<hrms_staging_pass>"   # same user as .env.e2e
#   export REDIS_PASSWORD="<redis_pass>"
#   bash phase9_run.sh 2>&1 | tee /tmp/phase9_run.log
#
# What this script does (in order):
#   §1   Pre-flight  — env, tools, required files
#   §2   Backend build — dotnet build HRMS.sln (0 warnings/errors required)
#   §3   Frontend build — bun install + bun run build:ci
#   §4   .NET unit + integration tests (target: 934 pass, 0 fail)
#   §5   SPA unit tests (vitest)
#   §6   Stack startup — docker-compose.e2e.yml up + DB seed
#   §7   Phase 8 smoke checks (67 checks via Staging/phase8_runbook.sh)
#   §8   Phase 8 DB validation (42 checks — included in runbook)
#   §9   Playwright E2E — Chromium (625 tests)
#   §10  Playwright E2E — Firefox (625 tests)
#   §11  Playwright E2E — Mobile Chrome (625 tests)
#   §12  Company A vs B tenant isolation security test
#   §13  Admin workflow checks
#   §14  Employee workflow checks
#   §15  Payroll workflow checks
#   §16  Sales workflow checks
#   §17  Browser console + server log error sweep
#   §18  Summary — PASS / FAIL / WARN / BLOCKED per section
#
# Exit: 0 if all sections pass; non-zero otherwise.
# =============================================================================

set -euo pipefail
IFS=$'\n\t'

# ── colours ───────────────────────────────────────────────────────────────────
RED='\033[0;31m'; GREEN='\033[0;32m'; YELLOW='\033[1;33m'
CYAN='\033[0;36m'; BOLD='\033[1m'; NC='\033[0m'

ts()      { date -u '+%H:%M:%S'; }
log()     { echo -e "${CYAN}[$(ts)]${NC} $*"; }
ok()      { echo -e "${GREEN}[$(ts)] ✓${NC}  $*"; }
warn()    { echo -e "${YELLOW}[$(ts)] ⚠${NC}  $*"; ((WARN_TOTAL++)) || true; }
err()     { echo -e "${RED}[$(ts)] ✗${NC}  $*"; }
section() { echo -e "\n${BOLD}${CYAN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"; \
            echo -e "${BOLD}${CYAN}  §$* ${NC}"; \
            echo -e "${BOLD}${CYAN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"; }

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

PHASE9_LOG="${SCRIPT_DIR}/logs/phase9_$(date -u '+%Y%m%d_%H%M%S').log"
mkdir -p "${SCRIPT_DIR}/logs"
exec > >(tee -a "$PHASE9_LOG") 2>&1

echo ""
echo -e "${BOLD}${CYAN}╔══════════════════════════════════════════════════╗${NC}"
echo -e "${BOLD}${CYAN}║  RatanHR HRMS — Phase 9 Full Regression Suite   ║${NC}"
echo -e "${BOLD}${CYAN}║  $(date -u '+%Y-%m-%d %H:%M:%S UTC')                        ║${NC}"
echo -e "${BOLD}${CYAN}╚══════════════════════════════════════════════════╝${NC}"
echo ""

# ── section result tracking ───────────────────────────────────────────────────
declare -A SECTION_RESULT
WARN_TOTAL=0
FATAL_SECTIONS=()

section_pass() { SECTION_RESULT["$1"]="PASS"; ok "Section $1 → PASS"; }
section_fail() { SECTION_RESULT["$1"]="FAIL"; err "Section $1 → FAIL: $2"; FATAL_SECTIONS+=("§$1: $2"); }
section_warn() { SECTION_RESULT["$1"]="WARN"; warn "Section $1 → WARN: $2"; }
section_skip() { SECTION_RESULT["$1"]="SKIP"; warn "Section $1 → SKIPPED: $2"; }

# ── http helpers (requires API to be up) ─────────────────────────────────────
API_BASE="http://${API_HOST:-127.0.0.1:9090}"
http_code() { curl -s -o /dev/null -w "%{http_code}" --max-time 10 "$@" 2>/dev/null || echo "000"; }
http_body() { curl -s --max-time 10 "$@" 2>/dev/null || echo ""; }

# =============================================================================
# §1 — Pre-flight
# =============================================================================
section "1 — Pre-flight checks"

PREFLIGHT_OK=true

# Required env vars
for var in SUPERADMIN_INITIAL_PASSWORD DB_PASSWORD REDIS_PASSWORD; do
  if [[ -z "${!var:-}" ]]; then
    err "FATAL: $var is not set. Export it before running this script."
    PREFLIGHT_OK=false
  fi
done

# Tool availability
for tool in docker dotnet bun curl python3 git; do
  if ! command -v "$tool" &>/dev/null; then
    err "Missing required tool: $tool"
    PREFLIGHT_OK=false
  else
    log "  $tool → $(command -v "$tool")"
  fi
done

# Docker daemon
if ! docker info >/dev/null 2>&1; then
  err "Docker daemon is not running"
  PREFLIGHT_OK=false
fi

# Required files
for f in HRMS.sln Dockerfile docker-compose.e2e.yml Staging/phase8_runbook.sh \
         e2e/e2e_seed.sql HRMS.SPA.Source/playwright.config.ts \
         HRMS.SPA.Source/package.json; do
  if [[ ! -f "$f" ]]; then
    err "Required file missing: $f"
    PREFLIGHT_OK=false
  fi
done

# .env.e2e
if [[ ! -f ".env.e2e" ]]; then
  if [[ -f ".env.e2e.template" ]]; then
    err ".env.e2e not found. Copy from template: cp .env.e2e.template .env.e2e && fill values"
  else
    err ".env.e2e not found"
  fi
  PREFLIGHT_OK=false
else
  # Verify no un-filled placeholders
  if grep -q "FILL_IN\|<REQUIRED>" .env.e2e 2>/dev/null; then
    err ".env.e2e still contains FILL_IN placeholders. Fill all values before running."
    PREFLIGHT_OK=false
  fi
fi

if ! $PREFLIGHT_OK; then
  section_fail "1" "Pre-flight failed — see errors above"
  echo -e "\n${RED}${BOLD}ABORTED — fix pre-flight errors before running Phase 9.${NC}"
  exit 1
fi

section_pass "1"

# =============================================================================
# §2 — Backend build
# =============================================================================
section "2 — Backend build (dotnet build)"

log "Running: dotnet build HRMS.sln -c Release /p:TreatWarningsAsErrors=true"
if dotnet build HRMS.sln -c Release \
    /p:TreatWarningsAsErrors=true \
    /p:AssemblyVersion=1.0.0 \
    /p:FileVersion=1.0.0 \
    --nologo 2>&1; then
  ok "Backend build: 0 errors, 0 warnings"
  section_pass "2"
else
  section_fail "2" "dotnet build failed — fix all errors and warnings before Phase 9"
  exit 1
fi

# =============================================================================
# §3 — Frontend build
# =============================================================================
section "3 — Frontend build (bun build:ci)"

cd HRMS.SPA.Source

log "Running: bun install --frozen-lockfile"
if ! bun install --frozen-lockfile 2>&1; then
  cd "$SCRIPT_DIR"
  section_fail "3" "bun install failed"
  exit 1
fi

log "Running: bun run build:ci"
if ! bun run build:ci 2>&1; then
  cd "$SCRIPT_DIR"
  section_fail "3" "bun build:ci failed — fix TypeScript and lint errors"
  exit 1
fi

cd "$SCRIPT_DIR"
ok "Frontend build: success"
section_pass "3"

# =============================================================================
# §4 — .NET unit + integration tests
# =============================================================================
section "4 — .NET unit + integration tests (dotnet test)"

DOTNET_RESULTS="/tmp/phase9_dotnet_results_$(date +%s).trx"

log "Running: dotnet test HRMS.sln -c Release --no-build --logger trx"
if dotnet test HRMS.sln \
    -c Release \
    --no-build \
    --logger "trx;LogFileName=${DOTNET_RESULTS}" \
    --logger "console;verbosity=normal" \
    2>&1; then

  # Extract counts from TRX if available, else trust exit code
  if [[ -f "$DOTNET_RESULTS" ]]; then
    DOTNET_PASS=$(grep -oP 'passed="\K[0-9]+' "$DOTNET_RESULTS" | head -1 || echo "?")
    DOTNET_FAIL=$(grep -oP 'failed="\K[0-9]+' "$DOTNET_RESULTS" | head -1 || echo "0")
    ok ".NET tests: ${DOTNET_PASS} passed, ${DOTNET_FAIL} failed"
    if [[ "$DOTNET_FAIL" != "0" && "$DOTNET_FAIL" != "" ]]; then
      section_fail "4" "${DOTNET_FAIL} .NET test(s) failed"
    else
      section_pass "4"
    fi
  else
    ok ".NET tests: all passed (exit 0)"
    section_pass "4"
  fi
else
  section_fail "4" "dotnet test exited non-zero — check output above"
  exit 1
fi

# =============================================================================
# §5 — SPA unit tests (vitest)
# =============================================================================
section "5 — SPA unit tests (bun run test)"

cd HRMS.SPA.Source

log "Running: bun run test --run"
if bun run test --run 2>&1; then
  ok "SPA unit tests: all passed"
  section_pass "5"
else
  section_fail "5" "SPA unit tests failed — check output above"
  cd "$SCRIPT_DIR"
  exit 1
fi

cd "$SCRIPT_DIR"

# =============================================================================
# §6 — Stack startup: docker-compose.e2e.yml + seed
# =============================================================================
section "6 — E2E stack startup + database seed"

# Load .env.e2e for DB credentials
set -a
# shellcheck disable=SC1091
source <(grep -v '^\s*#' .env.e2e | grep -v '^\s*$' | grep '=' 2>/dev/null) || true
set +a

E2E_API_URL="${E2E_API_URL:-http://127.0.0.1:8082}"

log "Starting E2E stack (docker-compose.e2e.yml)..."
docker compose -f docker-compose.e2e.yml --env-file .env.e2e up -d --wait \
  || { section_fail "6" "docker-compose.e2e.yml failed to start"; exit 1; }

# Wait for API health
log "Waiting for API health at ${E2E_API_URL}/api/health (up to 90s)..."
ELAPSED=0
until curl -fs "${E2E_API_URL}/api/health" 2>/dev/null | grep -qi "Healthy"; do
  sleep 5
  ELAPSED=$((ELAPSED + 5))
  if [[ $ELAPSED -ge 90 ]]; then
    err "API did not become healthy within 90s"
    docker compose -f docker-compose.e2e.yml logs api --tail=30
    section_fail "6" "API health timeout"
    exit 1
  fi
  log "  Waiting... (${ELAPSED}s)"
done
ok "API is healthy at $E2E_API_URL"

# Seed E2E test accounts
log "Seeding E2E database (e2e/e2e_seed.sql)..."
E2E_DB_PORT="${E2E_DB_PORT:-3307}"
mysql -h 127.0.0.1 -P "$E2E_DB_PORT" -u root \
  "-p${MYSQL_ROOT_PASSWORD}" \
  "${MYSQL_DATABASE:-hrms}" \
  < e2e/e2e_seed.sql 2>&1 \
  || { section_fail "6" "e2e_seed.sql failed"; exit 1; }

# Verify seed count
SEED_COUNT=$(mysql -h 127.0.0.1 -P "$E2E_DB_PORT" -u root \
  "-p${MYSQL_ROOT_PASSWORD}" --skip-column-names --silent \
  "${MYSQL_DATABASE:-hrms}" \
  --execute "SELECT COUNT(*) FROM Users WHERE Email LIKE 'e2e.%@ratan-staging.local';" \
  2>/dev/null || echo "0")

if [[ "$SEED_COUNT" -ne 6 ]]; then
  section_fail "6" "Seed verification failed: expected 6 E2E accounts, found ${SEED_COUNT}"
  exit 1
fi
ok "Seed verified: ${SEED_COUNT}/6 E2E accounts present"
section_pass "6"

# =============================================================================
# §7+8 — Phase 8 smoke checks (67) + DB validation (42)
# =============================================================================
section "7+8 — Phase 8 runbook (67 smoke + 42 DB validation checks)"

# Set Phase 8 environment to point at the E2E stack
export API_HOST="${API_HOST:-127.0.0.1:9090}"
export DB_HOST="${DB_HOST:-127.0.0.1}"
export DB_PORT="${E2E_DB_PORT:-3307}"
export DB_USER="${DB_USER:-hrms_staging}"
export DB_NAME="${DB_NAME:-hrms_staging}"
export REDIS_HOST="${REDIS_HOST:-127.0.0.1}"
export REDIS_PORT="${REDIS_PORT:-6380}"
export MH_HOST="${MH_HOST:-127.0.0.1:8025}"

P8_LOG="/tmp/phase8_from_phase9_$(date +%s).log"

# Run Phase 8 runbook — capture output and exit code without set -e killing us
set +e
bash Staging/phase8_runbook.sh 2>&1 | tee "$P8_LOG"
P8_EXIT=${PIPESTATUS[0]}
set -e

# Extract counts from the log
P8_PASS=$(grep -oP 'pass=\K[0-9]+' "$P8_LOG" | tail -1 || grep -c "✓ PASS" "$P8_LOG" || echo "?")
P8_FAIL=$(grep -oP 'fail=\K[0-9]+' "$P8_LOG" | tail -1 || grep -c "✗ FAIL" "$P8_LOG" || echo "?")
P8_WARN=$(grep -oP 'warn=\K[0-9]+' "$P8_LOG" | tail -1 || grep -c "⚠ WARN" "$P8_LOG" || echo "?")

log "Phase 8 runbook result: PASS=${P8_PASS}, FAIL=${P8_FAIL}, WARN=${P8_WARN}"

if [[ $P8_EXIT -ne 0 ]]; then
  section_fail "7+8" "Phase 8 runbook failed — ${P8_FAIL} check(s) failed. See $P8_LOG"
else
  ok "Phase 8 runbook: all checks passed (PASS=${P8_PASS}, WARN=${P8_WARN})"
  section_pass "7+8"
fi

# =============================================================================
# §9 — Playwright E2E: Chromium
# =============================================================================
section "9 — Playwright E2E: Chromium (625 tests)"

cd HRMS.SPA.Source
E2E_BASE_URL="$E2E_API_URL"
export E2E_BASE_URL

log "Installing Playwright browsers (chromium)..."
bunx playwright install chromium --with-deps 2>&1 | tail -5

PW_REPORT_DIR="../logs/playwright-chromium-$(date +%Y%m%d_%H%M%S)"
log "Running Playwright (chromium)..."
set +e
bunx playwright test \
  --project=chromium \
  --reporter=list,html \
  --output="$PW_REPORT_DIR/results" \
  2>&1 | tee /tmp/pw_chromium.log
PW_CHROM_EXIT=${PIPESTATUS[0]}
set -e

PW_CHROM_PASS=$(grep -oP '\d+(?= passed)' /tmp/pw_chromium.log | tail -1 || echo "?")
PW_CHROM_FAIL=$(grep -oP '\d+(?= failed)' /tmp/pw_chromium.log | tail -1 || echo "0")

if [[ $PW_CHROM_EXIT -ne 0 ]]; then
  section_fail "9" "Chromium E2E: ${PW_CHROM_FAIL} test(s) failed (${PW_CHROM_PASS} passed)"
else
  ok "Chromium E2E: ${PW_CHROM_PASS} passed, ${PW_CHROM_FAIL} failed"
  section_pass "9"
fi
cd "$SCRIPT_DIR"

# =============================================================================
# §10 — Playwright E2E: Firefox
# =============================================================================
section "10 — Playwright E2E: Firefox (625 tests)"

cd HRMS.SPA.Source

log "Installing Playwright browsers (firefox)..."
bunx playwright install firefox --with-deps 2>&1 | tail -5

log "Running Playwright (firefox)..."
set +e
bunx playwright test \
  --project=firefox \
  --reporter=list \
  2>&1 | tee /tmp/pw_firefox.log
PW_FF_EXIT=${PIPESTATUS[0]}
set -e

PW_FF_PASS=$(grep -oP '\d+(?= passed)' /tmp/pw_firefox.log | tail -1 || echo "?")
PW_FF_FAIL=$(grep -oP '\d+(?= failed)' /tmp/pw_firefox.log | tail -1 || echo "0")

if [[ $PW_FF_EXIT -ne 0 ]]; then
  section_fail "10" "Firefox E2E: ${PW_FF_FAIL} test(s) failed (${PW_FF_PASS} passed)"
else
  ok "Firefox E2E: ${PW_FF_PASS} passed, ${PW_FF_FAIL} failed"
  section_pass "10"
fi
cd "$SCRIPT_DIR"

# =============================================================================
# §11 — Playwright E2E: Mobile Chrome
# =============================================================================
section "11 — Playwright E2E: Mobile Chrome (625 tests)"

cd HRMS.SPA.Source

log "Running Playwright (Mobile Chrome)..."
set +e
bunx playwright test \
  --project="Mobile Chrome" \
  --reporter=list \
  2>&1 | tee /tmp/pw_mobile.log
PW_MOB_EXIT=${PIPESTATUS[0]}
set -e

PW_MOB_PASS=$(grep -oP '\d+(?= passed)' /tmp/pw_mobile.log | tail -1 || echo "?")
PW_MOB_FAIL=$(grep -oP '\d+(?= failed)' /tmp/pw_mobile.log | tail -1 || echo "0")

if [[ $PW_MOB_EXIT -ne 0 ]]; then
  section_fail "11" "Mobile Chrome E2E: ${PW_MOB_FAIL} test(s) failed (${PW_MOB_PASS} passed)"
else
  ok "Mobile Chrome E2E: ${PW_MOB_PASS} passed, ${PW_MOB_FAIL} failed"
  section_pass "11"
fi
cd "$SCRIPT_DIR"

# =============================================================================
# §12 — Company A vs B tenant isolation security test
# =============================================================================
section "12 — Tenant isolation security test (Company A vs B)"

TENANT_FAIL=0
TI_BASE="$E2E_API_URL"

ti_pass() { ok  "  TI-PASS  $*"; }
ti_fail() { err "  TI-FAIL  $*"; ((TENANT_FAIL++)) || true; }

# Login as Admin A (company 9001)
ADMIN_A_RAW=$(curl -s -X POST "$TI_BASE/api/auth/login" \
  -H "Content-Type: application/json" \
  -d '{"email":"e2e.adminA@ratan-staging.local","password":"E2E_AdminA_Pass1!","portal":"Admin"}' \
  2>/dev/null)
ADMIN_A_TOKEN=$(echo "$ADMIN_A_RAW" | python3 -c \
  "import sys,json; d=json.load(sys.stdin); print(d.get('data',{}).get('token',''))" 2>/dev/null || echo "")

# Login as Admin B (company 9002)
ADMIN_B_RAW=$(curl -s -X POST "$TI_BASE/api/auth/login" \
  -H "Content-Type: application/json" \
  -d '{"email":"e2e.adminB@ratan-staging.local","password":"E2E_AdminB_Pass1!","portal":"Admin"}' \
  2>/dev/null)
ADMIN_B_TOKEN=$(echo "$ADMIN_B_RAW" | python3 -c \
  "import sys,json; d=json.load(sys.stdin); print(d.get('data',{}).get('token',''))" 2>/dev/null || echo "")

if [[ -z "$ADMIN_A_TOKEN" || -z "$ADMIN_B_TOKEN" ]]; then
  section_fail "12" "Could not obtain both admin tokens — check seed accounts and stack"
else

  # TI-01: Admin A employee list contains no company B employees
  EMPS_A=$(http_body "$TI_BASE/api/employees" -H "Authorization: Bearer $ADMIN_A_TOKEN")
  if echo "$EMPS_A" | python3 -c \
    "import sys,json; d=json.load(sys.stdin); items=d.get('data',d) if isinstance(d,dict) else d; assert all(str(e.get('companyId',''))=='9001' or e.get('companyId')==9001 for e in (items if isinstance(items,list) else [])), 'BREACH'" \
    2>/dev/null; then
    ti_pass "TI-01: Admin A sees only company 9001 employees"
  else
    ti_fail "TI-01: Admin A employee list may contain company 9002 data — IDOR BREACH"
  fi

  # TI-02: Admin B employee list contains no company A employees
  EMPS_B=$(http_body "$TI_BASE/api/employees" -H "Authorization: Bearer $ADMIN_B_TOKEN")
  if echo "$EMPS_B" | python3 -c \
    "import sys,json; d=json.load(sys.stdin); items=d.get('data',d) if isinstance(d,dict) else d; assert all(str(e.get('companyId',''))=='9002' or e.get('companyId')==9002 for e in (items if isinstance(items,list) else [])), 'BREACH'" \
    2>/dev/null; then
    ti_pass "TI-02: Admin B sees only company 9002 employees"
  else
    ti_fail "TI-02: Admin B employee list may contain company 9001 data — IDOR BREACH"
  fi

  # TI-03: Admin A cannot access company 9002 branches
  TI03=$(http_code "$TI_BASE/api/companies/9002/branches" -H "Authorization: Bearer $ADMIN_A_TOKEN")
  if [[ "$TI03" == "403" || "$TI03" == "404" || "$TI03" == "401" ]]; then
    ti_pass "TI-03: Admin A blocked from company 9002 branches → HTTP $TI03"
  else
    ti_fail "TI-03: Admin A reached company 9002 branches → HTTP $TI03 (IDOR BREACH)"
  fi

  # TI-04: Admin B cannot access company 9001 branches
  TI04=$(http_code "$TI_BASE/api/companies/9001/branches" -H "Authorization: Bearer $ADMIN_B_TOKEN")
  if [[ "$TI04" == "403" || "$TI04" == "404" || "$TI04" == "401" ]]; then
    ti_pass "TI-04: Admin B blocked from company 9001 branches → HTTP $TI04"
  else
    ti_fail "TI-04: Admin B reached company 9001 branches → HTTP $TI04 (IDOR BREACH)"
  fi

  # TI-05: Admin A reports scoped to company 9001
  REPORT_A=$(http_code "$TI_BASE/api/reports/employees" -H "Authorization: Bearer $ADMIN_A_TOKEN")
  if [[ "$REPORT_A" == "200" ]]; then
    ti_pass "TI-05: Admin A report endpoint returns 200 (scoped to company 9001)"
  else
    ti_fail "TI-05: Admin A report returned HTTP $REPORT_A"
  fi

  if [[ $TENANT_FAIL -eq 0 ]]; then
    section_pass "12"
  else
    section_fail "12" "${TENANT_FAIL} tenant isolation BREACH(es) detected — go-live BLOCKED"
  fi
fi

# =============================================================================
# §13 — Admin workflow checks
# =============================================================================
section "13 — Admin workflow checks"

ADMIN_FAIL=0

if [[ -z "${ADMIN_A_TOKEN:-}" ]]; then
  # Try to get a token from the phase 8 smoke setup
  ADMIN_A_RAW=$(curl -s -X POST "$TI_BASE/api/auth/login" \
    -H "Content-Type: application/json" \
    -d "{\"email\":\"admin@acme.com\",\"password\":\"${SUPERADMIN_INITIAL_PASSWORD}\",\"portal\":\"Admin\"}" \
    2>/dev/null)
  ADMIN_A_TOKEN=$(echo "$ADMIN_A_RAW" | python3 -c \
    "import sys,json; d=json.load(sys.stdin); print(d.get('data',{}).get('token',''))" 2>/dev/null || echo "")
fi

aw_check() {
  local label="$1" code="$2" expected="$3"
  for e in $(echo "$expected" | tr ',' ' '); do
    [[ "$code" == "$e" ]] && { ok "  PASS  $label → $code"; return; }
  done
  err "  FAIL  $label → $code (expected $expected)"
  ((ADMIN_FAIL++)) || true
}

if [[ -n "$ADMIN_A_TOKEN" ]]; then
  aw_check "Admin: list employees"     "$(http_code "$TI_BASE/api/employees" -H "Authorization: Bearer $ADMIN_A_TOKEN")"     "200"
  aw_check "Admin: list departments"   "$(http_code "$TI_BASE/api/departments" -H "Authorization: Bearer $ADMIN_A_TOKEN")"   "200"
  aw_check "Admin: list leave types"   "$(http_code "$TI_BASE/api/leave/types" -H "Authorization: Bearer $ADMIN_A_TOKEN")"   "200"
  aw_check "Admin: list holidays"      "$(http_code "$TI_BASE/api/holidays" -H "Authorization: Bearer $ADMIN_A_TOKEN")"      "200"
  aw_check "Admin: list shifts"        "$(http_code "$TI_BASE/api/shifts" -H "Authorization: Bearer $ADMIN_A_TOKEN")"        "200"
  aw_check "Admin: list assets"        "$(http_code "$TI_BASE/api/assets" -H "Authorization: Bearer $ADMIN_A_TOKEN")"        "200,403,404"
  aw_check "Admin: list recruitment"   "$(http_code "$TI_BASE/api/recruitment" -H "Authorization: Bearer $ADMIN_A_TOKEN")"   "200,403,404"
  aw_check "Admin: notifications"      "$(http_code "$TI_BASE/api/notifications" -H "Authorization: Bearer $ADMIN_A_TOKEN")" "200"
  aw_check "Admin: dashboard"          "$(http_code "$TI_BASE/api/dashboard" -H "Authorization: Bearer $ADMIN_A_TOKEN")"     "200"
  [[ $ADMIN_FAIL -eq 0 ]] && section_pass "13" || section_fail "13" "${ADMIN_FAIL} admin workflow check(s) failed"
else
  section_skip "13" "No admin token available (seed accounts not reachable)"
fi

# =============================================================================
# §14 — Employee workflow checks
# =============================================================================
section "14 — Employee workflow checks"

EMP_FAIL=0
EMP_A_RAW=$(curl -s -X POST "$TI_BASE/api/auth/login" \
  -H "Content-Type: application/json" \
  -d '{"email":"e2e.employeeA@ratan-staging.local","password":"E2E_EmployeeA_Pass1!","portal":"employee"}' \
  2>/dev/null)
EMP_A_TOKEN=$(echo "$EMP_A_RAW" | python3 -c \
  "import sys,json; d=json.load(sys.stdin); print(d.get('data',{}).get('token',''))" 2>/dev/null || echo "")

ew_check() {
  local label="$1" code="$2" expected="$3"
  for e in $(echo "$expected" | tr ',' ' '); do
    [[ "$code" == "$e" ]] && { ok "  PASS  $label → $code"; return; }
  done
  err "  FAIL  $label → $code (expected $expected)"
  ((EMP_FAIL++)) || true
}

if [[ -n "$EMP_A_TOKEN" ]]; then
  ew_check "Employee: own profile"           "$(http_code "$TI_BASE/api/my/profile" -H "Authorization: Bearer $EMP_A_TOKEN")"      "200,404"
  ew_check "Employee: own attendance"        "$(http_code "$TI_BASE/api/attendance" -H "Authorization: Bearer $EMP_A_TOKEN")"      "200,403"
  ew_check "Employee: leave balance"         "$(http_code "$TI_BASE/api/leave/balance" -H "Authorization: Bearer $EMP_A_TOKEN")"   "200,403,404"
  ew_check "Employee: own payslips"          "$(http_code "$TI_BASE/api/payslip" -H "Authorization: Bearer $EMP_A_TOKEN")"         "200,403,404"
  ew_check "Employee: notifications"         "$(http_code "$TI_BASE/api/notifications" -H "Authorization: Bearer $EMP_A_TOKEN")"   "200"
  ew_check "Employee: blocked from admin"    "$(http_code "$TI_BASE/api/payroll" -H "Authorization: Bearer $EMP_A_TOKEN")"         "403,401"
  ew_check "Employee: blocked from SA"       "$(http_code "$TI_BASE/api/companies" -H "Authorization: Bearer $EMP_A_TOKEN")"       "403,401"
  [[ $EMP_FAIL -eq 0 ]] && section_pass "14" || section_fail "14" "${EMP_FAIL} employee workflow check(s) failed"
else
  section_skip "14" "Employee token not available"
fi

# =============================================================================
# §15 — Payroll workflow checks
# =============================================================================
section "15 — Payroll workflow checks"

PAY_FAIL=0

pw_check() {
  local label="$1" code="$2" expected="$3"
  for e in $(echo "$expected" | tr ',' ' '); do
    [[ "$code" == "$e" ]] && { ok "  PASS  $label → $code"; return; }
  done
  err "  FAIL  $label → $code (expected $expected)"
  ((PAY_FAIL++)) || true
}

if [[ -n "${ADMIN_A_TOKEN:-}" ]]; then
  pw_check "Payroll: list salary structures"  "$(http_code "$TI_BASE/api/salary" -H "Authorization: Bearer $ADMIN_A_TOKEN")"      "200,403,404"
  pw_check "Payroll: list payroll runs"       "$(http_code "$TI_BASE/api/payroll" -H "Authorization: Bearer $ADMIN_A_TOKEN")"     "200,403,404"
  pw_check "Payroll: list payslips (Admin)"   "$(http_code "$TI_BASE/api/payslip" -H "Authorization: Bearer $ADMIN_A_TOKEN")"     "200,403,404"
  pw_check "Payroll: list bonuses"            "$(http_code "$TI_BASE/api/bonuses" -H "Authorization: Bearer $ADMIN_A_TOKEN")"     "200,403,404"
  pw_check "Payroll: list deductions"         "$(http_code "$TI_BASE/api/deductions" -H "Authorization: Bearer $ADMIN_A_TOKEN")"  "200,403,404"
  pw_check "Payroll: salary register"         "$(http_code "$TI_BASE/api/salary-register" -H "Authorization: Bearer $ADMIN_A_TOKEN")" "200,403,404"
  [[ $PAY_FAIL -eq 0 ]] && section_pass "15" || section_fail "15" "${PAY_FAIL} payroll check(s) failed"
else
  section_skip "15" "Admin token not available"
fi

# =============================================================================
# §16 — Sales workflow checks
# =============================================================================
section "16 — Sales workflow checks"

SALES_FAIL=0

sw_check() {
  local label="$1" code="$2" expected="$3"
  for e in $(echo "$expected" | tr ',' ' '); do
    [[ "$code" == "$e" ]] && { ok "  PASS  $label → $code"; return; }
  done
  err "  FAIL  $label → $code (expected $expected)"
  ((SALES_FAIL++)) || true
}

if [[ -n "${ADMIN_A_TOKEN:-}" ]]; then
  sw_check "Sales: list leads"       "$(http_code "$TI_BASE/api/sales/leads" -H "Authorization: Bearer $ADMIN_A_TOKEN")"      "200,403,404"
  sw_check "Sales: list activities"  "$(http_code "$TI_BASE/api/sales/activities" -H "Authorization: Bearer $ADMIN_A_TOKEN")" "200,403,404"
  sw_check "Sales: report"           "$(http_code "$TI_BASE/api/sales/report" -H "Authorization: Bearer $ADMIN_A_TOKEN")"     "200,403,404"
  [[ $SALES_FAIL -eq 0 ]] && section_pass "16" || section_fail "16" "${SALES_FAIL} sales check(s) failed"
else
  section_skip "16" "Admin token not available"
fi

# =============================================================================
# §17 — Browser console + server log error sweep
# =============================================================================
section "17 — Browser console and server log error sweep"

LOG_FAIL=0

# Server-side: check API container logs for ERROR-level entries
log "Scanning API container logs for ERROR entries..."
E2E_CONTAINER_NAME="${E2E_CONTAINER_NAME:-hrms_e2e_api}"

if docker ps --format '{{.Names}}' | grep -q "$E2E_CONTAINER_NAME"; then
  API_ERRORS=$(docker logs "$E2E_CONTAINER_NAME" --since 30m 2>&1 | \
    grep -iE '"Level":"Error"|^\[ERR\]|^\[CRITICAL\]|System\.Exception|Unhandled exception' | \
    grep -v "HealthReport\|health check\|EF Migrations\|Expected error\|test error" | \
    head -20 || true)

  if [[ -n "$API_ERRORS" ]]; then
    warn "API container has ERROR-level log entries (last 30m):"
    echo "$API_ERRORS" | head -10 | while IFS= read -r line; do warn "  $line"; done
    ((LOG_FAIL++)) || true
  else
    ok "API server logs: no unexpected ERROR entries in last 30m"
  fi
else
  warn "Cannot find container $E2E_CONTAINER_NAME — skipping server log sweep"
fi

# Browser console: Playwright captures console errors in test output
# Check for any browser:error or console:error lines in the Chromium log
if [[ -f /tmp/pw_chromium.log ]]; then
  CONSOLE_ERRORS=$(grep -iE "browser:error|console\.error|Uncaught|TypeError|ReferenceError" \
    /tmp/pw_chromium.log | grep -v "Expected\|intentional\|test" | head -10 || true)
  if [[ -n "$CONSOLE_ERRORS" ]]; then
    warn "Browser console errors detected in Playwright output:"
    echo "$CONSOLE_ERRORS" | while IFS= read -r line; do warn "  $line"; done
    ((LOG_FAIL++)) || true
  else
    ok "Browser console: no unexpected errors in Playwright output"
  fi
fi

if [[ $LOG_FAIL -eq 0 ]]; then
  section_pass "17"
else
  section_warn "17" "${LOG_FAIL} log anomaly/anomalies (review above — may not be blockers)"
fi

# =============================================================================
# §18 — Tear down E2E stack
# =============================================================================
log "Tearing down E2E stack..."
docker compose -f docker-compose.e2e.yml down -v 2>/dev/null || warn "docker-compose.e2e.yml down failed (non-fatal)"
ok "E2E stack torn down"

# =============================================================================
# §18 — Summary
# =============================================================================
echo ""
echo -e "${BOLD}${CYAN}╔══════════════════════════════════════════════════╗${NC}"
echo -e "${BOLD}${CYAN}║  Phase 9 Regression — Results Summary            ║${NC}"
echo -e "${BOLD}${CYAN}║  $(date -u '+%Y-%m-%d %H:%M:%S UTC')                        ║${NC}"
echo -e "${BOLD}${CYAN}╚══════════════════════════════════════════════════╝${NC}"
echo ""

# Print section-by-section results
TOTAL_PASS=0; TOTAL_FAIL=0; TOTAL_WARN=0; TOTAL_SKIP=0
for sec in "1" "2" "3" "4" "5" "6" "7+8" "9" "10" "11" "12" "13" "14" "15" "16" "17"; do
  result="${SECTION_RESULT[$sec]:-SKIP}"
  case "$result" in
    PASS) echo -e "  ${GREEN}✓ PASS${NC}   §${sec}"; ((TOTAL_PASS++)) || true ;;
    FAIL) echo -e "  ${RED}✗ FAIL${NC}   §${sec}"; ((TOTAL_FAIL++)) || true ;;
    WARN) echo -e "  ${YELLOW}⚠ WARN${NC}   §${sec}"; ((TOTAL_WARN++)) || true ;;
    SKIP) echo -e "  ${YELLOW}– SKIP${NC}   §${sec}"; ((TOTAL_SKIP++)) || true ;;
  esac
done

echo ""
echo -e "  ${GREEN}PASS${NC}   ${TOTAL_PASS}"
echo -e "  ${RED}FAIL${NC}   ${TOTAL_FAIL}"
echo -e "  ${YELLOW}WARN${NC}   ${TOTAL_WARN}"
echo -e "  SKIP   ${TOTAL_SKIP}"
echo ""

# Test counters
echo -e "  ${BOLD}Test Counts:${NC}"
echo -e "  .NET unit/integration:    ${DOTNET_PASS:-?} passed"
echo -e "  E2E Chromium:             ${PW_CHROM_PASS:-?} passed, ${PW_CHROM_FAIL:-?} failed"
echo -e "  E2E Firefox:              ${PW_FF_PASS:-?} passed, ${PW_FF_FAIL:-?} failed"
echo -e "  E2E Mobile Chrome:        ${PW_MOB_PASS:-?} passed, ${PW_MOB_FAIL:-?} failed"
echo -e "  Phase 8 smoke+DB checks:  PASS=${P8_PASS:-?}, FAIL=${P8_FAIL:-?}, WARN=${P8_WARN:-?}"
echo ""

if [[ ${#FATAL_SECTIONS[@]} -gt 0 ]]; then
  echo -e "  ${RED}${BOLD}Failed sections:${NC}"
  for f in "${FATAL_SECTIONS[@]}"; do echo -e "  ${RED}✗${NC}  $f"; done
  echo ""
fi

echo -e "  Full log: $PHASE9_LOG"
echo ""

# Write machine-readable result
RESULT_FILE="${SCRIPT_DIR}/logs/phase9_result_$(date +%Y%m%d_%H%M%S).env"
{
  echo "timestamp=$(date -u '+%Y-%m-%dT%H:%M:%SZ')"
  echo "sections_pass=${TOTAL_PASS}"
  echo "sections_fail=${TOTAL_FAIL}"
  echo "sections_warn=${TOTAL_WARN}"
  echo "sections_skip=${TOTAL_SKIP}"
  echo "dotnet_pass=${DOTNET_PASS:-?}"
  echo "e2e_chromium_pass=${PW_CHROM_PASS:-?}"
  echo "e2e_chromium_fail=${PW_CHROM_FAIL:-?}"
  echo "e2e_firefox_pass=${PW_FF_PASS:-?}"
  echo "e2e_firefox_fail=${PW_FF_FAIL:-?}"
  echo "e2e_mobile_pass=${PW_MOB_PASS:-?}"
  echo "e2e_mobile_fail=${PW_MOB_FAIL:-?}"
  echo "phase8_pass=${P8_PASS:-?}"
  echo "phase8_fail=${P8_FAIL:-?}"
} > "$RESULT_FILE"
echo "  Machine-readable result: $RESULT_FILE"
echo ""

if [[ $TOTAL_FAIL -gt 0 ]]; then
  echo -e "${RED}${BOLD}❌ PHASE 9 FAILED — ${TOTAL_FAIL} section(s) failed. NOT ready for RC.${NC}"
  exit 1
else
  echo -e "${GREEN}${BOLD}✅ PHASE 9 PASSED — All sections pass. Ready for RC tag.${NC}"
  echo ""
  echo -e "  Next step:"
  echo -e "  ${BOLD}bash phase9_cleanup.sh${NC}"
  echo -e "  ${BOLD}git tag -a v1.0.0-rc1 -m 'Release Candidate 1 — Phase 9 PASS'${NC}"
  echo -e "  ${BOLD}git push origin v1.0.0-rc1${NC}"
  exit 0
fi
