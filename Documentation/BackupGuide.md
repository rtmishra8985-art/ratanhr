# Backup Guide
**HRMS v2.1.0** | MySQL 8.4

---

## Automated Backups (Encrypted)

The `backup` Docker service runs `mysqldump` daily at **02:00 UTC** and **encrypts the
output with AES-256-CBC (PBKDF2, 600 000 iterations)** before writing to disk.

- Backups are stored in `./backups/` on the host as `.sql.gz.enc` files
- Retention: 14 days (configurable via `BACKUP_RETAIN_DAYS` in `.env`)
- File naming: `hrms_YYYYMMDD_HHMMSS.sql.gz.enc`
- Encryption key: set `BACKUP_ENCRYPTION_KEY` in `.env` (generate with `openssl rand -base64 48`)

> **Important:** rotate `BACKUP_ENCRYPTION_KEY` quarterly. Keep the key in your
> password manager — without it, encrypted backups cannot be decrypted.

---

## What Is Backed Up

| Data | Included | Notes |
|------|----------|-------|
| MySQL data | ✅ | Full encrypted dump via `mysqldump` |
| Redis data | ❌ | Cache only — not required |
| File uploads | ⚠️ | Not in mysqldump — back up `hrms_uploads` volume separately (see below) |
| Application logs | ❌ | Ephemeral — not required |

---

## Manual Backup

```bash
# Trigger an immediate encrypted backup:
docker compose exec mysql \
  sh -c "mysqldump -u hrms -p'$MYSQL_PASSWORD' --single-transaction hrms_db" \
  | gzip \
  | openssl enc -aes-256-cbc -pbkdf2 -iter 600000 \
      -pass "pass:$BACKUP_ENCRYPTION_KEY" \
      -out "backups/hrms_manual_$(date -u +%Y%m%d_%H%M%S).sql.gz.enc"
```

---

## Restore

### Step 1 — decrypt and decompress

```bash
openssl enc -d -aes-256-cbc -pbkdf2 -iter 600000 \
  -pass "pass:$BACKUP_ENCRYPTION_KEY" \
  -in backups/hrms_20260722_020000.sql.gz.enc \
  | gunzip > /tmp/hrms_restored.sql
```

### Step 2 — restore into MySQL

```bash
# Stop the API to prevent writes during restore:
docker compose stop api

# Restore:
docker compose exec -T mysql \
  mysql -u hrms -p"$MYSQL_PASSWORD" hrms_db < /tmp/hrms_restored.sql

# Restart API:
docker compose start api

# Verify:
curl https://your-domain.com/healthz/ready
```

---

## File Uploads Backup

Employee documents and photos are stored in the `hrms_uploads` Docker volume:

```bash
# Backup uploads volume:
docker run --rm \
  -v hrms_uploads:/data \
  -v $(pwd)/backups:/backup \
  alpine tar czf /backup/uploads_$(date -u +%Y%m%d).tar.gz /data

# Restore uploads volume:
docker run --rm \
  -v hrms_uploads:/data \
  -v $(pwd)/backups:/backup \
  alpine tar xzf /backup/uploads_20260722.tar.gz -C /
```

---

## Backup Monitoring

A Prometheus alert fires if the `backup` container exits with a non-zero code.
See `monitoring/alertmanager.yml` for notification routing.

To verify the last backup ran successfully:

```bash
ls -lht backups/*.sql.gz.enc | head -5
```

---

---

## Off-Site Backup (S3 / S3-Compatible)

The `offsite-backup` Docker service uploads the latest local `.sql.gz.enc` backup to an S3
bucket after every local backup run. It is disabled by default and enabled via a Docker Compose
profile so it never runs accidentally without credentials.

### Prerequisites

Set the following variables in `.env` before starting the service:

```bash
S3_BUCKET=hrms-backups-prod             # bucket name
S3_PREFIX=hrms/mysql                     # key prefix (no trailing slash)
AWS_ACCESS_KEY_ID=AKIAxxx               # IAM or Backblaze/MinIO application key ID
AWS_SECRET_ACCESS_KEY=xxx               # IAM secret or application key
AWS_DEFAULT_REGION=ap-south-1           # AWS region (use "auto" for Backblaze/Cloudflare R2)
S3_RETAIN_DAYS=90                        # days to keep remote backups (default 90)

# For Backblaze B2, MinIO, or Cloudflare R2 (omit for AWS S3):
# AWS_ENDPOINT_URL=https://s3.us-west-001.backblazeb2.com
```

### Start the Off-Site Backup Service

```bash
docker compose --profile offsite up -d offsite-backup
```

### Verify

```bash
# Confirm the first upload succeeded:
docker compose logs offsite-backup | grep "✅ Upload verified"

# List remote backups:
aws s3 ls s3://hrms-backups-prod/hrms/mysql/

# Check pruning is working (objects older than S3_RETAIN_DAYS should be absent):
docker compose logs offsite-backup | grep "🗑️"
```

The service uploads once at startup (after a 60-second delay to allow the local backup
service to finish its first run) and then once every 24 hours thereafter.

---

## Key Rotation

When rotating `BACKUP_ENCRYPTION_KEY`:

1. Decrypt all existing backups with the old key (see Restore step 1).
2. Re-encrypt with the new key (see Manual Backup above).
3. Update `BACKUP_ENCRYPTION_KEY` in `.env`.
4. Restart the backup service: `docker compose up -d backup`.
5. Run a manual backup to verify the new key works end-to-end.
