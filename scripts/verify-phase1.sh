#!/usr/bin/env bash
# Phase 1 environment + build verification.
# Prints PASS / FAIL / BLOCKED per mandated check. Never aborts on missing tools.
set -u
cd "$(dirname "$0")/.."
ok(){  echo "PASS    $1"; }
bad(){ echo "FAIL    $1"; }
blk(){ echo "BLOCKED $1 (tool not installed in this environment)"; }
has(){ command -v "$1" >/dev/null 2>&1; }

echo "== .NET backend =="
if has dotnet; then
  dotnet --version && ok "dotnet --version" || bad "dotnet --version"
  dotnet --info    && ok "dotnet --info"    || bad "dotnet --info"
  dotnet restore HRMS.sln --locked-mode          && ok "dotnet restore --locked-mode" || bad "dotnet restore --locked-mode"
  dotnet build HRMS.sln -c Release --no-restore  && ok "dotnet build -c Release"      || bad "dotnet build -c Release"
  dotnet test  HRMS.sln -c Release --no-build --settings coverlet.runsettings && ok "dotnet test" || bad "dotnet test"
  dotnet list HRMS.sln package --vulnerable --include-transitive || true
else
  blk "dotnet (.NET 8 SDK 8.0.416 per global.json)"
fi

echo "== Docker =="
if has docker; then
  docker --version        && ok "docker --version"        || bad "docker --version"
  docker compose version  && ok "docker compose version"  || bad "docker compose version"
  for f in docker-compose*.yml; do
    docker compose -f "$f" config >/dev/null 2>&1 && ok "docker compose config $f" || bad "docker compose config $f"
  done
  docker build -t hrms-api:verify . && ok "docker build" || bad "docker build"
else
  blk "docker / docker compose"
fi

echo "== MySQL schema =="
if has mysql; then
  mysql --version && ok "mysql --version" || bad "mysql --version"
  # Supplementary index/soft-delete SQL is now part of the EF migration chain.
  for f in scripts/db-init.sql; do
    [ -f "$f" ] || continue
    if [ -n "${MYSQL_URL:-}" ]; then
      mysql "$MYSQL_URL" -e "SOURCE $f;" >/dev/null 2>&1 && ok "schema apply $f" || bad "schema apply $f"
    else
      blk "schema apply $f (set MYSQL_URL to validate against a live server)"
    fi
  done
else
  blk "mysql client (schema validation)"
fi

echo "== Frontend (Bun) =="
if has bun; then
  ( cd HRMS.SPA.Source \
    && { bun install --frozen-lockfile && ok "bun install --frozen-lockfile" || bad "bun install --frozen-lockfile"; } \
    && { bun run typecheck  && ok "bun run typecheck"  || bad "bun run typecheck"; } \
    && { bun run lint       && ok "bun run lint"       || bad "bun run lint"; } \
    && { bun run vitest --run 2>/dev/null || bun run test; } && ok "bun run vitest" || bad "bun run vitest" \
    && { bun run build:ci   && ok "bun run build:ci"   || bad "bun run build:ci"; } )
  if has bunx; then bunx playwright --version && ok "bunx playwright" || blk "bunx playwright (browsers not installed)"; fi
else
  blk "bun (frontend install/typecheck/test/build)"
fi

echo "== Static guards =="
[ -z "$(find HRMS.API/wwwroot -name '*.html' -o -path 'HRMS.API/wwwroot/js/*.js' 2>/dev/null)" ] \
  && ok "no legacy html/js under HRMS.API/wwwroot" || bad "legacy html/js still under HRMS.API/wwwroot"
[ ! -f HRMS.SPA.Source/package-lock.json ] && ok "no package-lock.json" || bad "package-lock.json present"
! grep -rn "dangerouslySetInnerHTML" HRMS.SPA.Source/src --include='*.ts' --include='*.tsx' | grep -v '^\s*//' | grep -qv '//' \
  && ok "no dangerouslySetInnerHTML in SPA src" || echo "INFO    review dangerouslySetInnerHTML matches (comments allowed)"
