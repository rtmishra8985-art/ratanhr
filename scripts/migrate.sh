#!/usr/bin/env bash
# ─────────────────────────────────────────────────────────────────────────────
# migrate.sh
# Runs EF Core migrations against the production MySQL database safely.
# Use this after setting Database:AutoMigrate=false in production.
#
# Phase 5: Updated from PostgreSQL to MySQL 8.4.
# Database tool changed from pg-backup.sh to mysql-backup.sh.
# Connection references changed from postgres:5432 to mysql:3306.
#
# Usage:
#   ./scripts/migrate.sh
# ─────────────────────────────────────────────────────────────────────────────
set -euo pipefail

ENV_FILE="$(dirname "$0")/../.env"
[[ -f "$ENV_FILE" ]] && source "$ENV_FILE"

echo "⚠️  This will apply pending EF Core migrations to the PRODUCTION database."
echo "   Database: ${MYSQL_DATABASE:-hrms_db} on mysql:3306"
echo ""
read -rp "Type 'yes' to continue: " CONFIRM
[[ "$CONFIRM" != "yes" ]] && echo "Aborted." && exit 0

echo "📦 Backing up database before migration..."
"$(dirname "$0")/mysql-backup.sh"

echo ""
echo "🚀 Starting the database dependency..."
docker compose up -d mysql

echo "🧹 Running the idempotent company backfill..."
docker compose run --rm backfill

echo "🚀 Applying migrations with the dedicated migration image..."
docker compose run --rm migrate

echo ""
echo "✅ Migrations applied successfully."
