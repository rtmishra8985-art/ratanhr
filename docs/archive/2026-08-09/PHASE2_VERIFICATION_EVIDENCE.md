# RatanHR — Phase 2 Verification Evidence (Session: 2026-08-08)

Every item below reflects a command **actually executed in this session's sandbox** (or
explicitly marked as not executed). Raw output is in `evidence/session-2026-08-08/`. This
session builds on, and re-verifies rather than blindly trusts, six prior sessions' findings in
`docs/phase-2-readiness.md` and related files.

## 1. Toolchain

```
Required: .NET SDK 8.0.416 (global.json, rollForward: latestFeature, allowPrerelease: false)
Actual before this session: no SDK installed
Commands:
  $ apt-get install -y dotnet-sdk-8.0
  -> installs 8.0.129-0ubuntu1~24.04.1 (only .NET 8 SDK build in Ubuntu 24.04's apt repos;
     apt-cache madison shows only 8.0.129 and 8.0.104 as candidates)
  $ dotnet --version   (from repo root, global.json present)
  -> "A compatible .NET SDK was not found. Requested SDK version: 8.0.416"
Result: BLOCKED — ENVIRONMENT. 8.0.129 is feature band 1xx; 8.0.416 requires feature band 4xx;
  rollForward:latestFeature correctly refuses to use 8.0.129. No apt package or reachable
  download source provides an 8.0.4xx SDK in this sandbox (dot.net, builds.dotnet.microsoft.com
  both return 403 host_not_allowed).
Diagnostic-only (not the delivered state): in a throwaway copy, global.json was relaxed to
  accept 8.0.129 purely to test whether NuGet itself was reachable with any SDK present.
  `dotnet restore` against api.nuget.org still failed with NU1301 (403/host not allowed at the
  proxy level) on every project. So even a matching SDK patch would not unblock restore/build/
  test/ef — the harder blocker is NuGet registry access, not the SDK patch version. global.json
  was left untouched in the delivered repo.

docker --version / docker compose version: docker is not installed; `which docker` returns
  nothing. BLOCKED — ENVIRONMENT.
mysql / mysqld: not installed. BLOCKED — ENVIRONMENT.
node --version: v22.22.2 — PASS (real, executed).
npm --version: 10.9.7 — PASS (real, executed).
bun --version: bun is not installed and not obtainable (oven.sh / GitHub release CDN not on
  the egress allowlist). BLOCKED — ENVIRONMENT. npm + the committed package-lock.json was used
  as an equally legitimate fallback (see §Frontend below).
```
Full transcript: `evidence/session-2026-08-08/toolchain-versions.txt`.

## 2. EF Snapshot

```
Command:  dotnet ef migrations has-pending-model-changes
Result:   NOT EXECUTED — requires `dotnet ef` tool + a restored project, and dotnet restore
          fails at NU1301 (NuGet unreachable, see §1). No SDK/NuGet path in this sandbox
          reaches a state where `dotnet ef` can run at all.
Pending model changes: UNKNOWN — cannot be determined without running the command against a
          restored build. Static reading of the DbContext and migrations was not substituted
          for this per the task's explicit rule against replacing runtime EF verification
          with static inspection. BLOCKED — ENVIRONMENT.
```

## 3. Migration Verification

```
Expected migration count: 19 — source unconfirmed. No file in this repository (code, docs, or
  the six prior sessions' evidence) states or explains where "19" originates.
Repository migration count: 15 (counted directly this session by filesystem inspection of
  HRMS.Infrastructure/Migrations/MySql/, excluding .Designer.cs and any ModelSnapshot.cs;
  sequential 20260726000001 -> 20260806000001, no gaps or duplicate identifiers observed).
Database migration count: NOT VERIFIED — no MySQL instance is reachable or installable in this
  sandbox (dev.mysql.com -> 403 host_not_allowed; no local mysqld binary; Docker unavailable to
  run a `mysql` container). `SELECT * FROM __EFMigrationsHistory` was never executed against a
  real database this session.
Payslip constraints: NOT VERIFIED against a live database this session, for the same reason.
  Statically, `20260806000001_AddUniquePayslipConstraint.cs` exists in the migration list and
  is the most recent migration on disk; its actual effect on a real schema is unconfirmed.
Result: BLOCKED — ENVIRONMENT for the database-backed parts. Repository migration count (15) is
  the only sub-item verified with real execution (a filesystem listing) this session.
Owner decision still required: the origin of "19" cannot be resolved by any amount of
  additional local inspection — it requires input from whoever originally stated that number.
```

## 4. Backend Tests

```
Command:  dotnet test
Result:   NOT EXECUTED. Requires a successful `dotnet restore`, which fails at NU1301 for
          every project (NuGet unreachable — see §1). The 29 reported failing tests were not
          re-run, individually investigated, or fixed this session, because the test binaries
          cannot be built at all without package restore.
Total / Passed / Failed / Skipped: UNKNOWN — cannot be captured without running the suite.
BLOCKED — ENVIRONMENT.
```

## 5. Docker

```
Dockerfile: present at repo root; multi-stage (spa-builder -> build -> migrate -> runtime).
Build command attempted: docker build ... / docker compose build ...
Result: docker binary itself is not installed in this sandbox (`which docker` -> nothing), so
  no build step, not even a failed one, could be attempted. This is a harder blocker than the
  registry-pull failures earlier sessions hit (they at least had a Docker daemon that could
  reach the point of a 403 pulling oven/bun / mcr.microsoft.com / mysql images).
Image / Digest: none produced. BLOCKED — ENVIRONMENT.
```

## 6. Compose

```
docker compose config -q: NOT EXECUTED — no `docker` binary present in this sandbox at all
  (not even the CLI without a daemon). Prior sessions got as far as a missing `compose` plugin;
  this session doesn't have the base `docker` binary either.
Result: BLOCKED — ENVIRONMENT. Not substituted with a Python/YAML parser this session (that
  substitution was already correctly flagged as insufficient by the prior session that did it,
  in docs/phase-2-readiness.md).
```

## 7. API Runtime

```
Startup: NOT EXECUTED — requires a built HRMS.API assembly, which requires `dotnet restore`/
  `dotnet build`, both blocked by NuGet unavailability (§1, §4).
Database / Redis: NOT VERIFIED — no MySQL or Redis instance available (§3, §1).
Health: `curl -i http://localhost:<port>/api/healthz` was NOT attempted — there is no running
  API to query. BLOCKED — ENVIRONMENT for actual runtime verification.
HTTP status: N/A — no server was ever listening.

Root-cause fix applied this session (code-level, not a runtime verification):
`HRMS.SPA.Source/e2e/global.setup.ts`'s "staging API health check" requested `/api/healthz`.
Cross-checked against `HRMS.API/Program.cs` (maps `/health`, `/healthz`, `/healthz/ready`,
`/healthz/live` — never under `/api/`) and `nginx/nginx.conf` (explicit
`location = /healthz { proxy_pass http://hrms_api/healthz; }`, with `/api/` handled by a
separate, non-prefix-stripped location block for actual API traffic). `/api/healthz` does not
exist anywhere in the stack and was guaranteed to 404 on every run; the test tolerated 404 in
its accepted-status list, so it always passed without verifying anything. Changed the request
to the real `/healthz` path and tightened the assertion to require exactly `200` (removing the
204/401/404 tolerance), matching ASP.NET Core's default `HealthCheckOptions` status mapping
(Healthy/Degraded -> 200, Unhealthy -> 503), so a genuinely unhealthy or down API now fails the
check instead of silently passing. This is a root-cause fix to a wrong URL, not a weakened
assertion — the endpoint that now gets checked is one that actually exists and actually
reflects health.
Re-verified this session (not full E2E execution): `npx playwright test --list --project=setup`
successfully discovers the edited file's 7 tests (6 auth-state checks + the health check),
confirming the edit is syntactically valid TypeScript that Playwright's own loader accepts.
This is discovery only — no HTTP request was made, no browser ran, and this is explicitly NOT
claimed as API-runtime or E2E verification.
```

## 8. Playwright

```
Browser installation:
  $ npx playwright install --with-deps chromium
  E: Failed to fetch https://deb.nodesource.com/node_22.x/dists/nodistro/InRelease  403 Forbidden
  Failed to install browsers / Installation process exited with code: 100
  BLOCKED — ENVIRONMENT (apt dependency step fails before even reaching Playwright's own
  browser-binary CDN).
Tests: NOT EXECUTED (`npx playwright test` was not run against real browsers this session).
Passed / Failed / Skipped: N/A — no run occurred.
What WAS verified for real: `npx playwright install --with-deps chromium` was actually
  attempted (not assumed blocked) and failed with the exact error above; `npx playwright test
  --list --project=setup` (test discovery, not execution) succeeded and lists all 7 setup
  tests including the corrected health check from §7.
```

## 9. OpenTelemetry

```
Packages: OpenTelemetry.Instrumentation.EntityFrameworkCore, Exporter.Prometheus.AspNetCore,
  Instrumentation.StackExchangeRedis — all pinned at 1.17.0-beta.1, version-aligned with the
  seven other stable 1.17.0 OpenTelemetry packages in the same project.
Decision: keep as documented, intentional exceptions — see OPENTELEMETRY_DECISION.md.
Reason: none of the three has ever shipped a stable release upstream; dropping them removes
  EF/Prometheus/Redis observability entirely rather than trading up to a stable equivalent.
```

## 10. Replica Compose

```
File: docker-compose.replica.yml
Purpose: documentation-only placeholder. Its entire content (read directly this session) is a
  comment block explaining the file previously implemented PostgreSQL WAL-streaming
  replication, that this is inapplicable now the project is on MySQL, and that MySQL replica
  setup (Group Replication or async replication) belongs at the infrastructure level, pointing
  to Documentation/MySqlMigrationGuide.md and the Database__EnableReadReplica /
  Database__ReplicaConnection settings consumed by
  HRMS.Infrastructure/Data/ReadReplicaDbContext.cs.
Decision: RESOLVED — not empty, not accidental, not obsolete. It is an intentional
  documentation stub correctly left in place; no action needed. (This corrects/updates the
  Session 5/6 finding of "empty," which no longer matches the file's current content.)
```

## 11. Remaining Blockers

```
BLOCKED — ENVIRONMENT:
  - .NET SDK 8.0.416 unobtainable (only 8.0.129/8.0.104 available via apt; dot.net and
    builds.dotnet.microsoft.com are not on the egress allowlist).
  - dotnet restore/build/test/ef — api.nuget.org not on the egress allowlist (confirmed via
    both a direct curl probe and an actual `dotnet restore` NU1301 failure).
  - docker — binary not installed, no daemon; Docker Hub / mcr.microsoft.com also not on the
    egress allowlist.
  - Live MySQL migration/constraint verification — dev.mysql.com not on the allowlist, no local
    mysqld, Docker unavailable to run a container.
  - ASP.NET Core API runtime/health verification — depends on a build that cannot happen (see
    dotnet restore above).
  - Playwright E2E execution — browser binaries unobtainable (apt dependency fetch from
    deb.nodesource.com returns 403; Playwright's own browser CDN was never reached because the
    apt step fails first).

BLOCKED — EXTERNAL DEPENDENCY:
  - None beyond the above (all failures traced to this sandbox's specific egress allowlist, not
    to genuine upstream outages).

BLOCKED — CODE:
  - None identified this session that is fixable without the blocked toolchain — the one
    concrete code bug found and fixed (wrong health-check URL, §7) was fixed in this session.

OWNER DECISION REQUIRED:
  - Source of the expected "19" migrations (repository has 15; no document anywhere states or
    explains 19).

RESOLVED THIS SESSION:
  - docker-compose.replica.yml purpose (§10) — confirmed intentional documentation placeholder.
  - /api/healthz false-positive in global.setup.ts (§7) — fixed to check the real /healthz
    endpoint and require 200, not tolerate 404.
  - OpenTelemetry prerelease package decision (§9) — reconfirmed, documented.
  - Frontend/SPA toolchain — independently re-verified for real via npm (install, typecheck,
    lint, 82/82 unit tests, production build all pass). See
    evidence/session-2026-08-08/frontend-verification.txt.
```

---

# PHASE 11 — FINAL RELEASE-GATE DECISION

| Gate | Status | Evidence |
|---|---|---|
| Required .NET SDK (8.0.416) | BLOCKED | `evidence/session-2026-08-08/toolchain-versions.txt` — apt provides only 8.0.129/8.0.104; dot.net/builds.dotnet.microsoft.com return 403 |
| Docker CLI | BLOCKED | `which docker` empty; not installed, not installable via allowed apt/network sources this session |
| Docker Compose | BLOCKED | depends on Docker CLI above; never attempted beyond confirming the binary is absent |
| EF snapshot (`has-pending-model-changes`) | BLOCKED | requires `dotnet ef` on a restored project; `dotnet restore` fails NU1301 (NuGet host not allowed) |
| Migration count | FAIL (repo=15 vs claimed 19) / BLOCKED (DB count) | filesystem count of `HRMS.Infrastructure/Migrations/MySql/` = 15; no DB reachable to cross-check |
| Payslip constraint | BLOCKED | no MySQL instance reachable this session |
| Backend tests | BLOCKED | `dotnet restore` fails before `dotnet test` can even build |
| Docker build | BLOCKED | Docker CLI not present |
| Compose config | BLOCKED | Docker CLI not present |
| API startup | BLOCKED | no build artifact exists (restore blocked) |
| `/api/healthz` (E2E setup check) | FIXED (code) / BLOCKED (runtime confirmation) | `HRMS.SPA.Source/e2e/global.setup.ts` now checks the real `/healthz` route and requires HTTP 200; Playwright test-discovery confirms the file is valid, but no live API exists in this sandbox to run the check against |
| Playwright E2E | BLOCKED | `npx playwright install --with-deps chromium` fails (deb.nodesource.com 403); real execution never attempted |
| OpenTelemetry | RESOLVED | `OPENTELEMETRY_DECISION.md` — keep all three prerelease packages, no stable equivalents exist |
| Replica compose | RESOLVED | `docker-compose.replica.yml` confirmed to be an intentional documentation placeholder, not empty/accidental |

## FINAL VERDICT

### `PHASE 2 VERIFICATION BLOCKED BY ENVIRONMENT`

The one code-level defect this session could identify and fix without a working .NET/Docker/
MySQL/Playwright-browser toolchain — the false-positive `/api/healthz` check in
`HRMS.SPA.Source/e2e/global.setup.ts` — has been fixed at the source and re-checked as far as
this sandbox allows (syntax validity + Playwright test discovery). The frontend/SPA toolchain
has been independently re-verified for real (install, typecheck, lint, 82/82 unit tests,
production build). Two of the six "Open Owner Decisions" from the task brief are resolved
(`docker-compose.replica.yml` purpose; OpenTelemetry prerelease packages).

Every gate that requires the .NET SDK, NuGet, Docker, a live MySQL instance, or Playwright
browser binaries remains genuinely unexecuted, because none of those hosts are reachable from
this sandbox's network egress allowlist — confirmed by direct probes (`403 host_not_allowed`)
and by an actual attempted `dotnet restore` (`NU1301`) and an actual attempted Playwright
browser install (`403` on the apt dependency step), not assumed. This is not a code-quality
verdict on the 29 reported backend test failures, EF snapshot drift, or database migration
state — those remain **unknown** and unresolved because they cannot be exercised here, not
because they were checked and found acceptable.

**What would move this to a real PASS/FAIL verdict:** run this same repository's Phase 1–8
commands (`dotnet restore/build/test`, `dotnet ef migrations has-pending-model-changes`,
`docker build`, `docker compose config -q`, a live MySQL query against
`__EFMigrationsHistory` and the payslip constraint, the actual API startup + `/healthz` curl,
and `npx playwright test`) on infrastructure with access to `api.nuget.org`,
`mcr.microsoft.com`/Docker Hub, a MySQL source, and the Playwright browser CDN — none of which
this sandbox's egress allowlist permits.

---

# ADDENDUM — Claude (Anthropic) sandbox re-check, same date 2026-08-08

The user asked Claude to run the Replit-agent prompt above. Claude's own tool sandbox was
probed directly rather than assumed, with the following real, reproducible results — which
turn out to match this session's findings almost exactly, confirming the blockers are a
property of "restricted sandbox," not of any one specific tool vendor.

## Network probe (executed)
```
$ curl -sS -m5 -o /dev/null -w "%{http_code}" https://mcr.microsoft.com        -> 403
$ curl -sS -m5 -o /dev/null -w "%{http_code}" https://hub.docker.com           -> 403
$ curl -sS -m5 -o /dev/null -w "%{http_code}" https://dot.net                  -> 403
$ curl -sS -m5 -o /dev/null -w "%{http_code}" https://builds.dotnet.microsoft.com -> 403
$ curl -sS -m5 -o /dev/null -w "%{http_code}" https://playwright.download.prod.cdn.azure.com
  -> DNS resolution failure (not even reachable to get a 403)
$ curl -sS -m5 -o /dev/null -w "%{http_code}" https://api.nuget.org            -> 403
$ curl -sS -m5 -o /dev/null -w "%{http_code}" https://registry.npmjs.org       -> 200 (reachable)
$ curl -sS -m5 -o /dev/null -w "%{http_code}" https://pypi.org                 -> 200 (reachable)
$ which docker dotnet   -> neither installed; no daemon
```
Result: **BLOCKED — ENVIRONMENT**, same category as this session, for every .NET/Docker/MySQL/
Playwright-browser gate. Confirmed by direct probe, not assumed.

## What Claude actually executed for real (frontend/SPA, re-verifying this session's claim)
```
$ npm ci --no-audit --no-fund        -> added 574 packages, real install, no errors
$ npm run typecheck                  -> tsc -p tsconfig.json --noEmit: 0 errors
$ npm run lint                       -> eslint ... --max-warnings 0: 0 errors/warnings
$ npm run test -- --run              -> Test Files 5 passed (5); Tests 82 passed (82)
$ npm run build:ci                   -> vite build succeeded, dist/public/assets/* emitted,
                                          "✓ built in 11.66s"
```
This independently reproduces the "82/82 unit tests, typecheck/lint/build all pass" claim in
§11 above with a second, separate execution — not just re-reading the prior transcript.

## New finding — a genuine EF model/migration inconsistency found by source inspection

**This is static inspection, not a substitute for `dotnet ef migrations has-pending-model-changes`
— it does not settle the question, but it is concrete evidence pointing at a real problem**,
found because the prior session's snapshot search pattern was too narrow:

- `PHASE2_REMEDIATION_BASELINE.md` §2 states: "No `ModelSnapshot.cs` file exists in the repo
  (searched `find . -iname "*ModelSnapshot.cs"` — zero results)." That search pattern only
  matches filenames literally ending in `ModelSnapshot.cs`. The actual file is named
  `HRMS.Infrastructure/Migrations/MySql/ApplicationDbContextModelSnapshot_MySql.cs` (suffix
  `_MySql.cs`, not `ModelSnapshot.cs`), so it was never found by that search. It does exist —
  1307 lines, a real EF `ModelSnapshot` subclass tied to `ApplicationDbContext`.
- Comparing that checked-in snapshot against the real entity classes shows the snapshot is a
  drastically reduced stub of the actual model. Example — `payslips`:
  - Snapshot (`ApplicationDbContextModelSnapshot_MySql.cs` line ~1283-1290) declares only
    3 properties for `payslips`: `id`, `employee_id`, `company_id`, and **no index or unique
    constraint** on it.
  - The real entity, `HRMS.Domain/Entities/Payroll/Payslip.cs`, declares 25 properties
    (`Month`, `Year`, `BasicPay`, `HRA`, `DA`, `GrossEarnings`, `PFEmployee`, `TDS`, etc.).
  - The most recent migration, `20260806000001_AddUniquePayslipConstraint.cs`, creates a real
    unique index `ux_payslips_employee_month_year` on `(employee_id, month, year)` — which the
    checked-in snapshot does not reflect at all.
- This pattern (stub snapshot vs. full entity) is not limited to `payslips`; `leave_types`,
  `audit_logs`, and `timesheets` in the same snapshot file show the same reduction to 1-3
  properties each.
- **Implication:** a snapshot this far out of sync with the actual `DbContext`/entity classes
  is a strong signal that `dotnet ef migrations has-pending-model-changes` would very likely
  report pending changes (i.e., fail), and/or that this snapshot was hand-authored/truncated
  rather than machine-generated by `dotnet ef migrations add`. **This cannot be confirmed as
  PASS or FAIL without actually running `dotnet ef` against a restored project — which remains
  blocked in every sandbox tried so far (this one included).** Flagging it here as a concrete
  lead for whoever next has real NuGet/dotnet access, rather than leaving Phase 2's EF gate as
  an unexplained blank.

## OpenTelemetry decision — re-confirmed with a live web search (new information, not available to prior offline sessions)
A web search against NuGet's live package listings (July 2026 data) confirms
`OpenTelemetry.Instrumentation.EntityFrameworkCore` was still shipping only prerelease
versions as of its most recent release (`1.16.0-beta.1`/`1.17.0-beta.1` lines, last updated
mid-2026), with no stable tag. `OPENTELEMETRY_DECISION.md`'s "no stable equivalent exists"
conclusion holds; no change recommended.

## Follow-up — attempted to actually install the missing toolchain via apt (Ubuntu archives are allowed), not just probe for it

The user asked to "fix remaining blockers." Rather than assume they're unfixable, each was
attempted for real:

```
$ apt-get install -y dotnet-sdk-8.0
  -> installs 8.0.129-0ubuntu1~24.04.1 (apt's only 8.0 SDK; band 1xx)
  $ dotnet --version  (repo's global.json present, requires 8.0.416, rollForward:latestFeature)
  -> "A compatible .NET SDK was not found. Requested SDK version: 8.0.416" — STILL BLOCKED,
     reproduced identically to the prior session.

  Isolated test (throwaway copy, global.json relaxed to 8.0.129, NOT applied to delivered repo):
  $ dotnet restore HRMS.API/HRMS.API.csproj
  -> NU1301: Unable to load the service index for source https://api.nuget.org/v3/index.json
  -> Confirms the real wall is NuGet network access, not the SDK feature band — matches the
     prior session's diagnostic exactly.

$ apt-get install -y docker.io
  -> installs docker.io 29.1.3; NEW: dockerd actually starts and `docker version` reports a
     working client+server (prior sessions never had the docker binary at all)
  $ docker pull hello-world
  -> "403 Forbidden" resolving registry-1.docker.io — Docker Hub pull itself is blocked, so
     `docker build` (needs mcr.microsoft.com/dotnet/sdk + aspnet base images) remains BLOCKED,
     one layer further than before but still blocked.

$ apt-get install -y mysql-server
  -> installs mysql-server 8.0.46-0ubuntu0.24.04.3
  $ service mysql start && mysqladmin ping
  -> "mysqld is alive" — NEW: a real local MySQL 8.0.46 instance genuinely runs in this
     sandbox. Prior sessions assumed MySQL was unobtainable because dev.mysql.com was blocked;
     they never tried the Ubuntu-archive package, which needs no external host at all.
  However: applying the repo's 15 C# EF migrations to this real instance still requires
  `dotnet ef database update` (or `dotnet ef migrations script`), both of which need a
  successful `dotnet restore` — blocked above. A real MySQL server existing does not by itself
  unblock migration/`__EFMigrationsHistory`/payslip-constraint verification.

$ npx playwright install --with-deps chromium  (re-attempted)
  -> same as prior session: deb.nodesource.com dependency fetch returns 403 before Playwright's
     own browser CDN is ever reached. STILL BLOCKED.
```

### Net effect
Two blockers moved from "tool literally not installed" to "tool installed and running, but the
external registry/package host it needs is still not reachable": Docker (daemon runs, Docker
Hub pull is 403) and MySQL (server runs for real, but migrations can't be applied without
`dotnet ef`, which needs NuGet, which is 403). The .NET SDK band mismatch and NuGet block, and
the Playwright browser CDN block, are unchanged and are the two remaining root blockers that
gate almost everything else (build, test, EF, API startup, Docker build, E2E). None of these
are fixable from inside this sandbox — they require this environment's network egress
allowlist to include `api.nuget.org`, Docker Hub/`mcr.microsoft.com`, and the Playwright
browser CDN, which is a platform/infrastructure setting, not something reachable by
apt-installing more packages or editing repository code.

## Verdict — unchanged
### `PHASE 2 VERIFICATION BLOCKED BY ENVIRONMENT`
Claude's sandbox hits the identical wall as every prior session for the .NET/Docker/MySQL/
Playwright-browser gates — confirmed by direct probe, not assumed. The only genuinely new
contributions this pass: (1) a second, independent real execution of the full frontend/SPA
toolchain (matches prior 82/82 result), and (2) a concrete, source-level EF snapshot/model
drift finding that the prior search missed, which is a real lead but explicitly **not** a
substitute for actually running `dotnet ef migrations has-pending-model-changes`. Backend
tests, migration-vs-database state, Docker build, Docker Compose validation, live API startup,
and Playwright E2E remain **unknown**, not passed — they still require an environment with
real access to `api.nuget.org`, `mcr.microsoft.com`/Docker Hub, a MySQL source, and the
Playwright browser CDN.

