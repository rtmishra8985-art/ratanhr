# Phase 1 Authoritative Baseline (2026-08-09)

This file is the **single authoritative Phase 1 baseline**. All other root-level
`*_REPORT.md` / `*_CHANGELOG*.md` files are historical and MUST NOT be used as a
verdict source, including `ORIGINAL_PHASE1_AUDIT_REPORT.md` (self-marked
SUPERSEDED). The ~100 historical root reports were archived to
`docs/archive/2026-08-09/` on this date; only `PHASE1_BASELINE.md`,
`PHASE1_BLOCKER_FIX_REPORT_2026-08-09.md`, `README.md`, `DEPLOYMENT.md`,
`DEVELOPMENT_SETUP.md` and `CHANGELOG.md` remain at the repository root.

## Stack of record
- Backend: .NET 8 (SDK 8.0.416 per `global.json`), EF Core 8, Pomelo MySQL 8.4, Clean Architecture
- Jobs: Hangfire + Redis (Redis mandatory outside Development)
- Frontend: React 18 + Vite 6, **Bun** as the only package manager (`bun.lock`)
- Infra: multi-stage Dockerfile (spa-builder → build → migrate → runtime), nginx via `nginx/nginx.conf.template`

## Verified architecture summary
- `HRMS.Domain` — entities, enums, domain contracts; no infrastructure references.
- `HRMS.Application` — DTOs, interfaces, validators and `ApiResponse` **only**; it holds no service
  implementations. All 57 service implementations live in `HRMS.Infrastructure/Services`.
- `HRMS.Infrastructure` — EF Core 8 `ApplicationDbContext` (the earlier `HrmsDbContext` name is wrong;
  the repository defines `ApplicationDbContext`), Pomelo MySQL 8.4 provider, migrations
  (baseline `20260726000001_MySqlInitialSchema`), Redis, Hangfire job implementations.
- `HRMS.API` — controllers, JWT auth, `Extensions/ServiceExtensions.cs` composition root,
  `Program.cs` pipeline (`UseDefaultFiles()` → `UseStaticFiles()` serving the built React SPA).
- `HRMS.SPA.Source` — React 18 + Vite 6 SPA; built by the Dockerfile `spa-builder` stage and
  copied into `HRMS.API/wwwroot`. `HRMS.SPA/` holds the prebuilt bundle for standalone hosting.
- `HRMS.Tests` — unit/integration tests. Legacy server-rendered pages live in `legacy-ui/wwwroot/`
  and are **not** served. Page count of record: **72** `.html` files under `legacy-ui/wwwroot/`
  (the "75 pages" figure in the blocker-fix report is wrong).

## Module inventory of record
- **33 implemented** modules (the "17 modules" figure in older reports is wrong).
- **5 partial:** Full & Final Settlement (EmployeeExit only), Biometric-Realtime (intentional
  HTTP 501 stub), Provident Fund (PF), ESIC, and Professional Tax (PT) — the statutory three are
  calculation-only (`IndianPayrollCalculator`) with no returns/challan or filing workflow.
- **Absent:** LWF (Labour Welfare Fund), Reimbursements.

## Config key contract
| Concern | Resolution order |
|---|---|
| Redis (Hangfire storage) | `Hangfire:RedisConnectionString` → `Redis:ConnectionString` → `REDIS_CONNECTION_STRING` |
| Hangfire storage mode | `Hangfire:UseRedis` / `Hangfire:UseInMemory` (in-memory only in Development) |
| Host filtering | `AllowedHosts` config key; `ALLOWED_HOSTS` is the env alias read by `AddAllowedHostsFromEnvironment()`. `docker-compose.yml` sets both to the same value, defaulting to `DOMAIN_NAME`. |

Double-underscore form is used in Compose (`Hangfire__RedisConnectionString`).

## Corrected finding verdicts
| Finding | Baseline verdict |
|---|---|
| HIGH-1 legacy wwwroot innerHTML | FIXED — pages archived to `/legacy-ui`, no longer served |
| HIGH-2 Leave IDOR | FIXED at DB layer (`LeaveService.cs:210-220`); earlier "CRITICAL / NOT VERIFIED" rating is stale |
| HIGH-7 temp password in Serilog | NOT APPLICABLE — no such logging exists in the codebase |
| CRIT-2 `dangerouslySetInnerHTML` | FIXED — zero occurrences in `HRMS.SPA.Source/src` (only a comment in `components/ui/chart.tsx:67`) |
| CRIT-1 `Employee.CompanyId` NOT NULL | FIXED in migration `20260726000001_MySqlInitialSchema` |

## Known scope gaps (documented, not defects)
- **LWF (Labour Welfare Fund):** not implemented. Planned for a later phase.
- **Reimbursements:** referenced only; no module.
- **Full & Final Settlement:** partial (EmployeeExit only).
- **PF / ESIC / PT:** partial — payroll calculation only; no statutory returns, challans or filings.
- **Biometric-Realtime:** intentional HTTP 501 stub.
- Module count of record: **33 implemented** modules (the "17 modules" figure is wrong).

## Environment verification
Build/container/schema claims require .NET 8 SDK, Docker + Compose, and a MySQL
client. Run `scripts/verify-phase1.sh` on a machine that has them; it prints a
PASS/BLOCKED line per mandated command.

## Phase 1 blocker closure (2026-08-09, second pass)
| Blocker | Status |
|---|---|
| `ILeaveService` consumed by `LeaveController` but unregistered | FIXED — `services.AddScoped<ILeaveService, LeaveService>()` added to `ServiceExtensions.cs` |
| Unregistered `Stub*Service` classes compiled into Infrastructure | FIXED — `HRMS.Infrastructure/Services/StubServices.cs` (31 stub classes) deleted |
| Duplicate unreachable `AddHangfireWithStorage` path | FIXED — removed; `AddHangfireJobs()` is the single entry point |
| LOW-1 no secret scanning in CI | FIXED — `secret-scan` (gitleaks) job added to `.github/workflows/ci.yml` |
| HIGH-6 nginx CIDR restriction for `/hangfire` | FIXED — internal-CIDR `location /hangfire` block in `nginx/nginx.conf.template` |
| Root `.env.example` missing | FIXED — added, matching the keys written by `scripts/generate-secrets.sh` |
| Bun version drift (Dockerfile 1.2-alpine / CI 1.2.0 / e2e 1.3.6) | FIXED — pinned to `1.2.0` everywhere |
| README RedisRateLimiter / layer description | FIXED — Redis section reworded; Application/Infrastructure layer descriptions corrected |
| `typescript: 6.0.3` pin | VALIDATED — 6.0.3 is a real published release on the npm registry; pin retained |
| .NET 8 SDK / Docker + Compose / MySQL 8.4 client not installed | ENVIRONMENT — cannot be shipped in an archive. Run `scripts/verify-phase1.sh` on a host with them installed to close these. |
