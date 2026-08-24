#!/usr/bin/env bash
# =============================================================================
# Staging/bootstrap-staging.sh — RatanHR HRMS One-Command Staging Bootstrap
#
# Brings up the full isolated staging stack, waits for all services to be
# healthy, seeds E2E test accounts, and runs the Phase 8 smoke-test runbook.
# Optionally runs the Playwright E2E suite when --run-e2e is passed.
#
# USAGE (from repo root):
#   # First time — populate secrets:
#   cp Staging/staging.env.template Staging/.env.staging
#   chmod 600 Staging/.env.staging
#   # Edit Staging/.env.staging — replace every <REPLACE_...> value
#
#   # Start staging + smoke test:
#   bash Staging/bootstrap-staging.sh
#
#   # Start staging + full Playwright E2E suite (requires bun/npx):
#   bash Staging/bootstrap-staging.sh --run-e2e
#
#   # Tear down after testing:
#   docker compose -f Staging/docker-compose.staging.yml --env-file Staging/.env.staging down -v
#
# WHAT THIS SCRIPT DOES:
#   1. Pre-flight: checks Docker, .env.staging, and no unfilled placeholders
#   2. Starts MySQL, Redis, MailHog, migrations, API, and frontend
#   3. Waits for the API health endpoint to return Healthy
#   4. Seeds the phase8 + E2E test accounts via SQL
#   5. Runs Staging/phase8_runbook.sh (auth, CRUD, tenant isolation, Redis,
#      background jobs, rate-limiting, migrations check)
#   6. Optionally runs the full 631-test Playwright suite (--run-e2e)
#   7. Prints a pass/fail summary with next-step instructions
#
# EXIT CODES:
#   0 — all required steps passed
#   1 — one or more steps failed (stack is left running for investigation)
# =============================================================================
set -uo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
STAGING_DIR="$ROOT_DIR/Staging"
ENV_FILE="$STAGING_DIR/.env.staging"
COMPOSE_FILE="$STAGING_DIR/docker-compose.staging.yml"

RUN_E2E=0
FAIL_COUNT=0

while [[ $# -gt 0 ]]; do
  case "$1" in
    --run-e2e) RUN_E2E=1; shift ;;
    --help|-h)
      sed -n '2,35p' "$0"; exit 0 ;;
    *) echo "Unknown option: $1" >&2; exit 1 ;;
  esac
done

# ── Colour helpers ─────────────────────────────────────────────────────────────
RED='\033[0;31m'; GREEN='\033[0;32m'; YELLOW='\033[1;33m'
CYAN='\033[0;36m'; BOLD='\033[1m'; NC='\033[0m'

pass()    { echo -e "  ${GREEN}✔ PASS${NC}  $*"; }
fail()    { FAIL_COUNT=$((FAIL_COUNT+1)); echo -e "  ${RED}✖ FAIL${NC}  $*" >&2; }
info()    { echo -e "  ${CYAN}ℹ${NC}      $*"; }
section() { echo -e "\n${BOLD}${CYAN}══ $* ══${NC}"; }

# =============================================================================
# STEP 1 — Pre-flight checks
# =============================================================================
section "STEP 1 — Pre-flight checks"

# Docker
if ! command -v docker >/dev/null 2>&1 || ! docker compose version >/dev/null 2>&1; then
  fail "Docker Engine + Compose v2 required. Install from https://docs.docker.com/get-docker/"
  exit 1
fi
pass "Docker + Compose: $(docker --version)"

# .env.staging
if [[ ! -f "$ENV_FILE" ]]; then
  fail ".env.staging not found."
  echo ""
  echo "  Run:"
  echo "    cp Staging/staging.env.template Staging/.env.staging"
  echo "    chmod 600 Staging/.env.staging"
  echo "    # Edit Staging/.env.staging — replace every <REPLACE_...> value"
  echo ""
  exit 1
fi
pass "Staging/.env.staging found"

# Check for unfilled placeholders
UNFILLED=$(grep -c '<REPLACE_' "$ENV_FILE" 2>/dev/null || true)
if [[ "$UNFILLED" -gt 0 ]]; then
  fail "$UNFILLED placeholder(s) still set in .env.staging — fill them all before continuing"
  grep '<REPLACE_' "$ENV_FILE" | sed 's/=.*//' | while read -r line; do
    echo "    Unfilled: $line"
  done
  exit 1
fi
pass "No unfilled placeholders in .env.staging"

# shellcheck source=/dev/null
set -a && source "$ENV_FILE" && set +a

# Required vars
REQUIRED_VARS=(
  STAGING_DB_ROOT_PASSWORD STAGING_DB_PASSWORD STAGING_REDIS_PASSWORD
  JWT_PRIVATE_KEY_PEM JWT_PUBLIC_KEY_PEM ENCRYPTION_KEY_STAGING
  SUPERADMIN_INITIAL_PASSWORD
)
for var in "${REQUIRED_VARS[@]}"; do
  val="${!var:-}"
  if [[ -z "$val" ]]; then
    fail "$var is empty in .env.staging"
  fi
done
[[ "$FAIL_COUNT" -eq 0 ]] && pass "All required env vars are set"

if [[ "$FAIL_COUNT" -gt 0 ]]; then
  echo ""
  echo -e "${RED}Pre-flight failed. Fix the issues above, then re-run.${NC}"
  exit 1
fi

# =============================================================================
# STEP 2 — Start the staging stack
# =============================================================================
section "STEP 2 — Starting staging stack"

info "Running: docker compose -f Staging/docker-compose.staging.yml up -d"
cd "$ROOT_DIR"
docker compose \
  -f "$COMPOSE_FILE" \
  --env-file "$ENV_FILE" \
  up -d --build 2>&1

pass "Compose up completed"

# =============================================================================
# STEP 3 — Wait for API health
# =============================================================================
section "STEP 3 — Waiting for API to become Healthy"

API_URL="http://127.0.0.1:8081"
MAX_WAIT=180
ELAPSED=0

info "Polling $API_URL/healthz (max ${MAX_WAIT}s) …"
until curl -fs "$API_URL/healthz" 2>/dev/null | grep -q "Healthy"; do
  sleep 5
  ELAPSED=$((ELAPSED+5))
  if [[ "$ELAPSED" -ge "$MAX_WAIT" ]]; then
    fail "API did not become healthy within ${MAX_WAIT}s"
    echo ""
    info "Check logs:"
    echo "  docker compose -f Staging/docker-compose.staging.yml logs hrms_staging_api | tail -50"
    exit 1
  fi
  info "  … ${ELAPSED}s elapsed"
done
pass "API is Healthy ($API_URL/healthz)"

# =============================================================================
# STEP 4 — Seed E2E test accounts
# =============================================================================
section "STEP 4 — Seeding E2E test accounts"

SEED_FILE="$ROOT_DIR/e2e/e2e_seed.sql"
if [[ ! -f "$SEED_FILE" ]]; then
  fail "e2e/e2e_seed.sql not found — E2E seed skipped"
else
  if mysql \
       -h 127.0.0.1 \
       -P 3307 \
       -u root \
       "-p${STAGING_DB_ROOT_PASSWORD}" \
       "${STAGING_DB_NAME:-hrms_staging}" \
       < "$SEED_FILE" 2>&1; then
    # Verify
    ROW_COUNT=$(mysql \
      -h 127.0.0.1 -P 3307 -u root "-p${STAGING_DB_ROOT_PASSWORD}" \
      --skip-column-names --silent \
      "${STAGING_DB_NAME:-hrms_staging}" \
      --execute "SELECT COUNT(*) FROM Users WHERE Email LIKE 'e2e.%@ratan-staging.local';" 2>/dev/null || echo "0")
    if [[ "$ROW_COUNT" -ge 6 ]]; then
      pass "E2E seed: $ROW_COUNT accounts present"
    else
      fail "E2E seed: expected 6 accounts, found $ROW_COUNT"
    fi
  else
    fail "E2E seed SQL execution failed"
  fi
fi

# =============================================================================
# STEP 5 — Phase 8 smoke-test runbook
# =============================================================================
section "STEP 5 — Phase 8 smoke-test runbook (auth, CRUD, tenant isolation, Redis, jobs)"

RUNBOOK="$STAGING_DIR/phase8_runbook.sh"
if [[ ! -f "$RUNBOOK" ]]; then
  fail "Staging/phase8_runbook.sh not found"
else
  export API_HOST="127.0.0.1:8081"
  export DB_HOST="127.0.0.1"
  export DB_PORT="3307"
  export DB_USER="${STAGING_DB_USER:-hrms_staging}"
  export DB_NAME="${STAGING_DB_NAME:-hrms_staging}"
  export DB_PASSWORD="${STAGING_DB_PASSWORD}"
  export REDIS_HOST="127.0.0.1"
  export REDIS_PORT="6380"
  export MH_HOST="127.0.0.1:8025"
  # phase8_runbook.sh reads SUPERADMIN_INITIAL_PASSWORD + DB_PASSWORD + REDIS_PASSWORD
  export REDIS_PASSWORD="${STAGING_REDIS_PASSWORD}"

  if bash "$RUNBOOK"; then
    pass "Phase 8 smoke-test runbook: ALL CHECKS PASSED"
  else
    fail "Phase 8 smoke-test runbook: one or more checks failed (see output above)"
  fi
fi

# =============================================================================
# STEP 6 — Playwright E2E suite (opt-in via --run-e2e)
# =============================================================================
section "STEP 6 — Playwright E2E suite"

if [[ "$RUN_E2E" -eq 0 ]]; then
  echo "  ○ SKIP  Playwright not requested. Re-run with --run-e2e to execute 631 tests."
else
  SPA_DIR="$ROOT_DIR/HRMS.SPA.Source"
  E2E_ENV_DEST="$SPA_DIR/.env.e2e"

  # Write the .env.e2e file expected by playwright.config.ts / globalSetup.ts
  cat > "$E2E_ENV_DEST" << EOF
E2E_BASE_URL=http://127.0.0.1:3001
E2E_API_URL=http://127.0.0.1:8081
E2E_SUPERADMIN_EMAIL=e2e.superadmin@ratan-staging.local
E2E_SUPERADMIN_PASS=E2E_SuperAdmin_Pass1!
E2E_ADMIN_A_EMAIL=e2e.adminA@ratan-staging.local
E2E_ADMIN_A_PASS=E2E_AdminA_Pass1!
E2E_EMPLOYEE_A_EMAIL=e2e.employeeA@ratan-staging.local
E2E_EMPLOYEE_A_PASS=E2E_EmployeeA_Pass1!
E2E_ADMIN_B_EMAIL=e2e.adminB@ratan-staging.local
E2E_ADMIN_B_PASS=E2E_AdminB_Pass1!
E2E_EMPLOYEE_B_EMAIL=e2e.employeeB@ratan-staging.local
E2E_EMPLOYEE_B_PASS=E2E_EmployeeB_Pass1!
E2E_AUDITOR_EMAIL=e2e.auditor@ratan-staging.local
E2E_AUDITOR_PASS=E2E_Auditor_Pass1!
EOF
  info "Wrote $E2E_ENV_DEST"

  # Determine runner
  if command -v bunx >/dev/null 2>&1; then
    RUNNER="bunx"
  elif command -v npx >/dev/null 2>&1; then
    RUNNER="npx"
  else
    fail "Neither bunx nor npx found — install bun (https://bun.sh) on the staging server"
    RUNNER=""
  fi

  if [[ -n "$RUNNER" ]]; then
    cd "$SPA_DIR"
    # Install dependencies if node_modules is absent
    if [[ ! -d node_modules ]]; then
      info "Installing SPA dependencies (bun install --frozen-lockfile) …"
      if command -v bun >/dev/null 2>&1; then
        bun install --frozen-lockfile
      else
        npm ci --prefer-offline
      fi
    fi

    info "Installing Playwright browsers if needed …"
    $RUNNER playwright install chromium firefox --with-deps 2>&1 | tail -3 || true

    info "Running 631 Playwright tests (chromium + firefox + Mobile Chrome) …"
    set +e
    $RUNNER playwright test \
      --project=chromium \
      --project=firefox \
      --project="Mobile Chrome" \
      --reporter=list,html
    PW_EXIT=$?
    set -e
    cd "$ROOT_DIR"

    if [[ "$PW_EXIT" -eq 0 ]]; then
      pass "Playwright: ALL tests passed"
    else
      fail "Playwright: one or more tests failed (exit $PW_EXIT)"
      info "HTML report: open $SPA_DIR/playwright-report/index.html"
    fi
  fi
fi

# =============================================================================
# Summary
# =============================================================================
echo ""
echo -e "${BOLD}╔══════════════════════════════════════════╗${NC}"
echo -e "${BOLD}║   STAGING BOOTSTRAP SUMMARY              ║${NC}"
echo -e "${BOLD}╠══════════════════════════════════════════╣${NC}"
if [[ "$FAIL_COUNT" -eq 0 ]]; then
  echo -e "${BOLD}║   VERDICT: ${GREEN}✅ ALL CHECKS PASSED${NC}${BOLD}          ║${NC}"
else
  echo -e "${BOLD}║   VERDICT: ${RED}❌ $FAIL_COUNT FAILURE(S) — review above${NC}${BOLD}  ║${NC}"
fi
echo -e "${BOLD}╚══════════════════════════════════════════╝${NC}"
echo ""

if [[ "$FAIL_COUNT" -eq 0 ]]; then
  echo "Services still running — access them at:"
  echo "  API:      http://127.0.0.1:8081"
  echo "  Frontend: http://127.0.0.1:3001"
  echo "  MailHog:  http://127.0.0.1:8025"
  echo ""
  echo "Tear down when done:"
  echo "  docker compose -f Staging/docker-compose.staging.yml --env-file Staging/.env.staging down -v"
fi

exit "$FAIL_COUNT"
