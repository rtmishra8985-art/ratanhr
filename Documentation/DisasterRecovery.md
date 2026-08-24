# Disaster Recovery Plan
**HRMS v2.1.0** | MySQL 8.4

---

## Declared RTO / RPO Targets

The audit noted that `docs/backup-restore.md` exists but its adequacy cannot be evaluated without RTO/RPO targets. This document declares those targets and validates that the current backup strategy meets them.

| Scenario | RPO (Max Data Loss) | RTO (Max Recovery Time) | Validated By |
|----------|--------------------|-----------------------|-------------|
| Single API container crash | 0 (no data loss) | < 30 seconds | Docker `restart: unless-stopped` policy |
| Single container crash (DB/Redis) | 0 (no data loss) | < 60 seconds | Docker restart + health check |
| Host server failure (data preserved on volumes) | ≤ 24 hours | ≤ 2 hours | Daily mysqldump; manual restore procedure below |
| Database corruption (detected at runtime) | ≤ 24 hours | ≤ 1 hour | Restore from last clean backup |
| Full server loss (hardware / cloud instance terminated) | ≤ 24 hours | ≤ 4 hours | Backup on separate storage + re-provision guide below |
| Multi-region disaster (entire data centre lost) | ≤ 24 hours | ≤ 8 hours | Off-site backup required (see Gap below) |

> **Current RPO Gap:** Daily backups give a worst-case RPO of 24 hours. For RPO < 1 hour, enable MySQL binary log replication (see Sprint 1 section).

---

## Backup Adequacy Assessment

| Requirement | Current State | Adequate for RTO/RPO? |
|-------------|--------------|----------------------|
| Automated daily backup | ✅ `mysqldump` at 02:00 UTC | ✅ Meets ≤ 24 h RPO |
| 14-day backup retention | ✅ `BACKUP_RETAIN_DAYS=14` | ✅ |
| File uploads backup | ⚠️ Manual only (volume tar) | ❌ Must automate in Sprint 1 |
| Off-site backup copy | ❌ Not configured | ❌ Must add S3/GCS/Azure Blob upload |
| Backup integrity test | ❌ Not scheduled | ❌ Must test restore monthly |
| MySQL replication | ❌ Not configured | ❌ Required for RPO < 1 hour |

---

## Failover Procedure: MySQL Primary Failure

### Detection

Prometheus alert: `mysql_up == 0` for > 2 minutes → PagerDuty / Alertmanager fires.

### Immediate Response (< 5 minutes)

```bash
# Step 1: Confirm MySQL is down
docker compose ps mysql
docker compose logs mysql --tail 50

# Step 2: Attempt restart (covers transient crash)
docker compose restart mysql

# Step 3: Verify health
docker compose exec mysql mysqladmin ping -u hrms -p"$MYSQL_PASSWORD"

# Step 4: If restart fails — check disk space (most common cause)
df -h
docker system df
```

### Full Restore (if restart fails)

```bash
# ⚠️  BEFORE YOU START: confirm BACKUP_ENCRYPTION_KEY is accessible from your
#     password manager / vault. Decryption cannot proceed without it.
#     export BACKUP_ENCRYPTION_KEY="$(vault kv get -field=key secret/hrms/backup)"

# Step 1: Stop the API to prevent partial writes
docker compose stop api

# Step 2: Identify the latest clean backup
ls -lt backups/hrms_*.sql.gz.enc | head -5

# Step 3: Destroy the corrupted volume and recreate
docker compose down mysql
docker volume rm hrms_mysqldata
docker compose up -d mysql

# Step 4: Wait for MySQL to be healthy (allow 10 seconds after startup before connecting)
sleep 10
docker compose exec mysql mysqladmin ping -u hrms -p"$MYSQL_PASSWORD" --wait=30

# Step 5: Decrypt and restore from backup
openssl enc -d -aes-256-cbc -pbkdf2 -iter 600000 \
  -pass "pass:$BACKUP_ENCRYPTION_KEY" \
  -in backups/hrms_YYYYMMDD_HHMMSS.sql.gz.enc \
  | gunzip \
  | docker compose exec -T mysql \
    mysql -u hrms -p"$MYSQL_PASSWORD" hrms_db

# Step 6: Run any pending EF Core migrations
#         Wait 10 s first — InnoDB needs a moment after a large restore before
#         accepting DDL statements (avoids "connection refused" on first attempt).
sleep 10
docker compose run --rm migrate

# Step 7: Restart the API
docker compose start api

# Step 8: Verify health check
curl https://your-domain.com/health

# Step 9: Spot-check data integrity
docker compose exec mysql mysql -u hrms -p"$MYSQL_PASSWORD" hrms_db -e \
  "SELECT COUNT(*) FROM employees; SELECT MAX(created_at) FROM audit_logs;"
```

**Estimated RTO: 45–60 minutes** (matches ≤ 1 hour target for database corruption scenario).

> **Drill finding (2026-07-22):** During the quarterly DR drill, two gaps were identified and corrected above:
> (1) `BACKUP_ENCRYPTION_KEY` must be retrieved from the password manager before starting — add this step to the pre-restore checklist.
> (2) A 10-second sleep is required after `docker compose up -d mysql` and after the SQL restore before running migrations — InnoDB needs time to finish startup/redo-log replay before accepting DDL connections.

---

## Full Server Loss — Re-Provisioning Procedure

### Prerequisites

- Backups stored off-site (Sprint 1: configure S3/GCS upload — see below)
- `.env.example` committed to repo (secrets NOT committed — see [SecretsRotationRunbook.md](SecretsRotationRunbook.md))
- Domain DNS A record updatable

### Re-Provisioning Steps

```bash
# Step 1: Provision new server (Ubuntu 22.04 LTS, minimum 4 vCPU / 8 GB RAM)
# Step 2: Install Docker Engine 24+
curl -fsSL https://get.docker.com | sh

# Step 3: Clone repo
git clone https://github.com/your-org/hrms.git && cd hrms

# Step 4: Download latest backup from off-site storage
# (S3 example)
aws s3 cp s3://your-bucket/hrms-backups/hrms_latest.sql.gz backups/

# Step 5: Create .env from template — fill in all secrets
cp .env.example .env && nano .env

# Step 6: Start infrastructure (DB, Redis, Nginx)
docker compose up -d mysql redis nginx certbot

# Step 7: Wait for MySQL to be healthy
sleep 30 && docker compose exec mysql mysqladmin ping -u hrms -p"$MYSQL_PASSWORD"

# Step 8: Restore from backup
gunzip -c backups/hrms_latest.sql.gz | \
  docker compose exec -T mysql mysql -u hrms -p"$MYSQL_PASSWORD" hrms_db

# Step 9: Run migrations
docker compose run --rm migrate

# Step 10: Start remaining services
docker compose up -d

# Step 11: Update DNS A record to new server IP
# Step 12: Wait for DNS propagation (TTL-dependent; set TTL to 60s before incident)
# Step 13: Verify SSL certificate
curl -v https://your-domain.com/health

# Step 14: Notify users of recovery
```

**Estimated RTO: 2–4 hours** (matches ≤ 4 hour target for full server loss).

---

## Backup Test Schedule

Untested backups are not backups. The following tests must be performed on a schedule:

| Test | Frequency | Procedure |
|------|-----------|----------|
| Backup file integrity | Weekly | `gunzip -t backups/hrms_latest.sql.gz` — exit code 0 = OK |
| Full restore to staging | Monthly | Restore latest backup to staging environment; verify app starts and health check passes |
| RTO drill | Quarterly | Full re-provisioning drill on a spare VM; measure actual time against targets |
| Off-site backup retrieval | Quarterly | Download backup from off-site storage and verify integrity |

```bash
# Weekly integrity check (add to cron: 0 6 * * 1)
#!/bin/bash
LATEST=$(ls -t /path/to/hrms/backups/hrms_*.sql.gz 2>/dev/null | head -1)
if [ -z "$LATEST" ]; then
  echo "ALERT: No backup file found" | mail -s "HRMS Backup MISSING" ops@your-company.com
  exit 1
fi
if ! gunzip -t "$LATEST" 2>/dev/null; then
  echo "ALERT: Backup file corrupted: $LATEST" | mail -s "HRMS Backup CORRUPT" ops@your-company.com
  exit 1
fi
AGE=$(( ($(date +%s) - $(stat -c %Y "$LATEST")) / 3600 ))
if [ "$AGE" -gt 26 ]; then
  echo "ALERT: Last backup is ${AGE}h old: $LATEST" | mail -s "HRMS Backup STALE" ops@your-company.com
  exit 1
fi
echo "OK: Backup healthy — $LATEST (${AGE}h ago, $(du -sh $LATEST | cut -f1))"
```

---

## Sprint 1: Improvements to Close RPO Gap

### Off-Site Backup Upload

```bash
# Add to the backup Docker service entrypoint after mysqldump completes:
aws s3 cp "/backups/$FILENAME" "s3://$BACKUP_BUCKET/hrms-backups/$FILENAME"
# OR for GCS:
gsutil cp "/backups/$FILENAME" "gs://$BACKUP_BUCKET/hrms-backups/$FILENAME"
```

Required env vars: `BACKUP_BUCKET`, `AWS_ACCESS_KEY_ID`, `AWS_SECRET_ACCESS_KEY` (or GCP service account).

### MySQL Binary Log Replication (RPO < 1 Hour)

```yaml
# docker-compose.yml addition:
mysql-replica:
  image: mysql:8.4
  environment:
    MYSQL_ROOT_PASSWORD: ${MYSQL_ROOT_PASSWORD}
  command: >
    bash -c "mysqladmin -h mysql --wait=30 ping &&
             mysql -h mysql -u root -p${MYSQL_ROOT_PASSWORD} -e
             'CHANGE REPLICATION SOURCE TO SOURCE_HOST=\"mysql\",
              SOURCE_USER=\"replicator\",
              SOURCE_PASSWORD=\"${REPLICATION_PASSWORD}\",
              SOURCE_AUTO_POSITION=1; START REPLICA;'"
```

With binary log replication, RPO drops to < 1 second in steady state. RTO for promotion to replica is < 5 minutes (manual failover).

---

## Communication Plan During Outage

| Elapsed Time | Action | Owner |
|-------------|--------|-------|
| 0–5 min | Confirm outage; begin triage | On-call engineer |
| 5–15 min | Notify internal stakeholders via Slack #incidents | Engineering Manager |
| 15 min | Tenant notification if outage affects users | Customer Success |
| 30 min | Status page update | DevOps |
| Recovery | All-clear notification; post-mortem scheduled | Engineering Manager |
| 5 business days | Post-mortem document shared | Engineering Manager |

---

## Roles and Contacts

| Role | Responsibility |
|------|---------------|
| On-Call Engineer | First responder; owns Steps 1–4 in any scenario |
| DevOps Lead | Owns infrastructure re-provisioning; holds off-site backup credentials |
| Engineering Manager | Stakeholder communication; post-mortem facilitation |
| Database Administrator | Called for complex data corruption scenarios |

Configure `ON_CALL_EMAIL` and `ON_CALL_PHONE` in `.env` for Alertmanager notifications.

---

*DR plan approved: 2026-07-26. RTO/RPO targets reviewed: 2026-07-26.*
*DR drill completed: 2026-07-22 — measured RTO 47 min 12 s (≤ 60 min target). See `DRDrillReport.md`.*
*Next drill due: 2026-10-22. Frequency: quarterly.*
