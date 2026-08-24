# STAGING ENVIRONMENT SETUP
**Project:** RatanHR HRMS  
**Version:** 2.0.0  
**Date:** 2026-08-01  
**Author:** Senior Staging-Validation Engineer  
**Status:** READY FOR CONTROLLED STAGING EXECUTION — NOT A COMPLETED SIGN-OFF

---

## Overview

This document describes how to provision an isolated staging environment for RatanHR HRMS. Staging uses its own MySQL database, Redis instance, JWT keys, and encryption keys. **It never shares credentials, data, or network resources with production.**

---

## Prerequisites

| Requirement | Version | Notes |
|---|---|---|
| Docker Engine | ≥ 24.0 | `docker --version` |
| Docker Compose | ≥ 2.20 | `docker compose version` |
| .NET SDK | 8.0.x | `dotnet --version` |
| MySQL client | 8.0 | For manual validation |
| Redis CLI | 7.x | For manual validation |
| Node.js | ≥ 18 LTS | For frontend build |
| pnpm / npm | Latest | Package manager |
| k6 | Latest | Smoke testing |

---

## Step 1 — Create the Staging Secrets File

Copy the template and fill in **staging-only** values. **Never reuse production credentials.**

```bash
cp Staging/staging.env.template Staging/.env.staging
chmod 600 Staging/.env.staging
```

Edit `Staging/.env.staging` — every placeholder must be replaced before starting the stack. Required entries:

- `STAGING_DB_PASSWORD` — unique strong password, not the production DB password
- `STAGING_REDIS_PASSWORD` — unique strong password
- `JWT_PRIVATE_KEY_PEM` — staging-only RSA private-key PEM (`openssl genrsa 2048`)
- `JWT_PUBLIC_KEY_PEM` — matching staging-only RSA public key
- `ENCRYPTION_KEY_STAGING` — 32 random bytes base64-encoded (`openssl rand -base64 32`)
- `SUPERADMIN_INITIAL_PASSWORD` — password for the seeded `superadmin@hrms.com` account on first start (MustChangePassword=true; you will be forced to change it on first login)

**SMTP is not required.** MailHog is bundled in the staging compose and captures all outbound mail locally — no external SMTP account or credentials needed. Leave all `SMTP_*` entries as their defaults.

> **SECURITY:** Never commit `Staging/.env.staging` to version control. It is listed in `.gitignore`.

---

## Step 2 — Start the Staging Stack

```bash
docker compose -f docker-compose.staging.yml --env-file .env.staging up -d
```

Expected containers:
- `hrms_staging_db` — MySQL 8.0 (port 127.0.0.1:3307)
- `hrms_staging_redis` — Redis 7 with password (port 127.0.0.1:6380)
- `hrms_staging_mailhog` — MailHog SMTP sink; web inbox at **http://127.0.0.1:8025** (port 127.0.0.1:1025 SMTP)
- `hrms_staging_api` — ASP.NET Core 8 API (port 127.0.0.1:8081)
- `hrms_staging_frontend` — Nginx serving the built frontend (port 127.0.0.1:3001)

Hangfire runs through the API's Redis-backed job storage; there is no separate Hangfire container.

---

## Step 3 — Run Migrations Against Staging Only

```bash
# From the HRMS.Infrastructure / HRMS.API project root
dotnet ef database update \
  --project HRMS.Infrastructure \
  --startup-project HRMS.API \
  --connection "Server=127.0.0.1;Port=3307;Database=hrms_staging;Uid=hrms_staging;Pwd=<STAGING_DB_PASSWORD>;"
```

> **IMPORTANT:** Port 3307 is the staging MySQL port (mapped in docker-compose.staging.yml).  
> Production MySQL runs on 3306. Confirm before running.

Verify migration was applied:

```bash
mysql -h 127.0.0.1 -P 3307 -u hrms_staging -p hrms_staging \
  -e "SELECT MigrationId, ProductVersion FROM __EFMigrationsHistory ORDER BY MigrationId;"
```

---

## Step 4 — Verify Health Checks

```bash
# API health
curl -s http://localhost:8081/healthz | jq .

# Redis health (via API health endpoint or direct)
redis-cli -h 127.0.0.1 -p 6380 -a <STAGING_REDIS_PASSWORD> PING

# MySQL health
mysqladmin -h 127.0.0.1 -P 3307 -u hrms_staging -p ping
```

Expected: `{"status":"Healthy"}` from API health endpoint.

---

## Step 5 — First Login and Test Account Setup

The API seed runs automatically on startup and creates exactly **one** account:

| Role | Email | Password |
|---|---|---|
| SuperAdmin | `superadmin@hrms.com` | Value of `SUPERADMIN_INITIAL_PASSWORD` (Replit Secret / `.env.staging`) |

> **MustChangePassword=true** — the API blocks all subsequent requests until you change the password on first login. Use the changed password for all smoke tests and k6 runs.

**Admin and Employee test accounts** are not auto-seeded; create them through the SuperAdmin portal after first login:

1. Log in as `superadmin@hrms.com` at `http://127.0.0.1:3001` (or `http://127.0.0.1:8081` directly)
2. Change the forced password when prompted — note the new password for smoke tests
3. Create a test company and branch via **Organization → Companies**
4. Create one Admin user via **Admin Users → New Admin**
5. Create one Employee via **Employees → New Employee**
6. Record the Admin and Employee emails and passwords (staging-only, never reuse in production)

Once these accounts exist, update `STAGING_SMOKE_PASSWORD` in `.env.staging` with the post-change SuperAdmin password, then proceed to Step 7.

---

## Step 6 — Build and Start Frontend

```bash
# The editable React/Vite frontend is in HRMS.SPA.Source.
cd HRMS.SPA.Source
bun install --frozen-lockfile
PORT=3001 BASE_PATH=/ NODE_ENV=production bun run build

# The staging compose file serves the packaged Nginx frontend in ../HRMS.SPA.
# If you rebuild the frontend, copy the generated dist/public contents into
# HRMS.SPA before starting the compose stack, preserving its nginx config:
rm -rf ../HRMS.SPA/assets
cp -R dist/public/* ../HRMS.SPA/
```

---

## Step 7 — Run Smoke Tests

See `STAGING_SMOKE_TEST_CHECKLIST.md` for the full checklist.

Before authenticated testing, validate the isolated stack configuration and
runtime endpoints:

```bash
bash scripts/validate-staging.sh --env-file Staging/.env.staging --start
```

The validator checks staging-only ports, required safety settings, API health,
MailHog, frontend loading, and cleanup. It does not claim authenticated
role, tenant-isolation, workflow, email-trigger, or Hangfire evidence.

Quick k6 smoke (run after Step 5 — requires the post-change SuperAdmin password):

```bash
k6 run \
  -e BASE_URL=http://localhost:8081 \
  -e ADMIN_EMAIL=superadmin@hrms.com \
  -e ADMIN_PASSWORD="$STAGING_SMOKE_PASSWORD" \
  k6/smoke-test.js
```

`STAGING_SMOKE_PASSWORD` is the SuperAdmin password you set during the forced change in Step 5. It is stored in `.env.staging` — never hard-coded here.

---

## Step 8 — Tear Down Staging

```bash
docker compose -f docker-compose.staging.yml down -v --remove-orphans
```

> The `-v` flag removes staging volumes. **Do not run this against the production compose file.**

---

## Configuration Reference

| Setting | Source | Notes |
|---|---|---|
| `ASPNETCORE_ENVIRONMENT` | `Staging` | Selects `appsettings.Staging.json` |
| MySQL port | 3307 | Avoids conflict with production (3306) |
| Redis port | 6380 | Avoids conflict with production (6379) |
| API port | 8081 | Avoids conflict with production (8080) |
| Frontend port | 3001 | Avoids conflict with production (3000) |
| MailHog SMTP port | 1025 | Internal Docker network; API → `hrms_staging_mailhog:1025` |
| MailHog web inbox | `http://127.0.0.1:8025` | View captured emails; no credentials required |
| Auto-migrate | DISABLED in staging | Run `dotnet ef database update` manually |
| SuperAdmin email | `superadmin@hrms.com` | Seeded on first startup; `SUPERADMIN_INITIAL_PASSWORD` sets initial password |
| Email | MailHog (bundled) | No external SMTP or credentials required; override with `SMTP_*` vars if needed |

---

## Security Checklist

- [ ] `Staging/.env.staging` is in `.gitignore` and not committed
- [ ] Staging MySQL password is unique (not reused from production)
- [ ] Staging Redis password is unique (not reused from production)
- [ ] Staging JWT RSA key pair is unique (not the production key pair)
- [ ] Staging encryption key is unique (32 bytes, base64)
- [ ] `SUPERADMIN_INITIAL_PASSWORD` is strong and unique; changed on first login
- [ ] No production data has been copied to staging
- [ ] Email is captured by MailHog — no real emails sent to real recipients
- [ ] CORS allowed origins are staging hostnames only (`http://localhost:3001`)
- [ ] All staging ports are bound to `127.0.0.1` (not `0.0.0.0`)
- [ ] Staging API is not publicly accessible (loopback / VPN only)
- [ ] `Biometric__EnableLiveSync=false` confirmed in compose env
- [ ] `Database__AutoMigrate=false` confirmed in compose env

---

## Troubleshooting

| Symptom | Cause | Fix |
|---|---|---|
| `Connection refused` on port 3307 | MySQL not started | `docker compose -f docker-compose.staging.yml ps` |
| Migration fails `Unknown database` | DB not created | Check `MYSQL_DATABASE=hrms_staging` in compose env |
| `401 Unauthorized` on health check | Wrong endpoint or missing forwarded HTTPS semantics | Use `/health`, `/healthz`, `/healthz/live`, or `/healthz/ready`; verify staging configuration |
| Emails not received | SMTP misconfigured | Open MailHog inbox at `http://127.0.0.1:8025`; verify `SMTP_HOST=hrms_staging_mailhog` and `SMTP_PORT=1025` in `.env.staging` |
| MailHog container not found | Image not pulled | `docker pull mailhog/mailhog:v1.0.1` |
| Redis `NOAUTH` error | Password not set | Verify `STAGING_REDIS_PASSWORD` matches redis config |
