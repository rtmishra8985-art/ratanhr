> ⚠️ **SUPERSEDED** — This report was generated during an earlier audit/fix pass and no longer reflects the current state of the codebase. The authoritative current-state documents are [`RELEASE_GATE_FINAL.md`](RELEASE_GATE_FINAL.md) and [`VERIFICATION_REPORT_FINAL_v2.md`](VERIFICATION_REPORT_FINAL_v2.md). Do not use this file to assess production readiness.

---

# Verification Fix Report — 5 Items
Generated: 2026-07-21

---

## [1] .NET SDK — replit.nix

**Status: Manual step required. Workspace restart needed after applying.**

`replit.nix` content to create at the project root:

```nix
{ pkgs }: {
  deps = [
    pkgs.dotnet-sdk_8
    pkgs.postgresql_15
  ];
}
```

**Note:** After saving `replit.nix`, Replit must restart before `dotnet --version` will succeed. The live tests in items (a)–(g) cannot be run until the runtime is available. Once dotnet is working, run:

```bash
dotnet --version
dotnet ef database update
dotnet run
```

Then proceed with the curl tests for CRUD, cross-tenant read, CORS rejection, /health, MFA bypass regression, Forgot Password, and password hash prefix checks.

---

## [2] IDOR Defects — Files Changed

### AppreciationService.cs
**File:** `HRMS.Infrastructure/Services/AppreciationService.cs`

| Method | Line (before) | Change |
|--------|--------------|--------|
| `GetByIdAsync` | 39–43 | Signature changed to `GetByIdAsync(int id, int? callerCompanyId)`. After `FindAsync`, joins Employees to verify `emp.CompanyId == callerCompanyId`. SuperAdmin (`callerCompanyId == null`) bypasses. |
| `DeleteAsync` | 77–84 | Signature changed to `DeleteAsync(int id, int? callerCompanyId)`. Same ownership check before remove. |

**File:** `HRMS.Application/Interfaces/IAppreciationService.cs`
- `GetByIdAsync(int id, int? callerCompanyId)` — added `callerCompanyId` param with XML doc.
- `DeleteAsync(int id, int? callerCompanyId)` — added `callerCompanyId` param with XML doc.

**File:** `HRMS.API/Controllers/Appreciation/AppreciationController.cs`
- `GetById`: now calls `_service.GetByIdAsync(id, CallerCompanyIdOrNull)`.
- `Delete`: now calls `_service.DeleteAsync(id, CallerCompanyIdOrNull)`.

---

### DepartmentService.cs
**File:** `HRMS.Infrastructure/Services/DepartmentService.cs`

| Method | Change |
|--------|--------|
| `GetDepartmentByIdAsync` | Added `int? callerCompanyId`. Allows `CompanyId == null` (global). Rejects `CompanyId != callerCompanyId` (when both non-null). |
| `UpdateDepartmentAsync` | Added `int? callerCompanyId`. Global records allowed only when `callerCompanyId == null` (SuperAdmin). |
| `DeleteDepartmentAsync` | Same as Update. |
| `GetDesignationByIdAsync` | Same pattern as Dept. |
| `UpdateDesignationAsync` | Same pattern. |
| `DeleteDesignationAsync` | Same pattern. |

**File:** `HRMS.Application/Interfaces/IDepartmentService.cs` — All 6 methods above updated with `callerCompanyId` parameter and XML doc.

**File:** `HRMS.API/Controllers/Organisation/DepartmentController.cs` — All 6 endpoints now pass `CallerCompanyIdOrNull` to the service.

---

### HolidayService.cs
**File:** `HRMS.Infrastructure/Services/HolidayService.cs`

| Method | Change |
|--------|--------|
| `GetByIdAsync` | Added `int? callerCompanyId`. Global holidays (`CompanyId == null`) visible to all. Company holidays require matching `callerCompanyId`. |
| `UpdateAsync` | Added `int? callerCompanyId, bool isSuperAdmin`. Global records blocked for non-SuperAdmin. Company records require match. |
| `DeleteAsync` | Same as UpdateAsync. |

**File:** `HRMS.Application/Interfaces/IHolidayService.cs` — Methods above updated with `callerCompanyId` and `isSuperAdmin` params.

**File:** `HRMS.API/Controllers/Organisation/HolidayController.cs`
- `GetById`: passes `CallerCompanyIdOrNull`.
- `Update` / `Delete`: passes `CallerCompanyIdOrNull, User.IsInRole("superadmin")`.

---

### ShiftService.cs
**File:** `HRMS.Infrastructure/Services/ShiftService.cs`

| Method | Change |
|--------|--------|
| `UpdateShiftAsync` | Added `int? callerCompanyId`. Requires `s.CompanyId == callerCompanyId`. SuperAdmin (`null`) bypasses. |
| `DeleteShiftAsync` | Same. |

**File:** `HRMS.Application/Interfaces/IShiftService.cs` — Both methods updated.

**File:** `HRMS.API/Controllers/Attendance/ShiftController.cs` — `Update` and `Delete` now pass `CallerCompanyIdOrNull`.

---

### Full Repository FindAsync/FirstOrDefaultAsync Audit

| File | Line | Call | Safe? | Note |
|------|------|------|-------|------|
| AdminUserController.cs | 81 | `FirstOrDefaultAsync()` | ✅ Safe | Scoped by email uniqueness in prior filter |
| AdminUserController.cs | 115, 138, 153 | `FirstOrDefaultAsync(u => u.Id == id && u.Role == "admin")` | ✅ Safe | Role filter prevents cross-role access |
| EmailQueueController.cs | 50 | `FindAsync(id)` | ✅ Safe | SuperAdmin-only endpoint; no tenant required |
| PayslipController.cs | 24 | `FindAsync(payslipId)` | ✅ Safe | Verified by downstream company check on line 43/49 |
| PayslipController.cs | 43, 49, 53 | `FirstOrDefaultAsync / FindAsync` | ✅ Safe | Employee ↔ company cross-check performed |
| SuperAdminController.cs | 59 | `FirstOrDefaultAsync(u => u.Id == id && u.Role == "superadmin")` | ✅ Safe | Role filter present |
| **AppreciationService.cs** | 43, 95 | `FindAsync(id)` | ✅ **Fixed this pass** | Now followed by Employee.CompanyId ownership check |
| AppreciationService.cs | 51, 103 | `FirstOrDefaultAsync(e => e.EmployeeId == a.EmployeeId)` | ✅ Safe | Used for ownership check, not entity lookup |
| AssetService.cs | 69, 105, 126, 142, 164 | `FirstOrDefaultAsync(a => ... && a.CompanyId == companyId)` | ✅ Safe | CompanyId filter in predicate |
| AttendanceService.cs | 43, 123, 128 | `FirstOrDefaultAsync` | ✅ Safe | Scoped by employeeId derived from JWT |
| AttendanceService.cs | 78, 196 | `FindAsync(attendanceId)` | ✅ Safe | AttendanceId is per-employee; scoped by JWT employeeId in prior check |
| AuthService.cs | 121, 158, 161, 190, 215, 224 | Various | ✅ Safe | Auth lookups by email/token hash; not tenant-scoped resources |
| **DepartmentService.cs** | 62, 84, 95, 131, 153, 164 | `FindAsync / FirstOrDefaultAsync` | ✅ **Fixed this pass** | Now include callerCompanyId ownership check |
| **HolidayService.cs** | 60, 87, 102 | `FirstOrDefaultAsync / FindAsync` | ✅ **Fixed this pass** | Now include callerCompanyId and isSuperAdmin checks |
| RecruitmentService.cs | 80, 222, 265, 276, 285, 337, 346 | `FirstOrDefaultAsync(x => x.Id == id && x.CompanyId == companyId)` | ✅ Safe | CompanyId filter in predicate |
| ReportService.cs | 578, 582, 611 | `FirstOrDefaultAsync` | ✅ Safe | Scoped by employeeId/global aggregate |
| RoleService.cs | 29, 38 | `FindAsync(id)` | ✅ Safe | Roles are global entities (no tenant scoping needed) | Global entity |
| SalaryStructureService.cs | 19 | `FirstOrDefaultAsync()` | ✅ Safe | Scoped by prior Where with employeeId |
| **ShiftService.cs** | 39, 55 | `FindAsync(id)` | ✅ **Fixed this pass** | Now include callerCompanyId ownership check |
| TimesheetService.cs | 72, 90, 108, 122, 135 | `FirstOrDefaultAsync(t => t.Id == id && t.CompanyId == companyId / EmployeeId)` | ✅ Safe | Tenant/employee filter in predicate |
| TrainingService.cs | 53, 82, 95, 106, 110, 159 | `FindAsync / FirstOrDefaultAsync` | ✅ Safe | Scoped by companyId in service layer |
| TravelService.cs | 47, 75, 85, 98 | `FindAsync(id)` | ✅ Safe | Scoped by employeeId JWT claim check after lookup |
| WebhookService.cs | 58 | `FindAsync(id)` | ✅ Safe | Subscription scoped to caller's companyId after lookup |

**Fixed in this pass:** AppreciationService (2 calls), DepartmentService (6 calls), HolidayService (3 calls), ShiftService (2 calls) — 13 calls total.
**Still needs fixing:** None identified.

---

## [3] localStorage Token Storage

### Files changed

#### `HRMS.API/wwwroot/login.html`
**Before (lines 95–96):**
```javascript
localStorage.setItem('hrms_token', data.data.token);
localStorage.setItem('hrms_refresh_token', data.data.refreshToken);
```
**After:**
```javascript
// FIX [3] — Do NOT store access tokens or refresh tokens in localStorage.
// Tokens are set as HttpOnly cookies by the server and must never be
// accessible from JavaScript. Only non-sensitive UI metadata is stored here.
```
Non-sensitive UI metadata (`hrms_role`, `hrms_name`, `hrms_userId`, `hrms_companyId`, `hrms_employeeId`) retained.

#### `HRMS.API/wwwroot/js/api.js` — full refactor

| Change | Detail |
|--------|--------|
| `getToken()` | **Removed.** Tokens live in HttpOnly cookies; no JS access. |
| `getRefreshToken()` | **Removed.** Same reason. |
| `saveSession()` | No longer stores `token` or `refreshToken` in any storage. Only UI metadata. |
| `tryRefreshToken()` | No longer reads token from localStorage. Sends `credentials: 'include'` — browser auto-attaches the HttpOnly cookie. Body is empty (server reads `Request.Cookies["hrms_refresh_token"]` first). |
| `apiFetch()` | Removed `Authorization: Bearer` header injection. Added `credentials: 'include'` to every fetch so the HttpOnly cookie is automatically sent. |
| `logout()` | Posts to `/api/auth/logout` with `credentials: 'include'` instead of passing a token in the body. |
| `HrmsApi.refreshToken` | Signature changed from `(refreshToken) => ...` to `() => apiFetch('/auth/refresh', { method: 'POST' })`. |
| `HrmsApi.logout` | Changed from `(refreshToken) => apiFetch('/auth/logout', ...)` to `() => logout()`. |
| `requireAuth()` | No longer checks for a token (unreadable from JS). Checks `hrms_role` metadata only; unauthenticated requests return 401 which redirects to login. |

#### Pages already safe (no token leaked):
- `bulk-payroll.html:142` — `token = null /* cookie */` — ✅ No token in localStorage
- `departments.html:120` — same pattern — ✅
- `leave-adjustments.html:127` — same pattern — ✅
- `holidays.html:122` — already commented out — ✅
- `reports-leave.html:118` — `token = null` — ✅
- `reports-salary-register.html:127` — `token = null` — ✅

---

## [4] Plaintext Passwords

### `PRODUCTION_READINESS_REPORT.md` line 459
**Before:**
```
- SuperAdmin: `superadmin@hrms.com` / `Admin@123` → forced password change on first login
```
**After:**
```
- SuperAdmin: `superadmin@hrms.com` — default password removed. Initial password is generated securely at runtime or provided via environment configuration. No default passwords are committed to documentation.
```

### `HRMS.SPA.Source/src/pages/LoginPage.tsx` lines 269–271
**Before:**
```jsx
<p>superadmin@hrms.com · SuperAdmin@123</p>
<p>admin@hrms.com · Admin@1234</p>
<p>employee@hrms.com · Employee@1234</p>
```
**After:** Password literals removed; only email addresses retained. The DEV-only guard (`import.meta.env.DEV`) remains.

---

## [5] Live MFA Verification

**Status: Blocked pending .NET SDK installation (item [1]).**

Once `dotnet run` is confirmed working, two curl sequences must be shown:

**Sequence 1 — refresh before MFA (must return HTTP 401):**
```bash
# Step 1: Login as MFA-enabled user
curl -c cookies.txt -s -X POST http://localhost:5000/api/auth/login \
  -H 'Content-Type: application/json' \
  -d '{"email":"<mfa-user>","password":"<password>","portal":"admin"}'

# Step 2: Attempt refresh before completing MFA (MfaVerified=false cookie)
curl -b cookies.txt -c cookies.txt -s -X POST http://localhost:5000/api/auth/refresh \
  -H 'Content-Type: application/json'
# Expected: HTTP 401
```

**Sequence 2 — full MFA flow (must return HTTP 200 on refresh):**
```bash
# Step 1: Login
curl -c cookies.txt -s -X POST http://localhost:5000/api/auth/login ...

# Step 2: Complete MFA
curl -b cookies.txt -c cookies.txt -s -X POST http://localhost:5000/api/auth/mfa/verify \
  -H 'Content-Type: application/json' \
  -d '{"code":"<totp-code>"}'

# Step 3: Refresh after MFA (MfaVerified=true cookie)
curl -b cookies.txt -c cookies.txt -s -X POST http://localhost:5000/api/auth/refresh \
  -H 'Content-Type: application/json'
# Expected: HTTP 200 with new token set in cookies
```

These cannot be fabricated. Run after `dotnet run` is confirmed.
