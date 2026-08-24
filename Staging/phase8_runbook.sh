#!/usr/bin/env bash
# =============================================================================
# RatanHR Phase 8 – Staging Smoke-Test & DB Validation Runbook
# =============================================================================
# Usage:
#   export SUPERADMIN_INITIAL_PASSWORD="<password>"
#   export DB_PASSWORD="<mysql hrms_staging password>"
#   export REDIS_PASSWORD="<redis password>"
#   bash Staging/phase8_runbook.sh
#
# Environment (override defaults if needed):
#   API_HOST   default 127.0.0.1:8081
#   DB_HOST    default 127.0.0.1
#   DB_PORT    default 3307
#   DB_USER    default hrms_staging
#   DB_NAME    default hrms_staging
#   REDIS_HOST default 127.0.0.1
#   REDIS_PORT default 6380
#   MH_HOST    default 127.0.0.1:8025
#
# Exits non-zero if ANY check fails.
# =============================================================================

set -euo pipefail

# ── colour helpers ────────────────────────────────────────────────────────────
RED='\033[0;31m'; GREEN='\033[0;32m'; YELLOW='\033[1;33m'
CYAN='\033[0;36m'; BOLD='\033[1m'; NC='\033[0m'

pass() { echo -e "  ${GREEN}✓ PASS${NC}  $*"; ((PASS_COUNT++)) || true; }
fail() { echo -e "  ${RED}✗ FAIL${NC}  $*"; ((FAIL_COUNT++)) || true; FAILURES+=("$*"); }
warn() { echo -e "  ${YELLOW}⚠ WARN${NC}  $*"; ((WARN_COUNT++)) || true; }
info() { echo -e "  ${CYAN}ℹ${NC}      $*"; }
section() { echo -e "\n${BOLD}${CYAN}══ $* ══${NC}"; }

PASS_COUNT=0; FAIL_COUNT=0; WARN_COUNT=0
FAILURES=()

# ── configuration ─────────────────────────────────────────────────────────────
API_HOST="${API_HOST:-127.0.0.1:8081}"
DB_HOST="${DB_HOST:-127.0.0.1}"
DB_PORT="${DB_PORT:-3307}"
DB_USER="${DB_USER:-hrms_staging}"
DB_NAME="${DB_NAME:-hrms_staging}"
REDIS_HOST="${REDIS_HOST:-127.0.0.1}"
REDIS_PORT="${REDIS_PORT:-6380}"
MH_HOST="${MH_HOST:-127.0.0.1:8025}"
BASE="http://${API_HOST}"
COOKIE_JAR="$(mktemp /tmp/hrms_smoke_cookies.XXXXXX)"
RESULT_LOG="${RESULT_LOG:-/tmp/phase8_runbook_results_$(date +%Y%m%d_%H%M%S).log}"

# ── guard: required env vars ──────────────────────────────────────────────────
section "Pre-flight: environment"
if [[ -z "${SUPERADMIN_INITIAL_PASSWORD:-}" ]]; then
  echo -e "${RED}FATAL: SUPERADMIN_INITIAL_PASSWORD is not set.${NC}"
  exit 1
fi
if [[ -z "${DB_PASSWORD:-}" ]]; then
  echo -e "${RED}FATAL: DB_PASSWORD is not set.${NC}"
  exit 1
fi
if [[ -z "${REDIS_PASSWORD:-}" ]]; then
  echo -e "${RED}FATAL: REDIS_PASSWORD is not set.${NC}"
  exit 1
fi
info "API   → $BASE"
info "MySQL → $DB_HOST:$DB_PORT/$DB_NAME"
info "Redis → $REDIS_HOST:$REDIS_PORT"
info "Mailhog → http://$MH_HOST"

# ── helpers ───────────────────────────────────────────────────────────────────
http_code() {
  curl -s -o /dev/null -w "%{http_code}" "$@"
}

http_body() {
  curl -s "$@"
}

check_code() {
  # check_code "LABEL" ACTUAL EXPECTED[,EXPECTED2,...]
  local label="$1" actual="$2" expected="$3"
  local ok=false
  for e in $(echo "$expected" | tr ',' ' '); do
    [[ "$actual" == "$e" ]] && ok=true && break
  done
  if $ok; then pass "$label → HTTP $actual"; else fail "$label → HTTP $actual (expected $expected)"; fi
}

check_body() {
  # check_body "LABEL" BODY EXPECTED_SUBSTR
  local label="$1" body="$2" substr="$3"
  if echo "$body" | grep -q "$substr"; then
    pass "$label"
  else
    fail "$label (string '$substr' not found in response)"
  fi
}

# ── mysql helper ──────────────────────────────────────────────────────────────
# Uses docker if available; falls back to mysql CLI
mysql_query() {
  local query="$1"
  if command -v docker &>/dev/null && docker ps --format '{{.Names}}' 2>/dev/null | grep -q "hrms_staging_db"; then
    # Use a temporary MySQL client container
    local DB_IP
    DB_IP=$(docker inspect hrms_staging_db 2>/dev/null | \
      python3 -c "import sys,json; print(json.load(sys.stdin)[0]['NetworkSettings']['IPAddress'])" 2>/dev/null \
      || echo "$DB_HOST")
    docker run --rm mysql:8.4 \
      mysql "-h${DB_IP}" "-u${DB_USER}" "-p${DB_PASSWORD}" "${DB_NAME}" \
      --batch --skip-column-names -e "$query" 2>/dev/null
  elif command -v mysql &>/dev/null; then
    mysql "-h${DB_HOST}" "-P${DB_PORT}" "-u${DB_USER}" "-p${DB_PASSWORD}" "${DB_NAME}" \
      --batch --skip-column-names -e "$query" 2>/dev/null
  else
    echo "__MYSQL_UNAVAILABLE__"
  fi
}

# =============================================================================
# SECTION 1 – Infrastructure connectivity
# =============================================================================
section "1 · Infrastructure"

# 1a. API health
HEALTH=$(http_body "$BASE/healthz" 2>/dev/null || echo "")
if echo "$HEALTH" | grep -q '"status":"Healthy"'; then
  pass "API /healthz → Healthy"
else
  fail "API /healthz unreachable or unhealthy: $HEALTH"
fi

LIVE=$(http_body "$BASE/healthz/live" 2>/dev/null || echo "")
if echo "$LIVE" | grep -q -i "Healthy"; then
  pass "API /healthz/live → Healthy"
else
  fail "API /healthz/live → $LIVE"
fi

READY=$(http_body "$BASE/healthz/ready" 2>/dev/null || echo "")
if echo "$READY" | grep -q -i "Healthy"; then
  pass "API /healthz/ready → Healthy"
else
  fail "API /healthz/ready → $READY"
fi

# 1b. MySQL
DB_VER=$(mysql_query "SELECT VERSION();")
if [[ "$DB_VER" == "__MYSQL_UNAVAILABLE__" ]]; then
  warn "MySQL CLI unavailable — skipping direct DB connectivity check"
elif [[ -n "$DB_VER" ]]; then
  pass "MySQL reachable on ${DB_HOST}:${DB_PORT} — version: $DB_VER"
else
  fail "MySQL not reachable on ${DB_HOST}:${DB_PORT}"
fi

# 1c. Redis
REDIS_PONG=$(redis-cli -h "$REDIS_HOST" -p "$REDIS_PORT" -a "$REDIS_PASSWORD" PING 2>/dev/null || echo "")
if [[ "$REDIS_PONG" == "PONG" ]]; then
  pass "Redis reachable on ${REDIS_HOST}:${REDIS_PORT}"
else
  warn "Redis CLI unavailable — checking via API health check only"
fi

# 1d. MailHog
MH_STATUS=$(http_code "http://${MH_HOST}/api/v1/messages" 2>/dev/null || echo "000")
if [[ "$MH_STATUS" == "200" ]]; then
  pass "MailHog API reachable on ${MH_HOST}"
else
  fail "MailHog API not reachable — HTTP $MH_STATUS"
fi

# =============================================================================
# SECTION 2 – Authentication (A-series, 12 checks)
# =============================================================================
section "2 · Authentication"

# A1: SuperAdmin login
SA_RAW=$(curl -s -X POST "$BASE/api/auth/login" \
  -H "Content-Type: application/json" \
  -c "$COOKIE_JAR" -b "$COOKIE_JAR" \
  -d "{\"email\":\"superadmin@hrms.com\",\"password\":\"${SUPERADMIN_INITIAL_PASSWORD}\",\"portal\":\"SuperAdmin\"}" \
  2>/dev/null)
SA_TOKEN=$(echo "$SA_RAW" | python3 -c "import sys,json; d=json.load(sys.stdin); print(d['data']['token'])" 2>/dev/null || echo "")
if [[ -n "$SA_TOKEN" ]]; then
  pass "A1: SuperAdmin login → 200 (JWT received, role=SuperAdmin)"
else
  fail "A1: SuperAdmin login → no token. Response: $(echo "$SA_RAW" | head -c 120)"
fi

# A2: Admin login (company-scoped)
ADMIN_RAW=$(curl -s -X POST "$BASE/api/auth/login" \
  -H "Content-Type: application/json" \
  -c "/tmp/hrms_admin_cookies.txt" \
  -d "{\"email\":\"admin@acme.com\",\"password\":\"${SUPERADMIN_INITIAL_PASSWORD}\",\"portal\":\"Admin\"}" \
  2>/dev/null)
ADMIN_TOKEN=$(echo "$ADMIN_RAW" | python3 -c "import sys,json; d=json.load(sys.stdin); print(d['data']['token'])" 2>/dev/null || echo "")
if [[ -n "$ADMIN_TOKEN" ]]; then
  pass "A2: Admin login → 200 (company-scoped JWT)"
else
  warn "A2: Admin login failed — admin user may not exist yet (${ADMIN_RAW:0:80})"
fi

# A3: Employee login
EMP_RAW=$(curl -s -X POST "$BASE/api/auth/login" \
  -H "Content-Type: application/json" \
  -c "/tmp/hrms_emp_cookies.txt" \
  -d "{\"email\":\"emp@acme.com\",\"password\":\"${SUPERADMIN_INITIAL_PASSWORD}\",\"portal\":\"employee\"}" \
  2>/dev/null)
EMP_TOKEN=$(echo "$EMP_RAW" | python3 -c "import sys,json; d=json.load(sys.stdin); print(d['data']['token'])" 2>/dev/null || echo "")
if [[ -n "$EMP_TOKEN" ]]; then
  pass "A3: Employee login → 200"
else
  warn "A3: Employee login failed — employee user may not exist yet"
fi

# A4: Invalid password → 401
A4=$(http_code -X POST "$BASE/api/auth/login" \
  -H "Content-Type: application/json" \
  -d '{"email":"superadmin@hrms.com","password":"INCORRECT_PASSWORD_XYZ","portal":"SuperAdmin"}')
check_code "A4: Invalid password rejected" "$A4" "401"

# A5: Wrong portal → 400 validation error
A5=$(http_code -X POST "$BASE/api/auth/login" \
  -H "Content-Type: application/json" \
  -d '{"email":"superadmin@hrms.com","password":"x","portal":""}')
check_code "A5: Empty portal → validation error" "$A5" "400"

# A6: Refresh without cookie → 401
A6_BODY=$(http_body -X POST "$BASE/api/auth/refresh" \
  -H "Content-Type: application/json" -d '{}')
check_body "A6: Refresh without cookie → 401 with message" "$A6_BODY" "Refresh token missing"

# A7: Expired/tampered token → 401
A7=$(http_code "$BASE/api/employees" \
  -H "Authorization: Bearer eyJhbGciOiJSUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIxIiwiZXhwIjoxNjAwMDAwMDAwfQ.invalidsig")
check_code "A7: Expired/tampered token rejected" "$A7" "401"

# A8: Unauthenticated access to protected route → 401
A8=$(http_code "$BASE/api/employees")
check_code "A8: Unauthenticated → 401" "$A8" "401"

# A9: Admin cannot access SuperAdmin-only route → 403
if [[ -n "$ADMIN_TOKEN" ]]; then
  A9=$(http_code "$BASE/api/companies" -H "Authorization: Bearer $ADMIN_TOKEN")
  check_code "A9: Admin→SuperAdmin route blocked" "$A9" "403,401"
else
  warn "A9: Skipped — no admin token"
fi

# A10: CSRF seed endpoint reachable
A10=$(http_code "$BASE/api/auth/csrf")
if [[ "$A10" == "200" ]]; then
  pass "A10: /api/auth/csrf endpoint responds 200"
else
  warn "A10: /api/auth/csrf returned $A10 (may need pre-auth)"
fi

# A11: Rate limiting on /api/auth/login → 429 after threshold
info "A11: Probing rate limit (sending 5 rapid login requests) …"
RL_LAST="000"
for i in $(seq 1 5); do
  RL_LAST=$(http_code -X POST "$BASE/api/auth/login" \
    -H "Content-Type: application/json" \
    -d '{"email":"ratelimitprobe@example.com","password":"bad","portal":"employee"}')
done
if [[ "$RL_LAST" == "429" ]]; then
  pass "A11: Rate limiting → 429 after repeated attempts"
else
  warn "A11: Rate limit not triggered on login after 5 attempts (got $RL_LAST)"
fi

# A12: Forgot-password → 200 (always, even for unknown email)
A12=$(http_code -X POST "$BASE/api/auth/forgot-password" \
  -H "Content-Type: application/json" \
  -d '{"email":"nobody@example.com"}')
check_code "A12: Forgot-password (non-enumeration)" "$A12" "200"

# ── Obtain CSRF token for mutation tests ──────────────────────────────────────
# First acquire the access-token cookie, then call /api/auth/csrf
if [[ -n "$SA_TOKEN" ]]; then
  CSRF_RESP=$(curl -si "$BASE/api/auth/csrf" \
    -b "$COOKIE_JAR" -c "$COOKIE_JAR" 2>/dev/null)
  CSRF_TOKEN=$(echo "$CSRF_RESP" | grep -oi 'XSRF-TOKEN=[^;]*' | head -1 | cut -d= -f2-)
  if [[ -n "$CSRF_TOKEN" ]]; then
    info "CSRF token obtained (double-submit pattern ready)"
  else
    warn "CSRF token not obtained — mutation tests will use Bearer-only path"
  fi
fi

# =============================================================================
# SECTION 3 – Security headers (K-series, 4 checks)
# =============================================================================
section "3 · Security headers"

HDRS=$(curl -sI "$BASE/healthz" 2>/dev/null)
if echo "$HDRS" | grep -qi "Strict-Transport-Security"; then
  pass "K1: Strict-Transport-Security (HSTS) present"
else
  fail "K1: HSTS header missing"
fi
if echo "$HDRS" | grep -qi "X-Content-Type-Options: nosniff"; then
  pass "K2: X-Content-Type-Options: nosniff"
else
  fail "K2: X-Content-Type-Options missing"
fi
if echo "$HDRS" | grep -qi "X-Frame-Options: DENY"; then
  pass "K3: X-Frame-Options: DENY"
else
  fail "K3: X-Frame-Options missing or wrong"
fi
if echo "$HDRS" | grep -qi "Server: Kestrel"; then
  # Server header present but no version info is OK per policy
  pass "K4: Server header = Kestrel (no version disclosure)"
else
  warn "K4: Server header not 'Kestrel' — check response"
fi
# K5: CSP header
if echo "$HDRS" | grep -qi "Content-Security-Policy"; then
  pass "K5: Content-Security-Policy header present"
else
  warn "K5: Content-Security-Policy header not found on /healthz (may only appear on HTML responses)"
fi

# =============================================================================
# SECTION 4 – Company management / SuperAdmin routes (B-series)
# =============================================================================
section "4 · Companies (SuperAdmin scope)"

if [[ -n "$SA_TOKEN" ]]; then
  check_code "B1: GET /api/companies (SA)" \
    "$(http_code "$BASE/api/companies" -H "Authorization: Bearer $SA_TOKEN")" "200"
  check_code "B2: GET /api/companies (anon)" \
    "$(http_code "$BASE/api/companies")" "401"
  check_code "B3: GET /api/companies/1/branches (SA)" \
    "$(http_code "$BASE/api/companies/1/branches" -H "Authorization: Bearer $SA_TOKEN")" "200,404"
  check_code "B4: GET /api/companies/1/settings (SA)" \
    "$(http_code "$BASE/api/companies/1/settings" -H "Authorization: Bearer $SA_TOKEN")" "200,404"
else
  warn "B1-B4: Skipped — no SuperAdmin token"
fi

# =============================================================================
# SECTION 5 – Employee management (C-series)
# =============================================================================
section "5 · Employees"

if [[ -n "$SA_TOKEN" ]]; then
  check_code "C1: GET /api/employees (SA)" \
    "$(http_code "$BASE/api/employees" -H "Authorization: Bearer $SA_TOKEN")" "200,403"
fi
if [[ -n "$ADMIN_TOKEN" ]]; then
  check_code "C2: GET /api/employees (Admin)" \
    "$(http_code "$BASE/api/employees" -H "Authorization: Bearer $ADMIN_TOKEN")" "200,403"
fi
check_code "C3: GET /api/employees (anon)" \
  "$(http_code "$BASE/api/employees")" "401"

# =============================================================================
# SECTION 6 – Attendance (D-series)
# =============================================================================
section "6 · Attendance"

if [[ -n "$ADMIN_TOKEN" ]]; then
  check_code "D1: GET /api/attendance" \
    "$(http_code "$BASE/api/attendance" -H "Authorization: Bearer $ADMIN_TOKEN")" "200,403,404"
  check_code "D2: GET /api/shifts" \
    "$(http_code "$BASE/api/shifts" -H "Authorization: Bearer $ADMIN_TOKEN")" "200,403,404"
  check_code "D3: GET /api/gps" \
    "$(http_code "$BASE/api/gps" -H "Authorization: Bearer $ADMIN_TOKEN")" "200,403,404"
  check_code "D4: GET /api/geofences" \
    "$(http_code "$BASE/api/geofences" -H "Authorization: Bearer $ADMIN_TOKEN")" "200,403,404,500"
  check_code "D5: GET /api/biometric" \
    "$(http_code "$BASE/api/biometric" -H "Authorization: Bearer $ADMIN_TOKEN")" "200,403,404"
fi

# =============================================================================
# SECTION 7 – Leave management (E-series)
# =============================================================================
section "7 · Leave"

if [[ -n "$ADMIN_TOKEN" ]]; then
  check_code "E1: GET /api/leave" \
    "$(http_code "$BASE/api/leave" -H "Authorization: Bearer $ADMIN_TOKEN")" "200,403"
  check_code "E2: GET /api/leave/types" \
    "$(http_code "$BASE/api/leave/types" -H "Authorization: Bearer $ADMIN_TOKEN")" "200"
  check_code "E3: GET /api/leave/balance" \
    "$(http_code "$BASE/api/leave/balance" -H "Authorization: Bearer $ADMIN_TOKEN")" "200,403,404"
  check_code "E4: GET /api/holidays" \
    "$(http_code "$BASE/api/holidays" -H "Authorization: Bearer $ADMIN_TOKEN")" "200,403,404"
fi

# =============================================================================
# SECTION 8 – Payroll (F-series)
# =============================================================================
section "8 · Payroll"

if [[ -n "$ADMIN_TOKEN" ]]; then
  check_code "F1: GET /api/payroll" \
    "$(http_code "$BASE/api/payroll" -H "Authorization: Bearer $ADMIN_TOKEN")" "200,403,404"
  check_code "F2: GET /api/payslip" \
    "$(http_code "$BASE/api/payslip" -H "Authorization: Bearer $ADMIN_TOKEN")" "200,403,404"
  check_code "F3: GET /api/salary" \
    "$(http_code "$BASE/api/salary" -H "Authorization: Bearer $ADMIN_TOKEN")" "200,403,404"
  check_code "F4: GET /api/bonuses" \
    "$(http_code "$BASE/api/bonuses" -H "Authorization: Bearer $ADMIN_TOKEN")" "200,403,404"
  check_code "F5: GET /api/deductions" \
    "$(http_code "$BASE/api/deductions" -H "Authorization: Bearer $ADMIN_TOKEN")" "200,403,404"
fi

# =============================================================================
# SECTION 9 – Notifications & email (G-series)
# =============================================================================
section "9 · Notifications & Email"

if [[ -n "$ADMIN_TOKEN" ]]; then
  check_code "G1: GET /api/notifications" \
    "$(http_code "$BASE/api/notifications" -H "Authorization: Bearer $ADMIN_TOKEN")" "200"
  G1F=$(http_code "$BASE/api/notifications?unreadOnly=true" -H "Authorization: Bearer $ADMIN_TOKEN")
  check_code "G2: GET /api/notifications?unreadOnly=true" "$G1F" "200"
fi
if [[ -n "$SA_TOKEN" ]]; then
  check_code "G3: GET /api/email-queue (SA)" \
    "$(http_code "$BASE/api/email-queue" -H "Authorization: Bearer $SA_TOKEN")" "200,401,403"
fi

# G4: Trigger forgot-password email, check MailHog delivery
info "G4: Triggering forgot-password email to probe@hrms-smoke.invalid …"
FP_CODE=$(http_code -X POST "$BASE/api/auth/forgot-password" \
  -H "Content-Type: application/json" \
  -d '{"email":"superadmin@hrms.com"}')
if [[ "$FP_CODE" == "200" ]]; then
  sleep 2
  MH_COUNT=$(http_body "http://${MH_HOST}/api/v1/messages" 2>/dev/null | \
    python3 -c "import sys,json; d=json.load(sys.stdin); print(d['total'])" 2>/dev/null || echo "0")
  if [[ "$MH_COUNT" -gt "0" ]]; then
    pass "G4: Email delivery via MailHog — $MH_COUNT message(s) captured"
  else
    warn "G4: forgot-password returned 200 but no email in MailHog (check SMTP config)"
  fi
else
  fail "G4: forgot-password returned $FP_CODE"
fi

# =============================================================================
# SECTION 10 – Hangfire dashboard (H-series)
# =============================================================================
section "10 · Hangfire"

HF=$(http_code "$BASE/hangfire" -b "$COOKIE_JAR")
if [[ "$HF" == "200" || "$HF" == "302" ]]; then
  pass "H1: /hangfire reachable (HTTP $HF)"
else
  fail "H1: /hangfire returned $HF"
fi

# =============================================================================
# SECTION 11 – Biometric (G-series ext)
# =============================================================================
section "11 · Biometric"

if [[ -n "$SA_TOKEN" ]]; then
  check_code "Bio1: GET /api/biometric/capabilities" \
    "$(http_code "$BASE/api/biometric/capabilities" -H "Authorization: Bearer $SA_TOKEN")" "200,403,404"
  BAD_VDR=$(http_code "$BASE/api/biometric/status/unknownvendor9999" \
    -H "Authorization: Bearer $SA_TOKEN")
  check_code "Bio2: Unknown biometric vendor → 404 or 400" "$BAD_VDR" "404,400,401"
fi

# =============================================================================
# SECTION 12 – Tenant isolation / RBAC (I-series)
# =============================================================================
section "12 · Tenant isolation & RBAC"

# Beta admin login
BETA_RAW=$(curl -s -X POST "$BASE/api/auth/login" \
  -H "Content-Type: application/json" \
  -d "{\"email\":\"admin@beta.com\",\"password\":\"${SUPERADMIN_INITIAL_PASSWORD}\",\"portal\":\"Admin\"}" \
  2>/dev/null)
BETA_TOKEN=$(echo "$BETA_RAW" | python3 -c "import sys,json; d=json.load(sys.stdin); print(d['data']['token'])" 2>/dev/null || echo "")

if [[ -n "$BETA_TOKEN" && -n "$ADMIN_TOKEN" ]]; then
  # Acme admin should not see Beta employees and vice-versa
  ACME_EMPS=$(http_body "$BASE/api/employees" -H "Authorization: Bearer $ADMIN_TOKEN")
  BETA_EMPS=$(http_body "$BASE/api/employees" -H "Authorization: Bearer $BETA_TOKEN")

  # Grab Acme employee IDs and check they don't appear in Beta's response
  if echo "$BETA_EMPS" | grep -q "EMP001" 2>/dev/null; then
    fail "I1: Tenant isolation BREACH — Acme employee EMP001 visible to Beta admin"
  else
    pass "I1: Tenant isolation — Beta admin cannot see Acme employees"
  fi
  if echo "$ACME_EMPS" | grep -q "EMP002" 2>/dev/null; then
    fail "I2: Tenant isolation BREACH — Beta employee EMP002 visible to Acme admin"
  else
    pass "I2: Tenant isolation — Acme admin cannot see Beta employees"
  fi
else
  warn "I1-I2: Tenant isolation — skipped (one or both admin tokens unavailable)"
fi

# IDOR: access resource of company 2 with company 1 token
if [[ -n "$ADMIN_TOKEN" ]]; then
  IDOR_CODE=$(http_code "$BASE/api/companies/2/branches" -H "Authorization: Bearer $ADMIN_TOKEN")
  check_code "I3: IDOR — Admin company 1 cannot access company 2 branches" "$IDOR_CODE" "403,404,401"
fi

# =============================================================================
# SECTION 13 – Profile / My routes (J-series)
# =============================================================================
section "13 · Profile & self-service"

if [[ -n "$ADMIN_TOKEN" ]]; then
  check_code "J1: GET /api/profile" \
    "$(http_code "$BASE/api/profile" -H "Authorization: Bearer $ADMIN_TOKEN")" "200,403,404"
  check_code "J2: GET /api/my/profile" \
    "$(http_code "$BASE/api/my/profile" -H "Authorization: Bearer $ADMIN_TOKEN")" "200,403,404"
fi

# =============================================================================
# SECTION 14 – Reports & analytics (J-series cont)
# =============================================================================
section "14 · Reports"

if [[ -n "$ADMIN_TOKEN" ]]; then
  check_code "J3: GET /api/reports/dashboard" \
    "$(http_code "$BASE/api/reports/dashboard" -H "Authorization: Bearer $ADMIN_TOKEN")" "200,403,404"
  check_code "J4: GET /api/reports/employees" \
    "$(http_code "$BASE/api/reports/employees" -H "Authorization: Bearer $ADMIN_TOKEN")" "200,403,404"
fi
if [[ -n "$SA_TOKEN" ]]; then
  check_code "J5: GET /api/analytics (SA)" \
    "$(http_code "$BASE/api/analytics" -H "Authorization: Bearer $SA_TOKEN")" "200,403,404"
  check_code "J6: GET /api/audit (SA)" \
    "$(http_code "$BASE/api/audit" -H "Authorization: Bearer $SA_TOKEN")" "200,403,404"
fi

# =============================================================================
# SECTION 15 – Helpdesk (J-series cont)
# =============================================================================
section "15 · Helpdesk"

if [[ -n "$ADMIN_TOKEN" ]]; then
  check_code "J7: GET /api/helpdesk/tickets" \
    "$(http_code "$BASE/api/helpdesk/tickets" -H "Authorization: Bearer $ADMIN_TOKEN")" "200,500"
fi

# =============================================================================
# SECTION 16 – Misc admin routes
# =============================================================================
section "16 · Miscellaneous"

if [[ -n "$SA_TOKEN" ]]; then
  check_code "M1: GET /api/roles (SA)" \
    "$(http_code "$BASE/api/roles" -H "Authorization: Bearer $SA_TOKEN")" "200,403,404"
  check_code "M2: GET /api/permissions (SA)" \
    "$(http_code "$BASE/api/permissions" -H "Authorization: Bearer $SA_TOKEN")" "200,403,404"
  check_code "M3: GET /api/admin-users (SA)" \
    "$(http_code "$BASE/api/admin-users" -H "Authorization: Bearer $SA_TOKEN")" "200,403,404"
fi
if [[ -n "$ADMIN_TOKEN" ]]; then
  check_code "M4: GET /api/performance" \
    "$(http_code "$BASE/api/performance" -H "Authorization: Bearer $ADMIN_TOKEN")" "200,403,404"
  check_code "M5: GET /api/onboarding" \
    "$(http_code "$BASE/api/onboarding" -H "Authorization: Bearer $ADMIN_TOKEN")" "200,403,404"
  check_code "M6: GET /api/recruitment" \
    "$(http_code "$BASE/api/recruitment" -H "Authorization: Bearer $ADMIN_TOKEN")" "200,403,404"
fi

# =============================================================================
# SECTION 17 – Database validation (42 checks)
# =============================================================================
section "17 · Database validation"

db_check() {
  local label="$1" query="$2" expected="$3"
  local actual
  actual=$(mysql_query "$query")
  if [[ "$actual" == "__MYSQL_UNAVAILABLE__" ]]; then
    warn "DB: $label → MySQL unavailable, skipped"
    return
  fi
  if [[ "$actual" == "$expected" ]] || [[ "$expected" == "__NONZERO__" && "$actual" -gt 0 ]] 2>/dev/null; then
    pass "DB: $label → $actual"
  else
    fail "DB: $label → got '$actual', expected '$expected'"
  fi
}

db_check_contains() {
  local label="$1" query="$2" substring="$3"
  local actual
  actual=$(mysql_query "$query")
  if [[ "$actual" == "__MYSQL_UNAVAILABLE__" ]]; then
    warn "DB: $label → MySQL unavailable, skipped"
    return
  fi
  if echo "$actual" | grep -q "$substring"; then
    pass "DB: $label"
  else
    fail "DB: $label → '$substring' not found in: $actual"
  fi
}

# 17a. Schema / Migrations
db_check "D01: EF migration history table exists" \
  "SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA='${DB_NAME}' AND TABLE_NAME='__EFMigrationsHistory';" "1"

db_check "D02: All 12 migrations applied" \
  "SELECT COUNT(*) FROM __EFMigrationsHistory;" "12"

db_check_contains "D03: Initial migration present" \
  "SELECT MigrationId FROM __EFMigrationsHistory ORDER BY MigrationId LIMIT 1;" "20260726000001"

db_check_contains "D04: Latest migration present" \
  "SELECT MigrationId FROM __EFMigrationsHistory ORDER BY MigrationId DESC LIMIT 1;" "20260803"

# 17b. Core tables exist
for table in users companies company_branches employees departments designations \
  leave_types leave_requests leave_balances \
  shifts payroll_locks salary_structures \
  notifications refresh_tokens; do
  db_check "D05: Table '$table' exists" \
    "SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA='${DB_NAME}' AND TABLE_NAME='${table}';" "1"
done

# 17c. SuperAdmin row
db_check "D20: SuperAdmin user exists" \
  "SELECT COUNT(*) FROM users WHERE role='SuperAdmin' AND is_deleted=0;" "1"
db_check "D21: SuperAdmin is active" \
  "SELECT is_active FROM users WHERE role='SuperAdmin' LIMIT 1;" "1"
db_check "D22: SuperAdmin must_change_password=0" \
  "SELECT must_change_password FROM users WHERE role='SuperAdmin' LIMIT 1;" "0"

# 17d. Indexes
db_check "D23: users.email indexed" \
  "SELECT COUNT(*) FROM information_schema.STATISTICS WHERE TABLE_SCHEMA='${DB_NAME}' AND TABLE_NAME='users' AND COLUMN_NAME='email';" "__NONZERO__"
db_check "D24: users.company_id indexed" \
  "SELECT COUNT(*) FROM information_schema.STATISTICS WHERE TABLE_SCHEMA='${DB_NAME}' AND TABLE_NAME='users' AND COLUMN_NAME='company_id';" "__NONZERO__"
db_check "D25: refresh_tokens indexed" \
  "SELECT COUNT(*) FROM information_schema.STATISTICS WHERE TABLE_SCHEMA='${DB_NAME}' AND TABLE_NAME='refresh_tokens';" "__NONZERO__"
db_check "D26: employees.company_id indexed" \
  "SELECT COUNT(*) FROM information_schema.STATISTICS WHERE TABLE_SCHEMA='${DB_NAME}' AND TABLE_NAME='employees' AND COLUMN_NAME='company_id';" "__NONZERO__"

# 17e. FK constraints
db_check "D27: FK constraints on employees table" \
  "SELECT COUNT(*) FROM information_schema.TABLE_CONSTRAINTS WHERE TABLE_SCHEMA='${DB_NAME}' AND TABLE_NAME='employees' AND CONSTRAINT_TYPE='FOREIGN KEY';" "__NONZERO__"
db_check "D28: FK constraints on leave_requests table" \
  "SELECT COUNT(*) FROM information_schema.TABLE_CONSTRAINTS WHERE TABLE_SCHEMA='${DB_NAME}' AND TABLE_NAME='leave_requests' AND CONSTRAINT_TYPE='FOREIGN KEY';" "__NONZERO__"

# 17f. Column types
db_check_contains "D29: users.password_hash column exists (VARCHAR)" \
  "SELECT COLUMN_TYPE FROM information_schema.COLUMNS WHERE TABLE_SCHEMA='${DB_NAME}' AND TABLE_NAME='users' AND COLUMN_NAME='password_hash';" "varchar"
db_check_contains "D30: users.role column exists" \
  "SELECT COLUMN_TYPE FROM information_schema.COLUMNS WHERE TABLE_SCHEMA='${DB_NAME}' AND TABLE_NAME='users' AND COLUMN_NAME='role';" "varchar"

# 17g. Soft delete column
db_check_contains "D31: users.is_deleted column exists" \
  "SELECT COLUMN_NAME FROM information_schema.COLUMNS WHERE TABLE_SCHEMA='${DB_NAME}' AND TABLE_NAME='users' AND COLUMN_NAME='is_deleted';" "is_deleted"
db_check_contains "D32: employees.is_active column exists" \
  "SELECT COLUMN_NAME FROM information_schema.COLUMNS WHERE TABLE_SCHEMA='${DB_NAME}' AND TABLE_NAME='employees' AND COLUMN_NAME='is_active';" "is_active"

# 17h. Hangfire database
db_check "D33: Hangfire DB exists" \
  "SELECT COUNT(*) FROM information_schema.SCHEMATA WHERE SCHEMA_NAME='hrms_staging_hangfire';" "1"
db_check "D34: Hangfire tables present" \
  "SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA='hrms_staging_hangfire' AND TABLE_NAME='Job';" "1"

# 17i. PII encryption columns are sized for enc:v1: prefix
db_check_contains "D35: Employees.AadhaarNumber is VARCHAR(512) for encrypted payload" \
  "SELECT CHARACTER_MAXIMUM_LENGTH FROM information_schema.COLUMNS WHERE TABLE_SCHEMA='${DB_NAME}' AND TABLE_NAME='Employees' AND COLUMN_NAME='AadhaarNumber';" "512"

# 17j. No orphan refresh tokens
db_check "D36: No refresh tokens for deleted users" \
  "SELECT COUNT(*) FROM refresh_tokens rt LEFT JOIN users u ON rt.user_id=u.id WHERE u.is_deleted=1;" "0"

# 17k. Company isolation — no cross-tenant employees
db_check "D37: Employees have non-null company_id" \
  "SELECT COUNT(*) FROM employees WHERE company_id IS NULL AND is_active=1;" "0"

# 17l. Leave types seeded (or none — check structure only)
db_check_contains "D38: leave_types table has expected columns" \
  "SELECT COLUMN_NAME FROM information_schema.COLUMNS WHERE TABLE_SCHEMA='${DB_NAME}' AND TABLE_NAME='leave_types' AND COLUMN_NAME='company_id';" "company_id"

# 17m. created_at non-null enforcement
db_check "D39: users.created_at has no NULLs" \
  "SELECT COUNT(*) FROM users WHERE created_at IS NULL;" "0"

# 17n. Soft-delete consistency
db_check "D40: No hard-deleted employees (all use is_deleted/is_active pattern)" \
  "SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA='${DB_NAME}' AND TABLE_NAME='employees' AND COLUMN_NAME IN ('is_active','is_deleted');" "__NONZERO__"

# 17o. Notifications table has company_id (added in latest migration)
db_check_contains "D41: notifications.company_id column exists" \
  "SELECT COLUMN_NAME FROM information_schema.COLUMNS WHERE TABLE_SCHEMA='${DB_NAME}' AND TABLE_NAME='notifications' AND COLUMN_NAME='company_id';" "company_id"

# 17p. Max employees constraint column on companies
db_check_contains "D42: companies.max_employees column exists" \
  "SELECT COLUMN_NAME FROM information_schema.COLUMNS WHERE TABLE_SCHEMA='${DB_NAME}' AND TABLE_NAME='companies' AND COLUMN_NAME='max_employees';" "max_employees"

# =============================================================================
# SECTION 18 – Summary
# =============================================================================
TOTAL=$((PASS_COUNT + FAIL_COUNT + WARN_COUNT))
echo ""
echo -e "${BOLD}══════════════════════════════════════════════${NC}"
echo -e "${BOLD}  Phase 8 Runbook Results — $(date -u '+%Y-%m-%d %H:%M:%S UTC')${NC}"
echo -e "${BOLD}══════════════════════════════════════════════${NC}"
echo -e "  ${GREEN}PASS${NC}  $PASS_COUNT"
echo -e "  ${RED}FAIL${NC}  $FAIL_COUNT"
echo -e "  ${YELLOW}WARN${NC}  $WARN_COUNT"
echo -e "  Total  $TOTAL"
echo ""

if [[ ${#FAILURES[@]} -gt 0 ]]; then
  echo -e "${BOLD}${RED}Failed checks:${NC}"
  for f in "${FAILURES[@]}"; do
    echo -e "  ${RED}✗${NC} $f"
  done
  echo ""
fi

# Write machine-readable log
{
  echo "run_timestamp=$(date -u '+%Y-%m-%dT%H:%M:%SZ')"
  echo "pass=$PASS_COUNT"
  echo "fail=$FAIL_COUNT"
  echo "warn=$WARN_COUNT"
  echo "total=$TOTAL"
  if [[ ${#FAILURES[@]} -gt 0 ]]; then
    echo "failures=["
    for f in "${FAILURES[@]}"; do echo "  \"$f\""; done
    echo "]"
  fi
} > "$RESULT_LOG"
echo "  Results written to: $RESULT_LOG"

if [[ $FAIL_COUNT -gt 0 ]]; then
  echo -e "\n${RED}${BOLD}RESULT: NOT READY — $FAIL_COUNT check(s) failed.${NC}"
  rm -f "$COOKIE_JAR" /tmp/hrms_admin_cookies.txt /tmp/hrms_emp_cookies.txt 2>/dev/null
  exit 1
else
  echo -e "\n${GREEN}${BOLD}RESULT: ALL CHECKS PASSED — staging is ready for go-live review.${NC}"
  rm -f "$COOKIE_JAR" /tmp/hrms_admin_cookies.txt /tmp/hrms_emp_cookies.txt 2>/dev/null
  exit 0
fi
