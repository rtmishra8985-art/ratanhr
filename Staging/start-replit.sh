#!/usr/bin/env bash
# =============================================================================
# start-replit.sh — RatanHR Staging stack launcher for Replit
#
# Replit's iptables rules block Docker bridge inter-container networking.
# This script applies the host-networking override and starts the stack
# in the correct order with readiness gates at each step.
#
# Prerequisites:
#   - Staging/.env.staging populated (copy from appsettings.Staging.json.template)
#   - Docker available in this environment
#
# Usage (run from the repo root):
#   bash Staging/start-replit.sh
# =============================================================================

set -euo pipefail

COMPOSE_BASE="Staging/docker-compose.staging.yml"
COMPOSE_REPLIT="Staging/docker-compose.staging.replit.yml"
ENV_FILE="Staging/.env.staging"

DC="docker compose -f ${COMPOSE_BASE} -f ${COMPOSE_REPLIT} --env-file ${ENV_FILE}"

echo "▶  Starting MySQL, Redis, and MailHog..."
${DC} up -d hrms_staging_db hrms_staging_redis hrms_staging_mailhog

# ── Wait for MySQL to be ready ────────────────────────────────
echo "⏳ Waiting for MySQL on 127.0.0.1:3307..."
until docker exec "$(docker ps -qf name=hrms_staging_db)" \
  mysqladmin ping -h 127.0.0.1 -P 3307 --silent 2>/dev/null; do
  echo "   MySQL not ready yet — retrying in 3s..."
  sleep 3
done
echo "✅ MySQL ready"

# ── Run EF Core migrations ────────────────────────────────────
echo "▶  Running EF Core migrations..."
${DC} up hrms_staging_migrate --no-deps
echo "✅ Migrations complete"

# ── Start API ─────────────────────────────────────────────────
echo "▶  Starting API..."
${DC} up -d hrms_staging_api

# ── Wait for API health ───────────────────────────────────────
echo "⏳ Waiting for API on 127.0.0.1:8081..."
until curl -sf http://127.0.0.1:8081/healthz 2>/dev/null | grep -q Healthy; do
  echo "   API not healthy yet — retrying in 3s..."
  sleep 3
done
echo "✅ Stack ready"
echo ""
echo "  API      → http://127.0.0.1:8081"
echo "  MySQL    → 127.0.0.1:3307"
echo "  Redis    → 127.0.0.1:6380"
echo "  MailHog  → http://127.0.0.1:8025"
echo ""
echo "To run Phase 8 runbook:"
echo "  export SUPERADMIN_INITIAL_PASSWORD=\"<from .env.staging>\""
echo "  export DB_PASSWORD=\"<STAGING_DB_PASSWORD from .env.staging>\""
echo "  export REDIS_PASSWORD=\"<STAGING_REDIS_PASSWORD from .env.staging>\""
echo "  export API_HOST=\"127.0.0.1:8081\""
echo "  bash Staging/phase8_runbook.sh 2>&1 | tee /tmp/phase8_run.log"
echo "  tail -20 /tmp/phase8_run.log"
