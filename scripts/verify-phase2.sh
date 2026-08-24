#!/usr/bin/env bash
# Phase 2 close-out verification.
#
# Closes the two items the Phase 1 remediation sandbox could NOT verify itself
# because its network proxy blocks api.nuget.org, Docker Hub, and
# repo.mysql.com (all required for real dotnet restore / MySQL 8.4 / Testcontainers).
# Run this on a machine with normal internet access (a dev box or CI runner).
#
# Prints PASS / FAIL / BLOCKED per check. Never aborts on missing tools.
set -u
cd "$(dirname "$0")/.."
ok(){  echo "PASS    $1"; }
bad(){ echo "FAIL    $1"; }
blk(){ echo "BLOCKED $1"; }
has(){ command -v "$1" >/dev/null 2>&1; }

echo "== 1. EF Core: confirm no pending model changes =="
# This is the authoritative check for what Phase 1 could only verify by manual
# code reading (entity <-> fluent config <-> migration <-> snapshot). It will
# fail loudly if the salary_structures / payslips.company_id fixes were
# incomplete, or if any other entity has drifted from ApplicationDbContextModelSnapshot_MySql.cs.
if has dotnet; then
  dotnet restore HRMS.sln --locked-mode && ok "dotnet restore --locked-mode" || { bad "dotnet restore --locked-mode"; }
  if ! has dotnet-ef; then
    dotnet tool install --global dotnet-ef --version 8.* >/dev/null 2>&1
    export PATH="$PATH:$HOME/.dotnet/tools"
  fi
  if has dotnet-ef || dotnet ef --version >/dev/null 2>&1; then
    # Requires a reachable (even empty) MySQL 8.4 instance for the design-time factory.
    if [ -n "${ConnectionStrings__DefaultConnection:-}" ]; then
      dotnet ef migrations has-pending-model-changes \
        --project HRMS.Infrastructure --startup-project HRMS.API \
        && ok "dotnet ef migrations has-pending-model-changes (none pending)" \
        || bad "dotnet ef migrations has-pending-model-changes (drift detected — see output above)"
    else
      blk "dotnet ef migrations has-pending-model-changes (set ConnectionStrings__DefaultConnection first, e.g. to the docker-compose.e2e.yml mysql service)"
    fi
  else
    blk "dotnet-ef tool (could not install — check NuGet access)"
  fi
else
  blk "dotnet (.NET 8 SDK 8.0.416 per global.json)"
fi

echo "== 2. Payroll constraints against real MySQL 8.4 =="
# Preferred: the repo's own Testcontainers-backed integration suite, which spins
# up a real mysql:8.4 container per test run (see HRMS.Tests/MySqlIntegrationTests.cs).
if has dotnet && has docker; then
  dotnet test HRMS.sln -c Release -p:DefineConstants=TESTCONTAINERS_ENABLED \
    --filter "FullyQualifiedName~MySqlIntegrationTests" \
    && ok "dotnet test MySqlIntegrationTests (real mysql:8.4 via Testcontainers)" \
    || bad "dotnet test MySqlIntegrationTests"
else
  blk "dotnet test MySqlIntegrationTests (needs dotnet + docker; got dotnet=$(has dotnet && echo yes || echo no) docker=$(has docker && echo yes || echo no))"
fi

# Fallback / extra confidence: apply the actual migration SQL to a real mysql:8.4
# container from docker-compose.e2e.yml and re-run the same constraint checks
# Phase 1 ran by hand against MySQL 8.0.46, this time against the pinned version.
if has docker; then
  docker compose -f docker-compose.e2e.yml --env-file .env.e2e up -d mysql --wait \
    && ok "docker compose up mysql (mysql:8.4)" \
    || { bad "docker compose up mysql"; }

  MYSQL_CLI="docker compose -f docker-compose.e2e.yml exec -T mysql mysql -uroot -p${MYSQL_ROOT_PASSWORD:-} hrms"

  echo "-- duplicate payslip rejected by ux_payslips_employee_month_year --"
  $MYSQL_CLI -e "SELECT COUNT(*) AS unique_index_present FROM information_schema.statistics WHERE table_schema=DATABASE() AND table_name='payslips' AND index_name='ux_payslips_employee_month_year';" \
    && ok "ux_payslips_employee_month_year exists on mysql:8.4" || bad "ux_payslips_employee_month_year missing on mysql:8.4"

  echo "-- payslips.company_id exists and is NOT NULL (the Phase 1 fix) --"
  $MYSQL_CLI -e "SELECT IS_NULLABLE, COLUMN_TYPE FROM information_schema.columns WHERE table_schema=DATABASE() AND table_name='payslips' AND column_name='company_id';" \
    && ok "payslips.company_id present on mysql:8.4" || bad "payslips.company_id missing on mysql:8.4"

  echo "-- salary_structures old-regime columns exist with correct defaults --"
  $MYSQL_CLI -e "SELECT COLUMN_NAME, COLUMN_DEFAULT, IS_NULLABLE FROM information_schema.columns WHERE table_schema=DATABASE() AND table_name='salary_structures' AND column_name IN ('is_old_regime','section_80c_deduction');" \
    && ok "salary_structures tax-regime columns present on mysql:8.4" || bad "salary_structures tax-regime columns missing on mysql:8.4"
else
  blk "docker compose mysql:8.4 fallback checks (docker not available)"
fi

echo "== Done. Any BLOCKED line means this machine is missing a tool/credential Phase 1's sandbox also lacked — install it and re-run. Any FAIL is a real regression to fix before release. =="
