> ⚠️ **SUPERSEDED** — This report was generated during an earlier audit/fix pass and no longer reflects the current state of the codebase. The authoritative current-state documents are [`RELEASE_GATE_FINAL.md`](RELEASE_GATE_FINAL.md) and [`VERIFICATION_REPORT_FINAL_v2.md`](VERIFICATION_REPORT_FINAL_v2.md). Do not use this file to assess production readiness.

---

# HRMS P1–P5 Security & Feature Fixes — Implementation Report
**Date:** 2026-07-19  
**Base version:** HRMS_Security_Fixed_v3  
**Fixes applied:** P1, P2, P3, P4, P5

---

## Summary of Changes

| Fix | Title | Files Changed | New Files |
|-----|-------|--------------|-----------|
| P1 | Force Password Change (server-side enforcement) | `JwtService.cs`, `Program.cs` | `MustChangePasswordMiddleware.cs` |
| P2 | CompanyId Guard on Bulk Payroll | `PayrollController.cs`, `PayrollService.cs` | — |
| P3 | WAL Streaming Replication Readiness | `ServiceExtensions.cs`, `appsettings.json`, `appsettings.Production.json`, `ApplicationDbContext.cs` | `DatabaseOptions.cs`, `ReadReplicaDbContext.cs` |
| P4 | Wire NotificationService | `LeaveService.cs`, `PayrollService.cs`, `EmployeeService.cs` | — |
| P5 | Shift Integration into Attendance | `Shift.cs`, `Employee.cs`, `AttendanceService.cs`, `ApplicationDbContext.cs` | `20260719100001_AddShiftThresholdsAndEmployeeShift.cs` |

**Total:** 11 files modified · 6 new files · 1 new EF Core migration

---

## P1 — Force Password Change (Server-Side Enforcement)

**Problem:** The `MustChangePassword` flag was enforced purely on the client side. A user could bypass it by calling any API endpoint directly.

**Fix:**

### `HRMS.Infrastructure/JWT/JwtService.cs`
Added `mustChangePassword` as a JWT claim so the server can read the flag from every request without a database round-trip:
```csharp
new("mustChangePassword", user.MustChangePassword.ToString().ToLower())
```

### `HRMS.API/Middleware/MustChangePasswordMiddleware.cs` *(new)*
New middleware that intercepts every authenticated request. If the `mustChangePassword` JWT claim is `"true"`, the middleware returns `403 Forbidden` with a machine-readable JSON body before the request reaches any controller. Only these paths are allowed through:
- `/api/auth/change-password`
- `/api/auth/logout`
- `/api/auth/refresh`
- `/api/auth/login`
- `/swagger`, `/health`, `/metrics`

### `HRMS.API/Program.cs`
Registered the middleware immediately after `UseAuthorization()`:
```csharp
app.UseAuthorization();
app.UseMiddleware<MustChangePasswordMiddleware>(); // P1
```

**How to verify:** Log in as the seeded SuperAdmin (which has `MustChangePassword = true`). Call any endpoint other than `/api/auth/change-password`. Expect `403` with `{"mustChangePassword":true}`. After calling `change-password`, the flag becomes `false`, a fresh JWT is issued without the claim, and all endpoints become accessible.

---

## P2 — CompanyId Guard on Bulk Payroll

**Problem 1 (Controller):** A superadmin calling `POST /api/payroll/bulk-generate` without a `CompanyId` would silently run payroll for *all* employees across *all* companies.

**Problem 2 (Service):** When `EmployeeIds` were passed alongside a `CompanyId`, there was no check that those employees actually belonged to that company — enabling cross-company payroll generation.

**Fix:**

### `HRMS.API/Controllers/Payroll/PayrollController.cs`
Added a mandatory `CompanyId` validation block after the non-superadmin scoping logic:
```csharp
if (!dto.CompanyId.HasValue)
    return BadRequest(ApiResponse.Fail(
        "CompanyId is required. A superadmin must explicitly specify the target company."));
```

### `HRMS.Infrastructure/Services/PayrollService.cs`
Added cross-company employee guard in `BulkGeneratePayslipsAsync`. After the employee list is loaded, employees whose `CompanyId` doesn't match `dto.CompanyId` cause an exception:
```csharp
var outsiders = employees.Where(e => e.CompanyId != dto.CompanyId).Select(e => e.EmployeeId).ToList();
if (outsiders.Count > 0)
    throw new InvalidOperationException($"Cross-company payroll rejected: {string.Join(", ", outsiders)}");
```

---

## P3 — WAL Streaming Replication Readiness

**Problem:** The application used a single hardcoded `DefaultConnection` string with no support for a read replica, making horizontal read-scaling impossible without code changes.

**Fix:**

### `HRMS.Infrastructure/Data/DatabaseOptions.cs` *(new)*
Config POCO bound to the `"Database"` appsettings section. Documents the required PostgreSQL server settings (`wal_level`, `archive_mode`, `max_wal_senders`, `hot_standby`) inline as XML documentation. Fields:
- `PrimaryConnection` — overrides `DefaultConnection` when set
- `ReplicaConnection` — target for the read-only DbContext
- `EnableReadReplica` — feature flag (default `false`; safe to deploy immediately)

### `HRMS.Infrastructure/Data/ReadReplicaDbContext.cs` *(new)*
A `ReadReplicaDbContext : ApplicationDbContext` registered with `NoTracking` query behavior. All read-heavy operations (reports, dashboards) can inject this instead of `ApplicationDbContext`. All writes continue using `ApplicationDbContext`.

### `HRMS.API/Extensions/ServiceExtensions.cs`
The primary connection now resolves `DatabaseOptions.PrimaryConnection` first, falling back to `DefaultConnection` for backward compatibility. The replica DbContext is registered conditionally:
```csharp
var replicaConn = dbOptions?.EnableReadReplica == true && !string.IsNullOrWhiteSpace(dbOptions.ReplicaConnection)
    ? dbOptions.ReplicaConnection : primaryConn;
services.AddDbContext<ReadReplicaDbContext>(options =>
    options.UseNpgsql(replicaConn).UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking));
```

### `appsettings.json` / `appsettings.Production.json`
New `"Database"` section with `PrimaryConnection`, `ReplicaConnection`, and `EnableReadReplica`. Production config uses `${POSTGRES_PRIMARY_CONNECTION}` / `${POSTGRES_REPLICA_CONNECTION}` env var placeholders.

**How to enable read replica:** Set `"EnableReadReplica": true` and provide the replica connection string. PostgreSQL server-side setup (from `DatabaseOptions.cs` docs): `wal_level = replica`, `max_wal_senders = 5`, `hot_standby = on` on the standby.

---

## P4 — Wire NotificationService

**Problem:** `INotificationService` was registered in DI but never injected into the three main business services. Seven notification events (leave approved/rejected, payslip generated, employee created/disabled) were silently swallowed.

**Fix:** Injected `INotificationService _notify` into three services and wired `NotifyAsync` calls. All calls are fire-and-forget (`_ = _notify.NotifyAsync(...)`) wrapped in `try/catch` so a notification failure never breaks the primary business transaction.

### `LeaveService.cs` — 2 events
- **Leave Approved:** After `DecideAsync` approves a request, the employee's user account receives `"Leave Approved"` notification.
- **Leave Rejected:** Same path, different title/body and `"warning"` type.

### `PayrollService.cs` — 1 event per employee
- **Payslip Generated:** After each successful payslip generation inside `BulkGeneratePayslipsAsync`, the employee is notified `"Payslip Generated"` with the month/year.

### `EmployeeService.cs` — 2 events
- **Account Created:** After a new employee is persisted, their user account receives a `"Welcome to HRMS"` notification with their Employee ID.
- **Account Deactivated:** When `UpdateStatusAsync` sets `IsActive = false`, a `"Account Deactivated"` warning notification is sent.

---

## P5 — Shift Integration into Attendance

**Problem:** Attendance status was calculated using a fixed `hours >= 8 → Present, hours >= 4 → Half Day, else Absent` formula, completely ignoring the shift schedules defined in the `shifts` table.

**Fix:**

### `HRMS.Domain/Entities/Attendance/Shift.cs`
Added three new threshold fields:
| Field | Default | Purpose |
|-------|---------|---------|
| `LateThresholdMinutes` | `0` | Additional tolerance after GracePeriod before marking Late |
| `HalfDayThresholdHours` | `4.0` | Minimum hours for Half Day (below this = Absent) |
| `EarlyExitThresholdMinutes` | `60` | Minutes before shift end; earlier checkout = Early Exit |

### `HRMS.Domain/Entities/Employee/Employee.cs`
Added `int? ShiftId` (nullable) so each employee can be assigned to a shift. Null means the legacy threshold logic applies.

### `HRMS.Infrastructure/Services/AttendanceService.cs`
`WebCheckOutAsync` now:
1. Loads the employee's assigned `Shift` via `GetEmployeeShiftAsync` (two `AsNoTracking` queries, change-tracker safe).
2. Passes it to the new `CalculateAttendanceStatus` static method.

**Status priority order (with shift):**
1. `Absent` — hours worked < `HalfDayThresholdHours`
2. `Early Exit` — checkout before `EndTime - EarlyExitThresholdMinutes`
3. `Half Day` — hours worked < 75% of full shift duration
4. `Late` — check-in after `StartTime + GracePeriodMinutes + LateThresholdMinutes`
5. `Present` — all conditions met

Night-shift crossing midnight is handled: if computed shift duration ≤ 0, 24 hours are added.

**Legacy fallback:** Employees without a shift assignment use the original `4h/8h` logic unchanged — no existing records are affected.

### `HRMS.Infrastructure/Data/ApplicationDbContext.cs`
Added column mappings for all new fields (`late_threshold_minutes`, `half_day_threshold_hours`, `early_exit_threshold_minutes`, `is_night_shift`, `grace_period_minutes` in `shifts`; `shift_id` in `employees`).

### Migration: `20260719100001_AddShiftThresholdsAndEmployeeShift`
EF Core migration that:
- Adds `late_threshold_minutes` (int, default 0), `half_day_threshold_hours` (decimal, default 4), `early_exit_threshold_minutes` (int, default 60) to `shifts`
- Adds `shift_id` (int, nullable) to `employees`
- Creates `IX_Employees_ShiftId` index

**To apply:** `dotnet ef database update` in `HRMS.Infrastructure` (or run in your CI/CD pipeline). Existing data is unaffected — all new columns have safe defaults.

---

## Deployment Checklist

- [ ] Run `dotnet ef database update` to apply the migration
- [ ] For P3: Set `POSTGRES_PRIMARY_CONNECTION` and (when ready) `POSTGRES_REPLICA_CONNECTION` environment variables in production
- [ ] For P5: Assign shifts to employees via the existing Shifts admin UI, then set `ShiftId` on employee records
- [ ] For P3 replica activation: Configure PostgreSQL streaming replication on the DB server, then flip `EnableReadReplica: true`
- [ ] Regression test: Verify seeded SuperAdmin login now triggers the 403 on protected routes until password is changed
