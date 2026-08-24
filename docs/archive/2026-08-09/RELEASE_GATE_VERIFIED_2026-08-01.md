# RatanHR Release Gate Verification

**Verification date:** 2026-08-01  
**Source:** `ratanhr-fixed-v5-updated` working copy  
**Verdict:** **NOT READY FOR PRODUCTION RELEASE**

This report supersedes historical audit claims where they conflict with the
checks below. No production database or real customer data was modified.

## Verified passes

| Area | Result | Evidence |
|---|---|---|
| .NET SDK | PASS | .NET SDK 8.0.416 |
| Backend restore | PASS | `dotnet restore HRMS.sln` |
| Backend Release build | PASS | 0 errors, 0 warnings |
| Backend tests | PASS | 930 passed, 0 failed, 0 skipped |
| Webhook coverage | PASS | Previously skipped webhook/outbox/dispatcher tests are active and passing |
| Docker Compose syntax | PASS | `docker compose config --quiet` |
| Compose service graph | PASS | MySQL, Redis, migration, API, observability, backup, and shared `hrms_internal` network resolve |
| Compose fixes | PASS | Email URL brace, Grafana password quoting, backup shell escaping, and network declaration corrected |
| Development Hangfire startup | PASS | Development mode reaches Hangfire in-memory server initialization and starts workers |
| Frontend source audit | PARTIAL | Source, scripts, Vitest config, and `bun.lock` inspected; dependencies were not installed in the archive workspace |

## Release blockers

### 1. Production Hangfire/MySQL storage is binary-incompatible

The API's production branch uses `Hangfire.MySql.Core` 2.2.5. During a real
startup probe with disposable RSA keys, the process failed while constructing
Hangfire storage:

```text
Could not load type
'MySql.Data.MySqlClient.MySqlConnection'
from assembly 'MySqlConnector, Version=2.0.0.0'
```

The project resolves modern `MySqlConnector` 2.3.5 for Pomelo EF Core, while the
published Hangfire adapter is a legacy package built against an obsolete
connector API. The documented `Hangfire.MySql.Core` 2.4.0 version is not
available on NuGet; 2.2.5 is the latest published version.

**Safe change made:** Development configuration now explicitly sets
`Hangfire:UseInMemory=true`, allowing local/test startup without pretending that
production distributed job storage is fixed.

**Required before release:** Replace the production Hangfire storage adapter
with a maintained implementation that is compatible with Hangfire 1.8.x and the
resolved MySQL connector, then verify job persistence and recurring jobs against
real MySQL.

### 2. Real MySQL and Redis runtime verification is outstanding

Docker is available, but no project MySQL/Redis containers or images were
running during this verification. The host-only API probe therefore could not
complete the database-dependent startup path or return live health responses.
Compose syntax and configuration were validated, but connectivity, migrations,
readiness, login, rate limiting, and Redis-backed behavior remain unverified.

### 3. Frontend build and tests are outstanding

`HRMS.SPA.Source` contains a `bun.lock` and scripts for typecheck, build, lint,
Vitest, and Playwright, but its dependencies are not installed in the extracted
workspace. Frontend build/test results must not be inferred from historical
reports.

### 4. Realtime biometric provider remains intentionally incomplete

`RealtimeProvider` still reports that the provider is not integrated and the
biometric sync endpoint returns HTTP 501 for that vendor. This is a known
feature limitation and must be accepted explicitly or completed before a
release that promises Realtime support.

## Source changes in this working copy

- Re-enabled and repaired webhook outbox, filtering, inactive-subscription,
  HMAC, dispatcher delivery/retry, and SSRF tests.
- Corrected Docker Compose interpolation and declared the shared internal
  network.
- Added an explicit Development-only Hangfire in-memory setting because the
  legacy production MySQL adapter cannot load with the current connector.
- Preserved the original uploaded ZIP unchanged.

## Recommended release sequence

1. Select and validate a compatible persistent Hangfire/MySQL adapter.
2. Start the full Compose stack with non-production test credentials.
3. Run migration, MySQL, Redis, API health, authentication, rate-limit, and
   background-job checks.
4. Install frontend dependencies from the committed `bun.lock` and run
   typecheck, build, lint, Vitest, and the required Playwright checks.
5. Resolve or explicitly exclude the Realtime biometric capability.
6. Re-run this gate with real runtime evidence before publishing.