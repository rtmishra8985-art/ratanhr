#!/usr/bin/env bash
# =============================================================================
# deploy.sh — RatanHR HRMS One-Command Production Deployer
#
# Usage (first deploy or any update):
#   bash deploy.sh
#
# What it does, in order:
#   1. Pre-flight checks  (env, Docker, TLS certs, required files)
#   2. Snapshot rollback  (tags the current immutable image as :previous)
#   3. Git pull           (fetches latest code)
#   4. SPA build          (bun install + bun run build:ci → spa-dist/)
#   5. nginx patch        (bakes DOMAIN_NAME into nginx.conf copy)
#   6. API image build    (docker compose build with SHA + timestamp labels)
#   7. DB backup          (encrypted pre-migration backup via mysql-backup.sh)
#   8. Migrations         (backfill one-shot, then migrate one-shot)
#   9. Stack up           (docker compose up -d --remove-orphans)
#  10. Health wait         (polls /health up to 120 s)
#  11. Smoke verify        (curl /health, /api/healthz)
#  12. Prune              (remove dangling images)
#  13. Result             (prints ✅ DEPLOYED or ❌ FAILED with reason)
#
# Rollback: bash rollback.sh
# =============================================================================

set -euo pipefail
IFS=$'\n\t'

# ── colour helpers ────────────────────────────────────────────────────────────
RED='\033[0;31m'; GREEN='\033[0;32m'; YELLOW='\033[1;33m'
CYAN='\033[0;36m'; BOLD='\033[1m'; NC='\033[0m'

log()     { echo -e "${CYAN}[$(date -u '+%H:%M:%S')]${NC} $*"; }
success() { echo -e "${GREEN}${BOLD}[$(date -u '+%H:%M:%S')] ✓${NC}  $*"; }
warn()    { echo -e "${YELLOW}[$(date -u '+%H:%M:%S')] ⚠${NC}  $*"; }
die()     { echo -e "${RED}${BOLD}[$(date -u '+%H:%M:%S')] ✗  FATAL: $*${NC}" >&2; echo ""; echo -e "${RED}${BOLD}❌ FAILED — $*${NC}"; exit 1; }

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

COMPOSE="docker compose -f docker-compose.prod.yml"
ENV_FILE="${SCRIPT_DIR}/.env"
DEPLOY_LOG="${SCRIPT_DIR}/logs/deploy_$(date -u '+%Y%m%d_%H%M%S').log"

mkdir -p "${SCRIPT_DIR}/logs"

# ── Tee all output to log ─────────────────────────────────────────────────────
exec > >(tee -a "$DEPLOY_LOG") 2>&1

echo ""
echo -e "${BOLD}${CYAN}══════════════════════════════════════════════${NC}"
echo -e "${BOLD}${CYAN}  RatanHR HRMS — Production Deploy            ${NC}"
echo -e "${BOLD}${CYAN}  $(date -u '+%Y-%m-%d %H:%M:%S UTC')                    ${NC}"
echo -e "${BOLD}${CYAN}══════════════════════════════════════════════${NC}"
echo ""

# =============================================================================
# STEP 1 — Pre-flight checks
# =============================================================================
log "Step 1/13 — Pre-flight checks"

# Docker available
command -v docker >/dev/null 2>&1 || die "Docker is not installed or not in PATH"
docker info >/dev/null 2>&1       || die "Docker daemon is not running"
docker compose version >/dev/null 2>&1 || die "Docker Compose plugin not found (need v2.24+)"
success "Docker $(docker --version | awk '{print $3}' | tr -d ',')"

# .env file
[[ -f "$ENV_FILE" ]] || die ".env file not found. Run: cp .env.production.template .env && nano .env"
chmod 600 "$ENV_FILE"

# Load .env (safely — skip lines with bash-incompatible syntax)
set -a
# shellcheck disable=SC1090
source <(grep -v '^\s*#' "$ENV_FILE" | grep -v '^\s*$' | grep '=') 2>/dev/null || true
set +a

# Required env vars
for var in DOMAIN_NAME MYSQL_PASSWORD MYSQL_ROOT_PASSWORD REDIS_PASSWORD \
           JWT_PRIVATE_KEY_PEM JWT_PUBLIC_KEY_PEM ENCRYPTION_KEY \
           EMAIL_HOST EMAIL_FROM_ADDRESS SUPERADMIN_INITIAL_PASSWORD; do
  val="${!var:-}"
  [[ -z "$val" ]]             && die "$var is not set in .env"
  [[ "$val" == *"<REQUIRED>"* ]] && die "$var still contains the placeholder value <REQUIRED>"
done
success "All required env vars present"

# DOMAIN_NAME must not be a placeholder
[[ "$DOMAIN_NAME" == "yourdomain.com" ]] && die "DOMAIN_NAME is still 'yourdomain.com'. Set your real domain in .env"

# TLS certificates
CERT_PATH="/etc/letsencrypt/live/${DOMAIN_NAME}/fullchain.pem"
KEY_PATH="/etc/letsencrypt/live/${DOMAIN_NAME}/privkey.pem"
[[ -f "$CERT_PATH" ]] || die "TLS certificate not found: $CERT_PATH\n  Run: sudo certbot certonly --standalone -d $DOMAIN_NAME"
[[ -f "$KEY_PATH" ]]  || die "TLS private key not found: $KEY_PATH"
# Check cert is not already expired
openssl x509 -checkend 86400 -noout -in "$CERT_PATH" >/dev/null 2>&1 \
  || warn "TLS certificate expires within 24 hours — run: sudo certbot renew"
success "TLS certificate valid for $DOMAIN_NAME"

# nginx.conf template exists
[[ -f "nginx/nginx.conf" ]] || die "nginx/nginx.conf not found"

# Dockerfile and compose file
[[ -f "Dockerfile" ]]              || die "Dockerfile not found"
[[ -f "docker-compose.prod.yml" ]] || die "docker-compose.prod.yml not found"

# bun for SPA build
command -v bun >/dev/null 2>&1 || die "bun is not installed. Run: curl -fsSL https://bun.sh/install | bash && source ~/.bashrc"

# SPA source
[[ -d "HRMS.SPA.Source" ]] || die "HRMS.SPA.Source/ directory not found"
[[ -f "HRMS.SPA.Source/package.json" ]] || die "HRMS.SPA.Source/package.json not found"

success "Pre-flight checks passed"

# =============================================================================
# STEP 2 — Snapshot current images for rollback
# =============================================================================
log "Step 2/13 — Snapshotting current images for rollback"

GIT_SHA="$(git rev-parse --short HEAD 2>/dev/null || echo 'unknown')"
PREV_TAG="previous"

# The Compose file receives explicit immutable image names for every release.
# Keep the currently deployed API under :previous only as a rollback snapshot;
# rollback.sh promotes it to a new timestamped rollback tag.
CURRENT_API_IMAGE="$(docker compose -f docker-compose.prod.yml images -q api 2>/dev/null | head -1 || true)"
if [[ -n "$CURRENT_API_IMAGE" ]]; then
  CURRENT_API_IMAGE="$(docker image inspect --format '{{.RepoTags}}' "$CURRENT_API_IMAGE" 2>/dev/null \
    | tr ',' '\n' | grep -E '(^|/)(ratanhr|hrms)[^:]*-?api:' | grep -v ':previous$' | head -1 || true)"
fi
if [[ -z "$CURRENT_API_IMAGE" ]]; then
  CURRENT_API_IMAGE="$(docker images --format '{{.Repository}}:{{.Tag}}' \
    | grep -E '(^|/)(ratanhr|hrms)[^:]*-?api:' | grep -v ':previous$' | head -1 || true)"
fi
if [[ -n "$CURRENT_API_IMAGE" ]]; then
  IMG_BASE="${CURRENT_API_IMAGE%%:*}"
  docker tag "$CURRENT_API_IMAGE" "${IMG_BASE}:${PREV_TAG}" 2>/dev/null && \
    success "Snapshotted ${CURRENT_API_IMAGE} → ${IMG_BASE}:${PREV_TAG}" || \
    warn "Could not snapshot current API image (first deploy?)"
else
  warn "No existing API image found — first deploy, skipping snapshot"
fi

# Record current git SHA for rollback reference
echo "$(git rev-parse HEAD 2>/dev/null || echo 'unknown')" > .last_deploy_sha
success "Rollback snapshot complete (SHA: $GIT_SHA)"

# =============================================================================
# STEP 3 — Pull latest code
# =============================================================================
log "Step 3/13 — Pulling latest code from git"

REMOTE="$(git remote 2>/dev/null | head -1 || true)"
if [[ -n "$REMOTE" ]]; then
  BRANCH="$(git rev-parse --abbrev-ref HEAD 2>/dev/null || echo 'main')"
  git pull "$REMOTE" "$BRANCH" --ff-only \
    || die "git pull failed — resolve conflicts or merge issues before deploying"
  NEW_SHA="$(git rev-parse --short HEAD)"
  success "Code up to date (SHA: $NEW_SHA)"
else
  warn "No git remote configured — skipping git pull (using current working tree)"
  NEW_SHA="$GIT_SHA"
fi

BUILD_TIMESTAMP="$(date -u '+%Y-%m-%dT%H:%M:%SZ')"
RELEASE_TAG="${NEW_SHA:-$GIT_SHA}-$(date -u '+%Y%m%d%H%M%S')"
export HRMS_API_IMAGE="${HRMS_API_IMAGE:-hrms-api:${RELEASE_TAG}}"
export HRMS_MIGRATE_IMAGE="${HRMS_MIGRATE_IMAGE:-hrms-api-migrate:${RELEASE_TAG}}"
success "Release images: ${HRMS_API_IMAGE} and ${HRMS_MIGRATE_IMAGE}"

# =============================================================================
# STEP 4 — Build the SPA
# =============================================================================
log "Step 4/13 — Building SPA (bun)"

cd HRMS.SPA.Source

log "  Installing dependencies (frozen lockfile)..."
bun install --frozen-lockfile \
  || die "bun install failed — check package.json and bun.lock"

log "  Running production build..."
bun run build:ci \
  || die "SPA build failed — run 'bun run build:ci' manually to see errors"

cd "$SCRIPT_DIR"

# Copy dist to spa-dist (nginx volume mount path)
rm -rf spa-dist
cp -r HRMS.SPA.Source/dist ./spa-dist
[[ -f "spa-dist/index.html" ]] || die "SPA build succeeded but spa-dist/index.html not found"
success "SPA built and placed in spa-dist/ ($(du -sh spa-dist | awk '{print $1}'))"

# =============================================================================
# STEP 5 — Patch nginx.conf with real domain name
# =============================================================================
log "Step 5/13 — Patching nginx.conf for domain: $DOMAIN_NAME"

# nginx.conf is mounted read-only from ./nginx/nginx.conf
# Bake the domain name in — never use envsubst at runtime in production
if grep -q "YOUR_DOMAIN_NAME" nginx/nginx.conf; then
  cp nginx/nginx.conf nginx/nginx.conf.bak
  sed -i "s/YOUR_DOMAIN_NAME/${DOMAIN_NAME}/g" nginx/nginx.conf
  success "nginx.conf: YOUR_DOMAIN_NAME → $DOMAIN_NAME"
else
  # Already patched or already correct
  if grep -q "$DOMAIN_NAME" nginx/nginx.conf; then
    success "nginx.conf already configured for $DOMAIN_NAME"
  else
    warn "nginx.conf does not contain YOUR_DOMAIN_NAME or $DOMAIN_NAME — verify manually"
  fi
fi

# =============================================================================
# STEP 6 — Build API Docker image
# =============================================================================
log "Step 6/13 — Building API Docker image"

$COMPOSE build \
  --build-arg GIT_SHA="$NEW_SHA" \
  --build-arg BUILD_TIMESTAMP="$BUILD_TIMESTAMP" \
  api \
  || die "Docker build failed — check Dockerfile and dotnet build output above"

success "API image built (SHA: $NEW_SHA, timestamp: $BUILD_TIMESTAMP)"

# =============================================================================
# STEP 7 — Pre-migration backup
# =============================================================================
log "Step 7/13 — Taking pre-migration database backup"

if [[ -z "${BACKUP_ENCRYPTION_KEY:-}" ]]; then
  warn "BACKUP_ENCRYPTION_KEY not set — skipping pre-migration backup"
  warn "Set BACKUP_ENCRYPTION_KEY in .env for encrypted backups (strongly recommended)"
else
  # Only attempt backup if MySQL is already running (update scenario)
  if $COMPOSE ps mysql 2>/dev/null | grep -q "running\|Up\|healthy"; then
    bash scripts/mysql-backup.sh \
      && success "Pre-migration backup complete" \
      || warn "Backup failed — continuing deploy (check scripts/mysql-backup.sh manually)"
  else
    warn "MySQL not yet running — skipping pre-migration backup (first deploy)"
  fi
fi

# =============================================================================
# STEP 8 — Run database migrations
# =============================================================================
log "Step 8/13 — Running database migrations"

# Bring up MySQL first and wait for it to be healthy
log "  Starting MySQL..."
$COMPOSE up -d mysql
log "  Waiting for MySQL to be healthy (up to 90s)..."
for i in $(seq 1 18); do
  STATUS="$($COMPOSE ps --format json mysql 2>/dev/null | python3 -c "import sys,json; d=json.load(sys.stdin); print(d[0].get('Health','') if isinstance(d,list) else d.get('Health',''))" 2>/dev/null || true)"
  if [[ "$STATUS" == "healthy" ]]; then
    success "MySQL is healthy"
    break
  fi
  if [[ $i -eq 18 ]]; then
    # Last chance — check with mysqladmin directly
    $COMPOSE exec mysql mysqladmin ping -u"${MYSQL_USER:-hrms}" -p"${MYSQL_PASSWORD}" --silent 2>/dev/null \
      && success "MySQL is responding" \
      || die "MySQL did not become healthy within 90s"
  fi
  sleep 5
done

# Run pre-migration backfill (idempotent — safe on fresh DB)
log "  Running pre-migration company backfill..."
$COMPOSE run --rm backfill \
  || die "Company backfill container failed — check logs: docker compose -f docker-compose.prod.yml logs backfill"
success "Backfill complete"

# Run EF Core migrations
log "  Applying EF Core migrations..."
$COMPOSE run --rm migrate \
  || die "Database migration failed — check logs: docker compose -f docker-compose.prod.yml logs migrate"
success "Migrations applied"

# =============================================================================
# STEP 9 — Start full stack
# =============================================================================
log "Step 9/13 — Starting full stack"

$COMPOSE up -d --remove-orphans \
  || die "docker compose up failed"

success "All containers started"

# =============================================================================
# STEP 10 — Wait for health
# =============================================================================
log "Step 10/13 — Waiting for services to become healthy (up to 120s)"

HEALTHY=false
for i in $(seq 1 24); do
  # Check both API and nginx
  API_HEALTH="$(curl -sf --max-time 5 "http://127.0.0.1/api/healthz" 2>/dev/null || true)"
  if echo "$API_HEALTH" | grep -qi "healthy"; then
    HEALTHY=true
    break
  fi
  if [[ $i -eq 12 ]]; then
    warn "Still waiting... (${i}×5s elapsed)"
    $COMPOSE ps --format "table {{.Name}}\t{{.Status}}" 2>/dev/null || true
  fi
  sleep 5
done

if ! $HEALTHY; then
  echo ""
  warn "Health check timed out. Showing container status:"
  $COMPOSE ps
  echo ""
  warn "Showing API logs (last 30 lines):"
  $COMPOSE logs --tail=30 api
  die "Stack did not become healthy within 120s"
fi

success "API is healthy"

# =============================================================================
# STEP 11 — Smoke verification
# =============================================================================
log "Step 11/13 — Running smoke verification"

FAIL_COUNT=0

smoke_check() {
  local label="$1" url="$2" expect="$3"
  local body
  body="$(curl -sf --max-time 10 "$url" 2>/dev/null || echo '__CURL_FAILED__')"
  if [[ "$body" == "__CURL_FAILED__" ]]; then
    echo -e "  ${RED}✗ FAIL${NC}  $label → unreachable ($url)"
    ((FAIL_COUNT++)) || true
  elif echo "$body" | grep -qi "$expect"; then
    echo -e "  ${GREEN}✓ PASS${NC}  $label"
  else
    echo -e "  ${RED}✗ FAIL${NC}  $label → unexpected response (expected '$expect')"
    echo "           Body: $(echo "$body" | head -c 200)"
    ((FAIL_COUNT++)) || true
  fi
}

smoke_check "HTTPS → nginx (HTTP/80 redirect)"  "http://127.0.0.1/"              "301\|Location\|Moved"
smoke_check "API /healthz (via nginx proxy)"     "http://127.0.0.1/api/healthz"  "[Hh]ealthy"
smoke_check "API /healthz/live"                  "http://127.0.0.1/api/healthz/live"  "[Hh]ealthy"
smoke_check "API /healthz/ready"                 "http://127.0.0.1/api/healthz/ready" "[Hh]ealthy"

# HTTPS smoke (requires TLS to be up — expected after first deploy)
HTTPS_CODE="$(curl -sk --max-time 10 -o /dev/null -w "%{http_code}" "https://127.0.0.1/health" \
  --resolve "${DOMAIN_NAME}:443:127.0.0.1" 2>/dev/null || echo "000")"
if [[ "$HTTPS_CODE" == "200" ]]; then
  echo -e "  ${GREEN}✓ PASS${NC}  HTTPS /health → 200"
else
  echo -e "  ${YELLOW}⚠ WARN${NC}  HTTPS /health → $HTTPS_CODE (verify TLS manually: curl -k https://$DOMAIN_NAME/health)"
fi

if [[ $FAIL_COUNT -gt 0 ]]; then
  die "Smoke verification failed ($FAIL_COUNT check(s)). Stack is UP but not healthy — see above"
fi
success "All smoke checks passed"

# =============================================================================
# STEP 12 — Prune dangling images
# =============================================================================
log "Step 12/13 — Pruning dangling images"
docker image prune -f >/dev/null 2>&1 && success "Dangling images pruned" || warn "Image prune failed (non-fatal)"

# =============================================================================
# STEP 13 — Result
# =============================================================================
log "Step 13/13 — Deploy complete"

echo ""
echo -e "${BOLD}${GREEN}══════════════════════════════════════════════${NC}"
echo -e "${BOLD}${GREEN}  ✅ DEPLOYED                                  ${NC}"
echo -e "${BOLD}${GREEN}══════════════════════════════════════════════${NC}"
echo ""
echo -e "  ${BOLD}App URL:${NC}      https://${DOMAIN_NAME}"
echo -e "  ${BOLD}Health:${NC}       https://${DOMAIN_NAME}/health"
echo -e "  ${BOLD}API health:${NC}   https://${DOMAIN_NAME}/api/healthz"
echo -e "  ${BOLD}Git SHA:${NC}      ${NEW_SHA}"
echo -e "  ${BOLD}Build time:${NC}   ${BUILD_TIMESTAMP}"
echo -e "  ${BOLD}Deploy log:${NC}   ${DEPLOY_LOG}"
echo ""
echo -e "  ${YELLOW}Post-deploy checklist:${NC}"
echo -e "  □ Log in as SuperAdmin and change the initial password"
echo -e "  □ Confirm email delivery: POST /api/auth/forgot-password"
echo -e "  □ Confirm Hangfire dashboard: https://${DOMAIN_NAME}/hangfire"
echo -e "  □ Schedule automated backups (cron → scripts/mysql-backup.sh)"
echo -e "  □ Set up certificate renewal hook (see DEPLOYMENT.md §4)"
echo ""
