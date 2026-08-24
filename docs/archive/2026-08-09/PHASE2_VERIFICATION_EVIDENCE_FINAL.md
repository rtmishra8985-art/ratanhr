# RatanHR Phase 2 Verification Evidence — Final

**Verification date:** 2026-08-09  
**Repository:** `RatanHR_source`  
**Evidence policy:** Every result below came from a command executed against the current extracted source tree. Historical reports were not used as proof of current status.

## Final gate table

| Gate | Status | Command / evidence | Observed result |
|---|---|---|---|
| .NET SDK | PASS | `dotnet --version` | `8.0.416`, matching `global.json` |
| Restore | PASS | `dotnet restore HRMS.sln` | Exit 0; all projects restored. The SDK printed a non-blocking workload-verification notice. |
| Targeted regression fixes | PASS | Filtered `dotnet test` for SuperAdmin cross-tenant payroll and permanent email failure | 2 passed, 0 failed |
| Full backend test suite | PASS | `dotnet test HRMS.sln --logger "console;verbosity=detailed"` | **1,142 passed, 1 skipped, 0 failed**; total 1,143 |
| EF tool restore | PASS | `dotnet tool restore` | Pinned `dotnet-ef` 8.0.8 restored |
| EF pending-model check | **FAIL / owner decision required** | `dotnet ef migrations has-pending-model-changes --context ApplicationDbContext --project HRMS.Infrastructure/HRMS.Infrastructure.csproj --startup-project HRMS.API/HRMS.API.csproj` | Build succeeded, then exited 1: `Changes have been made to the model since the last migration.` No generated migration was accepted or applied. |
| Docker image build | PASS | `docker build -t hrms:verify .` | Exit 0; image `hrms:verify` built successfully. One existing compiler warning (`ZKTecoProvider` async method without await) was emitted. |
| Base Compose config | PASS | `docker compose -f docker-compose.yml config -q` with temporary verification-only values | Exit 0 |
| Base + development override config | PASS | `docker compose -f docker-compose.yml -f docker-compose.override.yml config -q` with temporary values | Exit 0 |
| Base + backup overlay config | PASS | `docker compose -f docker-compose.yml -f docker-compose.backup.yml config -q` with temporary values | Exit 0 |
| Production Compose config | PASS | `docker compose -f docker-compose.prod.yml config -q` with temporary values | Exit 0 |
| E2E Compose config | PASS | `docker compose -f docker-compose.e2e.yml config -q` with temporary values | Exit 0 |
| E2E Compose `up --wait` | **BLOCKED — environment/runtime** | `docker compose -f docker-compose.e2e.yml up -d --wait` | MySQL and Redis processes logged ready, but Docker healthcheck `exec` calls failed with `OCI runtime exec failed: unable to start container process: error executing setns process: exit status 1: unknown`; Compose marked both unhealthy and did not start dependent services. |
| Live API `/healthz` | PASS (direct runtime probe) | API image run with host networking against published disposable MySQL/Redis ports; `curl -H 'Host: api.ratanhr.test' http://127.0.0.1:8082/healthz` | HTTP **200**; body reported `status: Healthy`, database Healthy, Redis Healthy, liveness Healthy. This is a direct runtime probe, not a claim that Compose `--wait` passed. |
| Playwright E2E | **BLOCKED — dependency/browser environment** | `npx playwright test` | Exit 1 before test execution: `Cannot find package '@playwright/test'`; no browser tests ran. The SPA workspace did not have installed Playwright dependencies. |
| Verification cleanup | PASS | `docker rm -f ...`; `docker volume rm ratanhr_source_hrms_e2e_mysql_data` | E2E containers and disposable MySQL volume removed; no verification containers/volume remained. |

## Source changes completed

- Corrected the SuperAdmin test fixture to use the canonical `AppRoles.SuperAdmin` role value.
- Corrected the email queue retry test to query the updated row through a fresh EF context rather than a stale tracked entity.
- Added `Hangfire__RedisConnectionString: "redis:6379,abortConnect=False"` to `docker-compose.e2e.yml`. The API requires a dedicated Hangfire Redis configuration outside Development; the prior E2E file supplied only the general Redis setting.
- No production authorization guard was weakened.
- No destructive EF-generated migration was created or applied.
- Temporary credentials and generated keys were used only in shell-local verification files; they were not committed to the source tree or included in the archive.

## EF snapshot decision

The current checked-in EF snapshot is not aligned with the runtime model, and the explicit EF command confirms pending model changes. This verification does **not** choose a destructive re-baseline and does **not** generate a migration. The release remains non-green until the owner chooses and reviews one of the prompt’s two supported paths:

1. Re-baseline with a real EF snapshot after schema-by-schema review, or
2. Declare the hand-authored SQL migration set authoritative and remove/replace the EF drift gate with an approved SQL-diff gate.

## Remaining non-green gates

1. **EF pending-model check:** FAIL because model changes are pending. This requires an owner-approved migration/snapshot strategy; no automatic migration was accepted.
2. **Compose `up --wait`:** BLOCKED by the current Docker runtime’s failed healthcheck process execution and bridge-network behavior. Direct host probes confirmed Redis/MySQL readiness and the API `/healthz` endpoint returned 200, but this does not erase the Compose healthcheck failure.
3. **Playwright E2E:** BLOCKED because `@playwright/test` is not installed in the SPA workspace and no browser suite executed.

## Raw transcripts

Raw command output for the major gates is stored in `evidence/session-2026-08-09/`:

- `dotnet-restore.txt`
- `dotnet-test.txt`
- `targeted-regressions.txt`
- `dotnet-tool-restore.txt`
- `ef-pending-model-changes.txt`
- `docker-build.txt`
- `compose-*-config.txt`
- `e2e-compose-up-wait.txt` and `e2e-compose-ps.txt`
- `api-healthz-response.json`
- `playwright.txt`

## Final verdict

**PHASE 2 VERIFICATION PARTIALLY COMPLETE — NOT RELEASE-GREEN.**

The .NET toolchain, restore, full backend suite, Docker image build, all five Compose syntax checks, and a direct live API health probe passed. The EF drift gate is a real failure, Compose `up --wait` is blocked by the Docker runtime’s healthcheck/network execution behavior, and Playwright E2E is blocked before test execution by missing SPA Playwright dependencies. These remaining statuses are recorded explicitly rather than inferred or simulated.
