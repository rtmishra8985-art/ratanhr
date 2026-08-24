> ⚠️ **SUPERSEDED** — This report was generated during an earlier audit/fix pass and no longer reflects the current state of the codebase. The authoritative current-state documents are [`RELEASE_GATE_FINAL.md`](RELEASE_GATE_FINAL.md) and [`VERIFICATION_REPORT_FINAL_v2.md`](VERIFICATION_REPORT_FINAL_v2.md). Do not use this file to assess production readiness.

---

# RatanHR — Bug-Fix Changelog V2
**Date:** 2026-07-20
**Scope:** 12 remaining bugs found by independent audit of the "BugFixed" (v1) release.
**Files changed:** 7 modified.

---

## 🔴 Critical Fixes

### BF2-01 — LeaveService: Balance double-counts pending days
**File:** `HRMS.Infrastructure/Services/LeaveService.cs`

`UsedDaysAsync` counts **Approved + Pending** (everything not Rejected/Cancelled). `GetMyBalanceAsync` subtracted `pending` a **second time**: `remaining = quota + credits - used - pending`. Because `used` already included pending, every employee saw a balance that was too low by exactly their pending days — causing legitimate leave applications to be incorrectly denied.

**Fix:** Changed formula to `remaining = quota + credits - used`. The `PendingDays` field is still computed and returned for UI display; it is no longer double-deducted from `RemainingDays`.

---

### BF2-02 — AuthService: `ChangePasswordAsync` does not revoke refresh tokens
**File:** `HRMS.Infrastructure/Services/AuthService.cs`

`ResetPasswordAsync` correctly revokes all active refresh tokens after a password reset. The authenticated `ChangePasswordAsync` path did not — an attacker who stole a refresh token would retain full access indefinitely even after the legitimate user changed their password.

**Fix:** Added the same token-revocation block (matching `ResetPasswordAsync`) to `ChangePasswordAsync`. Revocation count is recorded in the audit log.

---

### BF2-03 — IndianPayrollCalculator: TDS slabs wrong for FY 2025-26
**File:** `HRMS.Infrastructure/Payroll/IndianPayrollCalculator.cs`

The code was labeled "New regime (FY 2025-26)" but used **pre-Budget-2025 slabs** with a ₹7L Section 87A rebate ceiling. Finance Act 2025 (effective 1 Apr 2025) introduced entirely new slabs and raised the rebate ceiling to ₹12L. Every payslip generated had incorrect TDS.

**Old slabs (wrong):**
| Taxable Income | Rate |
|---|---|
| 0 – ₹3L | 0% |
| ₹3L – ₹7L | 5% |
| ₹7L – ₹10L | 10% |
| ₹10L – ₹12L | 15% |
| ₹12L – ₹15L | 20% |
| > ₹15L | 30% |
| 87A rebate ceiling | ₹7L |

**New slabs (Finance Act 2025):**
| Taxable Income | Rate |
|---|---|
| 0 – ₹4L | 0% |
| ₹4L – ₹8L | 5% |
| ₹8L – ₹12L | 10% |
| ₹12L – ₹16L | 15% |
| ₹16L – ₹20L | 20% |
| ₹20L – ₹24L | 25% |
| > ₹24L | 30% |
| 87A rebate ceiling | **₹12L** |

**Fix:** Updated `ComputeNewRegimeTax` with Finance Act 2025 slabs. Updated the 87A rebate check from `<= 700_000m` to `<= 1_200_000m`. Updated `tdsNote` string accordingly.

---

## 🟠 High Fixes

### BF2-04 — PayrollService: Bulk generation has no database transaction
**File:** `HRMS.Infrastructure/Services/PayrollService.cs`

`BulkGeneratePayslipsAsync` iterated employees calling `GeneratePayslipAsync` (with its own `SaveChangesAsync`) per employee, with no outer transaction. A DB timeout or application crash halfway through left some employees with payslips for the month and others without — with no rollback path.

**Fix:** Wrapped the entire bulk loop in `await _db.Database.BeginTransactionAsync()` / `CommitAsync()` with a `catch` that calls `RollbackAsync()` on unexpected failures. The cross-company guard `InvalidOperationException` is excluded from the rollback catch so it propagates correctly.

---

### BF2-05 — AnalyticsController: `ResolveCompanyId` falls back to company `0`
**File:** `HRMS.API/Controllers/Analytics/AnalyticsController.cs`

When a non-superadmin's `companyId` claim is absent or malformed, `CallerCompanyIdOrNull` returns `null`. The previous `?? 0` fallback silently resolved to company `0`, which could match real data (if any company row has `id = 0`) or return empty results with no error — the client had no indication the claim was missing.

**Fix:** Changed fallback from `?? 0` to `?? -1` (the same safe sentinel used by `BaseController.CompanyId`). No real company row has `id = -1`, so queries return empty results rather than accidentally matching data.

---

### BF2-06 — AuthController: Not inheriting `BaseController` (duplicate cookie helpers)
**File:** `HRMS.API/Controllers/Authentication/AuthController.cs`

Bug-fix BF-02 from v1 moved `SetAccessTokenCookie`/`SetRefreshTokenCookie` into `BaseController` (protected) so all auth controllers share them. However `AuthController` still extended `ControllerBase` directly and carried its own private duplicate copies. Any future change to cookie settings in `BaseController` would be silently missed by `AuthController`.

**Fix:** Changed `AuthController : ControllerBase` to `AuthController : BaseController`. Removed the private duplicate `SetAccessTokenCookie` and `SetRefreshTokenCookie` methods. `AuthController` now uses the inherited protected helpers.

---

### BF2-07 — AttendanceService: `DateTime.Today` mixes server-local time with UTC
**File:** `HRMS.Infrastructure/Services/AttendanceService.cs`

`WebCheckInAsync` and `EditWebAttendanceAsync` both used `DateOnly.FromDateTime(DateTime.Today)` to determine the current date. `DateTime.Today` uses the **server's local timezone**, while every other timestamp in the codebase uses `DateTime.UtcNow`. On a UTC server serving IST employees (UTC+5:30), a check-in after midnight IST but before midnight UTC would create an attendance record stamped with the wrong (previous) date, breaking idempotency and producing incorrect attendance totals.

**Fix:** Changed both occurrences to `DateOnly.FromDateTime(DateTime.UtcNow)` for consistency with the rest of the codebase.

---

## 🟡 Medium Fixes

### BF2-08 — LeaveService: Carry-forward penalises pending requests
**File:** `HRMS.Infrastructure/Services/LeaveService.cs`

The year-end carry-forward calculation called `UsedDaysAsync`, which counts Approved **and** Pending days. An employee with a pending leave request at year-end had those days deducted from their carry-forward entitlement — even if the request was later rejected. The employee permanently lost carry-forward credit for leave that was never actually taken.

**Fix:** Added a new `ApprovedOnlyDaysAsync` helper that counts only `Status == "Approved"` requests. The carry-forward calculation now calls `ApprovedOnlyDaysAsync` instead of `UsedDaysAsync`, ensuring only finalised leave reduces carry-forward credit.

---

### BF2-09 — PayrollService: Zero attendance silently generates full-pay payslip
**File:** `HRMS.Infrastructure/Services/PayrollService.cs`

When no attendance records were found for an employee in the target month (web or Excel), bulk generation silently defaulted to `daysPresent = defaultWorkingDays` and generated a 100%-salary payslip. This was almost always a data-quality problem (attendance not yet imported), not genuine full attendance.

**Fix:** When `daysPresent == 0`, the employee is now added to the `errors` list with a descriptive message and skipped (`continue`). The admin sees the skip in the bulk result and can import attendance first or generate the payslip manually with the correct day count.

---

### BF2-10 — nginx: `logout` not included in auth rate-limit block
**File:** `nginx/nginx.conf`

The strict `auth` rate-limit zone (5 req/min) covered `login`, `refresh`, and `forgot-password` only. `POST /api/auth/logout` fell through to the general `api` zone (30 req/min, burst 20). A script could hammer logout with random tokens and flood the `RefreshTokens` table with hash-lookup queries at 30 req/min per IP.

**Fix:** Added `logout` to the auth location regex: `^/api/auth/(login|refresh|forgot-password|logout)`.

---

### BF2-11 — SQL: `leave_balance_adjustments.days` has no `CHECK` constraint
**File:** `db_setup_additions.sql`

The `days` column accepted `0` (a no-op adjustment that silently pollutes the table) and any extreme value with no database-level guard. A zero-day adjustment would cause no balance change but would still appear in audit queries and confuse reporting.

**Fix:** Added `CHECK (days <> 0)` inline constraint. Zero-day adjustments are now rejected at the database level.

---

### BF2-12 — AuthService: Password reset token not removed after use
**File:** `HRMS.Infrastructure/Services/AuthService.cs`

After a successful password reset, `resetToken.UsedAt` was set to `DateTime.UtcNow`, making `IsValid` (a computed property: `UsedAt == null && ExpiresAt > DateTime.UtcNow`) return `false`. Correctness depended entirely on that computed property remaining implemented correctly. If `IsValid` were ever refactored, the same token could be reused.

**Fix:** Added `_db.PasswordResetTokens.Remove(resetToken)` after `UsedAt` is stamped. The token is physically deleted from the database on successful use — reuse is impossible regardless of how `IsValid` is implemented.

---

## Summary

| ID | Severity | Area | File | Status |
|---|---|---|---|---|
| BF2-01 | 🔴 Critical | Leave balance | LeaveService.cs | ✅ Fixed |
| BF2-02 | 🔴 Critical | Auth sessions | AuthService.cs | ✅ Fixed |
| BF2-03 | 🔴 Critical | Payroll TDS | IndianPayrollCalculator.cs | ✅ Fixed |
| BF2-04 | 🟠 High | Payroll bulk | PayrollService.cs | ✅ Fixed |
| BF2-05 | 🟠 High | Analytics IDOR | AnalyticsController.cs | ✅ Fixed |
| BF2-06 | 🟠 High | Auth cookies | AuthController.cs | ✅ Fixed |
| BF2-07 | 🟠 High | Attendance timezone | AttendanceService.cs | ✅ Fixed |
| BF2-08 | 🟡 Medium | Leave carry-fwd | LeaveService.cs | ✅ Fixed |
| BF2-09 | 🟡 Medium | Payroll bulk | PayrollService.cs | ✅ Fixed |
| BF2-10 | 🟡 Medium | nginx rate-limit | nginx.conf | ✅ Fixed |
| BF2-11 | 🟡 Medium | SQL constraint | db_setup_additions.sql | ✅ Fixed |
| BF2-12 | 🟡 Medium | Auth token | AuthService.cs | ✅ Fixed |
