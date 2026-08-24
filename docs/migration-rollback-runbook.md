# Migration Rollback Runbook

**Fix L-03** — Documented rollback procedure for database migrations.

---

## Overview

HRMS uses Entity Framework Core 8 migrations applied by the `migrate` Docker Compose service (a one-shot job that runs `dotnet ef database update` and exits). This runbook describes how to safely roll back a migration in production.

---

## Pre-Migration Checklist (always do before deploying a new migration)

1. **Take a database backup.**

   ```bash
   # Manual backup (in addition to the automated nightly backup)
   docker compose exec mysql sh -c \
     'mysqldump -u "${MYSQL_USER}" -p"${MYSQL_PASSWORD}" "${MYSQL_DATABASE}"' \
     --format=custom \
     --file=/backups/pre-migration-$(date -u '+%Y%m%d_%H%M%S').dump
   ```

2. **Record the current migration name.**

   ```bash
   docker compose run --rm migrate \
     dotnet ef migrations list --context ApplicationDbContext
   # Note the name of the topmost [✓] migration — this is your rollback target.
   ```

3. **Test the migration on a staging environment first.**

4. **Schedule a maintenance window** if the migration locks tables or rewrites large amounts of data.

---

## Rolling Back a Failed Migration

### Step 1 — Stop the API service (prevent writes during rollback)

```bash
docker compose stop api
```

### Step 2 — Identify the target migration to roll back to

```bash
# List all applied migrations in order
docker compose run --rm migrate \
  dotnet ef migrations list --context ApplicationDbContext

# The output is ordered oldest → newest.
# Find the migration *before* the one you want to undo.
# Example output:
#   20240101_InitialCreate                [✓]
#   20240615_AddEmployeeFields            [✓]   ← roll back to HERE
#   20241001_AddBiometricDeviceTable      [✓]   ← undo this one
```

### Step 3 — Apply the down migration

Replace `<TargetMigration>` with the name of the migration you want to roll back **to** (i.e., the one before the broken one).

```bash
docker compose run --rm \
  -e ConnectionStrings__DefaultConnection="${DB_CONNECTION_STRING}" \
  migrate \
  dotnet ef database update <TargetMigration> --context ApplicationDbContext
```

> **Example:** To undo `20241001_AddBiometricDeviceTable`, roll back to `20240615_AddEmployeeFields`:
>
> ```bash
> docker compose run --rm migrate \
>   dotnet ef database update 20240615_AddEmployeeFields --context ApplicationDbContext
> ```

### Step 4 — Verify the schema

```bash
docker compose exec mysql mysql \
  -u "${MYSQL_USER}" -p"${MYSQL_PASSWORD}" \
  "${MYSQL_DATABASE}" \
  -c "\dt"   # List all tables — confirm the rolled-back table is gone
```

### Step 5 — Deploy the previous application version

Roll back the container image tag to the last known-good version:

```bash
# Update IMAGE_TAG in .env to the previous release tag, then:
docker compose pull api
docker compose up -d api
```

### Step 6 — Verify the application health

```bash
curl -sf https://yourdomain.com/health | jq .
# Expected: { "status": "Healthy", ... }
```

### Step 7 — Remove the broken migration from source control

```bash
# In your development environment (NOT in production):
dotnet ef migrations remove --context ApplicationDbContext
git add -A && git commit -m "chore: remove broken migration <MigrationName>"
```

---

## Rolling Back to a Point-in-Time Backup

If the down migration itself is broken or data corruption occurred:

```bash
# 1. Stop all services
docker compose stop api

# 2. Drop and recreate the database
docker compose exec mysql mysql -u "${MYSQL_USER}" -p"${MYSQL_PASSWORD}" \
  -e "DROP DATABASE IF EXISTS ${MYSQL_DATABASE}; CREATE DATABASE ${MYSQL_DATABASE} CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;"

# 3. Restore the pre-migration backup
docker compose exec mysql mysql \
  -u "${MYSQL_USER}" -p"${MYSQL_PASSWORD}" \
  "${MYSQL_DATABASE}" \
  --clean --if-exists \
  /backups/pre-migration-<TIMESTAMP>.dump

# 4. Deploy the previous application version (Step 5 above)
# 5. Verify health (Step 6 above)
```

> The daily encrypted backups at `/backups/hrms_*.sql.gz.enc` can be decrypted with:
>
> ```bash
> openssl enc -d -aes-256-cbc -pbkdf2 -iter 600000 \
>   -pass "pass:${BACKUP_ENCRYPTION_KEY}" \
>   -in /backups/hrms_<TIMESTAMP>.sql.gz.enc | gunzip > restored.sql
> mysql -u "${MYSQL_USER}" -p"${MYSQL_PASSWORD}" "${MYSQL_DATABASE}" < restored.sql
> ```

---

## Kubernetes / InitContainer Deployments

If running on Kubernetes (replace Docker Compose with a Helm chart and use the `migrate` initContainer pattern):

1. Roll back the Deployment to the previous revision:
   ```bash
   kubectl rollout undo deployment/hrms-api
   ```
2. The initContainer will not re-run on rollback. Manually apply the EF down migration via a Job:
   ```bash
   kubectl apply -f manifests/jobs/migrate-down.yaml
   kubectl wait --for=condition=complete job/hrms-migrate-down --timeout=120s
   ```

---

## Contacts and Escalation

| Role | Action |
|---|---|
| Lead Developer | Diagnose migration failure, author fix |
| DevOps / SRE | Execute this runbook in production |
| DBA (if applicable) | Validate schema integrity after rollback |

---

## Revision History

| Date | Author | Change |
|---|---|---|
| 2026-07-23 | Pre-production audit | Initial runbook created |
