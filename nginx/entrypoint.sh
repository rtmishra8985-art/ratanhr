#!/bin/sh
# =============================================================
# nginx/entrypoint.sh — Environment-variable expansion entrypoint
#
# FIX 3: Dedicated nginx entrypoint that expands ${DOMAIN_NAME},
# ${API_URL}, ${SSL_CERT_PATH}, ${SSL_KEY_PATH}, and ${APP_ENV}
# in nginx.conf.template using envsubst before nginx starts.
#
# Required environment variables:
#   DOMAIN_NAME   — public hostname, e.g. app.yourcompany.com
#   API_URL       — backend API base URL for connect-src CSP directive
#   SSL_CERT_PATH — path to TLS certificate (fullchain.pem)
#   SSL_KEY_PATH  — path to TLS private key  (privkey.pem)
#   APP_ENV       — deployment environment label (default: production)
#
# Usage (Docker):
#   ENTRYPOINT ["/etc/nginx/entrypoint.sh"]
#
# The script:
#   1. Validates required variables
#   2. Runs envsubst to produce /etc/nginx/nginx.conf from the template
#   3. Validates that no ${...} placeholders remain (envsubst guard)
#   4. Validates the generated config with `nginx -t`
#   5. Starts nginx in the foreground
#   6. Launches a background loop that reloads nginx every 6 hours
#      so renewed Let's Encrypt certificates are picked up automatically
# =============================================================

# FIX 2: set -euo pipefail — any failed substitution or validation step
# kills the container immediately with a clear error instead of starting
# nginx with broken literal ${VARIABLE} placeholders.
set -euo pipefail

# ── 1. Validate required variables ───────────────────────────────────────────
missing=""
for var in DOMAIN_NAME SSL_CERT_PATH SSL_KEY_PATH; do
    eval val=\$$var
    if [ -z "$val" ]; then
        missing="$missing $var"
    fi
done

if [ -n "$missing" ]; then
    echo "[entrypoint] ERROR: required environment variables are not set:$missing" >&2
    echo "[entrypoint] Set them in your .env file or Kubernetes secrets and restart." >&2
    exit 1
fi

# Provide sensible defaults for optional vars
API_URL="${API_URL:-https://${DOMAIN_NAME}/api}"
APP_ENV="${APP_ENV:-production}"

echo "[entrypoint] Expanding template: DOMAIN_NAME=${DOMAIN_NAME}, APP_ENV=${APP_ENV}"

# ── 2. Expand environment variables from template ─────────────────────────────
# The single-quoted variable list prevents envsubst from expanding nginx's own
# $host, $uri, $proxy_add_x_forwarded_for, etc. — only the listed vars are substituted.
envsubst '$DOMAIN_NAME $API_URL $APP_ENV $SSL_CERT_PATH $SSL_KEY_PATH' \
    < /etc/nginx/nginx.conf.template \
    > /etc/nginx/nginx.conf

echo "[entrypoint] nginx.conf generated successfully."

# ── 3. Guard: verify no unsubstituted ${...} placeholders remain ──────────────
# FIX 2: If envsubst silently failed (e.g. missing env var not caught above),
# nginx would start with literal ${DOMAIN_NAME} in the config — HTTPS redirect
# would break and TLS cert paths would be wrong. Fail fast instead.
if grep -q '\${' /etc/nginx/nginx.conf; then
    echo "ERROR: nginx.conf still contains unsubstituted variables:" >&2
    grep '\${' /etc/nginx/nginx.conf >&2
    exit 1
fi

# ── 4. Validate the generated config ─────────────────────────────────────────
echo "[entrypoint] Validating nginx configuration..."
nginx -t
echo "[entrypoint] Configuration valid."

# ── 4. Auto-reload loop for Let's Encrypt certificate renewal (background) ───
# Certbot renews certs every ~60 days; nginx must reload to pick up the new cert.
# The loop fires every 6 hours — well within the certbot renewal window.
(
    while true; do
        sleep 6h
        echo "[entrypoint] Reloading nginx to pick up any renewed certificates..."
        nginx -s reload
    done
) &

# ── 5. Start nginx in the foreground ─────────────────────────────────────────
echo "[entrypoint] Starting nginx (daemon off)..."
exec nginx -g 'daemon off;'
