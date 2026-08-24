#!/usr/bin/env bash
# =============================================================================
# e2e/run-e2e.sh  —  RatanHR HRMS one-command E2E runner
#
# Run this script on the STAGING SERVER from the repo root:
#   bash e2e/run-e2e.sh
#
# Prerequisites on the staging server:
#   • Docker + Docker Compose v2
#   • Node.js / bun  (to run Playwright)
#   • mysql client   (mysql-client or default-mysql-client)
#   • .env.e2e filled in (copied from .env.e2e.template)
# =============================================================================

set -euo pipefail

# ── Colour helpers ─────────────────────────────────────────────────────────
RED='\033[0;31m'; GREEN='\033[0;32m'; YELLOW='\033[1;33m'; NC='\033[0m'
info()  { echo -e "${YELLOW}[E2E]${NC} $*"; }
ok()    { echo -e "${GREEN}[E2E]${NC} $*"; }
fail()  { echo -e "${RED}[E2E] ERROR:${NC} $*" >&2; }

# ── Step 1 — ensure .env.e2e exists ────────────────────────────────────────
info "STEP 1 — Checking .env.e2e …"

if [ ! -f ".env.e2e" ]; then
  if [ -f ".env.e2e.template" ]; then
    cp .env.e2e.template .env.e2e
    fail ".env.e2e was missing — copied from .env.e2e.template."
    fail "Fill in every  ← FILL IN  value in .env.e2e, then re-run this script."
    exit 1
  else
    fail ".env.e2e and .env.e2e.template both missing. Cannot continue."
    exit 1
  fi
fi

# shellcheck source=/dev/null
source .env.e2e

# Verify the user actually filled in the required secrets
for var in MYSQL_ROOT_PASSWORD JWT_PRIVATE_KEY_PEM JWT_PUBLIC_KEY_PEM ENCRYPTION_KEY; do
  val="${!var:-}"
  if [ -z "$val" ] || [[ "$val" == *"FILL_IN"* ]]; then
    fail "${var} is not set in .env.e2e. Please fill in all ← FILL IN values."
    exit 1
  fi
done

ok ".env.e2e loaded."

# ── Step 2 — start infrastructure ──────────────────────────────────────────
info "STEP 2 — Starting MySQL, Redis, and .NET API via docker compose …"

docker compose -f docker-compose.e2e.yml --env-file .env.e2e up -d --wait

ok "All containers healthy."

# ── Step 3 — wait for API health endpoint ──────────────────────────────────
info "STEP 3 — Waiting for API health check at ${E2E_API_URL:-http://localhost:8082}/api/health …"

API_URL="${E2E_API_URL:-http://localhost:8082}"
MAX_WAIT=60
ELAPSED=0

until curl -fs "${API_URL}/api/health" | grep -q "Healthy"; do
  sleep 5
  ELAPSED=$((ELAPSED + 5))
  if [ "$ELAPSED" -ge "$MAX_WAIT" ]; then
    fail "API did not become healthy within ${MAX_WAIT}s."
    fail "Check Docker logs:  docker compose -f docker-compose.e2e.yml logs api"
    docker compose -f docker-compose.e2e.yml down -v 2>/dev/null || true
    exit 1
  fi
  info "  … still waiting (${ELAPSED}s elapsed)"
done

ok "API is Healthy."

# ── Step 4 — seed the database ─────────────────────────────────────────────
info "STEP 4 — Seeding database with E2E accounts …"

mysql \
  -h 127.0.0.1 \
  -P 3307 \
  -u root \
  "-p${MYSQL_ROOT_PASSWORD}" \
  "${MYSQL_DATABASE:-hrms}" \
  < e2e/e2e_seed.sql

ok "Seed script executed."

# ── Step 5 — verify seed ───────────────────────────────────────────────────
info "STEP 5 — Verifying seed (expecting 6 E2E accounts) …"

ROW_COUNT=$(mysql \
  -h 127.0.0.1 \
  -P 3307 \
  -u root \
  "-p${MYSQL_ROOT_PASSWORD}" \
  --skip-column-names \
  --silent \
  "${MYSQL_DATABASE:-hrms}" \
  --execute "SELECT COUNT(*) FROM Users WHERE Email LIKE 'e2e.%@ratan-staging.local';")

if [ "$ROW_COUNT" -ne 6 ]; then
  fail "Expected 6 E2E users but found ${ROW_COUNT}. Aborting."
  mysql \
    -h 127.0.0.1 -P 3307 -u root "-p${MYSQL_ROOT_PASSWORD}" \
    "${MYSQL_DATABASE:-hrms}" < e2e/verify-seed.sql || true
  docker compose -f docker-compose.e2e.yml down -v 2>/dev/null || true
  exit 1
fi

ok "Seed verified — ${ROW_COUNT}/6 accounts present."


# ── Step 5b — wait for SPA container (FIX E2E-RUNSH-001) ─────────────────
info "STEP 5b — Waiting for SPA frontend at ${E2E_BASE_URL:-http://localhost:3000} …"
SPA_URL="${E2E_BASE_URL:-http://localhost:3000}"
MAX_SPA=120; ELAPSED_SPA=0
until curl -fso /dev/null "${SPA_URL}"; do
  sleep 5; ELAPSED_SPA=$((ELAPSED_SPA + 5))
  if [ "$ELAPSED_SPA" -ge "$MAX_SPA" ]; then
    fail "SPA not reachable within ${MAX_SPA}s. Check: docker compose -f docker-compose.e2e.yml logs spa"
    docker compose -f docker-compose.e2e.yml down -v 2>/dev/null || true
    exit 1
  fi
  info "  … waiting for SPA (${ELAPSED_SPA}s)"
done
ok "SPA is reachable."

# ── Step 5c — copy .env.e2e into HRMS.SPA.Source for Playwright ──────────────
info "STEP 5c — Copying .env.e2e into HRMS.SPA.Source (playwright.config.ts lives there) …"
ROOT_DIR_E2E="$(cd "$(dirname "$0")/.." && pwd)"
SPA_DIR="${ROOT_DIR_E2E}/HRMS.SPA.Source"
cp .env.e2e "${SPA_DIR}/.env.e2e"
ok "Copied .env.e2e to ${SPA_DIR}/.env.e2e"

# ── Step 6 — run Playwright ────────────────────────────────────────────────
# IMPORTANT: Playwright must be run from HRMS.SPA.Source/ because that is where
# playwright.config.ts and global-setup.ts live. Running from the repo root will
# fail with "Cannot find playwright.config.ts".
info "STEP 6 — Running Playwright (chromium + firefox + Mobile Chrome) …"
info "          Base URL : ${E2E_BASE_URL:-http://localhost:3000}"
info "          API URL  : ${API_URL}"
info "          Working dir: ${SPA_DIR}"

cd "${SPA_DIR}"

# Determine test runner
if command -v bunx &>/dev/null; then
  RUNNER="bunx"
elif command -v npx &>/dev/null; then
  RUNNER="npx"
else
  fail "Neither bunx nor npx found. Install Node.js / bun on the staging server."
  docker compose -f "${ROOT_DIR_E2E}/docker-compose.e2e.yml" down -v 2>/dev/null || true
  exit 1
fi

# Install dependencies if node_modules absent
if [ ! -d node_modules ]; then
  info "  Installing SPA dependencies …"
  if command -v bun &>/dev/null; then
    bun install --frozen-lockfile
  else
    npm ci --prefer-offline
  fi
fi

# Ensure Playwright browsers are installed
$RUNNER playwright install chromium firefox --with-deps 2>&1 | tail -3 || true

set +e   # don't exit on playwright failure — we want to capture exit code
$RUNNER playwright test \
  --project=chromium \
  --project=firefox \
  --project="Mobile Chrome" \
  --reporter=list,html
PW_EXIT=$?
set -e

# ── Step 7/8 — result ──────────────────────────────────────────────────────
if [ "$PW_EXIT" -eq 0 ]; then
  echo ""
  echo -e "${GREEN}✅ ALL TESTS PASSED — Update GO_LIVE_READINESS.md to ✅ GO LIVE APPROVED${NC}"
  echo ""
else
  fail "One or more Playwright tests FAILED (exit code ${PW_EXIT})."
  info "Review the HTML report: open ${SPA_DIR}/playwright-report/index.html"
  info "Full test list with status:"
  $RUNNER playwright test \
    --project=chromium \
    --project=firefox \
    --project="Mobile Chrome" \
    --reporter=list 2>&1 | grep -E "(FAILED|PASSED|✓|✗|×)" || true
fi

cd "${ROOT_DIR_E2E}"

# ── Step 9 — cleanup ───────────────────────────────────────────────────────
info "STEP 9 — Tearing down containers and volumes …"
docker compose -f docker-compose.e2e.yml down -v

ok "Cleanup complete."

exit "$PW_EXIT"
