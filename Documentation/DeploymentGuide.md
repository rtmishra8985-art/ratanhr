# Deployment Guide
**RatanHR HRMS v2.1.0** | Docker Compose on Linux | MySQL 8.4

> **Phase 2 updates:** ALLOWED_HOSTS configuration standardised; Hangfire Redis
> requirement enforced; SPA Docker stage name corrected to `spa-builder`.

---

## Prerequisites

- Docker Engine 24+ with Compose v2
- A domain name with an A record pointing to your server
- Ports 80 and 443 open in your firewall

---

## 1. Clone & Configure

```bash
git clone https://github.com/your-org/hrms.git
cd hrms
cp .env.production.template .env
```

Edit `.env` — **all required values must be set before first run**:

```bash
# MySQL
MYSQL_DATABASE=hrms_db
MYSQL_USER=hrms
MYSQL_PASSWORD=<strong-random-password>
MYSQL_ROOT_PASSWORD=<strong-root-password>

# JWT (generate: ./scripts/generate-rsa-keys.sh)
JWT_PRIVATE_KEY_PEM=<RSA-2048 private key PEM>
JWT_PUBLIC_KEY_PEM=<RSA-2048 public key PEM>

# PII encryption (generate: openssl rand -base64 32)
ENCRYPTION_KEY=<44-char-base64-string>

# Domain — used by nginx server_name and public URL construction
DOMAIN_NAME=api.yourcompany.com
APP_BASE_URL=https://api.yourcompany.com

# *** ALLOWED_HOSTS — ASP.NET Core host-header allowlist ***
# Semicolon-separated list of permitted Host header values.
# Must NOT be *, empty, or contain placeholders or example domains.
# Startup will be REJECTED if this is missing, empty, *, or a placeholder.
# Example:
ALLOWED_HOSTS=api.yourcompany.com;yourcompany.com

# Redis (Hangfire + distributed rate-limiting)
REDIS_PASSWORD=<strong-random-password>

# Email (optional but recommended)
EMAIL_HOST=smtp.sendgrid.net
EMAIL_PORT=587
EMAIL_USERNAME=apikey
EMAIL_PASSWORD=<sendgrid-api-key>
EMAIL_FROM_ADDRESS=noreply@yourcompany.com

# Observability (optional)
SEQ_URL=
OTEL_JAEGER_ENDPOINT=
OTEL_OTLP_ENDPOINT=
```

**ALLOWED_HOSTS vs DOMAIN_NAME** — these serve different purposes:

| Variable | Used by | Format |
|---|---|---|
| `DOMAIN_NAME` | nginx `server_name`, `APP_BASE_URL` | Single domain, no protocol |
| `ALLOWED_HOSTS` | ASP.NET Core `AllowedHosts` host-filtering middleware | Semicolon-separated list |

Do not reuse `DOMAIN_NAME` as `ALLOWED_HOSTS` in scripts; set both explicitly.

---

## 2. First-Time SSL Setup

Run this **once** to obtain a Let's Encrypt certificate:

```bash
chmod +x nginx/init-letsencrypt.sh
DOMAIN=api.yourcompany.com EMAIL=admin@yourcompany.com ./nginx/init-letsencrypt.sh
```

For staging/testing (avoids rate limits):
```bash
DOMAIN=api.yourcompany.com EMAIL=admin@yourcompany.com STAGING=1 ./nginx/init-letsencrypt.sh
```

---

## 3. Start the Stack

```bash
# Build images with build metadata
export BUILD_TIMESTAMP="$(date -u +%Y%m%dT%H%M%SZ)"
export GIT_SHA="$(git rev-parse HEAD)"

# Bring up the full stack — migrations run automatically before the API starts
docker compose -f docker-compose.prod.yml up -d

# Check service status
docker compose -f docker-compose.prod.yml ps

# Follow API logs
docker compose -f docker-compose.prod.yml logs -f api

# Check health
curl https://api.yourcompany.com/api/health
```

---

## 4. Database Setup

Database setup is **fully automated**. The `migrate` service (defined in
`docker-compose.prod.yml`) runs automatically before the `api` service starts and
applies in this order:

1. EF Core migrations (canonical path: `HRMS.Infrastructure/Migrations/MySql/` only)

That is the only step. As of 2026-08-11 the previous supplementary SQL files
(`db_performance.sql`, `db_indexes_fix.sql`, `db_softdelete_fix.sql`) are folded
into the migration chain (`20260811080000_FoldDbScriptIndexes`) and deleted.

**Operators do not run any SQL files manually.** Simply run `docker compose up`.

To run migrations manually (e.g., after a failed deployment):
```bash
docker compose -f docker-compose.prod.yml run --rm migrate
```

---

## 5. Hangfire Background Jobs

Hangfire is configured to use **Redis** in all non-Development environments. In-memory
storage is not available in Production; the application will refuse to start if Redis
is not reachable or `Hangfire:RedisConnectionString` is not configured.

```bash
# Verify Hangfire is connected to Redis
docker compose -f docker-compose.prod.yml exec redis \
  redis-cli -a ${REDIS_PASSWORD} KEYS "hangfire:*" | head -20
# Expected: hangfire:* keys present

# View Hangfire dashboard
open https://api.yourcompany.com/hangfire
```

---

## 6. Updates & Rolling Deployment

```bash
git pull
export BUILD_TIMESTAMP="$(date -u +%Y%m%dT%H%M%SZ)"
export GIT_SHA="$(git rev-parse HEAD)"

docker compose -f docker-compose.prod.yml build api
docker compose -f docker-compose.prod.yml run --rm migrate   # apply new migrations
docker compose -f docker-compose.prod.yml up -d --no-deps api
```

---

## 7. Health Verification

After deployment, verify:

```bash
source .env

# API health
curl "https://${DOMAIN_NAME}/api/health" | python3 -m json.tool

# AllowedHosts guard (must return 400)
curl -o /dev/null -w "%{http_code}" \
  -H "Host: evil.attacker.example.com" \
  "http://localhost/api/health"

# Hangfire Redis keys
docker compose -f docker-compose.prod.yml exec redis \
  redis-cli -a "${REDIS_PASSWORD}" KEYS "hangfire:*"
```

Expected `/api/health` response:
```json
{
  "status": "Healthy",
  "checks": [
    {"name": "database", "status": "Healthy"},
    {"name": "redis",    "status": "Healthy"},
    {"name": "email",    "status": "Healthy"}
  ]
}
```

---

## 8. Backup & Restore

Backups run daily at 02:00 UTC via the `backup` service:

```bash
# Manual backup
docker compose -f docker-compose.prod.yml exec mysql \
  sh -c "mysqldump -u hrms -p'$MYSQL_PASSWORD' hrms_db" \
  | gzip > backups/manual_$(date +%Y%m%d).sql.gz

# Restore
gunzip -c backups/hrms_20260719_020000.sql.gz | \
  docker compose -f docker-compose.prod.yml exec -T mysql \
  mysql -u"$MYSQL_USER" -p"$MYSQL_PASSWORD" hrms_db
```

---

## 9. Certificate Renewal

Certificates are renewed automatically by the `certbot` service.

To verify renewal works:
```bash
docker compose -f docker-compose.prod.yml run --rm certbot certbot renew --dry-run
```

---

## 10. Troubleshooting

| Issue | Command |
|-------|---------|
| API won't start | `docker compose -f docker-compose.prod.yml logs migrate` — migration errors? |
| API startup: "ALLOWED_HOSTS missing" | Set `ALLOWED_HOSTS` in `.env` (semicolon-separated, no wildcard) |
| API startup: "Hangfire cannot connect to Redis" | Check `REDIS_PASSWORD` and that the `redis` service is healthy |
| 502 Bad Gateway | `docker compose -f docker-compose.prod.yml logs api` — port binding? |
| Database connection failed | `docker compose -f docker-compose.prod.yml logs mysql` — healthcheck? |
| SSL not working | `docker compose -f docker-compose.prod.yml logs certbot` — ACME challenge? |
| Hangfire jobs not running | Check `KEYS hangfire:*` in redis-cli; verify `Hangfire:UseRedis=true` |

---

## Docker stage name reference

| Stage | Purpose |
|---|---|
| `spa-builder` | Node/Vite SPA build (produces `/spa/dist/`) |
| `build` | .NET publish |
| `migrate` | EF Core + supplementary SQL runner |
| `runtime` | Final ASP.NET Core runtime image |

Use `--target spa-builder` (not `--target spa-build`) when extracting SPA assets manually.
