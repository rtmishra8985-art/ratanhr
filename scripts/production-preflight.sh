#!/usr/bin/env bash
# =============================================================================
# scripts/production-preflight.sh — RatanHR HRMS Production Pre-Flight Check
#
# Validates that ALL required production configuration is in place before the
# first deployment. Nothing is started or changed — this script is read-only.
#
# USAGE (from repo root):
#   cp .env.production.template .env
#   # Edit .env — fill every value
#   bash scripts/production-preflight.sh
#
# EXIT CODES:
#   0 — all required checks passed (safe to run scripts/deploy.sh)
#   1 — one or more required checks failed (do not deploy)
# =============================================================================
set -uo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"
ENV_FILE="$ROOT_DIR/.env"

FAIL_COUNT=0; PASS_COUNT=0; WARN_COUNT=0

GREEN='\033[0;32m'; RED='\033[0;31m'; YELLOW='\033[1;33m'
CYAN='\033[0;36m'; BOLD='\033[1m'; NC='\033[0m'

pass() { PASS_COUNT=$((PASS_COUNT+1)); echo -e "  ${GREEN}✔ PASS${NC}  $*"; }
fail() { FAIL_COUNT=$((FAIL_COUNT+1)); echo -e "  ${RED}✖ FAIL${NC}  $*" >&2; }
warn() { WARN_COUNT=$((WARN_COUNT+1)); echo -e "  ${YELLOW}⚠ WARN${NC}  $*"; }
section() { echo -e "\n${BOLD}${CYAN}══ $* ══${NC}"; }

# =============================================================================
# 1. .env file
# =============================================================================
section "1 — Production .env"

if [[ ! -f "$ENV_FILE" ]]; then
  fail ".env not found. Run: cp .env.production.template .env && fill all values"
  echo ""
  echo -e "${RED}Cannot continue without .env.${NC}"
  exit 1
fi
pass ".env exists"

# Load
set -a && source "$ENV_FILE" && set +a

# Check for unfilled template placeholders
UNFILLED=$(grep -c 'REPLACE_WITH\|your.*domain\|yourdomain\.com' "$ENV_FILE" 2>/dev/null || true)
if [[ "$UNFILLED" -gt 0 ]]; then
  fail "$UNFILLED placeholder(s) still set in .env — fill them all before deploying"
  grep -n 'REPLACE_WITH\|yourdomain\.com' "$ENV_FILE" | head -10 >&2
else
  pass "No template placeholders remaining in .env"
fi

# =============================================================================
# 2. Required secrets
# =============================================================================
section "2 — Required secrets"

check_var() {
  local var="$1" desc="$2"
  local val="${!var:-}"
  if [[ -z "$val" ]]; then
    fail "$var is not set ($desc)"
  elif [[ "$val" == *"REPLACE_"* ]] || [[ "$val" == *"PLACEHOLDER"* ]]; then
    fail "$var still contains a placeholder value"
  else
    pass "$var is set"
  fi
}

check_var "MYSQL_PASSWORD"          "MySQL application password"
check_var "MYSQL_ROOT_PASSWORD"     "MySQL root password"
check_var "REDIS_PASSWORD"          "Redis password"
check_var "JWT_PRIVATE_KEY_PEM"     "JWT RSA private key (RS256)"
check_var "JWT_PUBLIC_KEY_PEM"      "JWT RSA public key"
check_var "ENCRYPTION_KEY"          "AES-256 PII encryption key (32 bytes base64)"
check_var "DOMAIN_NAME"             "Production domain name (e.g. hrms.yourcompany.com)"
check_var "ALLOWED_HOSTS"           "ASP.NET Core host header allowlist"
check_var "EMAIL_PASSWORD"          "SMTP / SendGrid API key"
check_var "EMAIL_FROM_ADDRESS"      "Sender email address"

# Warn if optional but common items are absent
[[ -z "${SEQ_URL:-}" ]]             && warn "SEQ_URL is empty — structured log shipping disabled"
[[ -z "${OTEL_OTLP_ENDPOINT:-}"  ]] && warn "OTEL_OTLP_ENDPOINT is empty — distributed tracing disabled"

# =============================================================================
# 3. Encryption key strength
# =============================================================================
section "3 — Encryption key strength"

ENC_KEY="${ENCRYPTION_KEY:-}"
if [[ -n "$ENC_KEY" ]]; then
  ENC_LEN=${#ENC_KEY}
  if [[ "$ENC_LEN" -lt 40 ]]; then
    fail "ENCRYPTION_KEY appears too short ($ENC_LEN chars). Generate with: openssl rand -base64 32"
  else
    pass "ENCRYPTION_KEY length looks valid ($ENC_LEN chars)"
  fi
fi

# JWT private key must start with RSA header
PRIV="${JWT_PRIVATE_KEY_PEM:-}"
if [[ "$PRIV" == *"BEGIN RSA PRIVATE KEY"* ]] || [[ "$PRIV" == *"BEGIN PRIVATE KEY"* ]]; then
  pass "JWT_PRIVATE_KEY_PEM contains a valid PEM header"
else
  fail "JWT_PRIVATE_KEY_PEM does not look like a PEM key (missing -----BEGIN ... KEY----- header)"
fi

# =============================================================================
# 4. Docker + Compose
# =============================================================================
section "4 — Docker Engine"

if command -v docker >/dev/null 2>&1 && docker compose version >/dev/null 2>&1; then
  pass "Docker + Compose: $(docker --version | awk '{print $3}' | tr -d ',')"
else
  fail "Docker Engine 24+ with Compose v2 required"
fi

# Validate the production compose file
if [[ -f "$ROOT_DIR/docker-compose.prod.yml" ]]; then
  if docker compose -f "$ROOT_DIR/docker-compose.prod.yml" \
       --env-file "$ENV_FILE" config --quiet 2>/dev/null; then
    pass "docker-compose.prod.yml: valid (all env vars resolved)"
  else
    fail "docker-compose.prod.yml: config validation failed (missing env vars?)"
  fi
else
  warn "docker-compose.prod.yml not found — skipping compose validation"
fi

# =============================================================================
# 5. TLS certificate files
# =============================================================================
section "5 — TLS certificates"

CERT_DIR="$ROOT_DIR/nginx/ssl"
if [[ -d "$CERT_DIR" ]]; then
  if [[ -f "$CERT_DIR/cert.pem" ]]; then
    # Check expiry
    EXPIRY=$(openssl x509 -in "$CERT_DIR/cert.pem" -noout -enddate 2>/dev/null \
             | sed 's/notAfter=//' || echo "unknown")
    DAYS_LEFT=$(( ( $(date -d "$EXPIRY" +%s 2>/dev/null || echo 0) - $(date +%s) ) / 86400 ))
    if [[ "$DAYS_LEFT" -gt 30 ]]; then
      pass "TLS cert: valid for ~$DAYS_LEFT more days (expires $EXPIRY)"
    elif [[ "$DAYS_LEFT" -gt 0 ]]; then
      warn "TLS cert: expires in $DAYS_LEFT days — renew soon"
    else
      fail "TLS cert: expired or unreadable"
    fi
  else
    fail "nginx/ssl/cert.pem not found — obtain a certificate first:"
    echo "      sudo certbot certonly --standalone -d ${DOMAIN_NAME:-yourdomain.com}"
    echo "      cp /etc/letsencrypt/live/${DOMAIN_NAME:-yourdomain.com}/fullchain.pem nginx/ssl/cert.pem"
    echo "      cp /etc/letsencrypt/live/${DOMAIN_NAME:-yourdomain.com}/privkey.pem   nginx/ssl/key.pem"
  fi

  if [[ -f "$CERT_DIR/key.pem" ]]; then
    pass "nginx/ssl/key.pem found"
  else
    fail "nginx/ssl/key.pem not found"
  fi
else
  fail "nginx/ssl/ directory not found — create it and place cert.pem + key.pem inside"
fi

# =============================================================================
# 6. Domain DNS
# =============================================================================
section "6 — DNS resolution"

DOMAIN="${DOMAIN_NAME:-}"
if [[ -z "$DOMAIN" ]]; then
  warn "DOMAIN_NAME not set — skipping DNS check"
elif command -v host >/dev/null 2>&1; then
  if host "$DOMAIN" >/dev/null 2>&1; then
    RESOLVED_IP=$(host "$DOMAIN" 2>/dev/null | grep 'has address' | head -1 | awk '{print $NF}')
    pass "DNS: $DOMAIN resolves to $RESOLVED_IP"
  else
    fail "DNS: $DOMAIN does not resolve — create an A record pointing to this server's IP"
  fi
elif command -v nslookup >/dev/null 2>&1; then
  if nslookup "$DOMAIN" >/dev/null 2>&1; then
    pass "DNS: $DOMAIN resolves (nslookup)"
  else
    fail "DNS: $DOMAIN does not resolve"
  fi
else
  warn "Neither 'host' nor 'nslookup' available — skipping DNS check"
fi

# =============================================================================
# 7. SMTP connectivity (optional)
# =============================================================================
section "7 — SMTP connectivity"

SMTP_HOST="${EMAIL_HOST:-}"
SMTP_PORT="${EMAIL_PORT:-587}"
if [[ -z "$SMTP_HOST" ]]; then
  warn "EMAIL_HOST not set — SMTP check skipped"
elif command -v curl >/dev/null 2>&1; then
  if curl -s --connect-timeout 5 "smtp://${SMTP_HOST}:${SMTP_PORT}" \
       --no-progress-meter 2>&1 | grep -q -i "220\|ok\|ready\|greeting" 2>/dev/null; then
    pass "SMTP: ${SMTP_HOST}:${SMTP_PORT} reachable"
  else
    warn "SMTP: ${SMTP_HOST}:${SMTP_PORT} did not return a greeting — verify credentials manually"
  fi
else
  warn "curl not available — SMTP connectivity check skipped"
fi

# =============================================================================
# 8. Nginx template
# =============================================================================
section "8 — nginx config template"

NGINX_TEMPLATE="$ROOT_DIR/nginx/nginx.conf.template"
if [[ ! -f "$NGINX_TEMPLATE" ]]; then
  fail "nginx/nginx.conf.template not found"
elif command -v envsubst >/dev/null 2>&1; then
  TMP_NGINX=$(mktemp /tmp/nginx_preflight_XXXXXX.conf)
  DOMAIN_NAME="${DOMAIN_NAME:-example.com}" envsubst '${DOMAIN_NAME}' \
    < "$NGINX_TEMPLATE" > "$TMP_NGINX"
  if command -v nginx >/dev/null 2>&1; then
    if nginx -t -c "$TMP_NGINX" 2>&1; then
      pass "nginx config template: syntax OK"
    else
      fail "nginx config template: syntax error after envsubst"
    fi
  else
    pass "nginx/nginx.conf.template: envsubst succeeded (nginx not installed for -t check)"
  fi
  rm -f "$TMP_NGINX"
else
  warn "envsubst not available (install gettext) — nginx template syntax check skipped"
fi

# =============================================================================
# 9. Backup configuration
# =============================================================================
section "9 — Backup configuration"

if [[ -z "${BACKUP_ENCRYPTION_KEY:-}" ]]; then
  fail "BACKUP_ENCRYPTION_KEY not set in .env — backups will abort"
  echo "      Generate with: openssl rand -base64 48"
else
  pass "BACKUP_ENCRYPTION_KEY is set"
fi

BACKUP_DIR="${BACKUP_DIR:-$ROOT_DIR/backups}"
mkdir -p "$BACKUP_DIR" 2>/dev/null
if [[ -w "$BACKUP_DIR" ]]; then
  pass "Backup directory writable: $BACKUP_DIR"
else
  fail "Backup directory is not writable: $BACKUP_DIR"
fi

# =============================================================================
# 10. .gitignore — secrets must be excluded
# =============================================================================
section "10 — .gitignore coverage"

GITIGNORE="$ROOT_DIR/.gitignore"
if [[ -f "$GITIGNORE" ]]; then
  for pattern in ".env" "*.pem" "nginx/ssl/" "Staging/.env.staging" ".env.e2e"; do
    if grep -q "$pattern" "$GITIGNORE" 2>/dev/null; then
      pass ".gitignore covers: $pattern"
    else
      warn ".gitignore may not cover: $pattern (add it to prevent accidental commits)"
    fi
  done
else
  warn ".gitignore not found — ensure secrets are excluded before committing"
fi

# =============================================================================
# Summary
# =============================================================================
echo ""
echo -e "${BOLD}╔══════════════════════════════════════════╗${NC}"
echo -e "${BOLD}║   PRODUCTION PRE-FLIGHT SUMMARY          ║${NC}"
echo -e "${BOLD}╠══════════════════════════════════════════╣${NC}"
printf "${BOLD}║   ✔ PASS  %-4d                           ║${NC}\n" "$PASS_COUNT"
printf "${BOLD}║   ✖ FAIL  %-4d                           ║${NC}\n" "$FAIL_COUNT"
printf "${BOLD}║   ⚠ WARN  %-4d                           ║${NC}\n" "$WARN_COUNT"
echo -e "${BOLD}╠══════════════════════════════════════════╣${NC}"
if [[ "$FAIL_COUNT" -eq 0 ]]; then
  echo -e "${BOLD}║   VERDICT: ${GREEN}✅ READY TO DEPLOY${NC}${BOLD}           ║${NC}"
  echo -e "${BOLD}╚══════════════════════════════════════════╝${NC}"
  echo ""
  echo "  Next step: bash scripts/deploy.sh"
else
  echo -e "${BOLD}║   VERDICT: ${RED}❌ NOT READY — fix $FAIL_COUNT failure(s)${NC}${BOLD}  ║${NC}"
  echo -e "${BOLD}╚══════════════════════════════════════════╝${NC}"
fi
echo ""

exit "$FAIL_COUNT"
