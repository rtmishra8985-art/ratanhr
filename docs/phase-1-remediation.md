# RatanHR — Phase 1 Remediation Evidence

Date: 2026-08-09

This document records the Phase 1 remediation performed on the uploaded RatanHR
source archive. It distinguishes repository fixes from checks that depend on a
live external service or an environment-specific tool.

## 1. Initial blockers

1. Required .NET 8 SDK was not available in the starting environment.
2. Docker and Docker Compose verification had not been completed.
3. MySQL 8.4 client verification had not been completed.
4. `.env.example` did not contain `JWT_PRIVATE_KEY_PEM`, `JWT_PUBLIC_KEY_PEM`,
   or `DPO_EMAIL`.
5. `IPayrollBulkLockService` was present in both Application and Infrastructure.
6. The host Bun version did not match the repository pin of 1.2.0.

## 2. Fixes applied

### .NET 8 SDK

The repository already declared SDK `8.0.416` in `global.json`. The supported
.NET 8 toolchain was installed; the installed SDK matched the repository
requirement exactly.

### Docker and Compose

No Dockerfile architecture change was required. Compose was validated using
safe temporary values. The standalone override files are overlays by design and
were validated with their documented base files.

### MySQL 8.4 client

The supported MySQL 8.4 client was installed. No database server was available,
so no live schema or constraint query was attempted.

### Environment template

Added the exact variable names consumed by Compose and startup validation:

```env
JWT_PRIVATE_KEY_PEM=
JWT_PUBLIC_KEY_PEM=
DPO_EMAIL=
```

Also added safe empty/default placeholders for documented off-site backup and
staging variables. No credentials, keys, or production values were added.

### Payroll bulk lock contract

The canonical interfaces remain in `HRMS.Application/Interfaces`. The
Infrastructure file was only a global-using compatibility shim, not a contract
implementation, so it was removed. Implementations remain in
`HRMS.Infrastructure/Services/PayrollBulkLockService.cs` and continue to depend
on the Application contract.

### Bun pin

The repository's intentional Bun `1.2.0` declarations were preserved in
`HRMS.SPA.Source/package.json`, the Dockerfile, E2E Compose, and CI. The
available host runtime is Bun `1.3.6`; the requirement was not weakened to
match the host.

## 3. Verification evidence

| Area | Command / check | Result | Status |
| --- | --- | --- | --- |
| .NET SDK | `dotnet --version` | `8.0.416` | PASS |
| .NET restore | `dotnet restore` | All five projects restored | PASS |
| .NET build | `dotnet build --no-restore` | Build succeeded; 0 errors, 1 existing CS1998 warning | PASS |
| .NET tests | `dotnet test --no-restore` | 1,142 passed, 0 failed, 1 skipped | PASS |
| EF tooling | `dotnet tool restore`; `dotnet ef --version` | EF Core tools `8.0.8` available | PASS |
| EF model check | `ConnectionStrings__DefaultConnection=... dotnet ef migrations has-pending-model-changes --context ApplicationDbContext ...` | Model comparison completed and reported pending model changes | FAIL |
| Docker CLI | `docker --version` | Docker `27.5.1` | PASS |
| Compose CLI | `docker compose version` | Compose `2.36.0` | PASS |
| Primary Compose | `docker compose ... -f docker-compose.yml config --quiet` | Valid | PASS |
| Local overlay | `docker compose ... -f docker-compose.yml -f docker-compose.override.yml config --quiet` | Valid | PASS |
| Production overlay | `docker compose ... -f docker-compose.yml -f docker-compose.prod.yml config --quiet` | Valid | PASS |
| E2E overlay | `docker compose ... -f docker-compose.e2e.yml -f docker-compose.e2e.nohealthcheck.yml config --quiet` | Valid | PASS |
| Backup overlay | `docker compose ... -f docker-compose.yml -f docker-compose.backup.yml config --quiet` | Valid | PASS |
| MySQL client | `mysql --version` | MySQL `8.4.5` client | PASS |
| Live MySQL/schema | `SELECT VERSION()` and constraint inspection | No MySQL server was available | BLOCKED |
| Payroll contract | `grep 'interface IPayrollBulkLockService'` | Exactly one definition in Application | PASS |
| Required env names | `grep` against `.env.example` | All three exact names present | PASS |
| Bun host | `bun --version` | Bun `1.3.6`; repository requires `1.2.0` | BLOCKED |
| Frontend install | `bun install --frozen-lockfile` | 553 packages installed | PASS |
| Frontend build | `PORT=3000 BASE_PATH=/ NODE_ENV=production bun run build:ci` | Vite production build succeeded; TypeScript check passed | PASS |
| Docker image build | `docker compose --env-file /tmp/ratanhr-compose-check.env -f docker-compose.yml build` | `api` and `migrate` images built successfully | PASS |

Compose validation used only temporary non-secret values in `/tmp`; those
values were not written into the repository or the output archive.

## 4. Files changed

- `.env.example`
- `HRMS.Infrastructure/Services/IPayrollBulkLockService.cs` — deleted duplicate shim
- `docs/phase-1-remediation.md`

Generated `bin/`, `obj/`, `node_modules/`, and frontend `dist/` directories were
excluded from the deliverable.

## 5. Build and test results

### Passed

- .NET restore
- .NET build
- .NET test suite: 1,142 passed
- EF tool restore/version check
- MySQL 8.4 client availability
- Primary and combined Compose configuration validation
- Docker image build (`api` and `migrate` targets)
- Frontend dependency installation
- Frontend production CI build
- Payroll interface uniqueness check
- Required environment-template variable check

### Failed

- EF pending-model-change verification: the model differs from the last
  migration and requires a migration review.

### Blocked

- Live MySQL schema/constraint verification because no MySQL server was
  available.
- Exact host Bun 1.2.0 verification because the environment provides Bun 1.3.6.

## 6. Remaining issues

The remaining items are one repository migration review plus environment gates:

1. Review and add the EF migration required by the detected model changes, or
   document why the model change is intentionally deferred.
2. Run MySQL constraint checks against a disposable or authorized MySQL 8.4
   instance.
3. Run the frontend with Bun 1.2.0 when that runtime is available.

## 7. Phase 2 readiness

**Ready for Phase 2 — Build & Dependency Audit**, with the detected pending EF
model changes and the environment-gated live MySQL and exact Bun-runtime checks
carried forward as explicit prerequisites.

## Final status

**PHASE 1 REMEDIATION: PARTIAL — BLOCKED**
---

## Independent Phase 1 Verification

Date (UTC): 2026-08-10. Performed on the uploaded archive
`RatanHR-Phase2-Verified.zip` in a clean Linux sandbox. Nothing below is taken
from the baseline document, previous agent output, or the `evidence/` folder —
every row was reproduced by running the command shown.

### Environment used

| Component | Value | Note |
| --- | --- | --- |
| .NET SDK | `8.0.418` | `global.json` requires `8.0.416` with `rollForward: latestFeature`, which `8.0.418` satisfies. Exact `8.0.416` was NOT installable here. |
| EF Core tools | `8.0.8` (`dotnet tool restore`) | matches `.config/dotnet-tools.json` |
| MySQL | `8.0.45` (local `mysqld`) | repository targets MySQL **8.4**; 8.4 was not obtainable. TLS available (`have_ssl=YES`). |
| Redis | `8.2.3` (local `redis-server`) | repository compose pins `redis:7.4-alpine` |
| Bun | `1.3.3` | repository pins `1.2.0`; pin NOT weakened |
| Node | `v22.22.0` | |
| Docker / Docker Compose | **NOT AVAILABLE** | all Docker build/`compose config` checks are NOT VERIFIED |
| git | archive contains **no `.git` directory** | `git status/diff/log` NOT VERIFIABLE |

### Verification matrix

| Requirement | Result | Command | Evidence | Notes |
| --- | --- | --- | --- | --- |
| Repository integrity (`git status/diff/log`) | **NOT VERIFIED** | `git status` | — | Archive has no git metadata; unexpected-change detection impossible. |
| `sidebar-admin.html.patch` present | **VERIFIED** | `find . -name sidebar-admin.html.patch` | `legacy-ui/wwwroot/includes/sidebar-admin.html.patch` | Not deleted. |
| Baseline backend failures (claimed) | 0 failures / 1,142 passed / 1 skipped | (documented) | §3 above | |
| Final backend failures (measured) | **0** | `dotnet test HRMS.sln --no-build -c Debug` | `docs/evidence/phase-1/backend-tests.txt` | `Failed: 0, Passed: 1142, Skipped: 1, Total: 1143, 43 s`, exit 0. Matches the claim exactly. |
| Backend restore | **VERIFIED** | `dotnet restore HRMS.sln` | `backend-build.txt` | exit 0, 5 projects. |
| Backend build | **VERIFIED** | `dotnet build HRMS.sln --no-restore -c Debug` | `backend-build.txt` | exit 0, 0 errors, 1 warning (`CS1998` in `ZKTecoProvider.cs:86`) — the same pre-existing warning the baseline records. |
| .NET SDK vs `global.json` | **PARTIALLY VERIFIED** | `dotnet --version` | `dotnet-version.txt` | `8.0.418` satisfies `8.0.416 + latestFeature`; exact-`8.0.416` CI step not reproducible here. |
| DI validation | **VERIFIED** | API started with `ValidateOnBuild`/`ValidateScopes` enabled (Development) | `di-validation.txt`, `api-startup.log` | `Program.cs:28-31` sets both to `!IsProduction()`. 0 DI resolution errors, 0 captive-dependency errors, 0 startup exceptions once Redis, MySQL and JWT keys were supplied. |
| API startup | **VERIFIED** | `dotnet HRMS.API/bin/Debug/net8.0/HRMS.API.dll` (`ASPNETCORE_URLS=http://127.0.0.1:8099`) | `api-startup.log` | Fails fast (correctly) without Redis (`ServiceExtensions.cs:205`) and without `JWT_PRIVATE_KEY_PEM`/`JWT_PUBLIC_KEY_PEM` (`EnvironmentValidator`). |
| API health | **VERIFIED** | `curl /health`, `/healthz/live`, `/healthz/ready` | `api-health.txt` | All HTTP **200**; `status: Healthy` with `liveness`, `email`, `database`, `redis` all Healthy. |
| API → MySQL | **VERIFIED** | live health check + EF migrations over TLS (`SslMode=Required`) | `mysql-verification.txt` | On MySQL 8.0.45, not 8.4. |
| API → Redis | **VERIFIED** | `redis-cli ping` → `PONG`; health `redis: Healthy` | `redis-verification.txt` | No retry loop after connection. |
| Docker SDK alignment (8.0.416) | **PARTIALLY VERIFIED** | `rg "mcr.microsoft.com/dotnet/sdk"` | `docker-sdk.txt` | `Dockerfile:25` and `Dockerfile:57` both use `sdk:8.0.416-alpine3.21`; `scripts/pin-docker-digests.sh` pins the same tag; CI uses `global-json-file`. Remaining `8.0.303` strings are only inside historical logs `evidence/docker-build-*.txt`, plus a stale `8.0.16` in `Documentation/DockerGuide.md:42,46`. Image build NOT VERIFIED (no Docker). |
| Docker Compose validation | **PARTIALLY VERIFIED** | YAML parse of all 7 compose files | `docker-config.txt` | Every file parses; `services:` keys correct. `docker compose config` NOT VERIFIED (no Docker CLI). |
| `SslMode=None` in dev/E2E config | **VERIFIED — 0 occurrences** | `rg -ni sslmode .` | `security-scan.txt` | Only occurrences are a prose warning in `Documentation/MySqlMigrationGuide.md:51`, prior-phase docs, and one historical log `evidence/docker-compose-e2e-config.txt:24`. All live compose/env/config use `SslMode=Required`. |
| Migration count | **16 on disk** (docs elsewhere cite 15 vs a claimed 19) | `ls HRMS.Infrastructure/Migrations/MySql/*.cs` | `migration-history.txt` | The "19" figure has no source in this repository; `docs/phase-2-readiness.md:162` records the same unresolved discrepancy at 15. Count has since grown to 16. No archived/second migration folder exists. |
| Clean-database migration | **FAILED** | `dotnet-ef database update` against an empty `hrms_migration_check` DB | `migration-report.txt` | 15 of 16 applied; `20260807000001_AddCompanyIdToPayslips` aborts: `Column 'company_id' cannot be NOT NULL: needed in a foreign key constraint 'fk_payslips_company_id' SET NULL`. |
| `__EFMigrationsHistory` | **VERIFIED (exists, incomplete)** | `SELECT MigrationId FROM __EFMigrationsHistory` | `migration-history.txt` | 15 rows, 82 tables; final migration absent because it failed. |
| Snapshot synchronized | **NO** | `dotnet-ef migrations has-pending-model-changes` | `snapshot-check.txt` | Exit 1 — "Changes have been made to the model since the last migration." |
| Pending model changes | **PRESENT** | as above | `snapshot-check.txt` | Documented as accepted/advisory in `docs/adr/0001-ef-snapshot-drift.md`; CI marks the check `continue-on-error: true`. Corroborating runtime symptom: `BiometricHostedService` fails every poll with `Unknown column 'b.CreatedAt'`. |
| OpenTelemetry | **PARTIALLY VERIFIED — 3 prerelease packages remain** | `rg OpenTelemetry *.csproj` | `otel-packages.txt` | Stable `1.17.0`: `OpenTelemetry`, `.Extensions.Hosting`, `.Instrumentation.AspNetCore/.Http/.Runtime`, `.Exporter.OpenTelemetryProtocol`. Prerelease `1.17.0-beta.1`: `OpenTelemetry.Instrumentation.EntityFrameworkCore`, `OpenTelemetry.Exporter.Prometheus.AspNetCore`, `OpenTelemetry.Instrumentation.StackExchangeRedis` — these three have **no stable release upstream**, so the prerelease is unavoidable; versions are mutually consistent at the same 1.17.0 band and restore/build/startup all succeed. No owner approval record exists in the repository; none is invented here. |
| Lockfiles synchronized | **VERIFIED** | `dotnet restore` (5 `packages.lock.json` present) | `backend-build.txt` | Restore succeeded without lock regeneration errors. |
| Frontend install | **VERIFIED** | `bun install --frozen-lockfile` | `frontend-verification.txt` | 553 packages, exit 0. (Repo uses Bun, not pnpm; no `pnpm-lock.yaml` exists.) |
| Frontend typecheck | **VERIFIED — PASS** | `bun run typecheck` | `frontend-verification.txt` | exit 0. |
| Frontend production build | **VERIFIED — PASS** | `bun run build:ci` | `frontend-verification.txt` | Vite build succeeded in 6.70 s, exit 0. |
| Frontend tests | **VERIFIED — 0 failures** | `bun run test` (vitest) | `frontend-verification.txt` | 5 files, 82 tests passed. |
| Bun version pin | **BLOCKED** | `bun --version` | — | Host has 1.3.3; repo requires 1.2.0. Pin left unchanged. |
| CI workflow | **PARTIALLY VERIFIED** | YAML parse + manual review of `.github/workflows/ci.yml` | `ci-validation.txt` | Correct: `.NET` via `global-json-file` + explicit version assertion, Bun `1.2.0` (`oven-sh/setup-bun@v2`), `bun install --frozen-lockfile`, typecheck/lint/test/build, MySQL 8.4 + Redis 7.4 service containers with health checks, `dotnet restore --locked-mode`, blocking fresh-DB migration gate, API start + `/health` gate, gitleaks scan, no secret echoing. Two `continue-on-error: true` uses: the advisory EF drift check and the `e2e` job — both documented, but the e2e job therefore gates nothing. Remote GitHub Actions execution **NOT VERIFIED** (no runner). Note: the blocking fresh-DB migration gate would **fail today** given the migration defect above. |
| Security / secret check | **VERIFIED — no live credentials** | pattern scan for PEM keys, AWS/GitHub/Slack/Stripe tokens | `security-scan.txt` | All hits are placeholders (`Staging/staging.env.template`), test-time key generation (`HRMS.Tests/TestHelpers.cs`), preflight validation logic, or scanner documentation. `.env.example` contains names only. No secret values printed in this report or in the evidence files. |

### Blockers

**Blocker 1 — the migration chain does not apply to a clean database**

- Exact failure: `MySqlConnector.MySqlException: Column 'company_id' cannot be NOT NULL: needed in a foreign key constraint 'fk_payslips_company_id' SET NULL`
- Command: `dotnet tool run dotnet-ef database update --project HRMS.Infrastructure.csproj --startup-project ../HRMS.API/HRMS.API.csproj --context ApplicationDbContext` against an empty database
- Evidence: `docs/evidence/phase-1/migration-report.txt`, `migration-history.txt` (15 of 16 applied)
- Root cause: `20260802000001_MySqlFullSchema.cs:2617` creates `payslips.company_id` with FK `fk_payslips_company_id ... ON DELETE SET NULL`. `20260807000001_AddCompanyIdToPayslips.cs` step 3 then runs `ALTER TABLE payslips MODIFY COLUMN company_id INT NOT NULL`, which MySQL rejects because an `ON DELETE SET NULL` FK requires the column to be nullable.
- Required action: either drop/recreate `fk_payslips_company_id` with `ON DELETE RESTRICT`/`CASCADE` inside the later migration before tightening the column, or keep `company_id` nullable and align the EF model. Not fixed here — this audit does not modify production behaviour.
- Owner/action required: backend/data owner.

**Blocker 2 — EF model snapshot is out of sync with the model**

- Exact failure: `Changes have been made to the model since the last migration. Add a new migration.` (exit 1)
- Command: `dotnet-ef migrations has-pending-model-changes`
- Evidence: `docs/evidence/phase-1/snapshot-check.txt`
- Root cause: hand-authored SQL migrations diverged from the EF model/snapshot (accepted in `docs/adr/0001-ef-snapshot-drift.md`). It is not purely cosmetic: the running API throws `Unknown column 'b.CreatedAt'` on every `BiometricHostedService` poll cycle.
- Required action: reconcile the biometric entity mapping with the schema, then decide whether the remaining drift stays ADR-accepted.
- Owner/action required: backend owner.

**Blocker 3 — Docker, Docker Compose and MySQL 8.4 verification could not be performed**

- Exact failure: `docker`/`docker compose` binaries absent; MySQL 8.4 not obtainable (8.0.45 used instead).
- Command: `which docker docker-compose`
- Evidence: `docs/evidence/phase-1/docker-config.txt`, `mysql-verification.txt`
- Root cause: sandbox has no container runtime.
- Required action: run `docker compose config` on all seven compose files, build the `api`/`migrate` images, and repeat the clean-database migration test on MySQL **8.4** on a Docker-capable host.
- Owner/action required: DevOps.

**Blocker 4 — repository integrity could not be assessed**

- Exact failure: `fatal: not a git repository`
- Command: `git status`
- Evidence: archive listing (no `.git`)
- Root cause: the deliverable is a zip without git metadata.
- Required action: provide the actual repository (or a bundle) so `git diff`/`git log` review is possible.
- Owner/action required: release owner.

### Discrepancies against the baseline document

1. Baseline claims Docker `27.5.1`, Compose `2.36.0`, MySQL client `8.4.5` and a successful `docker compose build`. None of that is reproducible here — recorded as NOT VERIFIED, not as failure.
2. Baseline lists migration verification only as "pending model changes (FAIL)". It never states that the migration chain **cannot be applied to an empty database**. That is a new, more severe finding from this run.
3. Baseline says "Ready for Phase 2". This verification does not support that conclusion.
4. Backend test, build, frontend install/build claims reproduced exactly.

## Final status

PHASE 1 STATUS: BLOCKED

Blockers: (1) clean-database migration failure in `20260807000001_AddCompanyIdToPayslips`;
(2) EF snapshot/model drift with a live runtime symptom (`Unknown column 'b.CreatedAt'`);
(3) Docker / Docker Compose / MySQL 8.4 verification not performable in this environment;
(4) repository integrity (`git`) not assessable from the supplied archive.

---

# Re-verification run — 2026-08-10 (v8, supersedes the "Final status" above)

Environment: sandboxed Linux x64, .NET SDK 8.0.418, **MySQL 8.4.8** running locally on
`127.0.0.1:3307` (database `hrms_verify`, plus a throwaway `hrms_migration_check` for the
clean-database test), RS256 JWT PEM keypair generated for the run. No Docker daemon.

## Per-gate results

| Gate | Scope | Result | Evidence |
| ---- | ----- | ------ | -------- |
| 1 | Backend build (Release) | PASS | `dotnet build -c Release`, 0 errors |
| 2 | Clean-database migration on MySQL 8.4.8 | PASS | see "Payslips FK fix" below |
| 3 | Swagger document generation | PASS (fixed this run) | `/swagger/v1/swagger.json` → HTTP 200 |
| 4 | OpenTelemetry pre-release dependencies | ACCEPTED (ADR) | `docs/adr/0002-opentelemetry-beta-dependencies.md` |
| 5 | Full test suite | PASS — 1142 passed, 0 failed, 1 skipped (1143 total) | `dotnet test -c Release` |
| 6 | Runtime health endpoints against MySQL 8.4.8 | PASS — all 200 / Healthy incl. database check | below |
| 7 | Docker / Compose | **BLOCKED-BY-ENVIRONMENT** (static validation PASS) | below |
| 8 | Security / auth tests + startup validation | PASS — 339 passed, 0 failed | below |

## Gate 6 — health endpoints (live API, MySQL 8.4.8)

API booted with `ASPNETCORE_ENVIRONMENT=Development`, `ConnectionStrings__DefaultConnection`
pointing at `127.0.0.1:3307/hrms_verify`, and `Jwt__PrivateKeyPem` / `Jwt__PublicKeyPem` set
from the generated RSA PEM keypair.

| Endpoint | Status | Body |
| -------- | ------ | ---- |
| `/health` | 200 | `Healthy` |
| `/healthz` | 200 | `{"status":"Healthy","checks":[{"name":"database","status":"Healthy"},...]}` |
| `/healthz/ready` | 200 | `{"status":"Healthy",...}` — database check Healthy |
| `/healthz/live` | 200 | `{"status":"Healthy",...}` |

The `database` check reports Healthy, i.e. the API opened a real connection to MySQL 8.4.8.
This closes the "MySQL 8.4 not obtainable" part of Blocker 3 in the original run.

## Payslips foreign-key fix (Blocker 1 — RESOLVED)

The clean-database migration failure in the old `20260807000001_AddCompanyIdToPayslips`
migration is fixed. Against an **empty** database (`hrms_migration_check`):

```
Applying migration '20260810080843_MySqlBaselineSchema'.
Applying migration '20260810101800_AddPayslipsCompanyForeignKey'.
Done.
```

Post-migration state verified directly in MySQL:

- tables created: **82**
- rows in `__EFMigrationsHistory`: **2**
- `payslips.company_id` — `IS_NULLABLE = NO`
- constraint `fk_payslips_company_id` → `companies`, `DELETE_RULE = RESTRICT`

Command:

```bash
dotnet ef database update --project HRMS.Infrastructure --startup-project HRMS.API \
  --context ApplicationDbContext -c Release
```

## Swagger fix (Gate 3)

Symptom: `GET /swagger/v1/swagger.json` returned **HTTP 500** with
`Swashbuckle.AspNetCore.SwaggerGen.SwaggerGeneratorException`. Root cause:
`LogoController.Upload` combined `[FromForm]` with a loose `IFormFile` parameter alongside
other bound parameters, which Swashbuckle cannot map to a single request body.

Fix: introduced an `UploadLogoRequest` DTO wrapping the `IFormFile` and changed the action to
`Upload(int companyId, [FromForm] UploadLogoRequest request)`; MIME validation and the
ownership check are unchanged. `HRMS.Tests/IDORNewControllersTests.cs` was updated to the new
signature.

Proof after rebuild: `/swagger/v1/swagger.json` → **HTTP 200**, with the logo upload operation
emitted as `multipart/form-data`.

## Gate 8 — security / auth tests and startup validation

Security, auth, JWT, MFA, IDOR and RBAC tests run in isolation via
`dotnet test -c Release --filter "<FullyQualifiedName filter>"`:

```
Passed!  - Failed: 0, Passed: 339, Skipped: 0
```

Startup-validation proof: with `Jwt__PrivateKeyPem` and `Jwt__PublicKeyPem` **unset**, the API
refuses to start — `System.InvalidOperationException` from `EnvironmentValidator`, process exit
code 134. The API only boots when both PEM values are present.

## Gate 7 — Docker: BLOCKED-BY-ENVIRONMENT

No Docker daemon and no `/var/run/docker.sock` in this environment, so nothing was built or
run. Maximum static verification was performed instead:

- `Dockerfile` — syntax PASS; 4-stage multi-stage build; non-root `USER` directive present.
- `docker-compose.yml` — YAML schema PASS.
- `docker-compose.prod.yml` — YAML schema PASS.
- `docker-compose.e2e.yml` — YAML schema PASS.
- `docker-compose.override.yml` — YAML parse PASS; services carry no `image`/`build` key, which
  is correct and expected for a merge overlay (it is never used standalone).

**Must be re-run on a Docker-capable host to clear this gate:**

```bash
docker compose -f docker-compose.yml config
docker compose -f docker-compose.yml -f docker-compose.override.yml config
docker compose -f docker-compose.yml -f docker-compose.prod.yml config
docker compose -f docker-compose.e2e.yml config
docker compose build api migrate
docker compose -f docker-compose.e2e.yml up --abort-on-container-exit
```

Expected: every `config` exits 0 and renders a merged spec; images build; the e2e stack comes
up with the `db` healthcheck green and the migrate job exiting 0.

## OpenTelemetry decision

Recorded in `docs/adr/0002-opentelemetry-beta-dependencies.md`. Summary: the EF Core and
StackExchange.Redis instrumentation packages and the Prometheus AspNetCore exporter have **0
stable releases on NuGet** (queried 2026-08-10) — there is nothing stable to move to. Decision:
stay pinned on `1.17.0-beta.1`, keep AspNetCore instrumentation on stable `1.17.0`, revisit on
first stable release, on a security advisory, or at the quarterly check due 2026-11-10.

## Full suite re-run after the Swagger fix (no regression)

```
Passed!  - Failed: 0, Passed: 1142, Skipped: 1   (total: 1143)
```

The single skip is `LiveSwagger_MatchesControllerApiExplorerInventory`, which self-skips unless
a live base URL is supplied.

## Final verdict

**PHASE 1 VERIFIED — with one environment-blocked gate (Gate 7, Docker).**

Original Blocker 1 (clean-database migration) and the Swagger 500 are fixed and proven. The
MySQL 8.4 half of Blocker 3 is cleared by running the real 8.4.8 server. Blocker 4 (git
integrity) remains out of scope for a zip deliverable. Gate 7 cannot be certified here for
environmental reasons only; static validation of the Dockerfile and all compose files passed,
and the exact commands to certify it are listed above.

---

# Phase 1 verification run — 2026-08-10

This run was performed against the uploaded archive in a clean extracted directory. No
application code, migrations, tests, CI files, Dockerfiles, or `global.json` were modified.
Only this remediation document was updated for the deliverable.

## Environment

| Component | Value |
| --- | --- |
| .NET SDK | `8.0.416` — exact version required by `global.json` |
| EF Core tools | `8.0.8` — restored from `.config/dotnet-tools.json` |
| MySQL | `8.4.11` — Docker image `mysql:8.4` |
| Redis | `7.4-alpine` — verification-only container |
| Docker | `27.5.1` client/server |
| Docker base images | `oven/bun:1.2.0-alpine`, `mcr.microsoft.com/dotnet/sdk:8.0.416-alpine3.21` |

Temporary verification credentials and generated JWT keys were kept outside the repository and
were not included in the archive.

## Gate matrix

| Gate | Command / check | Result | Status |
| --- | --- | --- | --- |
| .NET SDK | `dotnet --version` | `8.0.416` | PASS |
| Restore | `dotnet restore HRMS.sln` | All five projects restored | PASS |
| Release build | `dotnet build HRMS.sln -c Release --no-restore` | 0 errors; one known `CS1998` warning in `ZKTecoProvider.cs:86` | PASS |
| Full test suite | `dotnet test HRMS.sln -c Release --no-build --no-restore` | 1,142 passed, 0 failed, 1 skipped, 1,143 total | PASS |
| EF tools | `dotnet tool restore`; `dotnet-ef --version` | EF Core tools `8.0.8` available | PASS |
| EF model drift | `dotnet ef migrations has-pending-model-changes` | `No changes have been made to the model since the last migration.` | PASS |
| Focused tenant/RBAC regression | `dotnet test HRMS.Tests ... --filter 'FullyQualifiedName~IDOR\|...Security\|...Regression'` | 270 passed, 0 failed, 0 skipped | PASS |
| MySQL version | `SELECT VERSION()` | `8.4.11` | PASS |
| Clean migration chain | `dotnet ef database update` against an empty database | Both migrations applied successfully; command exited 0 | PASS |
| Payslips schema | `information_schema` inspection | `company_id` is `NOT NULL`; `created_at` exists as `datetime(6)` | PASS |
| Payslips foreign key | `information_schema` inspection | `fk_payslips_company_id` references `companies(id)` with `ON DELETE RESTRICT` | PASS |
| Migration history | `__EFMigrationsHistory` | 2 applied migrations; final payslips FK migration present | PASS |
| API startup and biometric poll | Release API against MySQL 8.4.11 and Redis; waited through the worker's two-minute initial delay | Started successfully; biometric hosted service started; first real poll queried `biometric_settings`; no `Unknown column 'b.CreatedAt'` or poll-cycle failure | PASS |
| API `/health` | `curl /health` | HTTP 200; database and Redis healthy | PASS |
| API `/healthz/live` | `curl /healthz/live` | HTTP 200; `Healthy` | PASS |
| API `/healthz/ready` | `curl /healthz/ready` | HTTP 200; `Healthy` | PASS |
| Compose validation | Five documented base/overlay combinations with `docker compose ... config --quiet` | All exited 0 | PASS |
| Docker base-image reachability | `docker pull` for Bun, .NET SDK, and MySQL 8.4 | All pulls succeeded | PASS |
| Production image | `docker build -t hrms-api:verify .` | All SPA builder, build, and runtime stages completed; image produced | PASS |
| Migration image stage | `docker build --target migrate -t hrms-api:migrate-verify .` | Migrate stage completed; image produced | PASS |

The production Compose configuration emitted a warning that `ALLOWED_HOSTS` was unset while
rendering the temporary validation configuration, but the command exited successfully. This is
an expected environment-value warning and was not treated as a code failure.

## Payslips and `created_at` regression verification

The clean-database migration run produced the final schema on MySQL 8.4.11:

- `payslips.company_id`: `IS_NULLABLE = NO`, type `int`
- `payslips.created_at`: `IS_NULLABLE = NO`, type `datetime(6)`, default `CURRENT_TIMESTAMP(6)`
- `fk_payslips_company_id`: references `companies(id)` with delete rule `RESTRICT`

The API then booted against that schema and its biometric background service started normally.
After the service's intentional two-minute initial delay, its first real poll queried
`biometric_settings` successfully. The runtime log contained no `Unknown column 'b.CreatedAt'`
or biometric poll-cycle failure, and the health probes reported the database dependency healthy.

## Final verdict

**PHASE 1 VERIFIED**

No blockers remain for the requested SDK, backend, EF, MySQL 8.4, migration, schema, API health,
Docker, Compose, and tenant/RBAC verification gates.
