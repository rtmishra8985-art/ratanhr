# Database Migration Guide
**HRMS v2.0.0** | Entity Framework Core 8

---

## Migration Strategy

### Development
`Database__AutoMigrate=true` (default) — the API automatically runs `EnsureCreated` / `Migrate` on startup.

### Production (Safe)
Migrations are handled by the **dedicated `migrate` init-container** in `docker-compose.yml`:

```yaml
migrate:
  build:
    context: .
    target: migrate
  depends_on:
    mysql:
      condition: service_healthy
  restart: "no"  # Runs exactly once
```

The `api` service sets `Database__AutoMigrate=false` and waits for `migrate` to exit cleanly before starting.

**Why this matters**: If you deploy 3 replicas simultaneously, all 3 will try to migrate in parallel → race condition → partial migration → data corruption. One `migrate` container prevents this.

---

## Creating a New Migration

```bash
# In development:
cd HRMS.Infrastructure
dotnet ef migrations add MigrationName \
  --startup-project ../HRMS.API \
  --project .

# Verify the generated migration
# Check Up() and Down() are correct before committing
```

### Migration Naming Convention

| Pattern | Example |
|---------|---------|
| `YYYYMMDD_NNN_Description` | `20260719000001_AddPerformanceIndexes` |

---

## Applying Migrations

```bash
# Development (automatic on startup — or manual):
dotnet ef database update \
  --project HRMS.Infrastructure \
  --startup-project HRMS.API

# Production (via migrate container):
docker compose run --rm migrate

# Check pending migrations:
dotnet ef migrations list \
  --project HRMS.Infrastructure \
  --startup-project HRMS.API
```

---

## Rollback

EF Core does not support automatic rollback in PostgreSQL. To revert:

```bash
# Roll back to a specific migration
dotnet ef database update PreviousMigrationName \
  --project HRMS.Infrastructure \
  --startup-project HRMS.API

# Remove the latest migration (if not yet applied to DB)
dotnet ef migrations remove \
  --project HRMS.Infrastructure \
  --startup-project HRMS.API
```

---

## Migration Checklist

Before committing a migration:
- [ ] Review `Up()` — is every change intentional?
- [ ] Review `Down()` — can it be rolled back safely?
- [ ] Add indexes for new foreign keys and common filter columns
- [ ] Test on a copy of production data
- [ ] Verify `docker compose run --rm migrate` exits with code 0

---

## Current Migrations

| Migration | Date | Description |
|-----------|------|-------------|
| `20240101000000_InitialCreate` | 2024-01-01 | Base schema |
| `20240601000000_AddExpandedStructure` | 2024-06-01 | Extended entities |
| `20260711141438_AddSecurityAndLeaveManagement` | 2026-07-11 | Security + leave |
| `20260715000001_AddAuditLog` | 2026-07-15 | Audit logging |
| `20260717000001_AddUserProfilePicture` | 2026-07-17 | Profile pictures |
| `20260718000001_AddNewFeatures` | 2026-07-18 | Feature additions |
| `20260718200000_AddPayrollLockAndAttendanceReason` | 2026-07-18 | Payroll locking |
| `20260719000001_AddPerformanceIndexes` | 2026-07-19 | Composite indexes |
