# Phase 2 — Runtime Verification Report

Environment: Linux sandbox, root, no container runtime. Network egress: open.
Evidence policy: every PASS below is backed by pasted command output from this run.
Static source inspection was NOT used as proof for any gate.

---

## BLOCKER 1 — Backend build / test / migration / API — RESOLVED

### Outbound access
- `api.nuget.org` reachable (restore completed against it).
- `builds.dotnet.microsoft.com` reachable (SDK downloaded from it).

### SDK pinning
apt only offered the 8.0.1xx band, so the official `dotnet-install.sh` was used.
Installed to `/opt/dotnet`, matching `global.json`:
```
$ dotnet --version
8.0.416
```

### restore / build / test
```
dotnet restore  -> Success
dotnet build    -> Build succeeded. 0 Error(s), 1 Warning(s)   [CS1998, ZKTecoProvider.cs]
dotnet test     -> total: 1143, passed: 1142, failed: 0, skipped: 1
```
Baseline expectation was 1,142 tests. Actual discovered total is **1143**
(1142 passed + 1 skipped) — i.e. the baseline number equals the passing count,
with one additional explicitly-skipped test. No failures.

### Migrations applied to a live MySQL 8.4.8 database
```
$ dotnet ef migrations list
20260810080843_MySqlBaselineSchema
20260810101800_AddPayslipsCompanyForeignKey

mysql> SELECT * FROM __EFMigrationsHistory;
20260810080843_MySqlBaselineSchema          8.0.8
20260810101800_AddPayslipsCompanyForeignKey 8.0.8

mysql> SELECT COUNT(*) FROM information_schema.tables WHERE table_schema='hrms_db';
82
```

### Model drift
```
$ dotnet ef migrations has-pending-model-changes
No changes have been made to the model since the last migration.
```
(4 informational EF warnings about global query filters on required relationships:
OnboardingTemplate, TrainingProgram, TravelRequest x2.)

### FK + CreatedAt behaviour — real DML, not schema review
```
### created_at auto-populates on INSERT with no explicit value
id 1  Phase2 Verify Co  2026-08-10 13:49:22.754221

### created_at on UPDATE
created_at_before            created_at_after             verdict
2026-08-10 13:49:22.754221   2026-08-10 13:49:22.754221   PASS: unchanged

### valid FK chain insert (company -> employee -> payslip)  PASS
payslip_id 1  employee_id 2  company_id 2  net_pay 64150.00

### payslip with non-existent company_id
ERROR 1452 (23000): Cannot add or update a child row: a foreign key constraint fails
(`hrms_db`.`payslips`, CONSTRAINT `fk_payslips_company_id` FOREIGN KEY (`company_id`)
REFERENCES `companies` (`id`) ON DELETE RESTRICT)          -> correctly rejected

### DELETE company still referenced by a payslip
ERROR 1451 (23000): Cannot delete or update a parent row: a foreign key constraint fails
(... ON DELETE RESTRICT)                                    -> RESTRICT confirmed

### row_version (optimistic concurrency)
before 2026-08-10 13:49:38.569667 -> after 2026-08-10 13:49:38.627639  PASS: bumped
```

### API start + health endpoint
```
$ curl -s http://127.0.0.1:5100/health
{"status":"Healthy","checks":[
 {"name":"liveness","status":"Healthy","description":"Service is alive."},
 {"name":"email","status":"Healthy","description":"SMTP not configured (non-production)."},
 {"name":"database","status":"Healthy","description":null}]}

/swagger/index.html      200
/swagger/v1/swagger.json 200 (629,200 bytes)
/metrics                 200
/api/employees (no token) 401
POST /api/auth/login (correct creds + portal) 200, RS256 JWT issued
/api/employees (valid token, mustChangePassword=true) 403 "Password change required."
```

---

## BLOCKER 2 — MySQL 8.4 parity — RESOLVED

Provisioned through Nix (no container runtime needed), real server, real socket:
```
$ mysql --version
mysql  Ver 8.4.8 for Linux on x86_64 (Source distribution)

$ mysqld --datadir=/tmp/mysqldata --socket=/tmp/mysqlrun/mysql.sock --port=3306
(running)
```
This is genuinely 8.4.x, not 8.0.x. The full migration set was applied against it
(see Blocker 1) and all DML tests above ran on this instance.
Compose files pin `mysql:8.4` (base file pins by digest), so runtime and declared
parity now agree.

---

## BLOCKER 3 — Docker / Compose / CI — PARTIALLY RESOLVED

### `docker build` — STILL BLOCKED
```
docker     MISSING
podman     MISSING
buildah    MISSING
nerdctl    MISSING
dockerd    MISSING
ls /var/run/docker.sock -> No such file or directory
```
Missing capability: **no container runtime and no Docker daemon socket in this
sandbox**; nested/privileged containers are not available. `docker build` of the
backend image therefore could not be executed here. It needs a machine with a
Docker/Podman daemon (or a CI runner).

### `docker compose config` on all 6 compose files — RESOLVED
A standalone Compose v2 binary needs no daemon, so this gate did run:
```
$ docker-compose version
Docker Compose version 2.40.3
```
With `.env` populated from `.env.example` and overlay fragments validated on top of
their base file (the two overlays are fragments and are invalid standalone by design):

| compose invocation | result | services |
|---|---|---|
| `-f docker-compose.yml` | VALID | redis alertmanager clamav mysql backfill migrate api backup jaeger nginx certbot prometheus grafana |
| `-f docker-compose.yml -f docker-compose.override.yml` | VALID | mysql backfill backup clamav prometheus grafana jaeger redis alertmanager migrate api |
| `-f docker-compose.prod.yml` | VALID | clamav mysql migrate redis api nginx backup certbot |
| `-f docker-compose.e2e.yml` | VALID | mysql redis api spa |
| `-f docker-compose.e2e.yml -f docker-compose.e2e.nohealthcheck.yml` | VALID | mysql redis api spa |
| `-f docker-compose.yml -f docker-compose.backup.yml` | VALID | mysql redis clamav backfill migrate api nginx prometheus grafana jaeger alertmanager backup certbot |

Note for operators: `docker-compose.override.yml` and
`docker-compose.e2e.nohealthcheck.yml` fail with
`has neither an image nor a build context specified` when passed alone. That is
expected for overlays, but any runbook or CI step that validates them
individually will report a false failure.

### GitHub Actions run — STILL BLOCKED
```
gh  MISSING
```
Missing capability: the `gh` CLI is absent **and** no GitHub credential
(`GH_TOKEN` / `GITHUB_TOKEN` / PAT with `workflow` scope) and no `origin` remote
authorisation are present in this environment, so `.github/workflows/ci.yml`
could not be dispatched and no run link exists.
The workflow's own jobs are: `secret-scan`, `backend`, `frontend`, `e2e`,
`docker-validate`, pinned to `DOTNET_VERSION: 8.0.416` (matches `global.json`).
To close this: run from a clone with a `workflow`-scoped token, or push the branch
and read the run in the GitHub UI.

---

## BLOCKER 4 — EF drift / migration-squash ratification — NEEDS HUMAN DECISION

**Requires owner sign-off before Phase 2 close.** Not resolved here by design.

Runtime state is self-consistent: `has-pending-model-changes` reports no drift and
82 tables were created. The discrepancies are between the migration history, the
documented history, and the enforced database constraints:

1. **Squash vs documentation.** `HRMS.Infrastructure/Migrations/README.md` states the
   chain "should start with `20260726000001_MySqlInitialSchema`". The actual chain
   starts with `20260810080843_MySqlBaselineSchema` — the history was squashed/rebased
   on 2026-08-10 and the README was not updated.
2. **The claimed historical archive does not exist.** The same README says root-level
   `Migrations/*.cs` files are a preserved pre-MySQL audit trail that must not be
   deleted. `ls HRMS.Infrastructure/Migrations/*.cs` -> *No such file or directory*.
   The archive was removed. The csproj still carries the `<Compile Remove>` rules that
   exist to exclude it. Decision needed: accept the squash and delete the stale
   guidance, or restore the archive.
3. **A second README contradicts the first.**
   `Migrations/AddAssetHelpdesk_README.md` instructs `--output-dir Migrations`, which
   the main README correctly identifies as the directory that is silently excluded
   from compilation. Following it would produce a migration that never runs.
   (The asset/helpdesk tables themselves ARE present in the baseline: `assets`,
   `asset_categories`, `asset_history`, `helpdesk_tickets`, `helpdesk_categories`,
   `helpdesk_comments`, `helpdesk_history`.)
4. **FK coverage is far narrower than the model implies** — the most material item.
   The squashed baseline emits only 22 foreign keys across 82 tables, and
   `payslips` is the ONLY table whose `company_id` is FK-enforced. 66 tables carry a
   `company_id` with no referential constraint, including `employees`, `users`,
   `audit_logs`, `payroll_locks`, and every sales/expense/asset table. Demonstrated,
   not inferred:
   ```
   mysql> INSERT INTO employees (employee_id,company_id,full_name,status,is_active)
          VALUES ('EMP-BAD',987654,'Bad FK','active',1);
   -- accepted, no error
   mysql> SELECT id,employee_id,company_id FROM employees WHERE employee_id='EMP-BAD';
   3  EMP-BAD  987654        <-- orphaned row pointing at a non-existent company
   ```
   Tenant isolation is therefore enforced only by application-level query filters,
   with no database backstop. Owner must decide whether that is the intended
   posture or whether the squash dropped constraints that should be restored.

---

## Additional runtime finding (not one of the 4 blockers)

**The mandatory first-login password change is unreachable over HTTPS.**
`MustChangePasswordMiddleware.AllowedPaths` permits
`/api/auth/change-password|logout|refresh|login`, `/swagger`, `/health`, `/metrics`
— but **not** `/api/auth/csrf`. Antiforgery is enforced (and over plain HTTP the
antiforgery cookie policy `SecurePolicy = Always` throws), so a user with
`mustChangePassword=true` cannot fetch a CSRF token, and `change-password` rejects
the request:
```
HTTPS:  GET  /api/auth/csrf            -> 403 {"message":"Password change required..."}
        POST /api/auth/change-password -> 401 {"message":"CSRF token missing or invalid."}
HTTP:   POST /api/auth/change-password -> 500 InvalidOperationException: antiforgery
        system has AntiforgeryOptions.Cookie.SecurePolicy = Always, but the current
        request is not an SSL request
```
Deadlock: the account cannot leave the must-change state through the API.
Fix is one line — add `/api/auth/csrf` to `AllowedPaths` — but it was left unmade
because this phase is verification only.

Also outstanding: the single build warning, CS1998 in
`HRMS.Infrastructure/Biometric/ZKTecoProvider.cs` (`async` method with no `await`).

---

## Summary

| Blocker | Verdict |
|---|---|
| 1 — build / test / migrations / API health | **RESOLVED** (1142 passed / 0 failed / 1 skipped; migrations applied; health 200) |
| 2 — MySQL 8.4 parity | **RESOLVED** (real 8.4.8 server, full migration set applied) |
| 3 — Docker / Compose / CI | **PARTIALLY RESOLVED** — all 6 compose files validate; `docker build` and the CI run are **STILL BLOCKED** (no container runtime/daemon; `gh` binary and workflow-scoped credential absent) |
| 4 — EF drift / squash ratification | **NEEDS HUMAN DECISION** — owner sign-off required before Phase 2 close |
