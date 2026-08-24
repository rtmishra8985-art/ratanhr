#!/usr/bin/env bash
# =============================================================================
# deploy.sh — RatanHR HRMS Production Deployment Script
#
# Usage:
#   chmod +x scripts/deploy.sh
#   ./scripts/deploy.sh [--env-file /path/to/.env] [--skip-pin] [--dry-run]
#
# What this script does:
#   1. Validates prerequisites (Docker, envsubst, .env file)
#   2. Validates all required environment variables are set (no placeholders)
#   3. Pins Docker image digests (unless --skip-pin)
#   4. Generates nginx/nginx.conf from nginx/nginx.conf.template using DOMAIN_NAME
#   5. Runs: docker compose -f docker-compose.prod.yml up -d --build
#   6. Waits for the API health check to pass
#   7. Prints a deployment summary
#
# Requirements:
#   • Docker Engine 24+ with Compose v2 plugin
#   • envsubst (part of gettext — apt install gettext / brew install gettext)
#   • .env file (copy .env.production.template → .env, fill all <REQUIRED> values)
#   • TLS certificate already provisioned at /etc/letsencrypt/live/${DOMAIN_NAME}/
#   • SPA built dist folder at ./spa-dist/ (see DEPLOYMENT.md §3)
# =============================================================================
set -euo pipefail

# ── Parse arguments ────────────────────────────────────────────────────────────
ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
ENV_FILE="$ROOT_DIR/.env"
SKIP_PIN=0
DRY_RUN=0

while [[ $# -gt 0 ]]; do
  case "$1" in
    --env-file)   ENV_FILE="$2"; shift 2 ;;
    --skip-pin)   SKIP_PIN=1; shift ;;
    --dry-run)    DRY_RUN=1; shift ;;
    --help|-h)
      sed -n '2,20p' "$0"; exit 0 ;;
    *)
      echo "ERROR: unknown argument: $1" >&2; exit 1 ;;
  esac
done

log()  { echo "[$(date '+%H:%M:%S')] $*"; }
ok()   { echo "[$(date '+%H:%M:%S')] ✔ $*"; }
fail() { echo "[$(date '+%H:%M:%S')] ✖ $*" >&2; exit 1; }

# ── Step 1: Prerequisites ──────────────────────────────────────────────────────
log "Checking prerequisites..."
command -v docker    >/dev/null 2>&1 || fail "docker is not installed."
docker compose version >/dev/null 2>&1 || fail "docker compose (v2 plugin) is not available."
command -v envsubst  >/dev/null 2>&1 || fail "envsubst is not installed. Install gettext: apt install gettext / brew install gettext"

[[ -f "$ENV_FILE" ]] || fail ".env file not found at '$ENV_FILE'. Copy .env.production.template and fill all <REQUIRED> values."
[[ -f "$ROOT_DIR/nginx/nginx.conf.template" ]] || fail "nginx/nginx.conf.template not found."
[[ -f "$ROOT_DIR/docker-compose.prod.yml" ]] || fail "docker-compose.prod.yml not found."
ok "Prerequisites satisfied."

# ── Step 2: Load and validate .env ─────────────────────────────────────────────
log "Loading environment from $ENV_FILE..."
# shellcheck disable=SC1090
set -o allexport; source "$ENV_FILE"; set +o allexport

required_vars=(
  DOMAIN_NAME
  MYSQL_PASSWORD
  MYSQL_ROOT_PASSWORD
  REDIS_PASSWORD
  JWT_PRIVATE_KEY_PEM
  JWT_PUBLIC_KEY_PEM
  ENCRYPTION_KEY
  EMAIL_HOST
  EMAIL_USERNAME
  EMAIL_PASSWORD
  EMAIL_FROM_ADDRESS
  APP_COMPANY_NAME
  APP_SUPPORT_EMAIL
  DPO_EMAIL
  SUPERADMIN_INITIAL_PASSWORD
)

log "Validating required environment variables..."
errors=()
for var in "${required_vars[@]}"; do
  value="${!var:-}"
  if [[ -z "$value" ]]; then
    errors+=("$var is not set")
  elif [[ "$value" == *"<REQUIRED>"* || "$value" == *"CHANGE_ME"* || "$value" == *"REPLACE_WITH"* ]]; then
    errors+=("$var still contains a placeholder value: $value")
  fi
done

if [[ ${#errors[@]} -gt 0 ]]; then
  echo "" >&2
  echo "ERROR: The following required values are missing or contain placeholders:" >&2
  for e in "${errors[@]}"; do echo "  • $e" >&2; done
  echo "" >&2
  echo "Edit '$ENV_FILE' and fill in all <REQUIRED> values before deploying." >&2
  exit 1
fi
ok "All required environment variables are set."

# Validate DOMAIN_NAME does not contain a protocol or trailing slash
if [[ "$DOMAIN_NAME" =~ ^https?:// || "$DOMAIN_NAME" =~ /$ ]]; then
  fail "DOMAIN_NAME must not include https:// or a trailing slash. Got: $DOMAIN_NAME"
fi

# ── Step 3: Pin Docker image digests ──────────────────────────────────────────
if [[ "$SKIP_PIN" -eq 0 ]]; then
  log "Pinning Docker image digests..."
  DIGEST_SCRIPT="$ROOT_DIR/scripts/pin-docker-digests.sh"
  if [[ -f "$DIGEST_SCRIPT" ]]; then
    chmod +x "$DIGEST_SCRIPT"
    if "$DIGEST_SCRIPT"; then
      ok "Docker image digests pinned and Dockerfile updated."
    else
      echo "WARNING: pin-docker-digests.sh failed (Docker daemon may not be available). Proceeding with existing digests." >&2
    fi
  else
    echo "WARNING: scripts/pin-docker-digests.sh not found. Skipping digest pin." >&2
  fi
else
  log "Skipping digest pin (--skip-pin)."
fi

# ── Step 4: Generate nginx/nginx.conf from template ────────────────────────────
log "Generating nginx/nginx.conf from template (DOMAIN_NAME=$DOMAIN_NAME)..."

NGINX_TEMPLATE="$ROOT_DIR/nginx/nginx.conf.template"
NGINX_CONF="$ROOT_DIR/nginx/nginx.conf"

# envsubst only substitutes ${DOMAIN_NAME} — all other ${...} nginx variables are preserved
envsubst '${DOMAIN_NAME}' < "$NGINX_TEMPLATE" > "$NGINX_CONF"

# Verify no placeholder remains
if grep -q 'YOUR_DOMAIN_NAME' "$NGINX_CONF" 2>/dev/null; then
  fail "nginx/nginx.conf still contains YOUR_DOMAIN_NAME after substitution. Check nginx.conf.template."
fi

# Verify the domain was substituted
if ! grep -q "$DOMAIN_NAME" "$NGINX_CONF"; then
  fail "nginx/nginx.conf does not contain DOMAIN_NAME=$DOMAIN_NAME after substitution."
fi

ok "nginx/nginx.conf generated with server_name $DOMAIN_NAME"

# ── Step 5: Verify TLS certificates exist ─────────────────────────────────────
TLS_CERT="/etc/letsencrypt/live/${DOMAIN_NAME}/fullchain.pem"
TLS_KEY="/etc/letsencrypt/live/${DOMAIN_NAME}/privkey.pem"
if [[ ! -f "$TLS_CERT" || ! -f "$TLS_KEY" ]]; then
  echo ""
  echo "WARNING: TLS certificates not found at expected paths:" >&2
  echo "  $TLS_CERT" >&2
  echo "  $TLS_KEY" >&2
  echo "  Provision certificates with Certbot before nginx starts:" >&2
  echo "  certbot certonly --webroot -w /var/www/certbot -d $DOMAIN_NAME" >&2
  echo ""
  if [[ "$DRY_RUN" -eq 0 ]]; then
    read -r -p "Continue without TLS certificates? (nginx will fail to start) [y/N] " confirm
    [[ "$confirm" =~ ^[Yy]$ ]] || { echo "Deployment aborted."; exit 1; }
  fi
fi

# ── Step 6: Verify SPA dist ────────────────────────────────────────────────────
SPA_DIST="$ROOT_DIR/spa-dist"
if [[ ! -d "$SPA_DIST" || ! -f "$SPA_DIST/index.html" ]]; then
  echo ""
  echo "WARNING: spa-dist/index.html not found." >&2
  echo "  Build the SPA first:" >&2
  echo "    cd HRMS.SPA.Source && npm ci && npm run build && cp -r dist ../spa-dist" >&2
  echo ""
  if [[ "$DRY_RUN" -eq 0 ]]; then
    read -r -p "Continue without SPA dist? (nginx will serve a blank page) [y/N] " confirm
    [[ "$confirm" =~ ^[Yy]$ ]] || { echo "Deployment aborted."; exit 1; }
  fi
fi

# ── Step 7: Run docker compose ────────────────────────────────────────────────
if [[ "$DRY_RUN" -eq 1 ]]; then
  log "DRY RUN: would execute:"
  echo "  docker compose -f docker-compose.prod.yml --env-file \"$ENV_FILE\" up -d --build"
  ok "Dry run complete. Remove --dry-run to deploy."
  exit 0
fi

log "Starting production stack (this may take several minutes on first run)..."
log "  ClamAV will download virus definitions (~250 MB) — allow 3-5 minutes for first startup."
docker compose \
  -f "$ROOT_DIR/docker-compose.prod.yml" \
  --env-file "$ENV_FILE" \
  up -d --build

# ── Step 8: Wait for API health check ─────────────────────────────────────────
log "Waiting for API to become healthy..."
API_URL="http://127.0.0.1:9090/health"  # Direct to API container port
NGINX_HEALTH="http://127.0.0.1/health"   # Via nginx (443 requires TLS)

attempts=0
while (( attempts < 30 )); do
  if curl -sf --max-time 5 "$NGINX_HEALTH" >/dev/null 2>&1; then
    ok "API is healthy at https://$DOMAIN_NAME"
    break
  fi
  attempts=$(( attempts + 1 ))
  echo "  ... waiting (${attempts}/30, checking every 10s)"
  sleep 10
done

if (( attempts >= 30 )); then
  echo ""
  echo "WARNING: API health check did not pass within 5 minutes." >&2
  echo "  Check logs with: docker compose -f docker-compose.prod.yml logs api" >&2
  echo "  ClamAV logs:     docker compose -f docker-compose.prod.yml logs clamav" >&2
fi

# ── Summary ───────────────────────────────────────────────────────────────────
echo ""
echo "=============================================="
echo "  RatanHR HRMS — Deployment Complete"
echo "=============================================="
echo "  Domain:    https://$DOMAIN_NAME"
echo "  Health:    https://$DOMAIN_NAME/health"
echo "  Logs:      docker compose -f docker-compose.prod.yml logs -f"
echo "  Status:    docker compose -f docker-compose.prod.yml ps"
echo ""
echo "  IMPORTANT next steps:"
echo "    1. Log in as SuperAdmin and change the initial password immediately."
echo "    2. Run the PII backfill script if migrating from an existing database:"
echo "       docker compose exec api dotnet HRMS.API.dll backfill-pii"
echo "    3. Verify file upload works (ClamAV must be healthy):"
echo "       docker compose -f docker-compose.prod.yml ps clamav"
echo "    4. Monitor logs for the first 30 minutes."
echo "=============================================="
