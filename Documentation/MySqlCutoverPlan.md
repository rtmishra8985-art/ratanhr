# MySQL Cutover Plan
**HRMS v2.1.0** | PostgreSQL → MySQL 8.4 Cutover Runbook

---

## Overview

This plan covers the steps required to cut over a production HRMS instance from PostgreSQL 16 to MySQL 8.4. It is a zero-downtime-preferred procedure with a defined rollback window.

**Estimated total cutover time:** 2–4 hours (depending on data volume)
**Rollback window:** 24 hours after cutover (PostgreSQL backup retained)
**Data migration tool:** `pgloader` or `mysqldump` + manual transform

---

## Prerequisites Checklist

Before beginning cutover:

- [ ] `dotnet restore` run; packages.lock.json contains no Npgsql entries
- [ ] All 18 verification sections PASS (run this audit document's checklist)
- [ ] MySQL 8.4 instance provisioned and accessible
- [ ] MySQL connection string validated: `Server=<host>;Port=3306;Database=hrms_db;...`
- [ ] `MYSQL_PASSWORD`, `MYSQL_ROOT_PASSWORD` generated via `scripts/generate-secrets.sh`
- [ ] PostgreSQL full backup taken and stored off-site
- [ ] Staging environment cutover tested successfully
- [ ] Team notified of maintenance window (minimum 2 hours notice)
- [ ] Rollback procedure reviewed by at least two engineers

---

## Phase 1 — Pre-Cutover (T-24h to T-1h)

### 1.1 Deploy MySQL-migrated codebase to staging

```bash
git checkout mysql-migration-branch
docker compose -f docker-compose.yml -f docker-compose.staging.yml up -d
docker compose run --rm migrate
```

Verify staging health:
```bash
curl https://staging.yourdomain.com/health
# Expected: {"status":"Healthy","checks":[{"name":"database","status":"Healthy"},...]}
```

### 1.2 Run data migration on staging

Use `pgloader` to migrate PostgreSQL data to MySQL:

```bash
pgloader postgresql://hrms:<POSTGRES_PASS>@old-db:5432/hrms_db \
         mysql://hrms:<MYSQL_PASS>@new-db:3306/hrms_db
```

Or use the incremental export/import approach:
```bash
# On PostgreSQL host:
pg_dump --data-only --format=plain hrms_db > hrms_data_$(date +%Y%m%d).sql

# Transform and import (requires schema-aware conversion):
python3 scripts/pg_to_mysql_data.py hrms_data_20260726.sql | \
  mysql -h new-db -u hrms -p hrms_db
```

### 1.3 Validate staging data integrity

```bash
# Row count comparison (run on both old PostgreSQL and new MySQL):
mysql -h new-db -u hrms -p hrms_db -e "
SELECT 'users' AS tbl, COUNT(*) AS cnt FROM users
UNION ALL SELECT 'employees', COUNT(*) FROM employees
UNION ALL SELECT 'payslips', COUNT(*) FROM payslips
UNION ALL SELECT 'audit_logs', COUNT(*) FROM audit_logs;"
```

Expected: row counts match PostgreSQL.

---

## Phase 2 — Cutover Window

### 2.1 Announce maintenance window

Notify users via status page and email. Set estimated duration: 2 hours.

### 2.2 Stop API traffic (T=0)

```bash
# Gracefully stop nginx to block new requests
docker compose stop nginx
# Wait for in-flight requests to complete
sleep 30
# Stop API
docker compose stop api
```

### 2.3 Take final PostgreSQL backup

```bash
docker compose exec postgres \
  sh -c "PGPASSWORD=$POSTGRES_PASSWORD pg_dump -U hrms hrms_db" \
  | gzip > backups/pre_cutover_final_$(date +%Y%m%d_%H%M%S).sql.gz
# Upload to off-site storage
aws s3 cp backups/pre_cutover_final_*.sql.gz s3://$S3_BUCKET/pre-cutover/
```

### 2.4 Run final incremental data migration

Migrate any data written after staging sync:

```bash
pgloader --with "batch size = 10000" \
  postgresql://hrms:<POSTGRES_PASS>@old-db:5432/hrms_db \
  mysql://hrms:<MYSQL_PASS>@new-db:3306/hrms_db
```

### 2.5 Deploy MySQL codebase

```bash
# Update .env to point to MySQL connection string
sed -i 's|DefaultConnection=.*|DefaultConnection=Server=new-db;Port=3306;Database=hrms_db;User ID=hrms;Password=<MYSQL_PASS>;AllowPublicKeyRetrieval=True;SslMode=Required|' .env

# Pull and deploy MySQL-migrated image
docker compose pull api
docker compose up -d api
```

### 2.6 Run EF Core migrations against MySQL

```bash
docker compose run --rm migrate
```

Expected output: `Applying migration '20260725000010_AddOnboardingStepsColumn'... Done.`

### 2.7 Verify health

```bash
curl http://localhost/health
# Expected: {"status":"Healthy","checks":[{"name":"database","status":"Healthy"},...]}
```

### 2.8 Smoke test

- [ ] Login with superadmin credentials
- [ ] Verify employee list loads
- [ ] Run a test payslip generation
- [ ] Verify Hangfire dashboard accessible at `/hangfire`
- [ ] Check audit log records new entries

### 2.9 Re-enable traffic

```bash
docker compose start nginx
```

---

## Phase 3 — Post-Cutover Validation (T+1h)

| Check | Command / Method | Expected |
|-------|-----------------|----------|
| API health | `curl /health` | `{"status":"Healthy"}` |
| Database health check | Health endpoint | `database: Healthy` |
| Hangfire jobs | `/hangfire` dashboard | No failed jobs |
| Login flow | Browser test | Successful authentication |
| Payroll lock concurrency | Test concurrent edit | `DbUpdateConcurrencyException` (expected) |
| Backup service | `docker compose logs backup` | No errors |

---

## Rollback Procedure

If cutover fails within the rollback window (24 hours):

### Step 1 — Stop MySQL-connected API

```bash
docker compose stop api nginx
```

### Step 2 — Restore .env to PostgreSQL connection string

```bash
# Restore old .env from backup
cp .env.postgresql.bak .env
```

### Step 3 — Deploy PostgreSQL-backed codebase

```bash
git checkout postgres-backup-branch
docker compose up -d api nginx
```

### Step 4 — Verify PostgreSQL connectivity

```bash
curl http://localhost/health
```

### Step 5 — Notify team and schedule post-mortem

---

## Sign-Off

| Role | Name | Approved | Date |
|------|------|----------|------|
| Engineering Lead | | ☐ | |
| DevOps Lead | | ☐ | |
| QA Lead | | ☐ | |
| Product Owner | | ☐ | |

---

*Cutover plan version: 2026-07-26. Review before each cutover attempt.*
