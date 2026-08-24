#!/bin/bash
# =============================================================================
# Let's Encrypt / Certbot Initial Certificate Issuance
#
# Run this ONCE on a fresh deployment to obtain your first certificate.
# After this, the certbot container renews automatically every 12 hours.
#
# Usage:
#   chmod +x nginx/init-letsencrypt.sh
#   DOMAIN=api.yourcompany.com EMAIL=admin@yourcompany.com ./nginx/init-letsencrypt.sh
# =============================================================================

set -euo pipefail

DOMAIN="${DOMAIN:?Set DOMAIN env var, e.g. api.yourcompany.com}"
EMAIL="${EMAIL:?Set EMAIL env var for Lets Encrypt notifications}"
STAGING="${STAGING:-0}"   # Set STAGING=1 to test without hitting rate limits

echo "=== HRMS SSL Initialisation ==="
echo "Domain  : $DOMAIN"
echo "Email   : $EMAIL"
echo "Staging : $STAGING"

# Create required directories
mkdir -p "$(dirname "$0")/../data/certbot/conf"
mkdir -p "$(dirname "$0")/../data/certbot/www"

# Download recommended TLS parameters
if [ ! -e "./nginx/options-ssl-nginx.conf" ]; then
    echo "Downloading TLS parameters..."
    curl -s https://raw.githubusercontent.com/certbot/certbot/master/certbot-nginx/certbot_nginx/_internal/tls_configs/options-ssl-nginx.conf \
        > ./nginx/options-ssl-nginx.conf
fi

# Create a temporary self-signed cert so nginx can start before Certbot runs
echo "Creating temporary self-signed certificate..."
docker compose run --rm --entrypoint "\
  openssl req -x509 -nodes -newkey rsa:2048 -days 1 \
    -keyout '/etc/letsencrypt/live/$DOMAIN/privkey.pem' \
    -out '/etc/letsencrypt/live/$DOMAIN/fullchain.pem' \
    -subj '/CN=localhost'" certbot

# Start nginx with the temporary cert
echo "Starting nginx..."
docker compose up -d nginx

# Delete the temporary cert
echo "Removing temporary cert..."
docker compose run --rm --entrypoint "\
  rm -Rf /etc/letsencrypt/live/$DOMAIN && \
  rm -Rf /etc/letsencrypt/archive/$DOMAIN && \
  rm -Rf /etc/letsencrypt/renewal/$DOMAIN.conf" certbot

# Request the real certificate
echo "Requesting certificate from Let's Encrypt..."
STAGING_FLAG=""
if [ "$STAGING" = "1" ]; then
    STAGING_FLAG="--staging"
    echo "  (Using staging server — certificate will NOT be trusted by browsers)"
fi

docker compose run --rm --entrypoint "\
  certbot certonly \
    --webroot \
    --webroot-path=/var/www/certbot \
    $STAGING_FLAG \
    --email $EMAIL \
    --agree-tos \
    --no-eff-email \
    -d $DOMAIN" certbot

# Reload nginx to pick up the real certificate
echo "Reloading nginx..."
docker compose exec nginx nginx -s reload

echo ""
echo "=== SSL Initialisation Complete ==="
echo "Certificate issued for: $DOMAIN"
echo "Auto-renewal: Certbot container renews every 12 h; nginx reloads every 6 h."
echo ""
echo "To verify renewal works: docker compose run --rm certbot certbot renew --dry-run"
