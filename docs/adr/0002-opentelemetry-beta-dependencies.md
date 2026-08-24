# ADR 0002 — Remain on OpenTelemetry 1.17.0-beta.1 instrumentation packages

- Status: Accepted
- Date: 2026-08-10
- Deciders: Backend / Platform owner
- Supersedes: none

## Context

The HRMS observability stack uses OpenTelemetry for tracing, metrics and Prometheus
scraping. Phase 1 verification flagged that several referenced OpenTelemetry packages are
pre-release (`1.17.0-beta.1`), which normally fails a "no pre-release dependencies in
production" policy.

Before accepting or rejecting the pre-release references we queried the NuGet registry
directly (`https://api.nuget.org/v3-flatcontainer/<id>/index.json`) and counted stable
versions per package.

### NuGet evidence — queried 2026-08-10 (UTC)

| Package                                             | Version referenced | Latest published | Stable releases available |
| --------------------------------------------------- | ------------------ | ---------------- | ------------------------- |
| `OpenTelemetry.Instrumentation.AspNetCore`            | 1.17.0             | 1.17.0 (stable)  | yes — stable in use       |
| `OpenTelemetry.Instrumentation.EntityFrameworkCore`   | 1.17.0-beta.1      | 1.17.0-beta.1    | **0 stable releases ever** |
| `OpenTelemetry.Instrumentation.StackExchangeRedis`    | 1.17.0-beta.1      | 1.17.0-beta.1    | **0 stable releases ever** |
| `OpenTelemetry.Exporter.Prometheus.AspNetCore`        | 1.17.0-beta.1      | 1.17.0-beta.1    | **0 stable releases ever** |

Key fact: for the three beta packages there is **no stable version to upgrade to** — not an
older stable, not a newer one. The OpenTelemetry .NET SIG has never shipped a stable release
of the EF Core instrumentation, the StackExchange.Redis instrumentation, or the Prometheus
AspNetCore exporter; the Prometheus exporter in particular is gated on the OpenTelemetry
metrics-exporter specification being declared stable upstream.

Options considered:

1. **Stay on `1.17.0-beta.1`** for the three packages, keep AspNetCore instrumentation on the
   stable 1.17.0.
2. **Remove EF Core / Redis instrumentation and the Prometheus exporter** to eliminate all
   pre-release references.
3. **Hand-roll replacements** (custom `DiagnosticListener` subscribers, custom `/metrics`
   endpoint).

Option 2 removes database query tracing, cache tracing and the Prometheus scrape endpoint —
the parts of the telemetry story operations actually depends on for latency triage. Option 3
replaces vendor-maintained, spec-aligned code with bespoke code carrying more risk than the
beta packages themselves and no upgrade path.

## Decision

Remain on OpenTelemetry `1.17.0-beta.1` for
`OpenTelemetry.Instrumentation.EntityFrameworkCore`,
`OpenTelemetry.Instrumentation.StackExchangeRedis` and
`OpenTelemetry.Exporter.Prometheus.AspNetCore`, pinned to exact versions. Keep
`OpenTelemetry.Instrumentation.AspNetCore` on the stable `1.17.0`. Treat the pre-release
references as an accepted, documented exception to the no-pre-release policy for Phase 1,
rather than as an open blocker.

All four packages are pinned to exact versions (no floating ranges) so a background restore
can never silently pull a different beta build.

## Consequences / risks

- **Breaking changes between betas.** Pre-release packages may change public API or semantic
  conventions without a major-version bump. Mitigated by exact pins and by keeping
  OpenTelemetry wiring isolated in the API composition root, so a break is a single-file fix.
- **Attribute/semantic-convention churn.** Span and metric attribute names emitted by the beta
  instrumentation may change, which can break dashboards and alert rules built on those names.
  Dashboards must be treated as revisable when the packages are bumped.
- **Support posture.** Beta packages get best-effort support from the OpenTelemetry .NET SIG.
  There is no stable alternative, so this risk is unavoidable rather than self-inflicted.
- **Compliance/audit.** Any policy scan that flags pre-release dependencies will flag these
  three. This ADR is the standing justification; reference it in scan waivers.
- **Not affected:** the core `OpenTelemetry` SDK and the ASP.NET Core instrumentation remain on
  stable releases, so the trace pipeline itself is not on pre-release code.

## Revisit trigger

Re-open this ADR and upgrade when **any** of the following becomes true:

1. Any of the three packages publishes its **first stable (non-pre-release) release** on NuGet.
   Upgrade to the stable version in the next maintenance window and delete the corresponding
   row from the exception list.
2. The OpenTelemetry metrics-exporter specification is declared stable upstream (this is the
   gating item for `Exporter.Prometheus.AspNetCore`).
3. A security advisory is published against any pinned beta version — treat as an immediate
   out-of-band upgrade, or drop the affected instrumentation if no fixed build exists.
4. Scheduled review: re-run the NuGet stable-release query each quarter (next check due
   **2026-11-10**) and record the result in `docs/phase-1-remediation.md`.

Verification command used for the evidence above:

```bash
for p in opentelemetry.instrumentation.aspnetcore \
         opentelemetry.instrumentation.entityframeworkcore \
         opentelemetry.instrumentation.stackexchangeredis \
         opentelemetry.exporter.prometheus.aspnetcore; do
  curl -s "https://api.nuget.org/v3-flatcontainer/$p/index.json"
done
```
