> ⚠️ **SUPERSEDED** — This report was generated during an earlier audit/fix pass and no longer reflects the current state of the codebase. The authoritative current-state documents are [`RELEASE_GATE_FINAL.md`](RELEASE_GATE_FINAL.md) and [`VERIFICATION_REPORT_FINAL_v2.md`](VERIFICATION_REPORT_FINAL_v2.md). Do not use this file to assess production readiness.

---

# MySQL Migration Audit — Phase 12 Zero-PostgreSQL Verification

**Migration date:** 2026-07-26  
**Source database:** PostgreSQL 16.4  
**Target database:** MySQL 8.4  
**Migration engineer:** Automated via migration script (all 12 phases)

---

## Phase 1 — Package Replacement ✅

| File | Removed | Added |
|------|---------|-------|
| `HRMS.Infrastructure/HRMS.Infrastructure.csproj` | `Npgsql.EntityFrameworkCore.PostgreSQL 8.0.8` | `Pomelo.EntityFrameworkCore.MySql 8.0.2` |
| `HRMS.API/HRMS.API.csproj` | `AspNetCore.HealthChecks.NpgSql 8.0.1` | `AspNetCore.HealthChecks.MySql 8.0.2` |
| `HRMS.API/HRMS.API.csproj` | `Hangfire.PostgreSql 1.9.13` | `Hangfire.MySql.Core 2.4.0` |
| `HRMS.Tests/HRMS.Tests.csproj` | (none in original csproj) | `MySqlConnector 2.3.7`, `Testcontainers.MySql 3.10.0` |

**Hangfire package selection:** `Hangfire.MySql.Core 2.4.0`  
**Reason:** Direct `UseMySqlStorage()` API compatible with Hangfire 1.8.x; minimal dependencies; actively maintained for .NET Core; widely used in production EF Core 8 stacks.

**Note on lock files:** `HRMS.API/packages.lock.json` and `HRMS.Infrastructure/packages.lock.json` must be regenerated after package changes by running `scripts/generate-lock-file.sh` before building Docker images (Dockerfile uses `--locked-mode`).

---

## Phase 2 — C# Code Changes ✅

### 2a. DbContext Registration (ServiceExtensions.cs)

- Replaced `options.UseNpgsql(primaryConn, npgsql => {...})` with `options.UseMySql(primaryConn, ServerVersion.AutoDetect(primaryConn), mysql => {...})` for `ApplicationDbContext`.
- Replaced same pattern for `ReadReplicaDbContext`.
- `EnableRetryOnFailure` parameters updated: `errorCodesToAdd: null` → `errorNumbersToAdd: null`.
- `CommandTimeout` increased to 60s (payroll queries).

### 2b. ReadReplicaDbContext.cs

- Removed all WAL streaming replica documentation comments.
- Added TODO note: MySQL replication requires Group Replication or standard async replication at infrastructure level.
- `EnableReadReplica` remains `false` by default until MySQL replication is configured.
- `ReadReplicaDbContext` class itself preserved for future MySQL replication support.

### 2c. Entity Type Configurations (Persistence/Configurations/)

- Scanned all configuration files — no `jsonb`, `timestamptz`, or `timestamp with time zone` types found in the Configurations directory (only in ApplicationDbContext.cs).

### 2d. ApplicationDbContext.cs

| Location | Before | After |
|----------|--------|-------|
| Line 1245 `BiometricLog.RawData` | `.HasColumnType("jsonb")` | `.HasColumnType("json")` |
| Line 494 `PayrollLock` | `e.UseXminAsConcurrencyToken()` | `e.Property(x => x.RowVersion).HasColumnName("row_version").IsRowVersion()` |
| Line 537 `Payslip` | `e.UseXminAsConcurrencyToken()` | `e.Property(x => x.RowVersion).HasColumnName("row_version").IsRowVersion()` |

### 2d — Entity domain changes

| Entity | File | Change |
|--------|------|--------|
| `PayrollLock` | `HRMS.Domain/Entities/Payroll/PayrollLock.cs` | Removed `public uint Version { get; set; }`. Added `public byte[] RowVersion { get; set; } = Array.Empty<byte>()`. |
| `Payslip` | `HRMS.Domain/Entities/Payroll/Payslip.cs` | Removed `public uint Version { get; set; }`. Added `public byte[] RowVersion { get; set; } = Array.Empty<byte>()`. |

### 2e. DateTime UTC Strategy

- No `timestamptz` or `timestamp with time zone` column type strings found in active C# files.
- All existing `DateTime` columns use `datetime(6)` by default with Pomelo.
- UTC strategy enforced at application level via `DateTime.UtcNow`.

### 2f. Health Checks (Program.cs)

- Replaced `.AddNpgSql(dbConnectionString, name: "database", tags: ["db", "ready"])` with `.AddMySql(dbConnectionString, name: "database", tags: ["db", "ready"])`.
- Updated Hangfire comment at line 157 from "PostgreSQL storage" to "MySQL storage — Phase 2g".

### 2g. Hangfire (ServiceExtensions.cs)

- Removed `using Hangfire.PostgreSql;`
- Added `using Hangfire.MySql;`
- Replaced `.UsePostgreSqlStorage(connectionString, new PostgreSqlStorageOptions {...})` with `.UseMySqlStorage(connectionString, new MySqlStorageOptions {...})`.
- All options preserved: `PrepareSchemaIfNecessary`, `QueuePollInterval`, `InvisibilityTimeout`, `DistributedLockTimeout`.
- Added `TablesPrefix = "Hangfire_"` and `TransactionIsolationLevel = ReadCommitted`.

### 2h. Raw SQL in Services

- Scanned `HRMS.Infrastructure/Services/` — no PostgreSQL-specific raw SQL found (ON CONFLICT, DO $$, RAISE NOTICE, pg_ functions).

### 2i. Using Statements

- Removed `using Hangfire.PostgreSql;` from `ServiceExtensions.cs`.
- No other PostgreSQL using statements found in active C# files.

### 2d (ILike) — LoginHistoryController.cs

- Replaced two `EF.Functions.ILike(field, pattern)` calls with `EF.Functions.Like(field.ToLower(), $"%{emailLower}%")`.

### SwaggerDocumentation description update

- Updated `Description` from "ASP.NET Core 8 + PostgreSQL" to "ASP.NET Core 8 + MySQL 8.4".

---

## Phase 3 — Configuration Files ✅

| File | Change |
|------|--------|
| `HRMS.API/appsettings.json` | Connection string changed to MySQL format `Server=localhost;Port=3306;...` |
| `HRMS.API/appsettings.Development.json` | Connection string changed to MySQL format |
| `HRMS.API/appsettings.Production.json` | Connection string placeholder comments updated |
| `.env.example` | `POSTGRES_*` → `MYSQL_*`, `MYSQL_ROOT_PASSWORD` added, `S3_PREFIX` updated to `hrms/mysql` |

---

## Phase 4 — Docker Compose and Dockerfile ✅

| File | Changes |
|------|---------|
| `docker-compose.yml` | Replaced `postgres:16.4-alpine` service with `mysql:8.4`; updated `migrate` service connection string; updated `api` depends_on; renamed volume `hrms_pgdata` → `hrms_mysqldata`; updated backup service env vars |
| `docker-compose.override.yml` | Replaced `postgres` port exposure with `mysql` (3306); removed `POSTGRES_*` env vars |
| `docker-compose.backup.yml` | Replaced `pg_dump` with `mysqldump`; replaced `pg-backup.sh` with `mysql-backup.sh`; updated env vars |
| `docker-compose.replica.yml` | Entire WAL replication overlay commented out with explanation |
| `Dockerfile` | No changes needed — no PostgreSQL references in build stages |

---

## Phase 5 — Scripts and SQL ✅

| File | Action |
|------|--------|
| `scripts/pg-backup.sh` | **Preserved** (original) — `scripts/mysql-backup.sh` created as replacement |
| `scripts/mysql-backup.sh` | **New file** — `mysqldump` equivalent of `pg-backup.sh`; all `POSTGRES_*` → `MYSQL_*` |
| `scripts/backup-s3.sh` | Updated: calls `mysql-backup.sh` instead of `pg-backup.sh`; `POSTGRES_*` → `MYSQL_*`; S3 prefix updated |
| `scripts/migrate.sh` | Updated: `POSTGRES_DB` → `MYSQL_DATABASE`; `postgres:5432` → `mysql:3306`; `pg-backup.sh` → `mysql-backup.sh` |
| `scripts/test-restore.sh` | Rewritten: `psql` / `pg_restore` → `mysql` / `gunzip | mysql`; `PGHOST/PGPORT` → `MYSQL_HOST/MYSQL_PORT` |
| `scripts/db-init.sql` | Replaced entirely with MySQL 8.4 init (CREATE DATABASE utf8mb4, CREATE USER, GRANT) |
| `scripts/generate-secrets.sh` | `POSTGRES_PASSWORD/DB/USER` → `MYSQL_PASSWORD/DATABASE/USER`; `MYSQL_ROOT_PASSWORD` added |
| `bootstrap_only_db_setup.sql` | Classified HISTORICAL — PostgreSQL syntax header added |
| `db_setup.sql` | Classified HISTORICAL — PostgreSQL syntax header added |
| `db_setup_additions.sql` | Classified HISTORICAL — PostgreSQL syntax header added |
| `db_crm.sql` | Classified HISTORICAL — PostgreSQL syntax header added |
| `db_performance.sql` | Classified HISTORICAL — PostgreSQL syntax header added |
| `db_recruitment.sql` | Classified HISTORICAL — PostgreSQL syntax header added |

---

## Phase 6 — postgres/ Directory → postgres-archive/ ✅

| File | Action |
|------|--------|
| `postgres/primary.conf` | Moved to `postgres-archive/primary.conf` |
| `postgres/pg_hba_replication.conf` | Moved to `postgres-archive/pg_hba_replication.conf` |
| `postgres/replica-entrypoint.sh` | Moved to `postgres-archive/replica-entrypoint.sh` |
| `postgres-archive/README.md` | Created with explanation |
| `postgres/` directory | Removed |

---

## Phase 7 — Kubernetes Manifests ✅

| File | Action |
|------|--------|
| `k8s/postgres-statefulset.yaml` | Renamed to `k8s/postgres-statefulset.yaml.bak` (rollback reference) |
| `k8s/mysql-statefulset.yaml` | **New** — MySQL 8.4 StatefulSet with `mysqladmin ping` probes |
| `k8s/backup-cronjob.yaml` | Rewritten — `mysqldump` replaces `pg_dump`; `POSTGRES_*` → `MYSQL_*` |
| `k8s/configmap.yaml` | `DB_HOST: postgres-svc` → `mysql-svc`; `DB_PORT: 5432` → `3306`; `POSTGRES_*` → `MYSQL_*` |
| `k8s/migrate-job.yaml` | `wait-for-postgres` initContainer → `wait-for-mysql` with `mysqladmin ping` |
| Legacy checked-in Kubernetes Secret template (removed) | `POSTGRES_DB/USER/PASSWORD` → `MYSQL_DATABASE/USER/PASSWORD/ROOT_PASSWORD`; superseded by External Secrets Operator mappings |
| `k8s/README.md` | Updated all deployment instructions for MySQL |

---

## Phase 8 — Test Files ✅

| File | Action |
|------|--------|
| `HRMS.Tests/PostgresIntegrationTests.cs` | Deleted — replaced by `MySqlIntegrationTests.cs` |
| `HRMS.Tests/MySqlIntegrationTests.cs` | **New** — `MySqlContainer`, `UseMySql`, `MySqlConnection`, `mysql:8.4` image, MySQL-compatible SQL |
| `HRMS.Tests/DockerfileValidationTests.cs` | Updated `DbInitSql_*` test: removed PostgreSQL extension assertions; added MySQL-compatible assertions; added `Dockerfile_Does_Not_Reference_PostgreSQL` test |
| `HRMS.Tests/HealthCheckIntegrationTests.cs` | Comment updated from "Hangfire PostgreSQL" to "Hangfire MySQL" |

---

## Phase 9 — Fresh EF Core Migrations ⚠️

The 39 existing migrations in `HRMS.Infrastructure/Migrations/` use the PostgreSQL EF Core provider and are **preserved unchanged** as per the migration instructions. They are explicitly excluded from the zero-reference check.

**Required action before first MySQL deployment:**
Generate a new initial migration targeting the MySQL provider:
```bash
dotnet ef migrations add InitialMySql \
  --project HRMS.Infrastructure \
  --startup-project HRMS.API
dotnet ef database update
```

Or use `dotnet ef database update` with the existing migrations if EnsureCreated is used in development.

---

## Phase 10 — Seed Data ✅

No seed data files were PostgreSQL-specific. SeedAsync in the application code uses EF Core abstractions and is provider-agnostic.

---

## Phase 11 — Documentation ✅

| File | Action |
|------|--------|
| `Documentation/MySqlMigrationGuide.md` | Created — covers packages, connection strings, row version, JSON columns, ILike replacement, read replica, Hangfire, migrations, Docker, Kubernetes, troubleshooting |

---

## Phase 12 — Zero PostgreSQL Verification

### Active files — zero PostgreSQL references remaining

The following active source files have been verified to contain no PostgreSQL references:

- ✅ `HRMS.API/Extensions/ServiceExtensions.cs` — `UseNpgsql` replaced; `Hangfire.PostgreSql` removed
- ✅ `HRMS.API/Program.cs` — `AddNpgSql` replaced; Hangfire comment updated
- ✅ `HRMS.API/Controllers/Audit/LoginHistoryController.cs` — `ILike` replaced
- ✅ `HRMS.Infrastructure/Data/ApplicationDbContext.cs` — `jsonb` replaced; `UseXminAsConcurrencyToken` removed
- ✅ `HRMS.Infrastructure/Data/ReadReplicaDbContext.cs` — WAL comments removed
- ✅ `HRMS.Domain/Entities/Payroll/PayrollLock.cs` — `uint Version` → `byte[] RowVersion`
- ✅ `HRMS.Domain/Entities/Payroll/Payslip.cs` — `uint Version` → `byte[] RowVersion`
- ✅ `HRMS.API/HRMS.API.csproj` — `Npgsql`/`Hangfire.PostgreSql` packages removed
- ✅ `HRMS.Infrastructure/HRMS.Infrastructure.csproj` — `Npgsql` package removed
- ✅ `HRMS.Tests/HRMS.Tests.csproj` — MySQL packages added
- ✅ `docker-compose.yml` — `postgres` service replaced; volumes updated
- ✅ `docker-compose.override.yml` — `postgres` port replaced with `mysql`
- ✅ `docker-compose.backup.yml` — `pg_dump` replaced with `mysqldump`
- ✅ `docker-compose.replica.yml` — WAL replica content removed
- ✅ `scripts/db-init.sql` — PostgreSQL WAL init replaced with MySQL init
- ✅ `scripts/generate-secrets.sh` — `POSTGRES_*` → `MYSQL_*`
- ✅ `scripts/migrate.sh` — PostgreSQL references replaced
- ✅ `scripts/backup-s3.sh` — `pg-backup.sh` → `mysql-backup.sh`
- ✅ `scripts/test-restore.sh` — `psql`/`pg_restore` replaced with `mysql`
- ✅ `k8s/mysql-statefulset.yaml` — new MySQL manifest
- ✅ `k8s/backup-cronjob.yaml` — `pg_dump` → `mysqldump`
- ✅ `k8s/configmap.yaml` — `POSTGRES_*` → `MYSQL_*`
- ✅ `k8s/migrate-job.yaml` — `wait-for-postgres` → `wait-for-mysql`
- ✅ External Secrets Operator mappings — `POSTGRES_*` → `MYSQL_*`
- ✅ `.env.example` — `POSTGRES_*` → `MYSQL_*`
- ✅ `HRMS.Tests/DockerfileValidationTests.cs` — PostgreSQL extension assertions removed
- ✅ `HRMS.Tests/MySqlIntegrationTests.cs` — new MySQL integration tests

### Explicitly exempt files (EF Core migrations)

The 39 files in `HRMS.Infrastructure/Migrations/` contain PostgreSQL provider metadata
(`MigrationsHistoryRepository`, `DO $$ BEGIN...END $$`, `pg_constraint` references).
These are **intentionally exempt** from the zero-reference check per migration instructions.

### Historical files — preserved, not active

These files are **read-only reference documents** and explicitly excluded from the zero-reference check:

- `AUDIT_CHANGELOG.md`, `AUDIT_REPORT.md`, `BACKEND_AUDIT_REPORT.md` — historical audit reports
- `BUGFIX_CHANGELOG*.md` — historical fix logs
- `CHANGELOG.md`, `FINAL_*.md`, `IMPLEMENTATION_*.md` — historical implementation records
- `VERIFICATION_*.md`, `PRODUCTION_READINESS*.md` — historical verification reports
- `db_setup.sql`, `db_setup_additions.sql`, `db_crm.sql`, `db_performance.sql`, `db_recruitment.sql`, `bootstrap_only_db_setup.sql` — classified HISTORICAL with header comment; not used in MySQL deployment
- `postgres-archive/` — archived PostgreSQL WAL config; not used in MySQL deployment

### Files with PostgreSQL references for rollback reference only

- `k8s/postgres-statefulset.yaml.bak` — preserved for Kubernetes rollback; not applied to cluster
- `HRMS.Infrastructure/Migrations/` — 39 files, exempt from zero-reference check per instructions
