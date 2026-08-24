# Phase 1 Blocker Fix Report — 2026-08-09

## Blockers fixed in code
1. **Hangfire Redis mandatory key missing in primary stack** — `docker-compose.yml` (api service) now sets
   `Hangfire__UseRedis=true`, `Hangfire__UseInMemory=false`, and `Hangfire__RedisConnectionString`.
   `ServiceExtensions.cs` additionally falls back to `Redis:ConnectionString`, so the split-key
   configuration can no longer crash startup. Error text updated to name both keys.
2. **Stored-XSS surface (HIGH-1)** — all 75 legacy `HRMS.API/wwwroot/*.html` pages plus their
   `js/`, `css/`, `includes/` folders moved to `legacy-ui/wwwroot/` (outside the web root, no longer
   served by `UseStaticFiles()`). `Program.cs` now uses `UseDefaultFiles()` to serve the SPA's
   `index.html`; the old `/ -> /login.html` redirect was removed.
3. **No authoritative Phase 1 baseline** — added `PHASE1_BASELINE.md` (single source of truth) and a
   SUPERSEDED banner on `ORIGINAL_PHASE1_AUDIT_REPORT.md`.
4. **Environment verification** — added `scripts/verify-phase1.sh`, which runs every mandated
   build/container/schema/frontend command and prints PASS/FAIL/BLOCKED per check. The missing
   .NET 8 SDK, Docker/Compose and MySQL client are environment prerequisites and cannot be shipped
   inside the archive; run this script on a machine that has them to close blockers 1–3.

## Discrepancies resolved
- HIGH-2, HIGH-7, CRIT-2 verdicts corrected in `PHASE1_BASELINE.md` (stale/not-applicable/fixed).
  `dangerouslySetInnerHTML` occurrences in `HRMS.SPA.Source/src`: **0**.
- **Package-manager conflict** — standardised on Bun: `package-lock.json` deleted, `packageManager: bun@1.2.0`
  declared, CI switched to `oven-sh/setup-bun` + `bun install --frozen-lockfile` / `bun run *` / `bunx playwright`,
  README `pnpm` instructions replaced with `bun`.
- **Frontend dependency hygiene** — `package.json` now has a real `dependencies` block (runtime libs) with
  build/test tooling left in `devDependencies`, plus `engines` (node >=22, bun >=1.2) and `.nvmrc` (22).
- **Redis/config key split** — unified via the fallback chain
  `Hangfire:RedisConnectionString → Redis:ConnectionString → REDIS_CONNECTION_STRING`.
- **Stale migration reference** — `Employee.cs` now cites `20260726000001_MySqlInitialSchema`.
- **Dead artifacts removed** — `HRMS.Infrastructure/Redis/RedisRateLimiter.cs` (unreferenced stub),
  `docker-compose.replica.yml` (no services), `nginx/nginx.conf` (unmounted, hardcoded `YOUR_DOMAIN_NAME`;
  `nginx/nginx.conf.template` is the live config used by both Compose stacks).
- **README:40** corrected — the backend Dockerfile *does* build the SPA (`spa-builder` stage → `wwwroot`).
- **Module inventory** — corrected in `PHASE1_BASELINE.md`: 33 implemented modules; LWF missing,
  Reimbursements absent, F&F partial (EmployeeExit only), Biometric-Realtime an intentional 501 stub.

## Explicitly not done
- LWF / Reimbursements / full F&F modules are feature work, not blocker remediation; they are recorded
  as scope gaps in `PHASE1_BASELINE.md` rather than partially implemented.
- The prebuilt `HRMS.SPA/` bundle is retained because the standalone staging frontend image consumes it.

## Final pass (2026-08-09, archive `RatanHR_source_code_fixed_2026-08-09.zip`)
- **Report consolidation completed** — 97 overlapping root reports/changelogs moved to
  `docs/archive/2026-08-09/` with an index README. Root now keeps only `PHASE1_BASELINE.md`,
  `PHASE1_BLOCKER_FIX_REPORT_2026-08-09.md`, `ORIGINAL_PHASE1_AUDIT_REPORT.md` (SUPERSEDED banner),
  `README.md`, `DEPLOYMENT.md`, `DEVELOPMENT_SETUP.md`, `CHANGELOG.md`.
- **`PHASE1_BASELINE.md` expanded** — verified architecture summary, module inventory of record
  (33 implemented / 2 partial / LWF + Reimbursements absent), corrected HIGH-2 / HIGH-7 / CRIT-2
  verdicts, and an explicit config-key contract table (Redis fallback chain + `AllowedHosts` vs
  `ALLOWED_HOSTS` alias).
- **AllowedHosts alias** — `docker-compose.yml` api service now sets both `AllowedHosts` and
  `ALLOWED_HOSTS` from the same value (`AllowedHosts` → `ALLOWED_HOSTS` → `DOMAIN_NAME`), matching
  `docker-compose.prod.yml` and `AddAllowedHostsFromEnvironment()`.
- **`scripts/verify-phase1.sh` rewritten** — now runs `dotnet --version`/`--info`,
  `dotnet restore --locked-mode`, `dotnet build -c Release`, `dotnet test`, `docker --version`,
  `docker compose version`, `docker compose config` on every `docker-compose*.yml`,
  `mysql --version` + schema apply (when `MYSQL_URL` is set), the full Bun chain
  (`install --frozen-lockfile`, `typecheck`, `lint`, `vitest`, `build:ci`), `bunx playwright`,
  plus static guards. Missing tools are reported BLOCKED and never abort the run.
- **`bun.lock` regenerated** from `bun install` after the dependencies/devDependencies split;
  `bun install --frozen-lockfile` is now clean.

### Verification results in this environment
| Check | Result |
|---|---|
| `bun install --frozen-lockfile` | PASS (564 installs, no changes) |
| `bun run typecheck` | PASS (0 errors) |
| `bun run test` (vitest) | PASS — 5 files, 82 tests |
| `bun run build:ci` | PASS — built in 6.65s |
| `docker-compose*.yml` YAML parse (6 files) | PASS |
| No `dangerouslySetInnerHTML` in `HRMS.SPA.Source/src` | PASS (1 comment only) |
| No `package-lock.json` | PASS |
| No `HRMS.API/wwwroot/*.html` or `wwwroot/js/*.js` | PASS (only `wwwroot/assets/`) |

### Environment-only (BLOCKED, not code defects)
`dotnet restore --locked-mode`, `dotnet build -c Release`, `dotnet test`,
`docker compose config`/`docker build`, and MySQL schema validation could not be executed:
the .NET 8 SDK, Docker/Compose and the MySQL client are not installed in this environment.
Run `scripts/verify-phase1.sh` on a machine with those tools to close them.
