# Canonical Deployment Runbook (Phase 2 – P2-A / P2-B / P2-C)

This is the **single, unambiguous** database-setup and deployment sequence for
RatanHR HRMS in production. Every step is numbered and must be executed in order.
Do NOT skip steps or run SQL files not listed here.

> **Changelog (Phase 2)**
> - Fixed: Docker build target corrected from `spa-build` → **`spa-builder`** throughout.
> - Fixed (2026-08-11, audit item 6): the supplementary SQL files
>   (db_performance.sql, db_indexes_fix.sql, db_softdelete_fix.sql) have been
>   **folded into the EF Core migration chain** (`20260811080000_FoldDbScriptIndexes`)
>   and deleted. The migration chain is the single source of truth; there is no
>   supplementary SQL step and no manual operator SQL at all.
> - Fixed: `ALLOWED_HOSTS` env var added to prerequisite checklist and pre-flight smoke test.
> - Fixed: `REDIS_PASSWORD` presence verified in prerequisite table.

---

## 1. Prerequisites

| Requirement | Verification command |
|---|---|
| Docker Engine 24+ with Compose v2 | `docker compose version` |
| `.env` file populated from `.env.production.template` | `grep -c REPLACE_WITH .env && echo "INCOMPLETE – fill placeholders" \|\| echo "OK"` |
| `DOMAIN_NAME` is set | `grep DOMAIN_NAME .env` |
| `ALLOWED_HOSTS` is set (semicolon-separated, no wildcard) | `grep ALLOWED_HOSTS .env` |
| `REDIS_PASSWORD` is set | `grep REDIS_PASSWORD .env` |
| `MYSQL_ROOT_PASSWORD` is set | `grep MYSQL_ROOT_PASSWORD .env` |
| `JWT_PRIVATE_KEY_PEM` and `JWT_PUBLIC_KEY_PEM` are set | `grep JWT_ .env \| wc -l` (expect 2) |
| `ENCRYPTION_KEY` is set | `grep ENCRYPTION_KEY .env` |
| TLS certificate provisioned for `DOMAIN_NAME` | `ls /etc/letsencrypt/live/${DOMAIN_NAME}/` |
| Migration chain present | `ls HRMS.Infrastructure/Migrations/MySql/*.cs` |

**Generate RSA keys if not yet done:**
```bash
chmod +x scripts/generate-rsa-keys.sh && ./scripts/generate-rsa-keys.sh
```

---

## 2. Build Docker images

```bash
export BUILD_TIMESTAMP="$(date -u +%Y%m%dT%H%M%SZ)"
export GIT_SHA="$(git rev-parse HEAD)"
export HRMS_API_IMAGE="hrms-api:${GIT_SHA::12}-${BUILD_TIMESTAMP}"
export HRMS_MIGRATE_IMAGE="hrms-api-migrate:${GIT_SHA::12}-${BUILD_TIMESTAMP}"

docker build \
  --build-arg BUILD_TIMESTAMP="${BUILD_TIMESTAMP}" \
  --build-arg GIT_SHA="${GIT_SHA}" \
  -t "${HRMS_API_IMAGE}" .
```

### 2a. Verify the SPA build stage name

The Dockerfile defines the SPA build stage as **`spa-builder`** (not `spa-build`).
If you need to extract the SPA artifacts manually:

```bash
# Correct target name: spa-builder
docker build --target spa-builder \
  --build-arg BUILD_TIMESTAMP="${BUILD_TIMESTAMP}" \
  --build-arg GIT_SHA="${GIT_SHA}" \
  -t hrms-spa-builder .

# Extract the built SPA dist/ from the image
docker run --rm hrms-spa-builder tar -czf - /spa/dist \
  | tar -xzf - --strip-components=2 -C ./spa-dist
```

> **Note:** In the normal deployment flow (step 4 onwards), the runtime image already
> contains the SPA files copied from the `spa-builder` stage. Manual extraction is only
> needed if you are serving the SPA from a separate CDN or nginx static directory
> outside of the API container.

---

## 3. Validate Docker Compose configuration

```bash
docker compose -f docker-compose.prod.yml config --quiet && echo "OK: config valid"
```

Check that all Docker service targets exist in the Dockerfile:

```bash
# spa-builder
grep "^FROM.*AS spa-builder" Dockerfile
# build
grep "^FROM.*AS build" Dockerfile
# migrate
grep "^FROM.*AS migrate" Dockerfile
# runtime
grep "^FROM.*AS runtime" Dockerfile
```

All four lines must return output. If any is missing, do not proceed.

---

## 4. Start infrastructure services (MySQL + Redis)

```bash
docker compose -f docker-compose.prod.yml up -d mysql redis

# Wait for health checks — both must show "healthy" before continuing
docker compose -f docker-compose.prod.yml ps
```

Poll until both are healthy (≤ 60 s):

```bash
until docker compose -f docker-compose.prod.yml ps \
      | grep -E "(mysql|redis)" | grep -v "healthy"; do
  echo "Waiting for mysql and redis..."; sleep 5
done
echo "OK: mysql and redis are healthy"
```

---

## 5. Apply database migrations and supplementary SQL (automatic)

```bash
docker compose -f docker-compose.prod.yml run --rm migrate
```

This single command, running the `migrate` service defined in `docker-compose.prod.yml`,
performs **all** database setup in order:

| Sub-step | What runs | Mechanism |
|---|---|---|
| 5a | EF Core migrations (`Migrations/MySql/` only) — schema, indexes, soft-delete columns | `dotnet ef database update` |

Since 2026-08-11 there are no supplementary SQL sub-steps: performance indexes,
composite tenant indexes and soft-delete columns/indexes are all part of the
migration chain (`20260811080000_FoldDbScriptIndexes`). EF Core records applied
migrations in `__EFMigrationsHistory`, so re-running the `migrate` service is safe.

**Operators do not run any SQL files manually.** The `migrate` service handles everything.

Verify migration success:

```bash
docker compose -f docker-compose.prod.yml exec mysql \
  mysql -u${MYSQL_USER} -p${MYSQL_PASSWORD} ${MYSQL_DATABASE} \
  -e "SELECT migration_id FROM __EFMigrationsHistory ORDER BY migration_id;"
```

Expected: only migration IDs beginning with `20260726000001_MySqlInitialSchema` or later.
If you see `InitialCreate` or `AddExpandedStructure`, the wrong migrations ran — restore
from backup immediately (see `BackupGuide.md`).

---

## 6. Start all services

```bash
docker compose -f docker-compose.prod.yml up -d
```

The `api` service will not start until `migrate` exits 0 (enforced by
`depends_on: migrate: condition: service_completed_successfully` in `docker-compose.prod.yml`).

---

## 7. Smoke tests

```bash
# Load DOMAIN_NAME from .env
source .env

# 7a. API health
curl -sf "https://${DOMAIN_NAME}/api/health" \
  | python3 -m json.tool || echo "FAIL: API health check"

# 7b. SPA root → HTTP 200
STATUS=$(curl -sf -o /dev/null -w "%{http_code}" "https://${DOMAIN_NAME}/")
[ "$STATUS" = "200" ] && echo "OK: SPA root" || echo "FAIL: SPA root (got ${STATUS})"

# 7c. AllowedHosts guard — must reject foreign Host header
# A 400 response confirms the host-filtering middleware is active.
curl -sf -o /dev/null -w "%{http_code}" \
  -H "Host: evil.attacker.example.com" \
  "http://localhost/api/health" \
  | grep -qE "^400$" && echo "OK: AllowedHosts guard" \
  || echo "WARNING: AllowedHosts guard did not return 400 — review ALLOWED_HOSTS config"
```

---

## 8. Verify Hangfire uses Redis (not in-memory)

```bash
source .env

# 8a. Open Hangfire dashboard (admin login required)
echo "Open: https://${DOMAIN_NAME}/hangfire"

# 8b. Confirm Hangfire keys exist in Redis
docker compose -f docker-compose.prod.yml exec redis \
  redis-cli -a "${REDIS_PASSWORD}" KEYS "hangfire:*" | head -20
```

Expected: at least `hangfire:queues`, `hangfire:locks`, or similar entries.
If no `hangfire:*` keys are present, the API started with in-memory storage
(a startup error that should have been caught by `EnvironmentValidator`).

---

## 9. Verify nginx configuration

```bash
# 9a. Confirm no unsubstituted placeholders remain in the rendered config
docker compose -f docker-compose.prod.yml exec nginx \
  grep -c '\${' /etc/nginx/nginx.conf \
  && echo "ERROR: unsubstituted placeholders in nginx.conf" \
  || echo "OK: no placeholders"

# 9b. Confirm DOMAIN_NAME was substituted
docker compose -f docker-compose.prod.yml exec nginx \
  grep "server_name" /etc/nginx/nginx.conf
```

---

## 10. Update deployment (rolling)

```bash
git pull
export BUILD_TIMESTAMP="$(date -u +%Y%m%dT%H%M%SZ)"
export GIT_SHA="$(git rev-parse HEAD)"

# Rebuild the API image
docker compose -f docker-compose.prod.yml build api

# Apply any new migrations and supplementary SQL
docker compose -f docker-compose.prod.yml run --rm migrate

# Restart only the API container (zero-downtime rolling)
docker compose -f docker-compose.prod.yml up -d --no-deps api
```

---

## Archived / superseded SQL files (DO NOT RUN)

The following files are **archived** in `archive/sql-legacy/` and must NOT be executed.
They were written for the original PostgreSQL backend and will fail against MySQL:

| File | Reason archived |
|---|---|
| `db_setup.sql` | PostgreSQL DDL |
| `db_crm.sql` | PostgreSQL DDL |
| `db_recruitment.sql` | PostgreSQL DDL |
| `db_setup_additions.sql` | PostgreSQL DDL |
| `bootstrap_only_db_setup.sql` | PostgreSQL DDL |

The former supplementary files (`db_performance.sql`, `db_indexes_fix.sql`,
`db_softdelete_fix.sql`) no longer exist: they were folded into the EF Core
migration chain on 2026-08-11 (`20260811080000_FoldDbScriptIndexes`) and deleted.

---

## Canonical migration path validation

```bash
# Confirm only MySql/ migrations are compiled into the assembly
dotnet ef migrations list \
  --context ApplicationDbContext \
  --project HRMS.Infrastructure/HRMS.Infrastructure.csproj \
  --startup-project HRMS.API/HRMS.API.csproj

# Expected first entry: 20260726000001_MySqlInitialSchema
```

The `HRMS.Infrastructure.csproj` excludes legacy migrations at build time:
```xml
<Compile Remove="Migrations/*.cs" />
<Compile Remove="Migrations/**/*.cs" />
<Compile Include="Migrations/MySql/**/*.cs" />
```

---

## Acceptance criteria checklist

| Criterion | How to verify |
|---|---|
| Every Docker target in the runbook exists | Step 3 grep commands |
| Every documented command is executable as written | Run each step; no "not found" errors |
| Only `HRMS.Infrastructure/Migrations/MySql` is canonical | Migration list check above |
| No SQL file is both archived and required | Cross-check archived list vs. step 5 |
| No duplicate or contradictory deployment sequences remain | This runbook is the single source |
| Runbook reflects actual `docker-compose.prod.yml` | All service names, target names match |
| No operator SQL execution required | Step 5 is fully automatic |
| `ALLOWED_HOSTS` validated at startup | Steps 7c + application logs |
| Hangfire uses Redis in production | Step 8b keys present |
