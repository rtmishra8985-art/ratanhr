# HRMS Backup and Restore Runbook

> **Audience:** System administrators and on-call engineers.  
> **Updated:** 2026-07-23  
> **Relates to:** `docker-compose.yml` `backup` service, `.env` `BACKUP_ENCRYPTION_KEY`

---

## 1. Overview

The HRMS stack runs an automated daily backup of the PostgreSQL database using the `backup` Docker service. Backups are:

- **Encrypted** with AES-256-CBC (OpenSSL, PBKDF2, 600 000 iterations).
- **Compressed** with gzip before encryption.
- **Stored** in `./backups/` on the host as `hrms_<YYYYMMDD_HHmmss>.sql.gz.enc`.
- **Pruned** automatically after `BACKUP_RETAIN_DAYS` days (default 14).

The default schedule is **02:00 UTC daily** (configurable via `BACKUP_CRON_SCHEDULE` in `.env`).

---

## 2. Prerequisites

| Tool | Version | Notes |
|------|---------|-------|
| `openssl` | ≥ 1.1.1 | Available in the mysql:8.4 image and most Linux distros |
| `mysql` / `mysqldump` | 8.4 | Must match the server major version |
| `gunzip` | any | Part of gzip package |
| `BACKUP_ENCRYPTION_KEY` | — | Must match the key used when the backup was created |

---

## 3. Verifying Backup Health

### Check backup service logs

```bash
docker compose logs backup --tail=50
```

Look for lines like:
```
2026-07-23 02:00:01 Encrypted backup written: /backups/hrms_20260723_020001.sql.gz.enc (4.2M)
2026-07-23 02:00:01 Old backups pruned (retention: 14 days).
```

### List available backup files

```bash
ls -lh ./backups/hrms_*.sql.gz.enc
```

### Verify a backup can be decrypted (integrity check — no restore)

```bash
# Set the key from your .env (never echo it to terminal history)
read -s BACKUP_ENCRYPTION_KEY
export BACKUP_ENCRYPTION_KEY

BACKUP_FILE="./backups/hrms_20260723_020001.sql.gz.enc"

openssl enc -d -aes-256-cbc -pbkdf2 -iter 600000 \
  -pass "env:BACKUP_ENCRYPTION_KEY" \
  -in "$BACKUP_FILE" | gunzip | head -5
```

If you see SQL output (e.g. `--` comment lines or `SET` statements), the backup is valid.

---

## 4. Manual Backup (on demand)

Run a one-off backup without waiting for the cron schedule:

```bash
docker compose exec backup /bin/sh /backup.sh
```

Or from the host (requires `mysqldump` and `openssl`):

```bash
read -s BACKUP_ENCRYPTION_KEY && export BACKUP_ENCRYPTION_KEY
BASENAME="./backups/hrms_manual_$(date -u '+%Y%m%d_%H%M%S')"

mysqldump -h localhost -u "$MYSQL_USER" -p"$MYSQL_PASSWORD" "$MYSQL_DATABASE" \
  | gzip \
  | openssl enc -aes-256-cbc -pbkdf2 -iter 600000 \
      -pass "env:BACKUP_ENCRYPTION_KEY" \
      -out "${BASENAME}.sql.gz.enc"

echo "Manual backup written: ${BASENAME}.sql.gz.enc"
```

---

## 5. Restore Procedure

> ⚠️ **CAUTION:** Restoring overwrites all data in the target database. Perform restores on a maintenance window or into a separate recovery database first.

### Step 1 — Stop the API to prevent writes during restore

```bash
docker compose stop api
```

### Step 2 — Decrypt and decompress the backup

```bash
read -s BACKUP_ENCRYPTION_KEY && export BACKUP_ENCRYPTION_KEY

BACKUP_FILE="./backups/hrms_20260723_020001.sql.gz.enc"
RESTORE_SQL="/tmp/hrms_restore_$(date +%s).sql"

openssl enc -d -aes-256-cbc -pbkdf2 -iter 600000 \
  -pass "env:BACKUP_ENCRYPTION_KEY" \
  -in "$BACKUP_FILE" | gunzip > "$RESTORE_SQL"

echo "Decrypted to $RESTORE_SQL ($(du -sh $RESTORE_SQL | cut -f1))"
```

### Step 3 — Drop and recreate the database (full restore)

> Skip this step for a partial restore (see §6).

```bash
# Connect to MySQL — drop and recreate the database
docker compose exec mysql mysql -u "$MYSQL_USER" -p"$MYSQL_PASSWORD" -e "
  DROP DATABASE IF EXISTS $MYSQL_DATABASE;
  CREATE DATABASE $MYSQL_DATABASE CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
"
```

### Step 4 — Restore from SQL dump

```bash
docker compose exec -T mysql mysql \
  -u "$MYSQL_USER" -p"$MYSQL_PASSWORD" \
  "$MYSQL_DATABASE" \
  < "$RESTORE_SQL"

echo "Restore complete."
```

### Step 5 — Run pending migrations (if restoring an older backup)

```bash
docker compose run --rm migrate
```

### Step 6 — Restart the API

```bash
docker compose start api
docker compose logs api --tail=30
```

### Step 7 — Verify

```bash
# Check health endpoint
curl -s http://localhost/health | jq .

# Spot-check row counts
docker compose exec mysql mysql -u "$MYSQL_USER" -p"$MYSQL_PASSWORD" "$MYSQL_DATABASE" -e "
  SELECT 'users' AS tbl, COUNT(*) FROM users
  UNION ALL SELECT 'employees', COUNT(*) FROM employees
  UNION ALL SELECT 'payslips', COUNT(*) FROM payslips;
"
```

---

## 6. Partial Restore (single table)

To restore a single table without touching the rest of the database:

```bash
# Extract the table's INSERT statements from the SQL dump
grep -A 999999 "COPY public.employees" "$RESTORE_SQL" \
  | grep -B 999999 "^\\\." \
  | docker compose exec -T mysql mysql -u "$MYSQL_USER" -p"$MYSQL_PASSWORD" "$MYSQL_DATABASE"
```

Or use `mysql <` to restore from a mysqldump file (see §7 for large-table options).

---

## 7. Retention and Key Rotation

### Retention

The `backup` service prunes files older than `BACKUP_RETAIN_DAYS` (default 14) automatically. To change:

```bash
# .env
BACKUP_RETAIN_DAYS=30
docker compose up -d backup
```

### Offsite copy (recommended)

Copy backups to S3/GCS/Azure Blob regularly. Example with `aws s3 cp`:

```bash
aws s3 cp ./backups/ s3://your-bucket/hrms-backups/ \
  --recursive --exclude "*" --include "*.sql.gz.enc"
```

### Key rotation

1. Generate a new key: `openssl rand -base64 48`
2. Update `BACKUP_ENCRYPTION_KEY` in `.env`.
3. Restart the backup service: `docker compose up -d backup`.
4. **Important:** Old backups encrypted with the previous key cannot be decrypted with the new key. Keep the old key in a secure vault until all old backups have expired.

---

## 8. Disaster Recovery Checklist

- [ ] Confirm backup file exists and is non-zero.
- [ ] Confirm `BACKUP_ENCRYPTION_KEY` is available (from vault / secrets manager).
- [ ] Verify decryption succeeds (`head -5` test).
- [ ] Stop the API container.
- [ ] Restore to a staging database first if possible.
- [ ] Run migrations after restore.
- [ ] Verify health endpoint and spot-check row counts.
- [ ] Update incident log with restore time and data loss window.
