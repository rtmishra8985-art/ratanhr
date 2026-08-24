# MySQL Migration Validation Checklist
**HRMS v2.1.0** | PostgreSQL → MySQL 8.4 Migration Validation

Run this checklist after every migration deployment. All items must PASS before going live.

---

## Section 1 — Package Replacement

- [ ] `HRMS.Infrastructure.csproj`: `Pomelo.EntityFrameworkCore.MySql` ≥ 8.0.0 present
- [ ] `HRMS.Infrastructure.csproj`: `Npgsql.EntityFrameworkCore.PostgreSQL` absent
- [ ] `HRMS.API.csproj`: `AspNetCore.HealthChecks.MySql` present
- [ ] `HRMS.API.csproj`: `AspNetCore.HealthChecks.NpgSql` absent
- [ ] `HRMS.API.csproj`: `Hangfire.MySql.Core` present
- [ ] `HRMS.API.csproj`: `Hangfire.PostgreSql` absent
- [ ] `HRMS.Tests.csproj`: `MySqlConnector` and `Testcontainers.MySql` present
- [ ] `HRMS.API/packages.lock.json`: No Npgsql entries
- [ ] `HRMS.Infrastructure/packages.lock.json`: No Npgsql entries

**Verify command:**
```bash
grep -rn "Npgsql" HRMS.API/packages.lock.json HRMS.Infrastructure/packages.lock.json
# Expected: zero matches
```

---

## Section 2 — C# Provider Registrations

- [ ] `ServiceExtensions.cs`: `UseMySql` with `ServerVersion.AutoDetect` present
- [ ] `ServiceExtensions.cs`: `EnableRetryOnFailure` configured
- [ ] `ServiceExtensions.cs`: `UseMySqlStorage` for Hangfire present
- [ ] `ServiceExtensions.cs`: `UseNpgsql` absent
- [ ] `Program.cs`: `AddMySql` health check registration present
- [ ] `Program.cs`: No `AddNpgSql` calls
- [ ] No `using Npgsql.*` outside `Migrations/` directory

**Verify command:**
```bash
grep -rn "UseNpgsql\|AddNpgSql\|using Npgsql" . --include="*.cs" | grep -v "Migrations/"
# Expected: zero matches
```

---

## Section 3 — Model Mappings

- [ ] `ApplicationDbContext.cs`: No `UseXminAsConcurrencyToken` calls
- [ ] `ApplicationDbContext.cs`: No `HasColumnName("xmin")` calls
- [ ] `ApplicationDbContext.cs`: `IsRowVersion()` on `PayrollLock.RowVersion`
- [ ] `ApplicationDbContext.cs`: `IsRowVersion()` on `Payslip.RowVersion`
- [ ] `PayrollLock.cs`: `public byte[] RowVersion` property present
- [ ] `Payslip.cs`: `public byte[] RowVersion` property present
- [ ] `ApplicationDbContext.cs`: No `jsonb` column type; `HasColumnType("json")` used
- [ ] `ApplicationDbContext.cs`: No `timestamptz` column type
- [ ] `ApplicationDbContext.cs`: `datetime(6)` applied globally to all DateTime columns
- [ ] `LoginHistoryController.cs`: No active `ILike` calls

**Verify commands:**
```bash
grep -n "UseXminAsConcurrencyToken\|HasColumnName(\"xmin\")" HRMS.Infrastructure/Data/ApplicationDbContext.cs
# Expected: zero matches

grep -n "datetime(6)" HRMS.Infrastructure/Data/ApplicationDbContext.cs | wc -l
# Expected: count > 0

grep -n "jsonb\|timestamptz" HRMS.Infrastructure/Data/ApplicationDbContext.cs
# Expected: zero matches
```

---

## Section 4 — Configuration Files

- [ ] `appsettings.json`: `Server=` and `Port=3306` in connection string
- [ ] `appsettings.json`: `EnableReadReplica: false`
- [ ] `appsettings.Development.json`: MySQL format; no real password literals
- [ ] `appsettings.Production.json`: All secrets empty (env var overrides only)
- [ ] `.env.example`: `MYSQL_DATABASE`, `MYSQL_USER`, `MYSQL_PASSWORD`, `MYSQL_ROOT_PASSWORD` present
- [ ] `.env.example`: No `POSTGRES_DB`, `POSTGRES_USER`, `POSTGRES_PASSWORD`
- [ ] `scripts/generate-secrets.sh`: Generates `MYSQL_*` variables
- [ ] `k8s/external-secrets/cluster-secret-store.yaml`: provider configured for the target secret backend
- [ ] `k8s/external-secrets/external-secret.yaml`: maps the MySQL and application secret keys to `hrms-secrets`
- [ ] `k8s/configmap.yaml`: `mysql-svc`; port `3306`

---

## Section 5 — Docker and Infrastructure

- [ ] `docker-compose.yml`: `mysql:8.4` service present
- [ ] `docker-compose.yml`: `mysqladmin ping` healthcheck
- [ ] `docker-compose.yml`: `hrms_mysqldata` volume declared
- [ ] `docker-compose.yml`: No active `postgres` service
- [ ] `docker-compose.override.yml`: No `POSTGRES_` references
- [ ] `docker-compose.replica.yml`: Fully commented out
- [ ] `docker-compose.backup.yml`: `mysqldump` used; `mysql-backup.sh` mounted
- [ ] `Dockerfile`: No `pg_isready`, `POSTGRES_*`, `psql` in active lines

---

## Section 6 — Scripts and SQL

- [ ] `scripts/pg-backup.sh`: **Does not exist** (deleted)
- [ ] `scripts/mysql-backup.sh`: Exists; uses `mysqldump`, `MYSQL_*` variables
- [ ] `scripts/backup-s3.sh`: Uses `mysqldump`, `MYSQL_*`
- [ ] `scripts/migrate.sh`: References `mysql:3306`, `mysql-backup.sh`
- [ ] `scripts/test-restore.sh`: Uses `mysql` client, `MYSQL_*`
- [ ] `scripts/db-init.sql`: `CREATE DATABASE IF NOT EXISTS` with `utf8mb4`
- [ ] Historical SQL files: All have `HISTORICAL` header comment

---

## Section 7 — Kubernetes Manifests

- [ ] `k8s/mysql-statefulset.yaml`: Exists; `mysql:8.4`; `mysqladmin ping` probe
- [ ] `k8s/postgres-statefulset.yaml.bak`: Exists as `.bak` (rollback reference)
- [ ] `k8s/postgres-statefulset.yaml`: **Does not exist** as active manifest
- [ ] `k8s/backup-cronjob.yaml`: `mysqldump`; `mysql-svc`
- [ ] `k8s/migrate-job.yaml`: `mysqladmin ping` wait; `MYSQL_*` env vars
- [ ] `k8s/README.md`: MySQL instructions; no active PostgreSQL deployment steps

---

## Section 8 — Test Files

- [ ] `HRMS.Tests/PostgresIntegrationTests.cs`: **Does not exist**
- [ ] `HRMS.Tests/MySqlIntegrationTests.cs`: Exists; `MySqlConnector`; `mysql:8.4`
- [ ] `HRMS.Tests/DockerfileValidationTests.cs`: No `pg_trgm` assertion; `DoesNotContain("pg_isready")` present
- [ ] `HRMS.Tests/HealthCheckIntegrationTests.cs`: No active Npgsql comments
- [ ] Full test tree: No active `Npgsql|UseNpgsql|POSTGRES_|pg_isready` in `*.cs`

---

## Section 9 — Seed Data

- [ ] `SeedAsync` in `Program.cs` exists
- [ ] `SUPERADMIN_INITIAL_PASSWORD` environment variable read when set
- [ ] BCrypt hashing applied before storing password
- [ ] No hardcoded password literals in active files
- [ ] Idempotency guard: superadmin not re-created if already exists with secure hash

---

## Section 10 — EF Core Migrations

- [ ] Original 39 PostgreSQL migration files in `HRMS.Infrastructure/Migrations/` preserved
- [ ] `HRMS.Infrastructure/Migrations/MySql/` directory exists (Option A)
- [ ] MySQL migration files: No Npgsql metadata; uses `datetime(6)`, `json`, `utf8mb4`
- [ ] Migration applies cleanly: `dotnet ef database update` exits 0

---

## Section 11 — Documentation

- [ ] `Documentation/MySqlMigrationAudit.md`: Exists; contains "Post-migration Verification" section
- [ ] `Documentation/MySqlMigrationGuide.md`: Exists; UTC datetime strategy documented
- [ ] `Documentation/MySqlCutoverPlan.md`: Exists; rollback procedure included
- [ ] `Documentation/MySqlValidationChecklist.md`: This file
- [ ] Active documentation files: No `pg_dump`, `psql`, `POSTGRES_*` commands
- [ ] `Architecture/DatabaseDictionary.md`: Uses `datetime(6)` not `timestamptz`

---

## Section 12 — Security

- [ ] No real password literals in `appsettings.*.json` (only placeholders)
- [ ] No hardcoded credentials in scripts (env var references only)
- [ ] No checked-in Kubernetes Secret manifest contains production credential values
- [ ] External Secrets Operator manifests reference the approved secret backend and materialize `hrms-secrets`
- [ ] `postgres-archive/` files preserved for rollback reference

---

## Final Sign-Off

| Section | Result | Notes |
|---------|--------|-------|
| 1 — Packages | ☐ PASS / ☐ FAIL | |
| 2 — Provider Registrations | ☐ PASS / ☐ FAIL | |
| 3 — Model Mappings | ☐ PASS / ☐ FAIL | |
| 4 — Configuration | ☐ PASS / ☐ FAIL | |
| 5 — Docker | ☐ PASS / ☐ FAIL | |
| 6 — Scripts | ☐ PASS / ☐ FAIL | |
| 7 — Kubernetes | ☐ PASS / ☐ FAIL | |
| 8 — Tests | ☐ PASS / ☐ FAIL | |
| 9 — Seed Data | ☐ PASS / ☐ FAIL | |
| 10 — EF Migrations | ☐ PASS / ☐ FAIL | |
| 11 — Documentation | ☐ PASS / ☐ FAIL | |
| 12 — Security | ☐ PASS / ☐ FAIL | |

**Overall Verdict:** ☐ PASS — Ready for cutover / ☐ FAIL — Remediation required

Signed by: _________________________ Date: _____________

---

*Checklist version: 2026-07-26.*
