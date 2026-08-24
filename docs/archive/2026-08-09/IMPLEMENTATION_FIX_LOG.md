# IMPLEMENTATION FIX LOG — RatanHR HRMS
**Date:** 2026-08-02  
**Engineer:** Implementation, Staging-Validation & Release Engineer

---

## Fix 1 — DEFECT-STAGING-DEPS-01: Staging compose `service_started` → `service_healthy`

**Classification:** Deployment/configuration defect  
**File:** `Staging/docker-compose.staging.yml`

### Root Cause
The API service's `depends_on` block used `condition: service_started` for all three dependencies (`hrms_staging_db`, `hrms_staging_redis`, `hrms_staging_mailhog`). `service_started` only waits for the container process to launch — it does not wait for the service inside to be accepting connections. All three services define `healthcheck` blocks that were never consulted during startup sequencing, causing the API to attempt connections before services were ready.

### Change
```diff
-      hrms_staging_db:
-        condition: service_started
-      hrms_staging_redis:
-        condition: service_started
-      hrms_staging_mailhog:
-        condition: service_started
+      hrms_staging_db:
+        condition: service_healthy
+      hrms_staging_redis:
+        condition: service_healthy
+      hrms_staging_mailhog:
+        condition: service_healthy
```

### Security Impact
None. Reliability improvement only.

---

## Fix 2 — DEFECT-STAGING-MIGRATE-01: Missing migrate service in staging compose

**Classification:** Deployment/configuration defect  
**File:** `Staging/docker-compose.staging.yml`

### Root Cause
The staging compose had no mechanism to run EF Core database migrations before the API started. The API has `Database:AutoMigrate=false` (correct — auto-migration is disabled). Without a migrate service, the API would start against an empty MySQL schema, causing immediate startup failures on every table access. The production `docker-compose.yml` correctly has a dedicated `migrate` service; the staging compose was missing it entirely.

### Change
Added `hrms_staging_migrate` service that:
- Builds from the `migrate` Dockerfile target (runs `dotnet ef database update`)
- Depends on `hrms_staging_db: service_healthy`
- Has `restart: "no"` (runs exactly once)
- The API service now also waits on `hrms_staging_migrate: service_completed_successfully`

```yaml
  hrms_staging_migrate:
    build:
      context: ..
      dockerfile: Dockerfile
      target: migrate
    container_name: hrms_staging_migrate
    restart: "no"
    environment:
      ConnectionStrings__DefaultConnection: >-
        Server=hrms_staging_db;Port=3306;Database=${STAGING_DB_NAME:-hrms_staging};
        Uid=${STAGING_DB_USER:-hrms_staging};Pwd=${STAGING_DB_PASSWORD};CharSet=utf8mb4;
    depends_on:
      hrms_staging_db:
        condition: service_healthy
    networks:
      - hrms_staging_net
```

### Security Impact
None. Migrations run as the application DB user, not root.

---

## Fix 3 — DEFECT-ALERTMANAGER-01: Removed broken `noop` webhook placeholder

**Classification:** Deployment/configuration defect  
**File:** `monitoring/alertmanager.yml`

### Root Cause
Both `default-receiver` and `critical-receiver` had `webhook_configs` pointing to `http://localhost:1/noop` — a URL that will always fail. Every alert fired by Prometheus would fail to deliver. The Alertmanager would log connection refused errors on every firing alert, and no human would ever receive an alert notification. This rendered the entire monitoring alert pipeline non-functional.

### Change
- Replaced `webhook_configs: url: http://localhost:1/noop` on both receivers with `email_configs` driven by `ALERTMANAGER_EMAIL_TO` (default receiver) and `ALERTMANAGER_ONCALL_EMAIL` (critical receiver) environment variables
- Slack and PagerDuty blocks retained as commented templates
- Added descriptive `Subject` header templates for email notifications
- Email `to` addresses are read from env vars injected by docker-compose; DevOps sets real addresses in `.env`

### Security Impact
None. Alert destinations are env-var-driven; no credentials in config file.

---

## Fix 4 — DEFECT-GRAFANA-LABEL-01: Grafana dashboard description "PostgreSQL" → "MySQL"

**Classification:** Configuration defect  
**File:** `monitoring/grafana-dashboard.json`

### Root Cause
The dashboard description string read: `"HRMS Production Dashboard — Request Rate, Latency, Errors, Auth, Health, Resources, PostgreSQL"`. The database was migrated from PostgreSQL to MySQL (Pomelo) but the dashboard label was not updated. This would cause confusion when operators view the dashboard and check metrics.

### Change
```diff
-"description": "HRMS Production Dashboard — Request Rate, Latency, Errors, Auth, Health, Resources, PostgreSQL",
+"description": "HRMS Production Dashboard — Request Rate, Latency, Errors, Auth, Health, Resources, MySQL",
```

### Security Impact
None. Label only.

---

## Fix 5 — DEFECT-NGINX-CONF-01: `nginx/nginx.conf` bash-default syntax inconsistent with template

**Classification:** Configuration defect  
**File:** `nginx/nginx.conf`

### Root Cause
The committed `nginx/nginx.conf` used bash parameter-expansion default syntax (`${DOMAIN_NAME:-localhost}`, `/etc/letsencrypt/live/${DOMAIN_NAME:-localhost}/fullchain.pem`) that:
1. nginx cannot parse — `:-` is not valid nginx variable syntax
2. envsubst does not expand — it would leave `${DOMAIN_NAME:-localhost}` as a literal string
3. Was inconsistent with `nginx/nginx.conf.template` which correctly uses `${DOMAIN_NAME}`, `${SSL_CERT_PATH}`, `${SSL_KEY_PATH}`

At runtime, the nginx Docker service uses `entrypoint: ["/bin/sh", "/etc/nginx/entrypoint.sh"]` which generates `/etc/nginx/nginx.conf` from the template, so the broken committed file was never actually used. However, it was dangerous because any operator who tried to use or reference it directly would get an unparseable config.

### Change
Replaced `nginx/nginx.conf` entirely with:
- A prominent auto-generation header explaining the file is produced at startup from `nginx.conf.template`
- The correct template-consistent content using `${DOMAIN_NAME}`, `${SSL_CERT_PATH}`, `${SSL_KEY_PATH}` (no `:-` defaults)
- The same security headers, rate-limit zones, proxy rules, and location blocks as the template

### Security Impact
None at runtime (template was already used). Eliminates confusion and potential misconfiguration if the file is ever used directly.

---

## Enhancement — `build:ci` script in `HRMS.SPA.Source/package.json`

**Not a defect fix — enhancement for CI usability.**

### Change
Added `"build:ci"` npm script:
```json
"build:ci": "tsc -p tsconfig.json --noEmit && PORT=3000 BASE_PATH=/ NODE_ENV=production vite build --config vite.config.ts"
```

`npm run build` (using `vite.config.ts`) requires `PORT` and `BASE_PATH` environment variables injected by the Replit/Docker workflow. Standard CI environments without these vars would see `Error: PORT environment variable is required`. The new `build:ci` script provides sensible production defaults (`PORT=3000`, `BASE_PATH=/`) so any CI pipeline can build the frontend without Replit-specific env configuration.

---
