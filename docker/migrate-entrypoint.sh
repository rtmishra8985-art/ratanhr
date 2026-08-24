#!/bin/sh
# migrate-entrypoint.sh
# Runs inside the "migrate" Docker stage.
# 1. Applies EF Core migrations (canonical path: Migrations/MySql/ only).
# 2. Applies supplementary SQL files in order.
# Both steps are idempotent; safe to re-run on every deployment.

set -eu

: "${MYSQL_HOST:?MYSQL_HOST is required}"
: "${MYSQL_PORT:=3306}"
: "${MYSQL_USER:?MYSQL_USER is required}"
: "${MYSQL_PASSWORD:?MYSQL_PASSWORD is required}"
: "${MYSQL_DATABASE:?MYSQL_DATABASE is required}"
: "${ConnectionStrings__DefaultConnection:?ConnectionStrings__DefaultConnection is required}"

echo "==> [migrate] Waiting for MySQL to be ready..."
until mysqladmin ping -h"$MYSQL_HOST" -P"$MYSQL_PORT" -u"$MYSQL_USER" -p"$MYSQL_PASSWORD" \
      --skip-ssl --silent 2>/dev/null; do
  echo "    MySQL not yet ready – retrying in 3s..."
  sleep 3
done
echo "==> [migrate] MySQL is ready."

echo "==> [migrate] Running EF Core migrations (MySql/ only)..."
dotnet tool run dotnet-ef database update \
  --context ApplicationDbContext \
  --project HRMS.Infrastructure/HRMS.Infrastructure.csproj \
  --startup-project HRMS.API/HRMS.API.csproj \
  --configuration Release \
  --no-build
echo "==> [migrate] EF Core migrations complete."

# Item 6 (2026-08-11): the supplementary SQL files (db_performance.sql,
# db_indexes_fix.sql, db_softdelete_fix.sql) have been folded into the EF Core
# migration chain (20260811080000_FoldDbScriptIndexes). The migration chain above
# is now the single source of truth for schema, indexes and soft-delete columns —
# there is no out-of-band SQL step any more.

echo "==> [migrate] All migration steps complete. Exiting 0."
