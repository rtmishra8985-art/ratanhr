> ⚠️ **SUPERSEDED** — This report was generated during an earlier audit/fix pass and no longer reflects the current state of the codebase. The authoritative current-state documents are [`RELEASE_GATE_FINAL.md`](RELEASE_GATE_FINAL.md) and [`VERIFICATION_REPORT_FINAL_v2.md`](VERIFICATION_REPORT_FINAL_v2.md). Do not use this file to assess production readiness.

---

# RatanHR — Bug-Fix Changelog
**Date:** 2026-07-20  
**Scope:** Security and correctness fixes identified in production-readiness audit.  
**Files changed:** 12 modified, 1 new.

---

## 🔴 Critical Fixes

### BF-01 — AnalyticsController: IDOR via `companyId` query parameter
**File:** `HRMS.API/Controllers/Analytics/AnalyticsController.cs`

All four analytics endpoints (`/headcount`, `/attendance`, `/payroll`, `/turnover`) accepted a `companyId` query parameter and only fell back to the JWT claim when `companyId <= 0`. A regular admin from Company 1 could request `?companyId=2` and receive Company 2's sensitive payroll/attendance data.

**Fix:** Added `ResolveCompanyId()` helper. Non-superadmins always use their JWT `companyId` claim; the query parameter is honoured only for superadmins.

---

### BF-02 — MfaController: JWT returned in response body (bypasses HttpOnly cookie)
**File:** `HRMS.API/Controllers/Authentication/MfaController.cs`  
**File:** `HRMS.API/Controllers/BaseController.cs`

`MfaController.Verify` returned the access token as `{ Token = "..." }` in the JSON body, making it vulnerable to XSS theft. `AuthController.Login` correctly stores tokens in HttpOnly cookies but `MfaController` did not follow the same pattern.

**Fix:** Moved `SetAccessTokenCookie()` and `SetRefreshTokenCookie()` helpers to `BaseController` (protected) so all auth controllers share them. `MfaController.Verify` now calls `SetAccessTokenCookie(token)` and returns a 200 OK with no token in the body.

---

### BF-03 — nginx: auth rate-limit regex never matched
**File:** `nginx/nginx.conf`

The `location` block for auth rate limiting used `^/api/v[0-9]+/auth/...` but all API routes are unversioned (`/api/auth/...`). The nginx-level rate limit was completely inactive.

**Fix:** Changed regex to `^/api/auth/(login|refresh|forgot-password)` to match actual routes.

---

### BF-04 — SuperAdminController.UpdateStatus: no role filter
**File:** `HRMS.API/Controllers/SuperAdmins/SuperAdminController.cs`

`UpdateStatus` used `_db.Users.FindAsync(id)` which returns any user by primary key. A superadmin could toggle `IsActive` on any employee or admin — not just other superadmins. Added a self-deactivation guard as well.

**Fix:** Changed lookup to `FirstOrDefaultAsync(u => u.Id == id && u.Role == "superadmin")`. Added guard rejecting self-status-change.

---

### BF-05 — AdminUserController.Delete / UpdateStatus: no role guard
**File:** `HRMS.API/Controllers/AdminUsers/AdminUserController.cs`

`Delete` and `UpdateStatus` both used `FindAsync(id)` (returns any user). A superadmin could delete any employee or superadmin through this endpoint with no audit trail.

**Fix:** Scoped both lookups to `u.Role == "admin"`. Added self-deletion guard in `Delete`.

---

## 🟠 High Fixes

### BF-06 — BaseController.CallerCompanyIdOrNull returned `-1` instead of `null`
**File:** `HRMS.API/Controllers/BaseController.cs`

When a non-superadmin's `companyId` claim was absent or unparseable, `CallerCompanyIdOrNull` returned `-1` (a non-null `int?`). Callers checking `HasValue` would treat `-1` as a valid company scope, silently returning empty data instead of rejecting the request.

**Fix:** Changed the fallback from `-1` to `(int?)null` so callers can explicitly handle the missing-claim case.

---

### BF-07 — RecruitmentController: local `CallerCompanyId` defaulted to 0
**File:** `HRMS.API/Controllers/Recruitment/RecruitmentController.cs`

The controller shadowed `BaseController.CompanyId` with `int.Parse(... ?? "0")`. A missing claim defaulted to company `0`, which could match real data if auto-increment started there.

**Fix:** Removed local property. `CallerCompanyId` now delegates to `BaseController.CompanyId` (returns `-1` on missing claim — safe sentinel).

---

### BF-08 — PerformanceController: local `CallerCompanyId` defaulted to 0
**File:** `HRMS.API/Controllers/Performance/PerformanceController.cs`

Same pattern as BF-07.

**Fix:** Removed local property; delegates to `BaseController.CompanyId`. Also removed unused `using System.Security.Claims` import.

---

### BF-09 — EmployeeSelfController: used `CreateEmployeeDto` for self-update
**Files:**  
- `HRMS.API/Controllers/Employees/EmployeeSelfController.cs`  
- `HRMS.Application/DTOs/Employee/UpdateSelfProfileDto.cs` *(new)*

`PUT /api/my/profile` accepted `CreateEmployeeDto` — the admin DTO for registering new employees — which includes `CompanyId`, `Designation`, `Department`, and `DateOfJoining`. An employee submitting these fields could attempt to overwrite admin-controlled employment data.

**Fix:** Created `UpdateSelfProfileDto` containing only the 20 fields an employee is permitted to edit. The controller maps this to `CreateEmployeeDto` before calling the existing service, keeping service signatures unchanged.

---

### BF-10 — appsettings.Production.json: `${}` syntax not expanded by .NET
**File:** `HRMS.API/appsettings.Production.json`

All secret values used `"${VAR_NAME}"` bash-interpolation syntax which .NET's `IConfiguration` does not expand. They would be passed literally to EF Core, JWT validation, Redis, etc., causing silent failures in any environment that reads the JSON file directly (e.g., `dotnet run --environment Production` without the docker-compose env vars).

**Fix:** Replaced `${}` values with empty strings and added `_comment` fields on every section clearly documenting the required environment variable name. The docker-compose.yml already provides all values correctly via `Key__Sub=value` env vars.

---

## 🟡 Medium Fixes

### BF-11 — PayrollServiceTests: wrong PF cap comment
**File:** `HRMS.Tests/PayrollServiceTests.cs`

Comment said `"PF employee = 12% of basic = ₹2,400"` but the assertion checked `₹1,800`. The ₹1,800 value is correct (Indian PF law caps the contribution base at ₹15,000 → 12% × ₹15,000 = ₹1,800). The misleading comment could cause a developer to "fix" the correct assertion.

**Fix:** Updated comment to explain the ₹15,000 cap and why ₹1,800 is the correct expected value.

---

### BF-12 — db_setup_additions.sql: missing foreign keys and unique constraints
**File:** `db_setup_additions.sql`

Five tables were created without referential integrity:

| Table | Column | Issue |
|---|---|---|
| `holiday_calendars` | `company_id` | No FK to `companies(id)` |
| `departments` | `company_id` | No FK + no `UNIQUE(company_id, name)` |
| `designations` | `company_id` | No FK + no `UNIQUE(company_id, name)` |
| `leave_balance_adjustments` | `leave_type_id` | No FK to `leave_types(id)` |
| `leave_balance_adjustments` | `adjusted_by_user_id` | No FK to `users(id)` |
| `notifications` | `user_id` | No FK to `users(id)` |

**Fix:** Added `REFERENCES` clauses with appropriate `ON DELETE` actions. Added `CONSTRAINT uq_departments_company_name` and `uq_designations_company_name` unique constraints.

---

### BF-13 — docker-compose.yml: backup used fragile `sleep 60` poll loop
**File:** `docker-compose.yml`

The backup service checked `date '+%H:%M'` every 60 seconds and ran the backup when it matched `02:00`. A container restart at `02:01` would skip the backup for 24 hours. The loop also consumed CPU polling every minute.

**Fix:** Replaced with `busybox crond` (available in the `postgres:16-alpine` image). A crontab entry is written at startup for the `BACKUP_CRON_SCHEDULE` (default `0 2 * * *`); `crond` is then exec'd in the foreground. The schedule is configurable without rebuilding the image.

---

## Summary

| ID | Severity | Area | Status |
|---|---|---|---|
| BF-01 | 🔴 Critical | Analytics IDOR | ✅ Fixed |
| BF-02 | 🔴 Critical | MFA cookie | ✅ Fixed |
| BF-03 | 🔴 Critical | nginx regex | ✅ Fixed |
| BF-04 | 🔴 Critical | SuperAdmin role filter | ✅ Fixed |
| BF-05 | 🔴 Critical | AdminUser role guard | ✅ Fixed |
| BF-06 | 🟠 High | BaseController null return | ✅ Fixed |
| BF-07 | 🟠 High | Recruitment companyId=0 | ✅ Fixed |
| BF-08 | 🟠 High | Performance companyId=0 | ✅ Fixed |
| BF-09 | 🟠 High | Self-update DTO privilege | ✅ Fixed |
| BF-10 | 🟠 High | Production appsettings | ✅ Fixed |
| BF-11 | 🟡 Medium | Payroll test comment | ✅ Fixed |
| BF-12 | 🟡 Medium | SQL FK constraints | ✅ Fixed |
| BF-13 | 🟡 Medium | Backup crond | ✅ Fixed |
