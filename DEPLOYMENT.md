# RatanHR HRMS — Production Deployment Guide

**Stack:** .NET 8 API · React SPA (Vite/bun) · MySQL 8.4 · Redis 7.4 · Nginx 1.27  
**Compose file:** `docker-compose.prod.yml`  
**Last updated:** 2026-08-04

---

## Quick reference

```bash
# ── One-time server setup (do once, then never again) ─────────────────────
bash <(curl -fsSL https://get.docker.com)          # install Docker
sudo usermod -aG docker $USER && newgrp docker     # add user to docker group
sudo apt-get install -y certbot                    # TLS cert tool
curl -fsSL https://bun.sh/install | bash           # SPA build tool
source ~/.bashrc

# ── First deploy ──────────────────────────────────────────────────────────
git clone https://github.com/YOUR_ORG/ratanhr.git /opt/ratanhr
cd /opt/ratanhr
bash scripts/generate-secrets.sh    # creates .env with all random secrets
nano .env                           # fill DOMAIN_NAME, EMAIL_*, DPO_EMAIL, etc.
sudo certbot certonly --standalone --non-interactive --agree-tos \
  --email admin@YOUR_DOMAIN --domains YOUR_DOMAIN
bash deploy.sh                      # builds, migrates, starts, verifies

# ── Every subsequent update ───────────────────────────────────────────────
bash deploy.sh                      # pulls, builds, migrates, restarts

# ── Emergency rollback ────────────────────────────────────────────────────
bash rollback.sh                    # reverts API to previous image

# ── Health check ─────────────────────────────────────────────────────────
curl -fsSL https://YOUR_DOMAIN/health | python3 -m json.tool
curl -fsSL https://YOUR_DOMAIN/api/healthz | python3 -m json.tool
docker compose -f docker-compose.prod.yml ps
```

---

## Prerequisites

| Tool | Minimum version | Purpose |
|------|----------------|---------|
| Docker Engine | 24.x | Container runtime |
| Docker Compose plugin | 2.24.x | Stack management |
| bun | 1.3+ | SPA build |
| certbot | any | TLS certificate provisioning |
| git | any | Source checkout |
| openssl | any | Key/secret generation |

---

## Step 1 — Provision the server

Minimum spec: **2 vCPUs, 4 GB RAM, 40 GB disk** (Ubuntu 22.04 LTS recommended).

```bash
# 1a. Install Docker Engine
curl -fsSL https://get.docker.com | bash
sudo usermod -aG docker $USER
newgrp docker                     # activate group without re-login

# Verify
docker --version                  # Docker version 24.x or later
docker compose version            # Docker Compose version v2.24.x or later

# 1b. Install Certbot
sudo apt-get update
sudo apt-get install -y certbot

# 1c. Install bun (SPA build tool)
curl -fsSL https://bun.sh/install | bash
source ~/.bashrc

# Verify
bun --version                     # 1.3.x or later

# 1d. Open firewall ports
sudo ufw allow 22    # SSH
sudo ufw allow 80    # HTTP (needed for ACME challenge + redirect)
sudo ufw allow 443   # HTTPS
sudo ufw deny 3306   # MySQL — must NOT be reachable from internet
sudo ufw deny 6379   # Redis — must NOT be reachable from internet
sudo ufw enable
```

---

## Step 2 — DNS setup

Create an **A record** pointing your domain to the server's public IP:

```
app.yourcompany.com.   A   <server-public-ip>
```

**Verify propagation** before continuing:
```bash
dig +short app.yourcompany.com      # should return your server IP
```

> **Tip:** Set TTL to 60 seconds the day before go-live, increase to 3600 afterward.

---

## Step 3 — Clone the repository and generate secrets

```bash
git clone https://github.com/YOUR_ORG/ratanhr.git /opt/ratanhr
cd /opt/ratanhr

# Generate all secrets in one step
bash scripts/generate-secrets.sh
# This creates .env with random passwords, RSA-2048 JWT keys, and AES-256 encryption key.

chmod 600 .env        # owner-only read/write — enforce before anything else

# Edit .env — fill every remaining <REQUIRED> value
nano .env
```

**Values to fill manually** (everything else is auto-generated):

| Variable | How to obtain |
|---|---|
| `DOMAIN_NAME` | Your DNS hostname, e.g. `app.yourcompany.com` |
| `EMAIL_HOST` | SMTP host, e.g. `smtp.sendgrid.net` |
| `EMAIL_PORT` | `587` (STARTTLS) or `465` (SSL) |
| `EMAIL_USERNAME` | SMTP username / API key |
| `EMAIL_PASSWORD` | SMTP password / API key |
| `EMAIL_FROM_ADDRESS` | e.g. `noreply@yourcompany.com` |
| `APP_COMPANY_NAME` | Your company name shown in emails and UI |
| `APP_SUPPORT_EMAIL` | Support contact shown in error messages |
| `DPO_EMAIL` | Data Protection Officer email (DPDP/GDPR compliance) |
| `SUPERADMIN_INITIAL_PASSWORD` | Strong temporary password; **change on first login** |
| `BACKUP_ENCRYPTION_KEY` | `openssl rand -base64 48` — encrypts database backups |

**Verify no placeholders remain:**
```bash
grep "<REQUIRED>" .env && echo "STOP — fill these before deploying" || echo "All values filled ✓"
```

---

## Step 4 — Obtain a TLS certificate (Let's Encrypt)

Run **before** starting the stack (port 80 must be free):

```bash
sudo certbot certonly \
  --standalone \
  --non-interactive \
  --agree-tos \
  --email YOUR_ADMIN_EMAIL \
  --domains YOUR_DOMAIN_NAME

# Verify the certificate was issued
sudo ls /etc/letsencrypt/live/YOUR_DOMAIN_NAME/
# Expected: cert.pem  chain.pem  fullchain.pem  privkey.pem
```

**Automatic renewal hook** — run once after first deploy so nginx reloads new certs:

```bash
sudo mkdir -p /etc/letsencrypt/renewal-hooks/post

sudo tee /etc/letsencrypt/renewal-hooks/post/hrms-nginx-reload.sh > /dev/null << 'EOF'
#!/bin/bash
cd /opt/ratanhr
docker compose -f docker-compose.prod.yml exec nginx nginx -s reload
EOF

sudo chmod +x /etc/letsencrypt/renewal-hooks/post/hrms-nginx-reload.sh

# Test renewal (dry run — no cert is actually changed)
sudo certbot renew --dry-run
```

---

## Step 5 — Deploy (one command)

```bash
cd /opt/ratanhr
bash deploy.sh
```

`deploy.sh` runs every step automatically:

| Step | What happens |
|------|-------------|
| Pre-flight | Checks .env, Docker, TLS cert, bun, SPA source |
| Snapshot | Tags current API image as `:previous` for rollback |
| Git pull | `git pull --ff-only` |
| SPA build | `bun install --frozen-lockfile && bun run build:ci` → `spa-dist/` |
| nginx patch | Bakes `DOMAIN_NAME` into `nginx/nginx.conf` |
| API build | `docker compose build api` with GIT_SHA + BUILD_TIMESTAMP labels |
| DB backup | Encrypted pre-migration snapshot via `scripts/mysql-backup.sh` |
| Migrations | `backfill` one-shot, then `migrate` one-shot (EF Core) |
| Stack up | `docker compose up -d --remove-orphans` |
| Health wait | Polls `/api/healthz` up to 120 seconds |
| Smoke tests | Verifies `/api/healthz`, `/api/healthz/live`, `/api/healthz/ready`, HTTPS |
| Image prune | Removes dangling images |
| **Result** | Prints **✅ DEPLOYED** or **❌ FAILED** with reason |

**Expected output on success:**
```
✅ DEPLOYED
  App URL:      https://app.yourcompany.com
  Health:       https://app.yourcompany.com/health
  API health:   https://app.yourcompany.com/api/healthz
  Git SHA:      a1b2c3d
  Build time:   2026-08-04T10:30:00Z
  Deploy log:   /opt/ratanhr/logs/deploy_20260804_103000.log
```

---

## Step 6 — Health verification

After deploy, run these checks:

```bash
# 1. Container status — all should show "running (healthy)"
docker compose -f docker-compose.prod.yml ps

# 2. API deep health (checks DB, Redis, and email connectivity)
curl -fsSL https://YOUR_DOMAIN/api/healthz | python3 -m json.tool
# Expected: {"status":"Healthy","components":{"database":"Healthy","redis":"Healthy","email":"Healthy"}}

# 3. Kubernetes-style liveness and readiness
curl -fsSL https://YOUR_DOMAIN/api/healthz/live   # → "Healthy"
curl -fsSL https://YOUR_DOMAIN/api/healthz/ready  # → "Healthy"

# 4. HTTPS + TLS
curl -v https://YOUR_DOMAIN/health 2>&1 | grep -E "subject|issuer|expire|HTTP/"

# 5. nginx logs (look for 5xx errors)
docker compose -f docker-compose.prod.yml logs nginx --tail=50

# 6. API logs
docker compose -f docker-compose.prod.yml logs api --tail=50

# 7. Check TLS certificate expiry
echo | openssl s_client -connect YOUR_DOMAIN:443 -servername YOUR_DOMAIN 2>/dev/null \
  | openssl x509 -noout -dates
```

**Post-deploy checklist:**
- [ ] Log in as SuperAdmin and change the initial password immediately
- [ ] Confirm email: trigger `POST /api/auth/forgot-password` and check inbox
- [ ] Confirm Hangfire dashboard at `https://YOUR_DOMAIN/hangfire` (SuperAdmin login required)
- [ ] Set `BACKUP_ENCRYPTION_KEY` in `.env` and test backup: `bash scripts/mysql-backup.sh`
- [ ] Add backup cron: `0 2 * * * cd /opt/ratanhr && bash scripts/mysql-backup.sh >> logs/backup.log 2>&1`
- [ ] Confirm certificate auto-renewal: `sudo certbot renew --dry-run`

---

## Step 7 — Updating (redeploying)

Every update uses the same command:

```bash
cd /opt/ratanhr
bash deploy.sh
```

`deploy.sh` automatically:
- Snapshots the current image for rollback
- Pulls latest code
- Rebuilds the SPA and API image
- Takes a pre-migration database backup
- Applies any new EF Core migrations
- Does a zero-downtime restart (nginx stays up throughout)
- Verifies health before marking the deploy complete

---

## Step 8 — Rollback to previous version

If a deploy breaks the app:

```bash
cd /opt/ratanhr
bash rollback.sh
```

`rollback.sh`:
1. Locates the `:previous` image snapshot
2. Asks for confirmation before proceeding
3. Stops the current API container
4. Restores the previous image
5. Starts the API
6. Verifies health
7. Prints **✅ ROLLED BACK** or **❌ ROLLBACK FAILED**

> **⚠ Database note:** `rollback.sh` does NOT roll back the database.
> If the failed deploy included a destructive migration, restore from the backup in
> `backups/` **before** running rollback. See §9 for the DB restore procedure.

---

## Step 9 — Database backup and restore

**Manual backup:**
```bash
cd /opt/ratanhr
BACKUP_ENCRYPTION_KEY="$(grep BACKUP_ENCRYPTION_KEY .env | cut -d= -f2-)" \
  bash scripts/mysql-backup.sh
# Backup written to: backups/hrms_YYYYMMDD_HHMMSS.sql.gz.enc
```

**Automated daily backup (add to crontab):**
```bash
crontab -e
# Add:
0 2 * * * cd /opt/ratanhr && bash scripts/mysql-backup.sh >> logs/backup.log 2>&1
```

**Restore from backup:**
```bash
# 1. Stop the API (to prevent writes during restore)
docker compose -f docker-compose.prod.yml stop api

# 2. Decrypt and decompress the backup
BACKUP_ENCRYPTION_KEY="$(grep BACKUP_ENCRYPTION_KEY .env | cut -d= -f2-)"
openssl enc -d -aes-256-cbc -pbkdf2 -iter 600000 \
  -pass pass:"$BACKUP_ENCRYPTION_KEY" \
  -in backups/hrms_YYYYMMDD_HHMMSS.sql.gz.enc \
  | gunzip > /tmp/hrms_restored.sql

# 3. Drop and recreate the database
source .env
docker compose -f docker-compose.prod.yml exec -T mysql \
  mysql -uroot -p"${MYSQL_ROOT_PASSWORD}" \
  -e "DROP DATABASE IF EXISTS ${MYSQL_DATABASE:-hrms_db}; CREATE DATABASE ${MYSQL_DATABASE:-hrms_db} CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;"

# 4. Restore the SQL dump
docker compose -f docker-compose.prod.yml exec -T mysql \
  mysql -u"${MYSQL_USER:-hrms}" -p"${MYSQL_PASSWORD}" "${MYSQL_DATABASE:-hrms_db}" \
  < /tmp/hrms_restored.sql

# 5. Restart the API
docker compose -f docker-compose.prod.yml start api

# 6. Verify
curl -fsSL https://YOUR_DOMAIN/api/healthz | python3 -m json.tool
```

---

## Step 10 — Monitoring and logs

```bash
# Live log stream (all services)
docker compose -f docker-compose.prod.yml logs -f

# API only
docker compose -f docker-compose.prod.yml logs -f api

# nginx access log
docker compose -f docker-compose.prod.yml logs -f nginx

# Container resource usage
docker stats

# Disk usage by volumes
docker system df -v | grep hrms
```

**Structured logs** — the API emits JSON logs. Parse with `jq`:
```bash
docker compose -f docker-compose.prod.yml logs api --no-log-prefix \
  | jq 'select(.Level == "Error" or .Level == "Warning")'
```

---

## Step 11 — Scaling and performance tuning

```bash
# Scale API horizontally (requires a load balancer in front of nginx)
docker compose -f docker-compose.prod.yml up -d --scale api=3

# MySQL tuning — edit mysql/my.cnf (then restart MySQL):
# [mysqld]
# innodb_buffer_pool_size = 512M    # 50–70% of available RAM
# innodb_log_file_size = 128M
# slow_query_log = ON
# long_query_time = 1

# Redis memory limit (set in docker-compose.prod.yml → redis.command):
# --maxmemory 512mb --maxmemory-policy allkeys-lru
```

---

## Appendix A — docker-compose.prod.yml review

**Production-readiness audit result:** ✅ PASS

| Check | Result | Note |
|---|---|---|
| No debug ports exposed to host | ✅ | MySQL 3306, Redis 6379: `expose` only (internal) |
| All secrets from env vars | ✅ | `:?` guards fail fast on missing vars |
| Health checks on all services | ✅ | mysql, redis, api, nginx — all configured |
| Restart policies | ✅ | `unless-stopped` on all persistent services |
| One-shot init containers | ✅ | `backfill` and `migrate` run and exit; no restart |
| Digest-pinned base images | ✅ | `@sha256:...` on mysql, redis, aspnet runtime |
| Non-root runtime user | ✅ | `USER hrms` in Dockerfile runtime stage |
| Resource limits | ✅ | CPU + memory limits on all services |
| Graceful shutdown (SIGTERM) | ✅ | `stop_grace_period: 30s`, `DOTNET_SHUTDOWNTIMEOUTSECONDS=25` |
| No dev-only services | ✅ | No MailHog, Jaeger, Prometheus, Grafana in prod |
| Deterministic internal subnet | ✅ | `172.18.0.0/16` (matches `KNOWN_PROXY_CIDRS`) |
| TLS termination in nginx | ✅ | TLS 1.2/1.3, Mozilla Intermediate ciphers, HSTS 2yr |

---

## Appendix B — Useful maintenance commands

```bash
# Shell into the API container
docker compose -f docker-compose.prod.yml exec api /bin/sh

# Shell into MySQL
source .env
docker compose -f docker-compose.prod.yml exec mysql \
  mysql -u"${MYSQL_USER}" -p"${MYSQL_PASSWORD}" "${MYSQL_DATABASE:-hrms_db}"

# Redis CLI
source .env
docker compose -f docker-compose.prod.yml exec redis \
  redis-cli -a "${REDIS_PASSWORD}"

# Check pending EF Core migrations
docker compose -f docker-compose.prod.yml exec api \
  dotnet ef migrations list --no-build 2>/dev/null || \
  docker compose -f docker-compose.prod.yml run --rm migrate \
    bash -c "dotnet tool run dotnet-ef migrations list --project ../HRMS.Infrastructure/HRMS.Infrastructure.csproj --startup-project . --no-build"

# Follow all logs
docker compose -f docker-compose.prod.yml logs -f

# Check disk usage of volumes
docker system df -v | grep hrms

# Prune dangling images after a deploy
docker image prune -f

# Reload nginx (e.g. after TLS cert renewal)
docker compose -f docker-compose.prod.yml exec nginx nginx -s reload

# Restart a single service without downtime
docker compose -f docker-compose.prod.yml restart api
```

---

## Appendix C — Certificate renewal test

```bash
# Dry-run renewal (does not contact ACME servers)
sudo certbot renew --dry-run

# Manual renewal (if auto-renewal fails)
sudo certbot renew --force-renewal

# After any manual renewal, reload nginx
docker compose -f docker-compose.prod.yml exec nginx nginx -s reload

# Verify the new cert is live
echo | openssl s_client -connect YOUR_DOMAIN:443 2>/dev/null \
  | openssl x509 -noout -dates
```

---

## Appendix D — GitHub Actions CI/CD (optional)

To trigger `bash deploy.sh` automatically on push to `main`, add to
`.github/workflows/deploy.yml`:

```yaml
name: Deploy to Production
on:
  push:
    branches: [main]

jobs:
  deploy:
    runs-on: ubuntu-latest
    steps:
      - name: Deploy via SSH
        uses: appleboy/ssh-action@v1.0.3
        with:
          host: ${{ secrets.PROD_HOST }}
          username: ${{ secrets.PROD_USER }}
          key: ${{ secrets.PROD_SSH_KEY }}
          script: |
            cd /opt/ratanhr
            bash deploy.sh
```

Store `PROD_HOST`, `PROD_USER`, and `PROD_SSH_KEY` in GitHub → Settings → Secrets and variables → Actions.

---

*Last updated: 2026-08-04 — deploy.sh + rollback.sh added; full commands verified*
