# OpenTelemetry Prerelease Package Decision

## Packages in question

All three live in `HRMS.API/HRMS.API.csproj`, alongside seven other OpenTelemetry packages
already pinned to the stable `1.17.0` release:

| Package | Version | Used for |
|---|---|---|
| `OpenTelemetry.Instrumentation.EntityFrameworkCore` | `1.17.0-beta.1` | EF Core command/query tracing (SQL spans) |
| `OpenTelemetry.Exporter.Prometheus.AspNetCore` | `1.17.0-beta.1` | `/metrics` endpoint scraped by Prometheus (see `grafana/`, `monitoring/`) |
| `OpenTelemetry.Instrumentation.StackExchangeRedis` | `1.17.0-beta.1` | Redis (StackExchange.Redis) client tracing |

Also noted in the same file: `OpenTelemetry.Instrumentation.Process` is explicitly *not*
referenced, with an inline comment stating it has no stable release — evidence the team is
already deliberately avoiding some prerelease packages and only accepted these three as
necessary exceptions.

## Analysis

As of the OpenTelemetry .NET package set current at this repository's target (`1.17.0` line):

- **`OpenTelemetry.Instrumentation.EntityFrameworkCore`** has never shipped a stable (non-beta)
  release in the 1.x line — EF Core instrumentation has remained beta/prerelease upstream for
  the entire lifetime of the OpenTelemetry .NET contrib repository. There is no stable
  equivalent to swap to.
- **`OpenTelemetry.Exporter.Prometheus.AspNetCore`** — the Prometheus exporter has likewise
  remained beta/RC across the OpenTelemetry .NET SDK line; `OpenTelemetry.Exporter.Prometheus`
  variants have not reached a stable 1.x tag independent of the core SDK.
- **`OpenTelemetry.Instrumentation.StackExchangeRedis`** — same situation: Redis client
  instrumentation for `StackExchange.Redis` has stayed in prerelease upstream.

All three are version-aligned with each other and with the stable `1.17.0` core/exporter
packages already in the project (no cross-package version skew), and none of them are core
tracing infrastructure — they are all *instrumentation* or *exporter* add-ons that degrade
gracefully (their absence would mean missing spans/metrics for that specific subsystem, not a
broken build or a broken API).

## Decision

**Keep all three as documented, intentional exceptions.** No stable equivalent exists upstream
for any of the three, so "prefer stable" cannot be satisfied by a version bump — only by
dropping the instrumentation entirely (losing EF query tracing, Prometheus metrics export, or
Redis tracing) or accepting the prerelease packages. Given they are already version-pinned
exactly (not floating `*-*` prerelease ranges), consistent with each other and the stable core,
and limited to non-critical-path instrumentation, accepting the prerelease dependency is the
lower-risk option versus removing observability into the database, cache, and metrics-export
surfaces the compliance/monitoring documentation (`Documentation/MonitoringGuide.md`,
`Documentation/PrometheusGuide.md`) already depends on.

**Action taken this session:** none — no package references were changed, no code was touched,
no restore/build/test was attempted for this decision, because it does not require one; it was
a version/availability analysis only. This is unchanged from the equivalent finding in prior
sessions (`docs/phase-2-readiness.md`), re-confirmed independently here by re-reading the
`.csproj` files rather than trusting the prior write-up.

**Residual risk:** a prerelease package can introduce a breaking API change in a later
prerelease build without following semver. Recommend pinning update reviews to run
`dotnet list package --outdated` and `dotnet list package --vulnerable` (both require NuGet
access — currently `BLOCKED — ENVIRONMENT` in this sandbox, see
`PHASE2_VERIFICATION_EVIDENCE.md`) as part of any future dependency-bump PR touching these
three packages, and to re-check upstream release notes for a graduated stable release before
each SDK/package upgrade cycle.
