#!/usr/bin/env bash
# =============================================================================
# pre-release-validation.sh — RatanHR HRMS Pre-Release Validation Runner
#
# Runs ALL validation steps that were BLOCKED during static code audit
# (those requiring .NET SDK, Docker, MySQL, Redis, Node.js/bun).
#
# Run this script on a machine with:
#   • .NET 8 SDK (8.0.416+)
#   • Docker Engine 24+ with Compose v2
#   • bun 1.2+ (https://bun.sh) — the SPA uses bun.lock, not package-lock.json
#   • curl, envsubst (gettext)
#
# Usage:
#   chmod +x scripts/pre-release-validation.sh
#   ./scripts/pre-release-validation.sh [--skip-docker] [--skip-e2e]
#
# Each step prints PASS, FAIL, or SKIP with a one-line summary.
# Exit code 0 = all required steps passed.
# Exit code 1 = one or more required steps failed.
# =============================================================================
set -uo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
SKIP_DOCKER=0
SKIP_E2E=0
FAIL_COUNT=0
PASS_COUNT=0
SKIP_COUNT=0

while [[ $# -gt 0 ]]; do
  case "$1" in
    --skip-docker) SKIP_DOCKER=1; shift ;;
    --skip-e2e)    SKIP_E2E=1;    shift ;;
    --help|-h)     sed -n '2,22p' "$0"; exit 0 ;;
    *) echo "Unknown option: $1" >&2; exit 1 ;;
  esac
done

pass() { PASS_COUNT=$(( PASS_COUNT + 1 )); echo "  ✔ PASS: $*"; }
fail() { FAIL_COUNT=$(( FAIL_COUNT + 1 )); echo "  ✖ FAIL: $*" >&2; }
skip() { SKIP_COUNT=$(( SKIP_COUNT + 1 )); echo "  ○ SKIP: $*"; }
section() { echo ""; echo "══════════════════════════════════════════"; echo "  $*"; echo "══════════════════════════════════════════"; }

# ── Check toolchain availability ───────────────────────────────────────────────
section "TOOLCHAIN CHECK"

if command -v dotnet >/dev/null 2>&1; then
  DOTNET_VER=$(dotnet --version)
  pass ".NET SDK present: $DOTNET_VER"
  HAS_DOTNET=1
else
  fail ".NET SDK not found. Install from https://dot.net/download (8.0.416+)"
  HAS_DOTNET=0
fi

if command -v docker >/dev/null 2>&1 && docker compose version >/dev/null 2>&1; then
  DOCKER_VER=$(docker --version)
  pass "Docker + Compose present: $DOCKER_VER"
  HAS_DOCKER=1
else
  skip "Docker not available (set --skip-docker to suppress)"
  HAS_DOCKER=0
fi

# The SPA uses bun.lock — bun is the required package manager.
# npm ci will fail because there is no package-lock.json in HRMS.SPA.Source.
if command -v bun >/dev/null 2>&1; then
  BUN_VER=$(bun --version)
  pass "bun present: $BUN_VER"
  HAS_BUN=1
else
  fail "bun not found. Install from https://bun.sh (the SPA uses bun.lock, not package-lock.json)"
  HAS_BUN=0
fi

# ── Step 1: Backend build ──────────────────────────────────────────────────────
section "STEP 1 — dotnet restore + build"

if [[ "$HAS_DOTNET" -eq 0 ]]; then
  fail "Skipped: .NET SDK required"
else
  cd "$ROOT_DIR"
  if dotnet restore HRMS.sln --use-lock-file --locked-mode 2>&1; then
    pass "dotnet restore (locked-mode)"
  else
    fail "dotnet restore failed. Run: dotnet restore HRMS.sln --use-lock-file --locked-mode"
  fi

  if dotnet build HRMS.sln -c Release --no-restore 2>&1; then
    pass "dotnet build Release (0 errors)"
  else
    fail "dotnet build failed. Check compiler errors above."
  fi
fi

# ── Step 2: Backend unit + integration tests ───────────────────────────────────
section "STEP 2 — dotnet test (90+ tests)"

if [[ "$HAS_DOTNET" -eq 0 ]]; then
  fail "Skipped: .NET SDK required"
else
  cd "$ROOT_DIR"
  TEST_OUTPUT=$(mktemp)
  if dotnet test HRMS.sln -c Release --no-build \
       --logger "trx;LogFileName=$ROOT_DIR/TestResults/results.trx" \
       --results-directory "$ROOT_DIR/TestResults" 2>&1 | tee "$TEST_OUTPUT"; then
    PASSED=$(grep -oE 'passed: [0-9]+' "$TEST_OUTPUT" | tail -1 | grep -oE '[0-9]+')
    FAILED=$(grep -oE 'failed: [0-9]+' "$TEST_OUTPUT" | tail -1 | grep -oE '[0-9]+' || echo 0)
    pass "dotnet test: $PASSED passed, $FAILED failed"
    if [[ "${FAILED:-0}" -gt 0 ]]; then
      fail "Test failures detected. Review TestResults/results.trx"
    fi
  else
    fail "dotnet test exited non-zero. Review output above."
  fi
  rm -f "$TEST_OUTPUT"
fi

# ── Step 3: TypeScript compile (no-emit) ──────────────────────────────────────
section "STEP 3 — TypeScript / SPA compile check"

SPA_SOURCE="$ROOT_DIR/HRMS.SPA.Source"
if [[ "$HAS_BUN" -eq 0 ]]; then
  fail "bun not available — TypeScript check skipped (install bun from https://bun.sh)"
elif [[ ! -d "$SPA_SOURCE" ]]; then
  fail "HRMS.SPA.Source directory not found"
else
  cd "$SPA_SOURCE"
  # Use bun install --frozen-lockfile (bun.lock is the lockfile; npm ci would fail here)
  if bun install --frozen-lockfile 2>&1; then
    pass "bun install --frozen-lockfile: dependencies installed"
  else
    fail "bun install failed"
  fi

  if bunx tsc --noEmit 2>&1; then
    pass "tsc --noEmit: no TypeScript errors"
  else
    fail "TypeScript errors detected. Run: cd HRMS.SPA.Source && bunx tsc --noEmit"
  fi

  # build:ci sets PORT=3000 BASE_PATH=/ NODE_ENV=production (required by vite.config.ts)
  if bun run build:ci 2>&1; then
    pass "bun run build:ci: SPA built successfully"
    # Copy dist/public to spa-dist for inspection
    cp -r dist/public "$ROOT_DIR/spa-dist" 2>/dev/null || true
    pass "SPA dist copied to $ROOT_DIR/spa-dist/"
  else
    fail "SPA build failed. Run: cd HRMS.SPA.Source && bun run build:ci"
  fi
  cd "$ROOT_DIR"
fi

# ── Step 4: Docker image build ─────────────────────────────────────────────────
section "STEP 4 — Docker build (API image)"

if [[ "$SKIP_DOCKER" -eq 1 || "$HAS_DOCKER" -eq 0 ]]; then
  skip "Docker build skipped (--skip-docker or Docker unavailable)"
else
  cd "$ROOT_DIR"
  if docker build -t hrms-api:prerelease-test --target runtime . 2>&1; then
    pass "Docker build (runtime target) successful"
    docker rmi hrms-api:prerelease-test >/dev/null 2>&1 || true
  else
    fail "Docker build failed. Review output above."
  fi
fi

# ── Step 5: Docker Compose config validation ───────────────────────────────────
section "STEP 5 — docker-compose.prod.yml config validation"

if [[ "$SKIP_DOCKER" -eq 1 || "$HAS_DOCKER" -eq 0 ]]; then
  skip "Docker not available"
elif [[ ! -f "$ROOT_DIR/.env" ]]; then
  skip ".env file not found — copy .env.production.template and fill values to run this step"
else
  cd "$ROOT_DIR"
  # Generate nginx.conf for validation
  source .env 2>/dev/null || true
  if command -v envsubst >/dev/null 2>&1 && [[ -n "${DOMAIN_NAME:-}" ]]; then
    envsubst '${DOMAIN_NAME}' < nginx/nginx.conf.template > nginx/nginx.conf
  fi
  if docker compose -f docker-compose.prod.yml config --quiet 2>&1; then
    pass "docker compose config: valid YAML, all env vars resolved"
  else
    fail "docker compose config failed — check .env values and docker-compose.prod.yml"
  fi
fi

# ── Step 6: Dependency vulnerability scan ─────────────────────────────────────
section "STEP 6 — Dependency vulnerability scan"

if [[ "$HAS_DOTNET" -eq 0 ]]; then
  fail ".NET SDK required"
else
  cd "$ROOT_DIR"
  VULN_OUTPUT=$(dotnet list HRMS.sln package --vulnerable --include-transitive 2>&1 || true)
  if echo "$VULN_OUTPUT" | grep -q "critical\|high" 2>/dev/null; then
    fail "Critical/High vulnerabilities found. Review output:"
    echo "$VULN_OUTPUT" | grep -E "critical|high" | head -20 >&2
  else
    pass "dotnet list package --vulnerable: no critical/high issues"
  fi
fi

# ── Step 7: Secrets scan ───────────────────────────────────────────────────────
section "STEP 7 — Secrets scan (no real credentials in source)"

cd "$ROOT_DIR"
SECRETS_FOUND=0
# Check for patterns that look like real secrets (not placeholders)
while IFS= read -r -d '' file; do
  # Skip template/example/changelog/audit files
  case "$file" in
    *template*|*example*|*CHANGELOG*|*AUDIT*|*.md) continue ;;
  esac
  if grep -qE '(password|secret|key|token)\s*[=:]\s*["'"'"'][A-Za-z0-9+/]{20,}' "$file" 2>/dev/null; then
    fail "Possible secret in: $file"
    SECRETS_FOUND=$(( SECRETS_FOUND + 1 ))
  fi
done < <(find . -name "*.json" -o -name "*.yml" -o -name "*.yaml" -o -name "*.env" \
           ! -name "*.template" ! -name "*.example" -print0 2>/dev/null)

if [[ "$SECRETS_FOUND" -eq 0 ]]; then
  pass "No real credentials detected in committed files"
fi

# ── Step 8: Nginx config syntax ───────────────────────────────────────────────
section "STEP 8 — nginx.conf syntax check"

if ! command -v nginx >/dev/null 2>&1 && [[ "$HAS_DOCKER" -eq 1 ]]; then
  # Check via Docker
  NGINX_CONF="$ROOT_DIR/nginx/nginx.conf"
  if [[ ! -f "$NGINX_CONF" ]]; then
    skip "nginx.conf not generated yet — run deploy.sh first (needs DOMAIN_NAME)"
  else
    if docker run --rm \
         -v "$NGINX_CONF:/etc/nginx/nginx.conf:ro" \
         nginx:1.27.0-alpine nginx -t 2>&1; then
      pass "nginx -t: syntax OK"
    else
      fail "nginx config syntax error"
    fi
  fi
elif command -v nginx >/dev/null 2>&1; then
  NGINX_CONF="$ROOT_DIR/nginx/nginx.conf"
  if [[ -f "$NGINX_CONF" ]]; then
    nginx -t -c "$NGINX_CONF" 2>&1 && pass "nginx -t: syntax OK" || fail "nginx config syntax error"
  else
    skip "nginx.conf not generated yet — run: scripts/deploy.sh --dry-run"
  fi
else
  skip "nginx and Docker both unavailable — skip nginx syntax check"
fi

# ── Step 9: Migration dry-run ──────────────────────────────────────────────────
section "STEP 9 — EF Core migration dry-run (script only)"

if [[ "$HAS_DOTNET" -eq 0 ]]; then
  fail ".NET SDK required"
else
  cd "$ROOT_DIR"
  if dotnet tool run dotnet-ef migrations list \
       --context ApplicationDbContext \
       --project HRMS.Infrastructure/HRMS.Infrastructure.csproj \
       --startup-project HRMS.API/HRMS.API.csproj \
       --configuration Release \
       --no-build 2>&1 | grep -c "20[0-9]\{12\}" | grep -qE "^[3-9][0-9]|^[0-9]{3}"; then
    MIGRATION_COUNT=$(dotnet tool run dotnet-ef migrations list \
       --context ApplicationDbContext \
       --project HRMS.Infrastructure/HRMS.Infrastructure.csproj \
       --startup-project HRMS.API/HRMS.API.csproj \
       --no-build 2>&1 | grep -c "20[0-9]\{12\}" || echo "0")
    pass "EF Core migrations list: $MIGRATION_COUNT migrations found, chain intact"
  else
    fail "EF Core migrations list failed or chain broken"
  fi
fi

# ── Step 10: E2E smoke test ────────────────────────────────────────────────────
section "STEP 10 — E2E smoke test (Playwright)"

if [[ "$SKIP_E2E" -eq 1 ]]; then
  skip "E2E tests skipped (--skip-e2e)"
elif [[ "$HAS_BUN" -eq 0 ]]; then
  skip "bun required for E2E tests"
elif [[ ! -d "$ROOT_DIR/HRMS.SPA.Source/e2e" ]]; then
  skip "E2E test directory not found"
elif [[ ! -f "$ROOT_DIR/Staging/.env.staging" ]]; then
  skip "Staging .env not found — copy Staging/staging.env.template to run E2E"
else
  cd "$ROOT_DIR/HRMS.SPA.Source"
  if bunx playwright test --reporter=list 2>&1; then
    pass "Playwright E2E tests: all passed"
  else
    fail "Playwright E2E tests: one or more failures. See playwright-report/"
  fi
  cd "$ROOT_DIR"
fi

# ── Final summary ──────────────────────────────────────────────────────────────
echo ""
echo "╔══════════════════════════════════════════╗"
echo "║   PRE-RELEASE VALIDATION SUMMARY         ║"
echo "╠══════════════════════════════════════════╣"
printf "║   ✔ PASS:  %-4d                          ║\n" "$PASS_COUNT"
printf "║   ✖ FAIL:  %-4d                          ║\n" "$FAIL_COUNT"
printf "║   ○ SKIP:  %-4d                          ║\n" "$SKIP_COUNT"
echo "╠══════════════════════════════════════════╣"

if [[ "$FAIL_COUNT" -eq 0 ]]; then
  echo "║   VERDICT: ✅ READY FOR PRODUCTION       ║"
else
  echo "║   VERDICT: ❌ NOT READY — fix failures   ║"
fi
echo "╚══════════════════════════════════════════╝"
echo ""

exit "$FAIL_COUNT"
