# Upgrade Notes — v1.5.0 → v2.0.0
**HRMS**

---

## Prerequisites

- Docker Engine 24+ with Compose v2
- .NET 8.0.16 SDK (for development builds)
- Existing v1.5.0 deployment running and healthy

---

## Step-by-Step Upgrade

### 1. Backup First

```bash
# Backup production database
docker compose exec mysql sh -c \
  "mysqldump -u \"$MYSQL_USER\" -p\"$MYSQL_PASSWORD\" \"$MYSQL_DATABASE\"" | \
  gzip > backups/pre_v2_upgrade_$(date +%Y%m%d_%H%M%S).sql.gz

echo "Backup complete: $(ls -lh backups/pre_v2_*.sql.gz | tail -1)"
```

### 2. Pull New Code

```bash
git pull origin main
git log --oneline -5  # verify you have the v2.0.0 commit
```

### 3. Update Environment Variables

Add to your `.env` file:

```bash
# OpenTelemetry (optional — leave blank to disable)
OTEL_JAEGER_ENDPOINT=
OTEL_ZIPKIN_ENDPOINT=
OTEL_OTLP_ENDPOINT=

# Disable auto-migrate in production (migrations now handled by migrate service)
# Set this in .env — the docker-compose.yml already sets it for the api service
```

### 4. Run New Migrations

```bash
# The migrate service handles 20260719000001_AddPerformanceIndexes
docker compose run --rm migrate
```

Expected output:
```
Building 20260719000001_AddPerformanceIndexes...
Done. 14 indexes created.
Migration complete
```

### 5. Deploy New API

```bash
docker compose build api
docker compose up -d api nginx
```

### 6. SSL Setup (if not already configured)

If you're upgrading to use Let's Encrypt for the first time:

```bash
chmod +x nginx/init-letsencrypt.sh
DOMAIN=api.yourcompany.com EMAIL=admin@yourcompany.com ./nginx/init-letsencrypt.sh
```

If you already have SSL, add the Certbot service:
```bash
docker compose up -d certbot
```

### 7. Verify

```bash
# Health check
curl -s https://your-domain.com/health | python3 -m json.tool

# Verify Prometheus metrics
curl -s http://localhost:8080/metrics | grep "hrms_" | head -10

# Check correlation IDs are present in responses
curl -I https://your-domain.com/api/v1/auth/login 2>&1 | grep -i x-correlation

# Check logs include correlation IDs
tail -5 /var/lib/docker/volumes/hrms_logs/_data/hrms-$(date +%Y%m%d).log
```

---

## What Changed (Technical)

| Component | v1.5.0 | v2.0.0 |
|-----------|--------|--------|
| Migrations | Auto-migrate in API startup | Dedicated `migrate` container |
| Correlation IDs | Not present | Every request |
| Distributed traces | Not present | OpenTelemetry → Jaeger/OTLP |
| Metrics | Not present | Prometheus /metrics |
| Excel exports (large) | ClosedXML in-memory | OpenXmlWriter streaming |
| DB indexes | Basic only | +14 composite indexes |
| CI/CD | Not configured | GitHub Actions |
| SSL renewal | Manual | Certbot auto-renew |

---

## Rollback

If the upgrade fails, restore from the backup taken in Step 1:

```bash
# Stop API
docker compose stop api

# Restore database
gunzip -c backups/pre_v2_upgrade_*.sql.gz | \
  docker compose exec -T mysql mysql -u "$MYSQL_USER" -p"$MYSQL_PASSWORD" "$MYSQL_DATABASE"

# Roll back code
git checkout v1.5.0

# Restart
docker compose up -d api
```
