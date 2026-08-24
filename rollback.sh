#!/usr/bin/env bash
# =============================================================================
# rollback.sh — RatanHR HRMS Production Rollback
#
# Reverts the API container to the image tagged :previous, which was
# snapshotted automatically by deploy.sh before the last deploy.
#
# Usage:
#   bash rollback.sh
#
# What it does:
#   1. Confirms a :previous image exists
#   2. Stops the running API container
#   3. Reactivates the :previous image under a new immutable rollback tag
#   4. Starts the API container with the reverted image
#   5. Waits for the API to become healthy
#   6. Prints ✅ ROLLED BACK or ❌ ROLLBACK FAILED
#
# Notes:
#   • nginx and the SPA are stateless — they are NOT rolled back (redeploy if needed)
#   • The database is NOT rolled back automatically. If the failed deploy included
#     a destructive migration, restore from the pre-migration backup in ./backups/
#     BEFORE running this script (see DEPLOYMENT.md §9 for manual DB restore steps)
#   • Only ONE previous image is kept. Running rollback twice returns to the same state.
# =============================================================================

set -euo pipefail
IFS=$'\n\t'

RED='\033[0;31m'; GREEN='\033[0;32m'; YELLOW='\033[1;33m'
CYAN='\033[0;36m'; BOLD='\033[1m'; NC='\033[0m'

log()     { echo -e "${CYAN}[$(date -u '+%H:%M:%S')]${NC} $*"; }
success() { echo -e "${GREEN}${BOLD}[$(date -u '+%H:%M:%S')] ✓${NC}  $*"; }
warn()    { echo -e "${YELLOW}[$(date -u '+%H:%M:%S')] ⚠${NC}  $*"; }
die()     { echo -e "${RED}${BOLD}[$(date -u '+%H:%M:%S')] ✗  FATAL: $*${NC}" >&2; echo ""; echo -e "${RED}${BOLD}❌ ROLLBACK FAILED — $*${NC}"; exit 1; }

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

COMPOSE="docker compose -f docker-compose.prod.yml"
ENV_FILE="${SCRIPT_DIR}/.env"
ROLLBACK_LOG="${SCRIPT_DIR}/logs/rollback_$(date -u '+%Y%m%d_%H%M%S').log"

mkdir -p "${SCRIPT_DIR}/logs"
exec > >(tee -a "$ROLLBACK_LOG") 2>&1

echo ""
echo -e "${BOLD}${YELLOW}══════════════════════════════════════════════${NC}"
echo -e "${BOLD}${YELLOW}  RatanHR HRMS — Production Rollback          ${NC}"
echo -e "${BOLD}${YELLOW}  $(date -u '+%Y-%m-%d %H:%M:%S UTC')                    ${NC}"
echo -e "${BOLD}${YELLOW}══════════════════════════════════════════════${NC}"
echo ""

# Load .env
[[ -f "$ENV_FILE" ]] || die ".env not found — cannot start services without credentials"
set -a
source <(grep -v '^\s*#' "$ENV_FILE" | grep -v '^\s*$' | grep '=' 2>/dev/null) || true
set +a

# =============================================================================
# STEP 1 — Confirm rollback target exists
# =============================================================================
log "Step 1/5 — Checking for previous image snapshot"

PREV_IMAGE="$(docker images --format '{{.Repository}}:{{.Tag}}' \
  | grep -E 'ratanhr.*api:previous|hrms.*api:previous|ratan.*api:previous' \
  | head -1 || true)"

if [[ -z "$PREV_IMAGE" ]]; then
  # Try to find by compose project label
  COMPOSE_PROJECT="$(basename "$SCRIPT_DIR" | tr '[:upper:]' '[:lower:]' | tr -cd 'a-z0-9_-')"
  PREV_IMAGE="$(docker images --format '{{.Repository}}:{{.Tag}}' \
    | grep "${COMPOSE_PROJECT}.*:previous\|hrms.*:previous" | head -1 || true)"
fi

if [[ -z "$PREV_IMAGE" ]]; then
  echo ""
  echo "  Available images:"
  docker images --format "  {{.Repository}}:{{.Tag}}\t{{.ID}}\t{{.CreatedSince}}" | head -20
  echo ""
  die "No :previous image found. deploy.sh snapshots the current image as :previous before each deploy.\n  If this is the first deploy, there is nothing to roll back to."
fi

PREV_SHA="$(docker inspect --format='{{index .Config.Labels "org.opencontainers.image.revision"}}' "$PREV_IMAGE" 2>/dev/null || echo 'unknown')"
PREV_CREATED="$(docker inspect --format='{{.Created}}' "$PREV_IMAGE" 2>/dev/null | cut -c1-19 || echo 'unknown')"
success "Rollback target found: $PREV_IMAGE"
log "  Built: $PREV_CREATED  |  SHA: $PREV_SHA"

# Confirm rollback
echo ""
echo -e "${YELLOW}${BOLD}  ⚠  This will replace the running API with the previous image.${NC}"
echo -e "     Target:  ${PREV_IMAGE}"
echo -e "     Created: ${PREV_CREATED}"
if [[ -f ".last_deploy_sha" ]]; then
  CURRENT_SHA="$(cat .last_deploy_sha | head -c 8)"
  echo -e "     Current SHA: ${CURRENT_SHA}"
fi
echo ""
read -rp "  Continue? [yes/N] " CONFIRM
[[ "$CONFIRM" == "yes" ]] || { echo "Rollback cancelled."; exit 0; }

# =============================================================================
# STEP 2 — Stop current API container
# =============================================================================
log "Step 2/5 — Stopping current API container"

$COMPOSE stop api 2>/dev/null && success "API container stopped" || warn "API was already stopped"

# =============================================================================
# STEP 3 — Restore previous image under an immutable rollback tag
# =============================================================================
log "Step 3/5 — Restoring previous image"

# Derive the base image name from the :previous tag
IMG_BASE="${PREV_IMAGE%:previous}"

# Tag the current image as :failed-$(date) to preserve it for post-mortem
FAILED_TAG="${IMG_BASE}:failed-$(date -u '+%Y%m%d%H%M%S')"
CURRENT_IMAGE="$(docker images --format '{{.Repository}}:{{.Tag}}' \
  | grep -E "^${IMG_BASE}:" | grep -vE ':(previous|failed-|rollback-)' | head -1 || true)"

if [[ -n "$CURRENT_IMAGE" ]]; then
  docker tag "$CURRENT_IMAGE" "$FAILED_TAG" 2>/dev/null \
    && success "Current (failed) image preserved as $FAILED_TAG" \
    || warn "Could not tag failed image (non-fatal)"
fi

# Give the rollback image a unique, immutable operational tag. The image digest
# is unchanged; the tag identifies this rollback event without using a
# mutable release alias.
ROLLBACK_TAG="${IMG_BASE}:rollback-$(date -u '+%Y%m%d%H%M%S')"
docker tag "$PREV_IMAGE" "$ROLLBACK_TAG" \
  || die "Could not retag $PREV_IMAGE as ${ROLLBACK_TAG}"
export HRMS_API_IMAGE="$ROLLBACK_TAG"
success "Restored $PREV_IMAGE → ${ROLLBACK_TAG}"

# Update docker-compose to use the restored image
# (HRMS_API_IMAGE is exported for the compose command below)

# =============================================================================
# STEP 4 — Start API with restored image
# =============================================================================
log "Step 4/5 — Starting API with restored image"

# Start only the API — MySQL, Redis, and nginx stay running
# Use --no-build to prevent accidentally rebuilding from source
$COMPOSE up -d --no-build --no-deps api \
  || die "docker compose up api failed"

success "API container restarted with previous image"

# Wait for health
log "  Waiting for API to become healthy (up to 90s)..."
HEALTHY=false
for i in $(seq 1 18); do
  HEALTH_BODY="$(curl -sf --max-time 5 "http://127.0.0.1/api/healthz" 2>/dev/null || true)"
  if echo "$HEALTH_BODY" | grep -qi "healthy"; then
    HEALTHY=true
    break
  fi
  sleep 5
done

if ! $HEALTHY; then
  warn "API did not become healthy within 90s. Showing logs:"
  $COMPOSE logs --tail=40 api
  die "Rolled-back API is not healthy. Manual intervention required."
fi

success "API is healthy with previous image"

# =============================================================================
# STEP 5 — Verify and report
# =============================================================================
log "Step 5/5 — Smoke check"

LIVE_CODE="$(curl -sf --max-time 10 -o /dev/null -w "%{http_code}" "http://127.0.0.1/api/healthz" 2>/dev/null || echo "000")"
[[ "$LIVE_CODE" == "200" ]] || die "POST-ROLLBACK HEALTH CHECK FAILED — HTTP $LIVE_CODE"

# Clean up the :previous tag after creating the rollback tag.
# The rollback tag remains available for auditability and a subsequent deploy.
docker rmi "$PREV_IMAGE" 2>/dev/null \
  && log "  Cleaned up :previous tag" \
  || warn "Could not remove :previous tag (non-fatal)"

echo ""
echo -e "${BOLD}${GREEN}══════════════════════════════════════════════${NC}"
echo -e "${BOLD}${GREEN}  ✅ ROLLED BACK                               ${NC}"
echo -e "${BOLD}${GREEN}══════════════════════════════════════════════${NC}"
echo ""
echo -e "  ${BOLD}Restored image:${NC}  ${ROLLBACK_TAG}"
echo -e "  ${BOLD}Image created:${NC}   ${PREV_CREATED}"
echo -e "  ${BOLD}Health:${NC}          https://${DOMAIN_NAME:-localhost}/api/healthz"
echo -e "  ${BOLD}Rollback log:${NC}    ${ROLLBACK_LOG}"
echo ""
echo -e "  ${YELLOW}Post-rollback actions:${NC}"
echo -e "  □ Investigate the failed deploy — check logs/deploy_*.log"
echo -e "  □ If the failed deploy included a migration, assess DB state:"
echo -e "    docker compose -f docker-compose.prod.yml exec mysql \\"
echo -e "      mysql -u\${MYSQL_USER} -p\${MYSQL_PASSWORD} \${MYSQL_DATABASE:-hrms_db} \\"
echo -e "      -e 'SELECT * FROM __EFMigrationsHistory ORDER BY MigrationId DESC LIMIT 5;'"
echo -e "  □ To re-deploy after fixing the issue: bash deploy.sh"
echo ""
