# Migration Runbook
**HRMS v2.0.0**

---

## Overview

Database migrations are managed with **EF Core** and run via a dedicated one-shot
`migrate` container (see `docker-compose.yml`).  The API container only starts after
the `migrate` container exits with code 0.

---

## Applying Migrations (Forward)

```bash
# Production — run the one-shot migrate container:
docker compose run --rm migrate

# Development — apply directly:
cd HRMS.API
dotnet ef database update \
  --project ../HRMS.Infrastructure/HRMS.Infrastructure.csproj \
  --startup-project .
```

---

## Rolling Back a Migration

> **Always take a backup before rolling back.** See `Documentation/BackupGuide.md`.

### Step 1 — identify the target migration

```bash
# List all applied migrations (most recent last):
dotnet ef migrations list \
  --project HRMS.Infrastructure/HRMS.Infrastructure.csproj \
  --startup-project HRMS.API
```

### Step 2 — roll back to the previous migration

```bash
# Replace <PreviousMigrationName> with the migration name from Step 1.
dotnet ef database update <PreviousMigrationName> \
  --project HRMS.Infrastructure/HRMS.Infrastructure.csproj \
  --startup-project HRMS.API
```

Example — roll back the last migration:

```bash
# Get the second-to-last migration name:
dotnet ef migrations list ... | tail -2 | head -1
# Then apply it as the target above.
```

### Step 3 — remove the bad migration file (if it was never in production)

```bash
dotnet ef migrations remove \
  --project HRMS.Infrastructure/HRMS.Infrastructure.csproj \
  --startup-project HRMS.API
```

### Step 4 — restart the API

```bash
docker compose up -d api
```

---

## Creating a New Migration

```bash
cd HRMS.API
dotnet ef migrations add <MigrationName> \
  --project ../HRMS.Infrastructure/HRMS.Infrastructure.csproj \
  --startup-project .
```

**Review the generated Up/Down methods before committing.**
Every migration must have a correct `Down()` method so rollback is possible.

---

## CI/CD Integration

The pipeline should:
1. Run `docker compose run --rm migrate` as a pre-deploy step.
2. Gate deployment on exit code 0.
3. On failure, trigger the rollback steps above and page on-call.

---

## Production Runbook Checklist

- [ ] Take an encrypted backup (`docker compose run --rm backup`) before any migration.
- [ ] Test the migration in staging first.
- [ ] Check that `Down()` is implemented and correct.
- [ ] Schedule a maintenance window for long-running `ALTER TABLE` operations on large tables.
- [ ] Monitor `/healthz/ready` after deployment — it includes the DB health check.
