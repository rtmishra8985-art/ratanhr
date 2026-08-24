> ⚠️ **SUPERSEDED** — This report was generated during an earlier audit/fix pass and no longer reflects the current state of the codebase. The authoritative current-state documents are [`RELEASE_GATE_FINAL.md`](RELEASE_GATE_FINAL.md) and [`VERIFICATION_REPORT_FINAL_v2.md`](VERIFICATION_REPORT_FINAL_v2.md). Do not use this file to assess production readiness.

---

# RatanHR — Bug-Fix Changelog V3
**Date:** 2026-07-20
**Scope:** 6 additional bugs found by deep-sweep audit of controllers, middleware, nginx, and service layer after V2 fixes.
**Files changed:** 6 modified.

---

## 🟠 High Fixes

### BF3-A — PayrollController: `Generate` (single payslip) has no employee ownership check (IDOR)
**Files:** `HRMS.API/Controllers/Payroll/PayrollController.cs`

`POST /api/payroll/generate` accepted any `EmployeeId` in the request body and called `GeneratePayslipAsync` without verifying the employee belongs to the caller's company. A Company-A admin could generate or overwrite payslips for Company-B employees. The existing helper `PayslipBelongsToCallerAsync` was already used on `GET /{id}` and `DELETE /{id}` — it was simply missing here.

**Fix:** Added `PayslipBelongsToCallerAsync(dto.EmployeeId)` check before the payroll-lock check and service call; returns `404 Not Found` on mismatch (consistent with how the other endpoints handle IDOR failures).

---

### BF3-B — ReportController + ReportService: Legacy `/api/reports/attendance` leaks cross-tenant web attendance
**Files:** `HRMS.API/Controllers/Reports/ReportController.cs` + `HRMS.Infrastructure/Services/ReportService.cs`

Two compounding defects:

1. **Controller:** `ReportController.AttendanceReport` (at `/api/reports`) passed the caller-supplied `filter` object straight to the service without injecting the JWT `companyId`. A non-superadmin admin could supply any `companyId` value (or none) in the query string and receive attendance data from another tenant. (The newer `AttendanceReportController` at `/api/reports/attendance` was written correctly with IDOR guards — this older endpoint was missed.)

2. **Service:** `ReportService.GetAttendanceReportAsync` applied the `filter.CompanyId` filter only to `ExcelAttendances`. The `WebAttendances` query had no company scope at all — `webQ` was filtered by date and optionally `EmployeeId` only. Even with the controller fix, the service would still return cross-company web records if the filter landed an `EmployeeId` from another company.

**Fix (controller):** Non-superadmin callers now have `filter.CompanyId` overridden with their JWT `companyId` claim before the service call. Superadmins may pass a specific company via the query parameter or omit it for all companies.

**Fix (service):** When `filter.CompanyId` is set, the employee IDs belonging to that company are resolved first, and `webQ` is filtered to only those employees — matching the approach used by `GetDailyAttendanceReportAsync` and the other report methods.

---

## 🟡 Medium Fixes

### BF3-C — PayrollController: `GetLocks` falls back to company `0`
**File:** `HRMS.API/Controllers/Payroll/PayrollController.cs`

```csharp
var cid = CallerCompanyIdOrNull ?? 0;  // before fix
```

`CallerCompanyIdOrNull` returns `null` for superadmins (unrestricted). The `?? 0` fallback queried payroll locks for company `0` — no real company has that ID, so superadmins always received an empty list. Same class of defect as BF2-05 (`AnalyticsController`) that was fixed in V2.

**Fix:** Pass `CallerCompanyIdOrNull` directly to `GetLocksAsync`. The service layer already accepts `null` as "all companies" for superadmins.

---

### BF3-D — `CsrfValidationFilter` is dead code for cookie-based authentication
**File:** `HRMS.API/Filters/CsrfValidationFilter.cs`

```csharp
if (!_safeMethods.Contains(req.Method) && req.Headers.ContainsKey("Authorization"))
```

The CSRF double-submit filter only activated when the request carried an `Authorization` **header**. Because the application stores JWTs in `HttpOnly` cookies (`hrms_access_token`) — not in `localStorage` or `Authorization` headers — every authenticated SPA request arrived without that header. The CSRF filter never fired for any real traffic; all `POST`/`PUT`/`PATCH`/`DELETE` from cookie-authenticated users bypassed it silently.

**Fix:** The trigger condition now checks for either `Authorization` header **or** the `hrms_access_token` cookie:

```csharp
bool isAuthenticated = req.Headers.ContainsKey("Authorization")
                    || req.Cookies.ContainsKey("hrms_access_token");
if (!_safeMethods.Contains(req.Method) && isAuthenticated) { … }
```

Anonymous endpoints (login, forgot-password, etc.) carry neither and remain exempt.

---

### BF3-E — `POST /api/auth/mfa/verify` not in the strict auth rate-limit zone
**File:** `nginx/nginx.conf`

`/api/auth/mfa/verify` is `[AllowAnonymous]` (requires only a temp token, not a full JWT). It fell through to the general `api` zone (30 req/min, burst 20). An attacker in possession of a valid temp token — obtained after a correct password — had 30 automated guesses per minute at a 6-digit TOTP code across two 30-second windows. The endpoint should be as restricted as `login`.

**Fix:** Added `mfa/verify` to the auth location regex:

```nginx
location ~ ^/api/auth/(login|refresh|forgot-password|logout|mfa/verify) {
    limit_req zone=auth burst=3 nodelay;
```

---

### BF3-F — `LeaveController` shadows `BaseController.CompanyId` — returns `null` (unrestricted) on missing claim
**File:** `HRMS.API/Controllers/Leave/LeaveController.cs`

```csharp
private new int? CompanyId =>
    int.TryParse(User.FindFirst("companyId")?.Value, out int cid) ? cid : null;
```

`BaseController.CompanyId` (the non-nullable version) returns `-1` when the claim is absent — a safe sentinel that matches no real company. This shadow returned `null`. `LeaveService` treats `null` as the superadmin "unrestricted" sentinel: `GetLeaveTypesAsync(null)` returns leave types for every company; `CreateLeaveTypeAsync(null, dto)` creates a global leave type accessible to all tenants. A non-superadmin user with a malformed or absent `companyId` JWT claim would silently get cross-tenant read/write access to leave types and balances.

**Fix:** Removed the `private new` shadow entirely. All call sites updated to use the inherited `CallerCompanyIdOrNull` from `BaseController`, which returns `null` **only when `IsInRole("superadmin")` is true** and returns `-1` (safe no-match) for all other missing-claim cases.

---

## Summary

| ID | Severity | File(s) | Issue | Status |
|---|---|---|---|---|
| BF3-A | 🟠 High | `PayrollController.cs` | `Generate` endpoint missing employee ownership IDOR check | ✅ Fixed |
| BF3-B | 🟠 High | `ReportController.cs` + `ReportService.cs` | Legacy attendance report leaks cross-tenant web records | ✅ Fixed |
| BF3-C | 🟡 Medium | `PayrollController.cs` | `GetLocks` falls back to company `0` for superadmin | ✅ Fixed |
| BF3-D | 🟡 Medium | `CsrfValidationFilter.cs` | CSRF filter never triggers for cookie-authenticated requests | ✅ Fixed |
| BF3-E | 🟡 Medium | `nginx/nginx.conf` | `/api/auth/mfa/verify` in general zone, not strict auth zone | ✅ Fixed |
| BF3-F | 🟡 Medium | `LeaveController.cs` | `private new CompanyId` shadow returns `null` instead of `-1` | ✅ Fixed |
