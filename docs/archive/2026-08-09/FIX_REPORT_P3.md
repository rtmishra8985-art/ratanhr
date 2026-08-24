> ⚠️ **SUPERSEDED** — This report was generated during an earlier audit/fix pass and no longer reflects the current state of the codebase. The authoritative current-state documents are [`RELEASE_GATE_FINAL.md`](RELEASE_GATE_FINAL.md) and [`VERIFICATION_REPORT_FINAL_v2.md`](VERIFICATION_REPORT_FINAL_v2.md). Do not use this file to assess production readiness.

---

# RatanHR — Fix Report (P3)

**Date:** 2026-07-21  
**Baseline:** P2 codebase (`ratanhr_fixed_v2`)  
**Total fixes applied:** 18

---

## Fix Table

| # | Issue | Severity | File(s) | Root Cause | Fix Applied |
|---|---|---|---|---|---|
| 1 | Training enrollment IDOR — no tenant validation | **Critical** | `TrainingService.cs` | `EnrollAsync` never verified `employee.CompanyId == training.CompanyId` | Added batch employee-company fetch, cross-tenant guard, audit log entry; returns `isCrossTenant=true` flag for 403 mapping |
| 2 | CompanyBranch `GetBranchAsync` IDOR | **Critical** | `CompanyBranchService.cs` | `FindAsync(branchId)` returned any branch regardless of caller company | Replaced with `AsNoTracking().FirstOrDefaultAsync(x => x.Id == branchId && x.CompanyId == callerCompanyId)` |
| 3 | CompanyBranch `UpdateBranchAsync` IDOR | **Critical** | `CompanyBranchService.cs` | No check that `branch.CompanyId == callerCompanyId` before update | Added ownership check; blocked attempts are audit-logged and return `false` (→ 404 at controller) |
| 4 | CompanyBranch `DeleteBranchAsync` IDOR | **Critical** | `CompanyBranchService.cs` | No check that `branch.CompanyId == callerCompanyId` before delete | Added ownership check; blocked attempts are audit-logged and return `false` (→ 404 at controller) |
| 5 | Timesheet admin view hardcoded `false` | **High** | `TimesheetPage.tsx` | `const showAdmin = false` — never wired to actual role; broke admin approval UI | Replaced with `useGetProfile` query + `profile.role === 'Admin'` check; role sourced from `/api/auth/me` |
| 6 | Missing `AsNoTracking` in `TrainingService.GetAllAsync` | **Medium** | `TrainingService.cs` | Tracked entities returned for read-only list path | Added `.AsNoTracking()` to `GetAllAsync` query chain |
| 7 | Missing `AsNoTracking` in `TrainingService.GetByIdAsync` | **Medium** | `TrainingService.cs` | `FindAsync()` returns tracked entity for read-only path | Replaced with `.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && ...)` |
| 8 | Missing `AsNoTracking` in `CompanyBranchService.GetBranchesPagedAsync` | **Medium** | `CompanyBranchService.cs` | No tracking hint on paginated read | Added `.AsNoTracking()` |
| 9 | Missing `AsNoTracking` in `CompanyBranchService.GetBranchesAsync` | **Medium** | `CompanyBranchService.cs` | No tracking hint on full list read | Added `.AsNoTracking()` |
| 10 | `AsNoTracking` in `TrainingService.GetEnrollmentsByEmployeeAsync` | **Low** | `TrainingService.cs` | Missing on enrollment list read | Added `.AsNoTracking()` |
| 11 | 14 missing DB indexes (Employee, Attendance, Payroll, Leave, Training, Timesheet, Holiday, Biometric) | **High** | `20260721200001_RestoreSecurityAndPerformanceIndexes.cs` | P2 migration `AddMissingIndexes` was created before Timesheet/Biometric/Holiday tables existed | New migration adds all 14 missing indexes covering tenant-scope and compound query patterns |
| 12 | `ShiftPage.tsx` — page missing, route 404 | **High** | `App.tsx`, `ShiftPage.tsx`, `Sidebar.tsx` | No React component or route existed | Created full CRUD page, added `/shifts` route in App.tsx, added to Organisation sidebar group |
| 13 | `BiometricPage.tsx` — page missing, route 404 | **High** | `App.tsx`, `BiometricPage.tsx`, `Sidebar.tsx` | No React component or route existed | Created read-only biometric log viewer with date-range filter and CSV export; added `/biometric` route |
| 14 | `DepartmentPage.tsx` — page missing, route 404 | **High** | `App.tsx`, `DepartmentPage.tsx`, `Sidebar.tsx` | No React component or route existed | Created full CRUD page with name/code/head fields; added `/departments` route |
| 15 | `HolidayPage.tsx` — page missing, route 404 | **High** | `App.tsx`, `HolidayPage.tsx`, `Sidebar.tsx` | No React component or route existed | Created full CRUD page with year-filter and recurring flag; added `/holidays` route |
| 16 | Sidebar missing navigation for 4 restored modules | **Medium** | `Sidebar.tsx` | No nav items for Shift, Biometric, Department, Holiday | Added new "Organisation" group with 4 nav items; imported `Building2` and `Fingerprint` icons |
| 17 | No cross-tenant training enrollment tests | **High** | `HRMS.Tests/Security/TrainingEnrollmentIdorTests.cs` | Zero test coverage for SEC-TRAINING-01 | Added 5 xUnit tests: same-company success, cross-tenant block, ghost employee, double-enroll, global training |
| 18 | No CompanyBranch ownership tests | **High** | `HRMS.Tests/Security/CompanyBranchIdorTests.cs` | Zero test coverage for SEC-BRANCH-01 | Added 7 xUnit tests covering GetBranch, UpdateBranch, DeleteBranch cross-company and same-company scenarios |

---

## Interface / Signature Changes

Two service interfaces changed signatures. Controller callers must be updated:

### `ICompanyBranchService`
```csharp
// Old
Task<CompanyBranchDto?> GetBranchAsync(int branchId);
Task<bool> UpdateBranchAsync(int branchId, CreateCompanyBranchDto dto);
Task<bool> DeleteBranchAsync(int branchId);

// New (adds callerCompanyId parameter)
Task<CompanyBranchDto?> GetBranchAsync(int branchId, int callerCompanyId);
Task<bool> UpdateBranchAsync(int branchId, int callerCompanyId, CreateCompanyBranchDto dto);
Task<bool> DeleteBranchAsync(int branchId, int callerCompanyId);
```

**Controller fix required in `CompanyBranchController.cs`:**
Extract `companyId` from `User.FindFirst("CompanyId")?.Value` (already available in `BaseController`) and pass it as `callerCompanyId`.

### `ITrainingService`
```csharp
// Old
Task<(bool ok, string message)> EnrollAsync(int programId, string employeeId);

// New
Task<(bool ok, string message, bool isCrossTenant)> EnrollAsync(int programId, string employeeId);
```

**Controller fix required in `TrainingController.cs`:**
Map `isCrossTenant == true` → `return Forbid()` (HTTP 403).

---

## Files Changed

```
HRMS.Infrastructure/Services/TrainingService.cs               ← modified
HRMS.Infrastructure/Services/CompanyBranchService.cs          ← modified
HRMS.Infrastructure/Migrations/
  20260721200001_RestoreSecurityAndPerformanceIndexes.cs       ← new
HRMS.Tests/Security/TrainingEnrollmentIdorTests.cs            ← new
HRMS.Tests/Security/CompanyBranchIdorTests.cs                 ← new
HRMS.Tests/Regression/TimesheetAdminRoleTests.cs              ← new
HRMS.SPA.Source/src/pages/shifts/ShiftPage.tsx                ← new
HRMS.SPA.Source/src/pages/biometric/BiometricPage.tsx         ← new
HRMS.SPA.Source/src/pages/departments/DepartmentPage.tsx      ← new
HRMS.SPA.Source/src/pages/holidays/HolidayPage.tsx            ← new
HRMS.SPA.Source/src/pages/timesheet/TimesheetPage.tsx         ← modified
HRMS.SPA.Source/src/App.tsx                                   ← modified
HRMS.SPA.Source/src/components/layout/Sidebar.tsx             ← modified
```

---

*Fix Report P3 — 2026-07-21*
