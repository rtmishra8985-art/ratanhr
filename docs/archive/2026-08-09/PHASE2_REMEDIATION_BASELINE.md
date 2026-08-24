# RatanHR — Phase 2 Remediation Baseline (Session: 2026-08-08)

This session picks up a repository that has already been through **six prior remediation
sessions** (see `docs/phase-2-readiness.md`, `docs/phase-2-blocker-remediation.md`,
`PHASE2-DELIVERABLE.md`, `PHASE2_DELIVERABLES_REPORT.md`, and the `evidence/` /
`docs/evidence/` trees). Those sessions ran in sandboxes with the same network restriction
this one has — no NuGet, no Docker registries, no MySQL install source, no Playwright browser
CDN — with the sole difference that earlier sessions used `bun` for the SPA where this session
used `npm` (both are legitimate; see `evidence/session-2026-08-08/frontend-verification.txt`).

This document records what is **actually present in the repository right now**, verified by
direct inspection this session, not carried forward from memory of prior reports.

## 1. Solution / Projects

```
HRMS.Domain/HRMS.Domain.csproj           net8.0
HRMS.Application/HRMS.Application.csproj net8.0
HRMS.Infrastructure/HRMS.Infrastructure.csproj net8.0
HRMS.API/HRMS.API.csproj                 net8.0
HRMS.Tests/HRMS.Tests.csproj             net8.0
```
No `.sln` file was found at the repo root during this inventory pass; the API project and its
references define the build graph.

## 2. EF Core

- DbContext(s): `HRMS.Infrastructure/Data` / `HRMS.Infrastructure/Persistence`.
- Migrations: `HRMS.Infrastructure/Migrations/MySql/` — **15** migration classes (excludes
  `.Designer.cs` and any `ModelSnapshot.cs`), sequential `20260726000001` →
  `20260806000001`. No `ModelSnapshot.cs` file exists in the repo (searched
  `find . -iname "*ModelSnapshot.cs"` — zero results), so EF's implicit snapshot is generated
  at build time from the migrations themselves rather than checked in.
- No occurrence anywhere in the repository (code, docs, or prior session evidence) explains or
  justifies an expected count of **19** migrations; every prior session and this one counted
  **15** independently from the filesystem. This remains **OWNER DECISION REQUIRED** (see
  Phase 3/11 below) — it cannot be resolved by inspection alone.

## 3. Docker / Compose

- `Dockerfile` — multi-stage: `spa-builder` (was `oven/bun:1.2-alpine`), `build` and `migrate`
  (`mcr.microsoft.com/dotnet/sdk:8.0.416-alpine3.21`), `runtime`
  (`mcr.microsoft.com/dotnet/aspnet:8.0.8-alpine3.20`).
- Compose files at repo root: `docker-compose.yml`, `docker-compose.e2e.yml`,
  `docker-compose.override.yml`, `docker-compose.backup.yml`, `docker-compose.prod.yml`,
  `docker-compose.replica.yml`, plus `Staging/docker-compose.staging*.yml`.
- `docker-compose.replica.yml` is **not empty**. It is a documentation-only placeholder: its
  entire content is a comment block explaining that the file previously implemented PostgreSQL
  WAL-streaming replication, that this is not applicable now that the project is on MySQL, and
  that MySQL replica configuration (Group Replication or async replication) must be done at the
  infrastructure level, pointing to `Documentation/MySqlMigrationGuide.md`. This differs from
  Session 5/6's finding of "empty" — the file's current content is a deliberate, documented
  placeholder, which resolves that open item (see Phase 11).

## 4. Frontend (SPA)

- `HRMS.SPA.Source/package.json` — Vite + React + TypeScript SPA. Both `package-lock.json`
  and `bun.lock` are committed, so either `npm` or `bun` can build it.
- Playwright E2E: `HRMS.SPA.Source/e2e/`, including `global.setup.ts` (auth-state + staging
  health check) and `global-setup.ts` (per-role login).

## 5. Health Checks

- `HRMS.API/Program.cs` maps `/health`, `/healthz`, `/healthz/ready`, `/healthz/live` via
  `MapHealthChecks`. There is **no** `/api/health*` route anywhere in the API.
- `nginx/nginx.conf` proxies `location = /health` and `location = /healthz` directly to the API
  at the same paths; `/api/` is a separate `location` block for actual API traffic and is not
  prefix-stripped.
- `HRMS.SPA.Source/e2e/global.setup.ts` requested `/api/healthz` (through the SPA's base URL,
  i.e. through nginx), which does not exist anywhere in the stack and was guaranteed to 404.
  The test tolerated `404` in its accepted-status list, so it always "passed" without ever
  confirming the API was actually healthy. **Fixed this session** — see
  `PHASE2_VERIFICATION_EVIDENCE.md` §7 and §8.

## 6. OpenTelemetry

Three prerelease (`-beta.1`) packages in `HRMS.API/HRMS.API.csproj`, all otherwise on the
stable `1.17.0` line:
- `OpenTelemetry.Instrumentation.EntityFrameworkCore` `1.17.0-beta.1`
- `OpenTelemetry.Exporter.Prometheus.AspNetCore` `1.17.0-beta.1`
- `OpenTelemetry.Instrumentation.StackExchangeRedis` `1.17.0-beta.1`

See `OPENTELEMETRY_DECISION.md` for the full analysis.

## 7. Toolchain required vs. available

| | Required (from `global.json` / Dockerfile / CI) | Available this session |
|---|---|---|
| .NET SDK | `8.0.416` (feature band 4xx) | none preinstalled; `apt-get install dotnet-sdk-8.0` gets `8.0.129` (band 1xx) — does not satisfy `rollForward: latestFeature` |
| Docker | required for build/compose/API/E2E phases | not installed, no daemon |
| MySQL | required for migration/constraint verification | not installed |
| Node | for SPA | `v22.22.2` present |
| npm | package manager fallback for SPA | `10.9.7` present |
| bun | SPA's primary package manager per scripts | not installed; not obtainable (CDN blocked) |

Full command-level evidence: `evidence/session-2026-08-08/toolchain-versions.txt`.

## 8. Network egress reality (measured this session, not assumed)

Reachable: `archive.ubuntu.com`, `security.ubuntu.com`, `registry.npmjs.org` / `npmjs.com`,
`github.com` / `api.github.com` / `raw.githubusercontent.com`, `pypi.org`, `crates.io`.

Confirmed **blocked** (`403 host_not_allowed` from the egress proxy) this session:
`api.nuget.org`, `dot.net`, `builds.dotnet.microsoft.com`, `registry-1.docker.io`,
`dev.mysql.com`, `deb.nodesource.com` (blocks Playwright's `apt`-based browser dependency
install). This is why Phases 2, 3, 4, 5, 6, 7, 8 of the task brief cannot be executed to
completion in this sandbox regardless of code correctness — see
`PHASE2_VERIFICATION_EVIDENCE.md` for the per-phase evidence and exact blocked commands.

No application behavior was changed during this inventory step.
