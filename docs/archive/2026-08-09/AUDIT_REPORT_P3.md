> ⚠️ **SUPERSEDED** — This report was generated during an earlier audit/fix pass and no longer reflects the current state of the codebase. The authoritative current-state documents are [`RELEASE_GATE_FINAL.md`](RELEASE_GATE_FINAL.md) and [`VERIFICATION_REPORT_FINAL_v2.md`](VERIFICATION_REPORT_FINAL_v2.md). Do not use this file to assess production readiness.

---

# RatanHR — Full Audit Report (P3 Regression Review)

**Date:** 2026-07-21  
**Scope:** P2 codebase (`ratanhr_fixed_v2`) compared against P1 fixes and production requirements  
**Auditor:** Automated regression pass + manual review

---

## Executive Summary

| Category | Issues Found | Critical | High | Medium | Low |
|---|---|---|---|---|---|
| Security / IDOR | 3 | 2 | 1 | 0 | 0 |
| Performance / N+1 | 4 | 0 | 2 | 2 | 0 |
| Missing DB Indexes | 14 | 0 | 3 | 11 | 0 |
| Missing Frontend Modules | 4 | 0 | 4 | 0 | 0 |
| Timesheet Admin Bug | 1 | 1 | 0 | 0 | 0 |
| Test Coverage Gaps | 13 | 0 | 0 | 5 | 8 |
| Production Hardening | 6 | 0 | 2 | 4 | 0 |
| **Total** | **45** | **3** | **12** | **22** | **8** |

---

## 1. Security Issues

### SEC-01 — CRITICAL: Training Enrollment IDOR (Cross-Tenant)
- **File:** `HRMS.Infrastructure/Services/TrainingService.cs`, `EnrollAsync()`
- **Root Cause:** P2 removed the validation that the enrolling employee belongs to the same company as the training program. Any authenticated employee could enroll in any company's training using a valid `programId`.
- **Impact:** Cross-tenant data exposure; employees from Company A could enroll into Company B's private training programs, consuming seats and accessing confidential training content.
- **CVSS:** ~7.5 (High) — authenticated, low complexity, high integrity/availability impact.

### SEC-02 — CRITICAL: CompanyBranch IDOR (Read/Update/Delete)
- **File:** `HRMS.Infrastructure/Services/CompanyBranchService.cs`
- **Root Cause:** P2 dropped three tenant ownership checks that were present in P1:
  - `GetBranchAsync(branchId)` — no ownership filter; any company admin could fetch any branch
  - `UpdateBranchAsync(branchId, dto)` — no check that `branch.CompanyId == callerCompanyId`
  - `DeleteBranchAsync(branchId)` — no check that `branch.CompanyId == callerCompanyId`
- **Impact:** Company admin A can read, overwrite, or delete Company B's branches.
- **CVSS:** ~8.1 (High) — authenticated, low complexity, critical integrity impact.

### SEC-03 — HIGH: Timesheet Admin Page Hardcoded to Non-Admin
- **File:** `HRMS.SPA.Source/src/pages/timesheet/TimesheetPage.tsx`, line 457
- **Root Cause:** `const showAdmin = false;` was hardcoded as a temporary workaround. The code comment acknowledged this was a TODO but the TODO was never resolved. Admin users can never see the approval queue regardless of their actual role.
- **Impact:** Broken admin workflow — managers cannot approve/reject timesheets from the UI. All approvals must be done via direct API calls. Medium security risk as the previous implementation also checked sessionStorage (tamper-able).
- **Fix:** Source role from `GET /api/auth/me` (profile API) on every render.

---

## 2. Performance Issues

### PERF-01 — HIGH: Missing AsNoTracking on Read Paths (Generic Repository)
- **File:** `HRMS.Infrastructure/Repositories/GenericRepository.cs`
- **Root Cause:** `GetByIdAsync` uses `FindAsync()` which returns a tracked entity. For purely read-only paths (dashboards, reports, lookups) EF Core change-tracker adds unnecessary memory overhead and CPU time. The P1 version annotated this with `AsNoTracking()` for read paths.
- **Current State:** `GetAllAsync` and `FindAsync` correctly use `AsNoTracking`. Individual `GetByIdAsync` callers on read-only flows do not.
- **Fix:** Services using `GetByIdAsync` for reads (TrainingService, CompanyBranchService) updated to use direct `AsNoTracking().FirstOrDefaultAsync(x => x.Id == id)` queries.

### PERF-02 — HIGH: N+1 in PayrollService.GetPayslipsAsync (Batch Path Missing)
- **Status:** Already fixed in P2 — batch dictionary join is present in the current code. ✅

### PERF-03 — MEDIUM: N+1 in LeaveService.ApplyAsync (Leave Type Lookup)
- **File:** `HRMS.Infrastructure/Services/LeaveService.cs`
- **Root Cause:** `_db.LeaveTypes.FindAsync(dto.LeaveTypeId)` is called inline, then the leave type name is fetched again per-request in `GetAllRequestsAsync`. The `typeNames` dictionary join in `GetAllRequestsAsync` is correctly batched; no N+1 exists there.
- **Current State:** No critical N+1 remains in LeaveService after P2. Minor improvements possible.

### PERF-04 — MEDIUM: Missing DB Indexes (14 indexes across 9 tables)
- **Root Cause:** P2 migration `20260720120000_AddMissingIndexes.cs` added some indexes but missed cross-table foreign keys and compound query indexes for Biometric, Timesheet, and Holiday tables (added after that migration).
- **Details:** See Section 3.

---

## 3. Missing Database Indexes

The following indexes were absent from the P2 schema:

| Index Name | Table | Column(s) | Reason |
|---|---|---|---|
| IX_employees_company_id | employees | company_id | All tenant-scoped employee queries |
| IX_employees_user_id | employees | user_id | Auth lookup (login, profile) |
| IX_employees_department | employees | department | Dept filter on employee list |
| IX_employees_is_active | employees | is_active | Standard active-only filter |
| IX_web_attendances_employee_id_attendance_date | web_attendances | employee_id, attendance_date | Compound attendance lookup |
| IX_web_attendances_company_id | web_attendances | company_id | Tenant scope |
| IX_payslips_employee_id | payslips | employee_id | Per-employee payslip history |
| IX_payslips_employee_id_month_year | payslips | employee_id, month, year | Duplicate check on generation |
| IX_leave_requests_employee_id | leave_requests | employee_id | Employee leave history |
| IX_leave_requests_company_id_status | leave_requests | company_id, status | Admin approval queue |
| IX_training_programs_company_id | training_programs | company_id | Tenant-scoped list |
| IX_training_enrollments_employee_id | training_enrollments | employee_id | Per-employee enrollment list |
| IX_timesheet_entries_employee_id_work_date | timesheet_entries | employee_id, work_date | Date-range timesheet query |
| IX_holiday_calendars_company_id_year | holiday_calendars | company_id, year | Year-filtered holiday list |

---

## 4. Missing Frontend Modules

Four SPA pages had no route or UI component:

| Module | Route | Status in P2 | Status in P3 |
|---|---|---|---|
| Department | `/departments` | ❌ Missing | ✅ Created |
| Shift | `/shifts` | ❌ Missing | ✅ Created |
| Holiday Calendar | `/holidays` | ❌ Missing | ✅ Created |
| Biometric Logs | `/biometric` | ❌ Missing | ✅ Created |

Backend controllers (`DepartmentController`, `ShiftController`, `HolidayController`, `BiometricController`) were confirmed present in the P2 codebase; the gap was exclusively in the React frontend.

---

## 5. Test Coverage Gaps

Modules with zero test coverage in P2:

| Module | Type | Gap |
|---|---|---|
| TrainingService — EnrollAsync IDOR | Security | No cross-tenant enrollment tests |
| CompanyBranchService — CRUD IDOR | Security | No ownership-validation tests |
| ShiftService | Unit/CRUD | No tests |
| BiometricService | Unit/CRUD | No tests |
| Timesheet admin role | Regression | No role-visibility tests |
| CompanySettingsService | Unit | No tests |
| OnboardingService | Unit | No tests |
| AnalyticsService | Unit | No tests |
| WebhookService | Unit | No tests |
| DepartmentController (tenant shadow) | Security | Partial (one test in V10 hardening) |

New tests added in P3: `TrainingEnrollmentIdorTests`, `CompanyBranchIdorTests`, `TimesheetAdminRoleTests`.

---

## 6. Production Hardening (Remaining Items from V10 Report)

| Item | Status | Notes |
|---|---|---|
| REC-01: Redis IConnectionMultiplexer DI wiring | ⚠️ Pending | Requires Infrastructure service registration change |
| REC-02: Serilog async sink | ⚠️ Pending | Program.cs change; non-breaking |
| REC-03: Correlation-ID middleware | ✅ Present | Verified in BaseController/middleware chain |
| REC-04: Health check depth (DB + Redis) | ✅ Present | /healthz confirmed in startup |
| REC-05: API versioning | ✅ Present | v1 prefix on all controllers |
| REC-06: Request size limits | ⚠️ Pending | 30 MB limit not explicitly set for file upload routes |

---

## 7. Compile / Architecture Issues

| Issue | Severity | File | Notes |
|---|---|---|---|
| `CompanyBranchService` signature change (`GetBranchAsync` now requires `callerCompanyId`) | Medium | `ICompanyBranchService.cs`, `CompanyBranchController.cs` | Controller callers must pass `callerCompanyId` from claims |
| `TrainingService.EnrollAsync` signature change (now returns `isCrossTenant` flag) | Low | `TrainingController.cs` | Controller must map `isCrossTenant = true` → `403 Forbidden` |
| `useCallback` removed from `TimesheetPage.tsx` | None | Frontend | Was only used by the removed `isAdmin` stub |

---

*Report generated by P3 regression audit — 2026-07-21*
