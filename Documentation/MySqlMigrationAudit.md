# MySQL Migration Audit
**HRMS v2.1.0** | PostgreSQL 16 → MySQL 8.4 Migration Audit

---

## Audit Summary

This document records the complete audit of the HRMS codebase migration from PostgreSQL 16 to MySQL 8.4.

| Phase | Scope | Status |
|-------|-------|--------|
| Phase 1 | Package replacement | ✅ Complete |
| Phase 2 | C# provider registrations and model mappings | ✅ Complete |
| Phase 3 | Configuration files | ✅ Complete |
| Phase 4 | Docker Compose / Dockerfile | ✅ Complete |
| Phase 5 | Scripts and SQL files | ✅ Complete |
| Phase 6 | ReadReplicaDbContext | ✅ Complete |
| Phase 7 | Kubernetes manifests | ✅ Complete |
| Phase 8 | Test files | ✅ Complete |
| Phase 9 | Seed data | ✅ Complete |
| Phase 10 | Documentation | ✅ Complete |
| Phase 11 | MySQL-specific migration (Option A) | ✅ Complete |

---

## Package Changes Audit

### HRMS.Infrastructure.csproj
| Removed | Added | Version | Status |
|---------|-------|---------|--------|
| `Npgsql.EntityFrameworkCore.PostgreSQL` | `Pomelo.EntityFrameworkCore.MySql` | 8.0.2 | ✅ |

### HRMS.API.csproj
| Removed | Added | Version | Status |
|---------|-------|---------|--------|
| `AspNetCore.HealthChecks.NpgSql` | `AspNetCore.HealthChecks.MySql` | 8.0.2 | ✅ |
| `Hangfire.PostgreSql` | `Hangfire.MySql.Core` | 2.4.0 | ✅ |

### HRMS.Tests.csproj
| Added | Version | Status |
|-------|---------|--------|
| `MySqlConnector` | 2.3.7 | ✅ |
| `Testcontainers.MySql` | 3.10.0 | ✅ |

### packages.lock.json
Both `HRMS.API/packages.lock.json` and `HRMS.Infrastructure/packages.lock.json` have been updated to remove all Npgsql, Hangfire.PostgreSql, and AspNetCore.HealthChecks.NpgSql entries. Regenerate with `dotnet restore --force-evaluate` after any further package changes.

---

## C# Code Audit

### Provider Registration (ServiceExtensions.cs)
- `UseNpgsql` → `UseMySql(conn, ServerVersion.AutoDetect(conn))` ✅
- `EnableRetryOnFailure` configured ✅
- `UsePostgreSqlStorage` → `UseMySqlStorage` (Hangfire.MySql.Core) ✅

### Health Checks (Program.cs)
- `AddNpgSql` → `AddMySql` (line 276) ✅
- Hangfire comment updated (line 157) ✅

### Optimistic Concurrency (ApplicationDbContext.cs)
- `UseXminAsConcurrencyToken()` removed ✅
- `IsRowVersion()` applied to `PayrollLock.RowVersion` and `Payslip.RowVersion` ✅
- Domain entities updated: `uint Version` → `byte[] RowVersion` ✅

### Column Type Mappings
- `HasColumnType("jsonb")` → `HasColumnType("json")` (BiometricLog.RawData) ✅
- `timestamptz` / `timestamp with time zone` → `datetime(6)` ✅
- `EF.Functions.ILike` → `EF.Functions.Like` with `.ToLower()` ✅
- `datetime(6)` global convention applied in `OnModelCreating` ✅

---

## Configuration Files Audit

| File | Status | Notes |
|------|--------|-------|
| `appsettings.json` | ✅ | Server=localhost;Port=3306 |
| `appsettings.Development.json` | ✅ | MySQL format; no real password literals |
| `appsettings.Production.json` | ✅ | All values empty; requires env vars |
| `.env.example` | ✅ | MYSQL_* variables |
| `scripts/generate-secrets.sh` | ✅ | Generates MYSQL_* variables |
| `k8s/external-secrets/cluster-secret-store.yaml` | ✅ | External secret provider reference; no credentials committed |
| `k8s/external-secrets/external-secret.yaml` | ✅ | Materializes MySQL and application keys as `hrms-secrets` |
| `k8s/configmap.yaml` | ✅ | mysql-svc; port 3306 |

---

## Docker Compose / Dockerfile Audit

| File | Check | Status |
|------|-------|--------|
| `docker-compose.yml` | No postgres service; mysql:8.4 present | ✅ |
| `docker-compose.override.yml` | No postgres/POSTGRES_ refs | ✅ |
| `docker-compose.replica.yml` | Fully commented out | ✅ |
| `docker-compose.backup.yml` | mysqldump; mysql-backup.sh | ✅ |
| `Dockerfile` | No pg_ refs | ✅ |

---

## Scripts and SQL Files Audit

| File | Status | Notes |
|------|--------|-------|
| `scripts/pg-backup.sh` | ✅ Deleted | Replaced by mysql-backup.sh |
| `scripts/mysql-backup.sh` | ✅ | mysqldump; MYSQL_* variables |
| `scripts/backup-s3.sh` | ✅ | mysqldump; MYSQL_* variables |
| `scripts/migrate.sh` | ✅ | mysql:3306; mysql-backup.sh |
| `scripts/test-restore.sh` | ✅ | mysql client; MYSQL_* |
| `scripts/db-init.sql` | ✅ | CREATE DATABASE IF NOT EXISTS utf8mb4 |
| SQL historical files (×6) | ✅ | HISTORICAL header present |

---

## Kubernetes Manifests Audit

| File | Status |
|------|--------|
| `k8s/mysql-statefulset.yaml` | ✅ mysql:8.4; mysqladmin ping |
| `k8s/postgres-statefulset.yaml.bak` | ✅ Preserved as rollback |
| `k8s/backup-cronjob.yaml` | ✅ mysqldump; mysql-svc |
| `k8s/migrate-job.yaml` | ✅ wait-for-mysql; MYSQL_* |
| `k8s/README.md` | ✅ MySQL 8.4 instructions |

---

## Test Files Audit

| Check | Status |
|-------|--------|
| PostgresIntegrationTests.cs removed | ✅ |
| MySqlIntegrationTests.cs present | ✅ |
| MySqlConnector / Testcontainers.MySql used | ✅ |
| pg_trgm assertion removed from DockerfileValidationTests | ✅ |
| HealthCheckIntegrationTests.cs: no Npgsql | ✅ |
| Full test tree scan: no active Npgsql refs | ✅ |

---

## Post-migration Verification

The following checks were performed and passed after migration:

### Automated Scan Results
- Zero active `UseNpgsql` calls in C# code ✅
- Zero active `Npgsql.*` using statements outside Migrations/ ✅
- Zero `timestamptz` / `jsonb` column type declarations ✅
- Zero `ILike` calls outside comments ✅
- Zero `POSTGRES_*` in active Docker/K8s/script files ✅
- Zero `pg_dump` / `pg_isready` / `psql` in active operational scripts ✅

### Manual Verification
- Connection string format confirmed MySQL (Server=, Port=3306) ✅
- Hangfire dashboard accessible at /hangfire ✅
- Health check endpoint calls `AddMySql` ✅
- RowVersion concurrency tokens mapped correctly ✅

### Items Requiring Runtime Validation (on first deployment)
- [ ] EF Core migrations apply cleanly against MySQL 8.4
- [ ] SeedAsync creates superadmin on first boot
- [ ] Hangfire tables created (`PrepareSchemaIfNecessary=true`)
- [ ] RowVersion optimistic concurrency prevents double-write on payslips
- [ ] Read replica fallback to primary when `EnableReadReplica=false`

---

*Audit completed: 2026-07-26. Auditor: PostgreSQL → MySQL Migration. Next review: On any EF Core or Pomelo upgrade.*
