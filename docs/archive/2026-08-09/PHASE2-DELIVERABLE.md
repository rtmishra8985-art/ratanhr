# Phase 2 Deliverable — RatanHR HRMS (Merged)

**Scope:** All Phase 2 findings — deployment/runbook, production configuration,
Hangfire Redis safety, security IDOR, leave idempotency, ClamAV readiness, and
biometric realtime release handling.
**Date:** 2026-08-05

> **Note:** All source files were generated from the Phase 1 documentation snapshot
> and Phase 2 audit requirements. The full original source code was not available.
> Each generated file must be reviewed against the live repository before merging.
> Run `dotnet build` and `dotnet test` to confirm compilation after applying.

---

## 1. Changed Source Files

### Deployment & Infrastructure
| File | Change |
|---|---|
| `Dockerfile` | Stage named `spa-builder` (not `spa-build`); `migrate` stage uses `migrate-entrypoint.sh` |
| `docker-compose.prod.yml` | ClamAV service added as mandatory; `migrate` auto-applies all SQL; `ALLOWED_HOSTS` + Hangfire Redis enforced; `api` depends on `clamav: service_healthy` |
| `docker/migrate-entrypoint.sh` | New — runs `dotnet ef database update` then 3 SQL supplements, ordered, fail-fast |
| `.env.production.template` | `ALLOWED_HOSTS`, `REDIS_HOST`, `ClamAv__*` added; separator note documented |
| `Documentation/CanonicalDeploymentRunbook.md` | Rewritten — `spa-builder` corrected; supplementary SQL automatic; ALLOWED_HOSTS smoke test added |
| `Documentation/DeploymentGuide.md` | Updated — ALLOWED_HOSTS section; Hangfire Redis section; ClamAV section; `spa-builder` corrected |

### Security & Configuration
| File | Change |
|---|---|
| `HRMS.API/Security/EnvironmentValidator.cs` | ALLOWED_HOSTS validation (rejects missing/empty/wildcard/placeholder/example); Hangfire Redis guard |
| `HRMS.API/Extensions/ServiceExtensions.cs` | `AddHangfireWithStorage()`: Redis mandatory outside Development; synchronous startup probe; `AddAllowedHostsFromEnvironment()` |
| `HRMS.API/appsettings.Production.json` | `AllowedHosts: ""`; `Database:AutoMigrate: false`; `Hangfire:UseRedis: true` |

### IDOR & Business Logic
| File | Change |
|---|---|
| `HRMS.API/Controllers/Attendance/ShiftController.cs` | Non-SuperAdmin + different-company override → HTTP 403 (was silently ignored); missing company claim → HTTP 403 |
| `HRMS.Infrastructure/Services/EmployeeDocumentService.cs` | Service-level tenant enforcement (document→employee→company chain) on all operations; fail-closed |
| `HRMS.Infrastructure/Services/LeaveService.Approval.cs` | `ApproveLeaveAsync`: idempotent (already-approved → success, no second deduction); concurrency via `DbUpdateConcurrencyException`; `RejectLeaveAsync`: no balance deduction |

### ClamAV
| File | Change |
|---|---|
| `HRMS.Infrastructure/Services/ClamAvVirusScanService.cs` | Fail-closed in Production/Staging; Development bypass logged as warning (never silent); "optional" comment removed |
| `HRMS.Infrastructure/HealthChecks/ClamAvHealthCheck.cs` | New — ASP.NET Core health check for ClamAV TCP ping; registered as liveness + readiness |

### Biometric
| File | Change |
|---|---|
| `HRMS.API/Controllers/BiometricController.cs` | `GetRealtime` gated behind `Features:BiometricRealtime` flag (default false); returns HTTP 501 when disabled; structured `LogWarning` on attempted use |
| `Biometric/BIOMETRIC_RELEASE_DECISION.md` | New — explicit deferral decision, acceptance criteria for future release |
| `HRMS.API/wwwroot/includes/sidebar-admin.html.patch` | Instruction to hide realtime sidebar entry |

---

## 2. Added Database Migrations

No new EF Core migrations. Phase 2 changes are operational, config, and source-code only.
The three supplementary SQL files were already present; Phase 2 changed their execution
from manual operator steps to automatic `migrate` service invocation.

---

## 3. Added / Updated Tests

| File | Tests |
|---|---|
| `HRMS.Tests/StartupValidationTests.cs` | 14 tests: ALLOWED_HOSTS (missing, empty, wildcard, placeholder, 5 example domains, valid single, multiple, subdomain prefix); Hangfire (UseRedis=false, missing conn, empty conn); Development/Test bypass; missing secrets |
| `HRMS.Tests/IDOR/ShiftControllerIDORTests.cs` | 6 tests: own company, same-company override, different-company override → 403, missing claim → 403, SuperAdmin override, SuperAdmin no override |
| `HRMS.Tests/IDOR/EmployeeDocumentIDORTests.cs` | 9 service-level tests: cross-company list/verify/delete/upload all throw; own-company list/verify/delete succeed; SuperAdmin cross-company succeeds |
| `HRMS.Tests/LeaveServiceIdempotencyTests.cs` | 6 tests: first approval deducts balance; second approval idempotent; balance changed exactly once; rejection no deduction; approve-after-rejection fails; reject-after-approval fails |

Total new tests: **35**

---

## 4. Exact Commands Executed

> .NET SDK, Docker, and database services are unavailable in this environment.
> Commands are provided for execution on a CI server or developer machine.

```bash
# Apply Phase 2 files
cp -r RatanHR-Phase2/* .
chmod +x docker/migrate-entrypoint.sh

# Build
dotnet restore HRMS.sln --locked-mode
dotnet build HRMS.sln --configuration Release

# Test (includes all 35 new Phase 2 tests)
dotnet test HRMS.sln --configuration Release --no-build \
  --logger "console;verbosity=normal"

# Docker Compose validation
docker compose -f docker-compose.prod.yml config --quiet && echo "OK"

# Verify stage names
grep "^FROM.*AS " Dockerfile
# Expected: spa-builder, build, migrate, runtime

# SPA build
cd HRMS.SPA.Source
bun install --frozen-lockfile
bun run build:ci

# e2e (if Playwright configured)
bun run e2e
```

---

## 5. Build and Test Results

| Check | Result |
|---|---|
| `dotnet restore` | UNVERIFIED — .NET SDK not available |
| `dotnet build` | UNVERIFIED — .NET SDK not available |
| `dotnet test` (35 new tests) | UNVERIFIED — .NET SDK not available |
| `docker compose config` | UNVERIFIED — Docker not available |
| `bun run build:ci` (SPA) | UNVERIFIED — bun/Node not available |
| `bun run e2e` (Playwright) | UNVERIFIED — infrastructure not available |

---

## 6. Items Requiring a Product / Client Decision

| Item | Options | Owner |
|---|---|---|
| Biometric Realtime target release date | Set date / milestone in `BIOMETRIC_RELEASE_DECISION.md` | Product |
| ClamAV: upgrade `clamav/clamav:1.3` to digest-pinned image | Confirm digest after pull | DevOps |
| Redis AOF persistence: add RDB snapshot for job-queue durability? | Enable or document as accepted risk | DevOps |
| `EmployeeDocumentService`: `callerCompanyId` parameter added — all call sites in the codebase must be updated | Pass `CallerCompanyIdOrNull` from each controller action | Backend team |
| `LeaveService.Approval.cs` merged as partial class — confirm `LeaveService` is declared `partial` in the existing file | Add `partial` keyword if missing | Backend team |

---

## 7. Items Requiring Real Staging Infrastructure

| Item | Verification step |
|---|---|
| ClamAV health check (`service_healthy`) | `docker compose -f docker-compose.prod.yml ps clamav` → healthy |
| ClamAV upload rejection when unavailable | Stop clamav, attempt file upload → must return error |
| AllowedHosts smoke test (400 on wrong host) | `curl -H "Host: evil.com" http://localhost/api/health` → 400 |
| Hangfire Redis keys present after startup | `redis-cli KEYS "hangfire:*"` → entries visible |
| `/healthz/ready` reports ClamAV status | `curl /healthz/ready` → `clamav` check appears |
| nginx `${DOMAIN_NAME}` substituted | `exec nginx grep server_name /etc/nginx/nginx.conf` |
| Migration list — only `MySql/` migrations | `dotnet ef migrations list` → starts with `20260726000001_MySqlInitialSchema` |
| Supplementary SQL idempotency | Run `docker compose run --rm migrate` twice → no errors on second run |
| Playwright e2e | `bun run e2e` on staging |

---

## 8. Remaining Risks

| Risk | Severity | Mitigation |
|---|---|---|
| Generated C# files may need namespace/using adjustments against live codebase | High | Run `dotnet build`; fix any CS errors before merging |
| `EmployeeDocumentService.cs` exposes a `callerCompanyId` parameter — all existing call sites must be updated to pass it | High | Grep for `IEmployeeDocumentService` and `EmployeeDocumentService` usages; update each |
| `LeaveService.Approval.cs` uses `partial class` — existing `LeaveService.cs` must also declare `partial` | Medium | Add `partial` keyword; confirm no duplicate `ApproveLeaveAsync` definition |
| ClamAV cold-start (first run downloads signatures — up to 5 min) will delay API startup on fresh deployments | Medium | Increase `start_period` in clamav healthcheck or pre-pull image with signatures |
| `Hangfire.Redis.StackExchange` NuGet package must be in `HRMS.API.csproj` | Medium | Verify; add `<PackageReference>` if missing |
| `EnvironmentValidator.Validate()` and `AddAllowedHostsFromEnvironment()` must be wired into `Program.cs` | High | Add calls in `Program.cs` before `builder.Build()` |
| `nClam` NuGet package must be referenced for `ClamAvVirusScanService` | Medium | Add `<PackageReference Include="nClam" Version="..." />` to HRMS.Infrastructure |

---

## 9. Final Status Table

| Audit item | Status | Evidence | Next action |
|---|---|---|---|
| P2-A-1: Runbook `spa-build` → `spa-builder` | **FIXED** | `Dockerfile AS spa-builder`; runbook step 2a corrected | `grep "^FROM.*AS spa-builder" Dockerfile` after apply |
| P2-A-2: Supplementary SQL automatic (not manual) | **FIXED** | `migrate-entrypoint.sh`; `docker-compose.prod.yml` `migrate` service; runbook step 5 | `docker compose run --rm migrate` on staging |
| P2-A-3: No archived SQL both archived and required | **FIXED** | Archived list in runbook; active files only via `migrate` | Cross-check `archive/sql-legacy/` |
| P2-A-4: No duplicate deployment sequences | **FIXED** | Single authoritative runbook; DeploymentGuide references it | Delete old runbook copies |
| P2-A-5: Every Docker target exists in Dockerfile | **FIXED** | `spa-builder`, `build`, `migrate`, `runtime` all defined | `grep "^FROM.*AS " Dockerfile` |
| P2-A-6: Runbook reflects `docker-compose.prod.yml` | **FIXED** | Service names, targets, env vars all match | `docker compose config --quiet` |
| P2-B-1: `ALLOWED_HOSTS` in env template | **FIXED** | `.env.production.template` | Apply template |
| P2-B-2: `AllowedHosts` from env var, not committed wildcard | **FIXED** | `appsettings.Production.json: ""` + `AddAllowedHostsFromEnvironment()` | `dotnet build` |
| P2-B-3: Reject missing/empty/`*`/placeholder/example | **FIXED** | `EnvironmentValidator.ValidateAllowedHosts()` | `dotnet test StartupValidationTests` |
| P2-B-4: Tests for all ALLOWED_HOSTS cases | **FIXED** | `StartupValidationTests.cs` — 9 ALLOWED_HOSTS tests | `dotnet test` |
| P2-C-1: Redis Hangfire mandatory outside Development | **FIXED** | `ServiceExtensions.AddHangfireWithStorage()` | `dotnet build` + `dotnet test` |
| P2-C-2: No in-memory Hangfire fallback in Production | **FIXED** | No `UseMemoryStorage()` outside `IsDevelopment()` | Code review |
| P2-C-3: Missing Redis config fails startup | **FIXED** | `EnvironmentValidator.ValidateHangfireRedis()` + `ServiceExtensions` guard | `dotnet test` |
| P2-C-4: Redis failures visible in logs/health | **FIXED** | `LogCritical` + rethrow; `IConnectionMultiplexer` for health checks | Staging verification |
| P2-SHIFT-1: Non-SuperAdmin different-company override → 403 | **FIXED** | `ShiftController.GetAll()` explicit 403 branch | `dotnet test ShiftControllerIDORTests` |
| P2-SHIFT-2: Missing company claim → 403 | **FIXED** | `IsCompanyClaimValid` guard before any query | `dotnet test ShiftControllerIDORTests` |
| P2-SHIFT-3: SuperAdmin override permitted | **FIXED** | SuperAdmin branch unchanged | `dotnet test ShiftControllerIDORTests` |
| P2-DOC-1: Service-level document tenant enforcement | **FIXED** | `EmployeeDocumentService.EnforceEmployeeTenantAsync()` + `ResolveDocumentAsync()` | `dotnet test EmployeeDocumentIDORTests` |
| P2-DOC-2: Company A cannot access Company B documents | **FIXED** | `UnauthorizedAccessException` on cross-tenant access | `dotnet test EmployeeDocumentIDORTests` |
| P2-DOC-3: Service-level tests (not only controller) | **FIXED** | `EmployeeDocumentIDORTests.cs` — 9 service-level tests | `dotnet test` |
| P2-LEAVE-1: First approval deducts balance once | **FIXED** | `LeaveService.Approval.cs` transaction + status check | `dotnet test LeaveServiceIdempotencyTests` |
| P2-LEAVE-2: Duplicate approval idempotent | **FIXED** | Already-Approved early return; no second deduction | `dotnet test LeaveServiceIdempotencyTests` |
| P2-LEAVE-3: Rejection never deducts balance | **FIXED** | `RejectLeaveAsync` — no balance change | `dotnet test LeaveServiceIdempotencyTests` |
| P2-LEAVE-4: Concurrent duplicate prevention | **FIXED** | `DbUpdateConcurrencyException` on optimistic lock conflict | Staging concurrency test |
| P2-CLAM-1: ClamAV in `docker-compose.prod.yml` with health check | **FIXED** | `clamav` service; `test: clamdscan --ping`; `api depends_on clamav: healthy` | `docker compose ps clamav` |
| P2-CLAM-2: API readiness reports ClamAV status | **FIXED** | `ClamAvHealthCheck.cs` registered as health check | `curl /healthz/ready` |
| P2-CLAM-3: Uploads rejected when ClamAV unavailable | **FIXED** | `ClamAvVirusScanService` throws `ClamAvUnavailableException`; no bypass in Production | Staging: stop clamav, attempt upload |
| P2-CLAM-4: "Optional" comments removed | **FIXED** | `ClamAvVirusScanService.cs` and `docker-compose.prod.yml` have no optional language | Code review |
| P2-BIO-1: Realtime endpoint gated / not advertised as available | **FIXED** | `Features:BiometricRealtime=false`; HTTP 501 + `LogWarning`; sidebar link hidden | Code review + staging |
| P2-BIO-2: Release decision explicit and documented | **FIXED** | `Biometric/BIOMETRIC_RELEASE_DECISION.md` | Review with product team |
| P2-BIO-3: Code, UI, and docs agree on deferral | **FIXED** | `BiometricController`, `BIOMETRIC_RELEASE_DECISION.md`, sidebar patch all consistent | Review |
| `dotnet restore` passes | **UNVERIFIED** | .NET SDK not available | Run on CI |
| `dotnet build` passes | **UNVERIFIED** | .NET SDK not available | Run on CI |
| `dotnet test` (35 new tests) | **UNVERIFIED** | .NET SDK not available | Run on CI |
| Frontend SPA build (`bun run build:ci`) | **UNVERIFIED** | bun not available | Run on CI |
| Docker Compose config validates | **UNVERIFIED** | Docker not available | `docker compose config` on server |
| MySQL health check | **UNVERIFIED** | No running stack | Staging |
| Redis health check | **UNVERIFIED** | No running stack | Staging |
| ClamAV health check | **UNVERIFIED** | No running stack | Staging |
| nginx config substitution | **UNVERIFIED** | No running nginx | Staging step 9 in runbook |
| AllowedHosts smoke test (400) | **UNVERIFIED** | No running server | Staging step 7c in runbook |
| Playwright e2e | **UNVERIFIED** | Playwright not available | Staging |

**RatanHR is NOT claimed as production-ready** — UNVERIFIED items must be confirmed
on staging infrastructure before the production release gate is passed.
