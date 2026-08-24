# MySQL Migration Guide

**Phase 11: Documentation for PostgreSQL → MySQL 8.4 migration.**

---

## Overview

HRMS has been migrated from PostgreSQL 16 to MySQL 8.4. This guide covers:
- What changed and why
- How to run new EF Core migrations
- UTC datetime strategy
- MySQL replication setup (future)
- Troubleshooting

---

## Package Changes

| Removed | Added | Notes |
|---------|-------|-------|
| `Npgsql.EntityFrameworkCore.PostgreSQL 8.0.8` | `Pomelo.EntityFrameworkCore.MySql 8.0.2` | EF Core provider |
| `AspNetCore.HealthChecks.NpgSql 8.0.1` | `AspNetCore.HealthChecks.MySql 8.0.2` | Health check |
| `Hangfire.PostgreSql 1.9.13` | `Hangfire.MySql.Core 2.4.0` | Background jobs storage |
| (Tests) n/a | `MySqlConnector 2.3.7` | Integration tests |
| (Tests) n/a | `Testcontainers.MySql 3.10.0` | Testcontainers MySQL support |

**Hangfire package choice:** `Hangfire.MySql.Core 2.4.0` was selected over `Hangfire.Storage.MySql`
because it provides a direct `UseMySqlStorage()` API compatible with Hangfire 1.8.x, has minimal
external dependencies, and is widely used in production EF Core 8 stacks.

---

## Connection String Format

**MySQL format** (used throughout the migrated codebase):

```
Server=localhost;Port=3306;Database=hrms_db;User ID=hrms;Password=<PASS>;AllowPublicKeyRetrieval=True;SslMode=Required
```

Key differences from PostgreSQL:
- `Server=` instead of `Host=`
- `Port=3306` instead of `Port=5432`
- `User ID=` instead of `Username=`
- `AllowPublicKeyRetrieval=True;SslMode=Required` — MySQL 8.4's default server configuration
  auto-generates a self-signed CA/cert pair on first start, so `SslMode=Required` works out of
  the box for local development too: the client uses TLS but does not validate the server's
  certificate chain. `AllowPublicKeyRetrieval=True` lets the client fetch the server's RSA
  public key over that TLS channel for `caching_sha2_password` auth. Do **not** downgrade to
  `SslMode=None`, even locally — it disables TLS entirely and sends credentials in clear text.

---

## Optimistic Concurrency (RowVersion)

PostgreSQL used `UseXminAsConcurrencyToken()` which backed concurrency on the system `xmin` column.
MySQL does not have an equivalent. Both `PayrollLock` and `Payslip` have been updated:

**Domain entities:**
```csharp
// Before (PostgreSQL)
public uint Version { get; set; }

// After (MySQL)
public byte[] RowVersion { get; set; } = Array.Empty<byte>();
```

**EF Core configuration in `ApplicationDbContext.cs`:**
```csharp
// Before
e.UseXminAsConcurrencyToken();

// After
e.Property(x => x.RowVersion).HasColumnName("row_version").IsRowVersion();
```

Pomelo maps `IsRowVersion()` to a `TIMESTAMP(6)` column with `DEFAULT CURRENT_TIMESTAMP(6) ON UPDATE CURRENT_TIMESTAMP(6)`, providing the same optimistic concurrency semantics.

---

## JSON Column Type

| PostgreSQL | MySQL |
|-----------|-------|
| `HasColumnType("jsonb")` | `HasColumnType("json")` |

Affected: `BiometricLog.RawData` (line 1245 in `ApplicationDbContext.cs`).
MySQL `JSON` type supports JSON validation and path expressions. Binary indexing (equivalent to `jsonb`) is not needed for the HRMS use case.

---

## Case-Insensitive Search (ILike Replacement)

PostgreSQL's `EF.Functions.ILike()` was replaced with:

```csharp
// Before
EF.Functions.ILike(a.PerformedByName, $"%{email}%")

// After
EF.Functions.Like(a.PerformedByName.ToLower(), $"%{emailLower}%")
```

The `hrms_db` database uses `utf8mb4_unicode_ci` collation which is case-insensitive by default, so the `.ToLower()` is belt-and-suspenders. MySQL `LIKE` with this collation already performs case-insensitive matching.

---

## UTC DateTime Strategy

All timestamp columns use `datetime(6)` with UTC storage. Existing EF Core `DateTime` properties continue to work; ensure application code always uses `DateTime.UtcNow` (not `DateTime.Now`).

For `DateTimeOffset` columns, the following conversion is applied:
```csharp
.HasConversion(
    v => v.ToUniversalTime(),
    v => DateTime.SpecifyKind(v, DateTimeKind.Utc))
```

---

## Read Replica

PostgreSQL WAL streaming replication has been removed. MySQL replication options:

1. **MySQL Group Replication** — synchronous, high availability cluster (recommended for production)
2. **Standard async replication** — binary log based, master/replica setup

To enable, configure replication at the infrastructure level, then:
```json
// appsettings.json or environment variable
"Database": {
  "ReplicaConnection": "Server=mysql-replica;Port=3306;Database=hrms_db;User ID=hrms;Password=<PASS>;...",
  "EnableReadReplica": true
}
```

The `ReadReplicaDbContext` safely falls back to the primary when `EnableReadReplica=false`.

---

## Hangfire Storage

```csharp
// Before (PostgreSQL)
.UsePostgreSqlStorage(connectionString, new PostgreSqlStorageOptions { ... })

// After (MySQL)
.UseMySqlStorage(connectionString, new MySqlStorageOptions
{
    TablesPrefix = "Hangfire_",
    PrepareSchemaIfNecessary = true,
    QueuePollInterval = TimeSpan.FromSeconds(5),
    InvisibilityTimeout = TimeSpan.FromMinutes(30),
    DistributedLockTimeout = TimeSpan.FromMinutes(1),
    TransactionIsolationLevel = IsolationLevel.ReadCommitted,
    UseTransactions = true
})
```

Hangfire creates its tables automatically on first run (`PrepareSchemaIfNecessary = true`).

---

## EF Core Migrations

The existing 39 EF Core migrations in `HRMS.Infrastructure/Migrations/` use PostgreSQL provider
metadata and are **preserved but intentionally not modified**. They are exempt from the
zero-PostgreSQL-reference check.

To generate new MySQL migrations after this migration:

```bash
# Ensure MySQL is running
# Set ASPNETCORE_ENVIRONMENT=Development and connection string

dotnet ef migrations add <MigrationName> \
  --project HRMS.Infrastructure \
  --startup-project HRMS.API

dotnet ef database update \
  --project HRMS.Infrastructure \
  --startup-project HRMS.API
```

For production, use the Docker migrate service or Kubernetes migrate Job.

---

## Docker Compose

```bash
# Start the full stack
docker compose up -d

# Run migrations only
docker compose run --rm migrate

# Local dev (exposes MySQL port 3306)
docker compose up -d  # docker-compose.override.yml merged automatically
```

---

## Kubernetes

```bash
# Apply MySQL StatefulSet (replaces postgres-statefulset.yaml)
kubectl apply -f k8s/mysql-statefulset.yaml
kubectl apply -f k8s/configmap.yaml

# Configure the external secret provider first. The generated hrms-secrets
# Kubernetes Secret replaces the removed checked-in secrets template.
kubectl apply -f k8s/external-secrets/cluster-secret-store.yaml
kubectl apply -f k8s/external-secrets/external-secret.yaml
kubectl wait --for=condition=ready externalsecret/hrms-external-secret \
  -n hrms --timeout=120s

# Run migration job
kubectl apply -f k8s/migrate-job.yaml
kubectl wait --for=condition=complete job/hrms-migrate -n hrms --timeout=120s

# Apply backup CronJob
kubectl apply -f k8s/backup-cronjob.yaml
```

The `postgres-statefulset.yaml.bak` file is preserved as rollback reference.

---

## Troubleshooting

| Symptom | Cause | Fix |
|---------|-------|-----|
| `Authentication failed` at startup | `MYSQL_PASSWORD` not set | Set in `.env` |
| `Table 'hrms_db.hangfire_*' doesn't exist` | Hangfire schema not created | `PrepareSchemaIfNecessary=true` is set; check connection string |
| `DbUpdateConcurrencyException` on payroll | Concurrent payroll run | Expected — retry after conflict |
| `Unable to connect to MySQL server` | Container not healthy | Check `docker compose ps` and logs |
| `Data too long for column 'row_version'` | RowVersion column type mismatch | Re-run migrations; ensure `IsRowVersion()` mapping is applied |
