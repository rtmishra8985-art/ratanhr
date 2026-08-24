# PHASE 3 — Runtime & Container Verification

Date: 2026-08-10
Source baseline: `hrms-phase2-fixed-2026-08-10.zip` (Phase 2 PASS — build 0 errors / 0 warnings, 1142 tests passing)

## PHASE 3 STATUS: BLOCKED (execution) — carry-over item 4 RESOLVED (code)

### Blocker (environmental, not code)

The audit sandbox is gVisor-backed with no Docker CLI and no Docker daemon:

```
$ which docker docker-compose dotnet bun
which: no docker … / no docker-compose … / no dotnet … / /bin/bun
$ docker info
bash: docker: command not found
$ uname -a
Linux … 4.19.0-gvisor #1 SMP … x86_64 GNU/Linux
$ dmesg | head -1
[    0.000000] Starting gVisor...
```

Per the audit constraint, no chroot/skopeo/podman simulation was attempted. Every
container-dependent Phase 3 item therefore remains unexecuted:

| # | Item | Status |
|---|------|--------|
| 1 | `docker build --target spa-builder .` | BLOCKED — no daemon |
| 2 | `docker build .` (spa-builder → build → migrate → runtime) | BLOCKED — no daemon |
| 3 | `bun run build:ci` inside `oven/bun:1.2.0-alpine` | BLOCKED — no daemon |
| 5 | `docker compose … up -d` (MySQL + Redis + API) | BLOCKED — no daemon |
| 6 | EF Core migrations via the migrate stage | BLOCKED — no daemon, no .NET SDK |
| 7 | `/health`, `/health/ready`, `/swagger` status codes | BLOCKED — no runtime |
| 8 | Redis connectivity outside Development | BLOCKED — no runtime |
| 9 | e2e suite (`docker-compose.e2e.yml`) | BLOCKED — no daemon |
| 10 | Container logs for non-zero exits | BLOCKED — no runtime |

**Unblock requirement:** a host with a real rootful Docker daemon (runc, not runsc/gVisor)
and .NET SDK 8.0.416 (`global.json` pinned). No other change is needed.

### One-command re-run on a real host

```bash
./scripts/verify-phase3.sh          # full run; writes evidence/phase3/*.txt
./scripts/verify-phase3.sh --down   # teardown + delete throwaway env files
```

`scripts/verify-phase3.sh` (new in this drop) performs, in order, and captures full
unabridged output per step:

0. Refuses to run under gVisor / a runsc-backed daemon (no simulation fallback).
0b. Generates a throwaway `.env.phase3` (random MySQL/Redis passwords, freshly
   generated RS256 JWT keypair, AES-256 encryption key), `chmod 600`, gitignored,
   deleted by `--down`. Nothing is ever committed.
1. `docker build --target spa-builder .`
2. `bun run build:ci` executed directly inside `oven/bun:1.2.0-alpine`
   (`bun install --frozen-lockfile`; Bun only, never npm).
3. `docker build --target build|migrate|runtime` plus the full `docker build .`
4. `docker compose -f docker-compose.yml -f docker-compose.override.yml up -d`
   for `mysql`, `redis`, `migrate`, `api`.
5. Applies **existing** EF Core migrations through the migrate stage and asserts
   exit 0. It never runs `migrations add` and never touches
   `HRMS.Infrastructure/Migrations`.
6. `GET /health`, `/health/ready`, `/swagger` — records exact status codes.
7. Redis verified with `ASPNETCORE_ENVIRONMENT=Staging` (explicitly **not**
   Development): container env assertion + `redis-cli PING` + the redis entry in
   the readiness report.
8. e2e stack via `scripts/e2e-up.sh` then `bun run e2e`.
9. Dumps full logs for any service that exited non-zero.

Exit code: 0 = PASS, 1 = FAIL, 2 = BLOCKED, with explicit blockers printed.

## Carry-over item 4 — deprecated packages

### A. FluentValidation.AspNetCore 11.3.0 → FluentValidation 11.x DI pattern — APPLIED

Audit finding: `FluentValidation.AspNetCore` was referenced only in
`HRMS.API/HRMS.API.csproj`. `AddFluentValidation(...)` is called nowhere in the
codebase, and `HRMS.Application` already ships `FluentValidation` 11.9.2 +
`FluentValidation.DependencyInjectionExtensions` 11.9.2 with
`AddValidatorsFromAssembly`. The deprecated package contributed nothing but a
deprecation warning and a stale 11.5.1 floor.

```diff
--- a/HRMS.API/HRMS.API.csproj
+++ b/HRMS.API/HRMS.API.csproj
-    <PackageReference Include="FluentValidation.AspNetCore" Version="11.3.0" />
+    <PackageReference Include="FluentValidation" Version="11.9.2" />
+    <PackageReference Include="FluentValidation.DependencyInjectionExtensions" Version="11.9.2" />
```

Lock files updated to match exactly what `dotnet restore` emits (required because
the Dockerfile build stage runs `dotnet restore --locked-mode`):

- `HRMS.API/packages.lock.json` — `FluentValidation.AspNetCore` entry removed;
  `FluentValidation` and `FluentValidation.DependencyInjectionExtensions`
  promoted `Transitive` → `Direct`, `requested: "[11.9.2, )"`, resolved 11.9.2,
  content hashes unchanged.
- `HRMS.Tests/packages.lock.json` — `FluentValidation.AspNetCore` transitive entry
  removed and the `hrms.api` project dependency map updated to the two direct
  references.

No Domain/Application/Infrastructure/API **source** file was modified. Zero
behavioural change: MVC auto-validation was never enabled.

Validation still owed on a real host (blocked here, covered by check 3 of the
script): `dotnet restore HRMS.sln --locked-mode` and `dotnet build -warnaserror`.
If the lock hashes are ever rejected, regenerate with
`./scripts/generate-lock-file.sh`.

### B. xunit 2.9.0 → xunit v3 — NOT APPLIED (deliberate)

Recommendation: defer. xunit v3 converts the test project into a self-executing
console app (`<OutputType>Exe</OutputType>`), moves `ITestOutputHelper` out of
`Xunit.Abstractions`, changes `IAsyncLifetime` to `ValueTask`, and interacts with
this project's `TreatWarningsAsErrors=true` plus the `xUnit1031` suppression. That
is a multi-hour migration across 1142 tests that cannot be compiled or run in this
environment, and applying it blind would invalidate the Phase 2 green baseline
mid-audit. xunit 2.9.0 carries a deprecation notice only — no security advisory.
Proposed diff is retained in the Phase 3 chat record and can be applied on request
once Phase 3 runtime verification is green.

## Also added in this drop

- `scripts/verify-phase3.sh` — the runner described above.
- `.gitignore` — was absent from the Phase 2 archive. Ignores `.env`/`.env.*`
  (allowing only `*.example`), `*.pem`, build output, `node_modules/`,
  Playwright reports and test results, so throwaway Phase 3 secrets can never be
  committed.
