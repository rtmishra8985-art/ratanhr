> ⚠️ **SUPERSEDED** — This report was generated during an earlier audit/fix pass and no longer reflects the current state of the codebase. The authoritative current-state documents are [`RELEASE_GATE_FINAL.md`](RELEASE_GATE_FINAL.md) and [`VERIFICATION_REPORT_FINAL_v2.md`](VERIFICATION_REPORT_FINAL_v2.md). Do not use this file to assess production readiness.

---

# Gap Fix Changelog
**Date:** 2026-07-23
**Pass:** Gap Analysis → Production Fix (post-audit)

All 5 gaps identified in the cross-check audit have been resolved in this pass.

---

## Fixes Applied

### GAP-01 — HIGH: `showAdmin` hardcoded `false` in `TimesheetPage.tsx`

**File:** `HRMS.SPA.Source/src/pages/timesheet/TimesheetPage.tsx`

**Root cause:** The prior fix replaced the `sessionStorage` privilege-escalation vulnerability
with `showAdmin = false` as a temporary stub. The intended replacement using `useGetProfile`
was wired up, but compared `_profile.role === 'Admin'` (capital A) against the JWT role claim
which is always lowercase (`'admin'`, `'superadmin'`). Result: `showAdmin` was always `false`
for every real admin user — the "Pending Approvals" tab was invisible to all admins, making
timesheet approval impossible from the UI.

**Fix:**
```tsx
// Before (broken — 'Admin' never matches lowercase JWT claim)
const showAdmin =
  (_profile as any)?.role === 'Admin' ||
  (_profile as any)?.roles?.includes('Admin') ||
  false;

// After — case-insensitive lowercase comparison
const _profileRole = ((_profile as any)?.role ?? '').toLowerCase();
const _profileRoles: string[] = Array.isArray((_profile as any)?.roles)
  ? (_profile as any).roles : [];
const showAdmin =
  _profileRole === 'admin' ||
  _profileRole === 'superadmin' ||
  _profileRoles.some((r: string) => ['admin', 'superadmin'].includes(r.toLowerCase()));
```

---

### GAP-02 — MEDIUM: Inconsistent response format in `RecruitmentController`

**File:** `HRMS.API/Controllers/Recruitment/RecruitmentController.cs`

**Root cause:** All 20+ endpoints returned raw anonymous objects
`new { success = true, data = ... }` instead of the project-standard
`ApiResponse<T>` wrapper used by all other controllers. This caused:
- TypeScript-typed Orval hooks to receive unexpected JSON shapes
- `requisitions?.items` to be undefined at runtime (frontend bug)
- Missing `message` and `errors` fields that clients rely on for error handling

**Fix:** Every endpoint converted to `ApiResponse<T>.Ok(...)` / `ApiResponse.Fail(...)`.
POST endpoints that create resources now correctly return HTTP 201.
File-upload catch blocks use `ApiResponse.Fail(ex.Message)` instead of raw anon objects.

---

### GAP-03 — MEDIUM: Inconsistent response format in `PerformanceController`

**File:** `HRMS.API/Controllers/Performance/PerformanceController.cs`

**Root cause:** Same as GAP-02 — 20+ endpoints using raw `new { success, data }` pattern.

**Fix:** Same approach — all endpoints converted to `ApiResponse<T>` / `ApiResponse`.
Paged list endpoints now return `ApiResponse<object>.Ok(result)` where `result` is a
`PagedResult<T>`, giving the frontend the `.items` property it expects.

---

### GAP-04 — MEDIUM: Serilog I/O blocking request threads (REC-02)

**File:** `HRMS.API/Program.cs`

**Root cause:** `.WriteTo.Console()` and `.WriteTo.File()` were called directly on the
`LoggerConfiguration`. Under high log volume (request storms, exception bursts), synchronous
log writes to Console and File block ASP.NET Core's request-handling thread pool, causing
latency spikes and potential thread starvation.

**Fix:** Both sinks wrapped inside `.WriteTo.Async(a => { ... })`, which offloads all I/O
to a dedicated background thread with a configurable blocking buffer.

```csharp
// Before
.WriteTo.Console(outputTemplate: "...")
.WriteTo.File("Logs/hrms-.log", ...);

// After
.WriteTo.Async(a => {
    a.Console(outputTemplate: "...");
    a.File("Logs/hrms-.log", ...);
});
```

---

### GAP-05 — MEDIUM: Missing Kubernetes liveness/readiness health endpoints (MED-02)

**File:** `HRMS.API/Program.cs`

**Root cause:** Only `/health` existed. Kubernetes liveness and readiness probes that target
`/healthz/live` and `/healthz/ready` would return HTTP 404, causing incorrect pod-restart
decisions and preventing proper rolling-deploy behavior.

**Fix:** Two new endpoints added alongside the existing `/health` (kept for backward compat):

```
GET /healthz/live   — Liveness: no health checks run, just confirms process responds.
                      Kubernetes restarts the pod only if this returns 5xx.

GET /healthz/ready  — Readiness: runs all checks tagged "ready" (PostgreSQL + Redis).
                      Kubernetes stops routing traffic to pod if this returns Unhealthy.
```

Both endpoints share a `WriteHealthJson` local function to avoid duplication.

---

### GAP-06 — MEDIUM: No explicit request body size limit (REC-06)

**File:** `HRMS.API/Program.cs`

**Root cause:** Kestrel's default 30 MB limit was relied on implicitly. This is fragile —
a future config change or hosting environment could silently raise the limit, allowing
multi-GB request bodies to reach the app and exhaust memory.

**Fix:** Limit set explicitly in `builder.WebHost.ConfigureKestrel()`:

```csharp
builder.WebHost.ConfigureKestrel(options => {
    options.Limits.MaxRequestBodySize = 30 * 1024 * 1024; // 30 MB
});
```

This enforces the cap at the Kestrel transport layer — before any middleware reads the body.

---

## Files Modified

| File | Gap Fixed |
|------|-----------|
| `HRMS.SPA.Source/src/pages/timesheet/TimesheetPage.tsx` | GAP-01 |
| `HRMS.API/Controllers/Recruitment/RecruitmentController.cs` | GAP-02 |
| `HRMS.API/Controllers/Performance/PerformanceController.cs` | GAP-03 |
| `HRMS.API/Program.cs` | GAP-04, GAP-05, GAP-06 |

## Build Status

All changes are source-only edits. No new packages added. No schema changes.
`dotnet build` must be run in a .NET 8 environment to confirm zero compile errors.

## Remaining Manual Items (unchanged from prior audit)

- Apply `db_performance.sql` to the production database
- Set `ENCRYPTION_KEY`, `JWT_SECRET`, `DB_CONNECTION_STRING` in environment
- Run `dotnet ef database update` against the target database
- Configure Redis `ConnectionString` for production rate limiting
- Complete R1–R15 runtime checks from `RELEASE_GATE_FINAL.md`
- Rotate initial superadmin password after first deployment
