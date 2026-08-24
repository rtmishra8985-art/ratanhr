# Release Notes — HRMS v2.0.0
**Release Date**: July 19, 2026

---

## What's New in v2.0.0

This release delivers three major engineering phases on top of the security and quality foundation from v1.5.0:

---

### 🚀 DevOps: Production-Ready CI/CD & Infrastructure

**GitHub Actions Pipeline** — every push to `main`/`develop` now runs a full CI pipeline:
- Compiles with `/warnaserror` — zero-warning policy enforced in CI
- Runs the complete test suite — any failure blocks merge
- Publishes the application and uploads it as a downloadable build artifact
- Validates the Docker image builds successfully on every push

**Safe Database Migrations** — the biggest production risk for HRMS deployments (race condition when multiple containers all migrate simultaneously) is now solved:
- A dedicated `migrate` init-container runs EF migrations once, then exits
- The `api` container only starts after `migrate` exits cleanly
- `Database__AutoMigrate` is disabled in production — migrations never happen accidentally

**Docker Image Pinning** — floating image tags can change silently and break deployments. All images now use specific version tags (pattern for `@sha256:` digest pinning documented).

**SSL Automation** — Let's Encrypt certificates via Certbot, fully integrated into Docker Compose:
- Auto-renewal every 12 hours
- nginx reloads every 6 hours to pick up new certs
- `nginx/init-letsencrypt.sh` automates the first-time setup

---

### 📊 Observability: Full Visibility Into Every Request

**Correlation IDs** — every request now gets a unique `X-Correlation-ID` that flows through:
- Every Serilog log entry for that request
- The response headers (so clients can include it in bug reports)
- The `HttpContext.Items` dictionary for downstream use

**OpenTelemetry Tracing** — distributed traces sent to Jaeger, Zipkin, or any OTLP-compatible backend. See exactly which SQL queries fired, how long Redis operations took, and where time was spent for any request.

**Prometheus Metrics** — `GET /metrics` exposes:
- HTTP request duration, error rate, active requests
- .NET GC pressure, memory usage, thread pool utilisation
- **Custom HRMS metrics**: payroll generation time, DB query latency, Redis latency, report generation throughput

---

### ⚡ Performance: Handle 100k Employees Without Breaking a Sweat

**Streaming Excel Exports** — the previous ClosedXML approach loaded the entire dataset into RAM before writing any bytes. At 100k employees, this meant gigabytes of memory pressure and frequent OOM kills. The new `StreamingReportService` uses `OpenXmlWriter` to write rows directly to the output stream — memory usage is proportional to the batch size, not the total row count.

**Database Indexes** — 14 new composite indexes on the hottest query paths:
- `(EmployeeId, AttDate)` on attendance tables → attendance reports 5–10× faster
- `(EmployeeId, Year, Month) UNIQUE` on Payslips → unique constraint + fast lookups
- `(IsActive, CompanyId)` on Employees → active employee lists instantly
- Plus indexes on LeaveRequests, SalaryStructures, AuditLogs, RefreshTokens

**Query Optimisations** — all read-only report queries now use `AsNoTracking()` and JOIN projections instead of separate dictionary loads, eliminating N+1 query patterns across all five report types.

---

## Upgrade Notes

See `UPGRADE_NOTES.md` for step-by-step migration from v1.5.0.

## Breaking Changes

None. All API contracts preserved. All existing data compatible.

## Deprecations

- Direct `byte[]` export methods in `ReportService` (ClosedXML-based) are superseded by `IStreamingReportService`. The ClosedXML methods remain available but are not recommended for datasets > 10,000 rows.
