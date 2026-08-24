# Phase 9 — Full Regression Plan
## RatanHR HRMS v1.0.0-rc1

**Prepared:** 2026-08-04  
**Executed by:** DevOps / QA on staging server  
**Entry condition:** Phase 8 staging runbook completed, all gates 1–8 PASS  
**Exit condition:** All 21 module sections PASS, 0 FAILs, tenant isolation PASS  
**Script:** `bash phase9_run.sh 2>&1 | tee /tmp/phase9_run.log`

---

## Test inventory

| Layer | Count | Tool |
|---|---|---|
| .NET unit + integration tests | 934 | `dotnet test` (xUnit) |
| SPA unit tests | 3 | `bun run test` (vitest) |
| E2E tests | 625 | Playwright (Chromium · Firefox · Mobile Chrome) |
| Phase 8 smoke checks | 67 | `Staging/phase8_runbook.sh` |
| Phase 8 DB validation checks | 42 | `Staging/phase8_runbook.sh` |
| Phase 9 workflow checks | 38 | `phase9_run.sh` inline |
| **Total automated checks** | **~1,709** | |

---

## Environment requirements

```
Server:     Ubuntu 22.04 LTS — 4 vCPUs, 8 GB RAM, 40 GB disk
Docker:     Engine 24+ and Compose plugin 2.24+
bun:        1.3+
dotnet:     .NET SDK 8.0.416 (or match Dockerfile)
mysql:      mysql-client (for seed verification)
Files:      .env.e2e filled from .env.e2e.template
            e2e/e2e_seed.sql present
Stack:      docker-compose.e2e.yml up and healthy before E2E
```

---

## Module-by-module regression plan

---

### Module 1 — Login & Session

**Source files:** `AuthController.cs`, `e2e/auth.spec.ts`, `e2e/session.spec.ts`  
**Test class:** `HRMS.Tests/MiddlewareTests/MustChangePasswordMiddlewareTests.cs`

| ID | Check | Method | Expected | Layer |
|---|---|---|---|---|
| M01-01 | SuperAdmin login with correct credentials | POST /api/auth/login | 200 + JWT cookie | E2E + smoke |
| M01-02 | Admin login (company-scoped) | POST /api/auth/login (portal=Admin) | 200 + company claim in token | E2E + smoke |
| M01-03 | Employee login | POST /api/auth/login (portal=employee) | 200 | E2E + smoke |
| M01-04 | Wrong password → 401 | POST /api/auth/login | 401 | smoke |
| M01-05 | Empty portal → 400 | POST /api/auth/login | 400 | smoke |
| M01-06 | Account lockout after 5 bad attempts | POST /api/auth/login ×6 | 429 or 423 on 6th | unit |
| M01-07 | MFA TOTP enroll and verify flow | POST /api/mfa/setup + /api/mfa/verify | 200 both | E2E |
| M01-08 | MFA required flag blocks login until verified | POST /api/auth/login (MFA enabled) | 200 + requiresMfa:true | unit |
| M01-09 | MustChangePassword middleware redirects | GET any protected route | 403 with mustChangePassword | unit |
| M01-10 | Session cookie HttpOnly and Secure flags | Any authenticated response | Set-Cookie: HttpOnly; Secure | smoke |
| M01-11 | Logout clears session | POST /api/auth/logout | 200, cookie cleared | E2E |
| M01-12 | Login history recorded in DB | GET /api/login-history | Row inserted per login | integration |

**Pass criteria:** All 12 checks PASS, 0 token-in-localStorage, 0 stack traces in error responses.

---

### Module 2 — JWT / Token Lifecycle

**Source files:** `AuthController.cs`, `e2e/session.spec.ts`, `HRMS.Tests/Security/`

| ID | Check | Method | Expected | Layer |
|---|---|---|---|---|
| M02-01 | Access token in HttpOnly cookie only | GET /api/employees | Cookie header, not Authorization | smoke |
| M02-02 | Refresh token rotation on /api/auth/refresh | POST /api/auth/refresh | 200 + new access token, old refresh invalidated | unit |
| M02-03 | Tampered JWT → 401 | GET /api/employees (forged token) | 401 | smoke |
| M02-04 | Expired token → 401 | GET /api/employees (expired token) | 401 | smoke |
| M02-05 | Token algorithm RS256 (not HS256) | Decode token header | alg: RS256 | unit |
| M02-06 | Refresh without cookie → 401 | POST /api/auth/refresh | 401 "Refresh token missing" | smoke |
| M02-07 | CSRF double-submit token present on mutating routes | POST /api/employees | X-CSRF-TOKEN validated | unit |
| M02-08 | CSRF endpoint responds | GET /api/auth/csrf | 200 | smoke |

**Pass criteria:** All 8 checks PASS. No symmetric (HS256) signing anywhere in production path.

---

### Module 3 — RBAC (Role-Based Access Control)

**Source files:** `RolesController.cs`, `PermissionsController.cs`, `e2e/rbac.spec.ts`  
**Test class:** `HRMS.Tests/Regression/TimesheetAdminRoleTests.cs`

| ID | Check | Expected | Layer |
|---|---|---|---|
| M03-01 | SuperAdmin can access all companies | GET /api/companies → 200 | smoke |
| M03-02 | Admin cannot access SuperAdmin routes | GET /api/superadmin/* → 403 | smoke + E2E |
| M03-03 | Employee cannot access Admin routes | GET /api/employees → 403 | E2E |
| M03-04 | Employee can access own profile only | GET /api/my/profile → 200, /api/employees → 403 | E2E |
| M03-05 | Payroll role required for payroll CRUD | GET /api/payroll (employee token) → 403 | E2E |
| M03-06 | Audit role can read audit log | GET /api/audit (auditor token) → 200 | E2E |
| M03-07 | All 62 protected controllers have [Authorize] | Static analysis — `[Authorize(Roles=` present | static |
| M03-08 | Timesheet admin role edge case | POST /api/timesheet (TimesheetAdmin role) | unit |

**Pass criteria:** All 8 checks PASS. No endpoint accepts unauthenticated writes.

---

### Module 4 — Employee Management

**Source files:** `EmployeeController.cs`, `EmployeeSelfController.cs`, `e2e/employees.spec.ts`, `e2e/employees-crud.spec.ts`  
**Test class:** `HRMS.Tests/Regression/EmployeeByIdMySqlRegressionTests.cs`, `HRMS.Tests/Security/EmployeeSelfControllerIdorIntegrationTests.cs`

| ID | Check | Expected | Layer |
|---|---|---|---|
| M04-01 | List employees (Admin, paginated) | GET /api/employees → 200 + pagination | E2E |
| M04-02 | Create employee (Admin) | POST /api/employees → 201 + employeeId | E2E |
| M04-03 | Update employee details | PUT /api/employees/{id} → 200 | E2E |
| M04-04 | Soft-delete employee | DELETE /api/employees/{id} → 200, IsActive=false | E2E |
| M04-05 | Employee self-profile (own data only) | GET /api/my/profile → 200 | E2E |
| M04-06 | Employee cannot update other employee | PUT /api/employees/{other_id} (emp token) → 403 | unit |
| M04-07 | IDOR: employee A cannot read employee B's record | GET /api/employees/{B_id} (A token) → 403/404 | unit |
| M04-08 | PII fields (Aadhaar, PAN) encrypted at rest | DB direct query → encrypted ciphertext | integration |
| M04-09 | Employee transfer history recorded | POST /api/employee-transfers → 201 | E2E |
| M04-10 | Employee promotion recorded | POST /api/employee-promotions → 201 | E2E |
| M04-11 | Employee exit flow | POST /api/employee-exits → 200 | E2E |
| M04-12 | Pagination + search + sort | GET /api/employees?search=&sort= → 200 + correct order | E2E |

**Pass criteria:** All 12 checks PASS. PII ciphertext confirmed in DB — never plaintext.

---

### Module 5 — Attendance

**Source files:** `AttendanceController.cs`, `GpsAttendanceController.cs`, `GeoFenceController.cs`, `BiometricController.cs`, `e2e/attendance.spec.ts`  
**Test class:** `HRMS.Tests/Attendance/AttendanceEdgeCaseTests.cs`, `HRMS.Tests/IntegrationTests/AttendanceIntegrationTests.cs`

| ID | Check | Expected | Layer |
|---|---|---|---|
| M05-01 | Clock-in records attendance | POST /api/attendance/clock-in → 201 | E2E |
| M05-02 | Clock-out updates record | POST /api/attendance/clock-out → 200 | E2E |
| M05-03 | Duplicate clock-in rejected | POST /api/attendance/clock-in ×2 → 409 | unit |
| M05-04 | GPS attendance with coordinates | POST /api/gps/attendance → 201 | E2E |
| M05-05 | Geofence validation (outside range → rejected) | POST /api/gps/attendance (out of range) → 422 | unit |
| M05-06 | Biometric capabilities endpoint | GET /api/biometric/capabilities → 200 | smoke |
| M05-07 | Attendance report accessible to Admin | GET /api/attendance/report → 200 | E2E |
| M05-08 | Employee can only view own attendance | GET /api/attendance (emp token) → own records only | unit |
| M05-09 | Cross-tenant attendance isolation | Admin A cannot see company B attendance | unit |

**Pass criteria:** All 9 checks PASS.

---

### Module 6 — Leave Management

**Source files:** `LeaveController.cs`, `e2e/leave.spec.ts`  
**Test class:** `HRMS.Tests/Leave/LeaveEdgeCaseTests.cs`, `HRMS.Tests/IntegrationTests/LeaveIntegrationTests.cs`

| ID | Check | Expected | Layer |
|---|---|---|---|
| M06-01 | Apply for leave | POST /api/leave → 201 (status=Pending) | E2E |
| M06-02 | Admin approves leave | PUT /api/leave/{id}/approve → 200 (status=Approved) | E2E |
| M06-03 | Admin rejects leave | PUT /api/leave/{id}/reject → 200 (status=Rejected) | E2E |
| M06-04 | Leave balance updated on approval | GET /api/leave/balance → balance decremented | integration |
| M06-05 | Cannot apply if insufficient balance | POST /api/leave (balance=0) → 422 | unit |
| M06-06 | Overlapping leave rejected | POST /api/leave (overlap dates) → 409 | unit |
| M06-07 | Leave types listing | GET /api/leave/types → 200 + list | smoke |
| M06-08 | Leave report (Admin) | GET /api/leave/report → 200 | E2E |
| M06-09 | Employee can only view own leaves | GET /api/leave (emp token) → own records only | unit |

**Pass criteria:** All 9 checks PASS. Balance arithmetic validated.

---

### Module 7 — Holiday Calendar

**Source files:** `HolidayController.cs`, `e2e/attendance.spec.ts` (holiday section)

| ID | Check | Expected | Layer |
|---|---|---|---|
| M07-01 | List company holidays | GET /api/holidays → 200 + list | smoke |
| M07-02 | Create holiday (Admin) | POST /api/holidays → 201 | E2E |
| M07-03 | Edit holiday | PUT /api/holidays/{id} → 200 | E2E |
| M07-04 | Delete holiday | DELETE /api/holidays/{id} → 200 | E2E |
| M07-05 | Holiday appears in leave calculation | Leave on holiday day → handled correctly | unit |

**Pass criteria:** All 5 checks PASS.

---

### Module 8 — Shift Management

**Source files:** `ShiftController.cs`

| ID | Check | Expected | Layer |
|---|---|---|---|
| M08-01 | List shifts | GET /api/shifts → 200 | smoke |
| M08-02 | Create shift | POST /api/shifts → 201 | E2E |
| M08-03 | Assign employee to shift | PUT /api/employees/{id}/shift → 200 | E2E |
| M08-04 | Shift appears on employee attendance record | GET /api/attendance → shift_name populated | integration |
| M08-05 | Cross-tenant shift isolation | Admin A cannot see company B shifts | unit |

**Pass criteria:** All 5 checks PASS.

---

### Module 9 — Department Management

**Source files:** `DepartmentController.cs`, `e2e/org-chart.spec.ts`

| ID | Check | Expected | Layer |
|---|---|---|---|
| M09-01 | List departments | GET /api/departments → 200 | smoke |
| M09-02 | Create department | POST /api/departments → 201 | E2E |
| M09-03 | Edit department | PUT /api/departments/{id} → 200 | E2E |
| M09-04 | Delete unused department | DELETE /api/departments/{id} → 200 | E2E |
| M09-05 | Cannot delete department with employees | DELETE /api/departments/{id} (has employees) → 409 | unit |
| M09-06 | Department org chart | GET /api/org-chart → 200 + tree structure | E2E |

**Pass criteria:** All 6 checks PASS.

---

### Module 10 — Designation Management

**Source files:** Designation endpoints in `EmployeeController.cs` / `DepartmentController.cs`

| ID | Check | Expected | Layer |
|---|---|---|---|
| M10-01 | List designations | GET /api/designations → 200 | smoke |
| M10-02 | Create designation | POST /api/designations → 201 | E2E |
| M10-03 | Edit designation | PUT /api/designations/{id} → 200 | E2E |
| M10-04 | Designation appears on employee record | Employee.Designation populated | integration |

**Pass criteria:** All 4 checks PASS.

---

### Module 11 — Payroll

**Source files:** `PayrollController.cs`, `SalaryController.cs`, `BonusController.cs`, `DeductionController.cs`, `e2e/payroll.spec.ts`  
**Test class:** `HRMS.Tests/Payroll/PayrollEdgeCaseTests.cs`, `HRMS.Tests/IntegrationTests/PayrollIntegrationTests.cs`, `HRMS.Tests/Security/BonusDeductionSecurityTests.cs`, `HRMS.Tests/Security/PayrollAttendanceTenantTests.cs`

| ID | Check | Expected | Layer |
|---|---|---|---|
| M11-01 | List salary structures | GET /api/salary → 200 | smoke |
| M11-02 | Create salary structure | POST /api/salary → 201 | E2E |
| M11-03 | Run payroll for a period | POST /api/payroll/run → 200 + batch result | E2E |
| M11-04 | Payroll calculation correct (basic + HRA + deductions) | Computed net = gross − deductions | unit |
| M11-05 | Bonus added to payroll run | POST /api/bonuses → 201, included in run | integration |
| M11-06 | Deduction applied | POST /api/deductions → 201, reduces net | integration |
| M11-07 | Payroll locked after processing (cannot re-run) | POST /api/payroll/run (already run) → 409 | unit |
| M11-08 | Cross-tenant payroll isolation | Admin A cannot view company B payroll | unit |
| M11-09 | Payroll report accessible | GET /api/payroll/report → 200 | E2E |

**Pass criteria:** All 9 checks PASS. Net salary arithmetic verified in unit tests.

---

### Module 12 — Payslip

**Source files:** `PayslipController.cs`, `SalaryRegisterController.cs`, `e2e/payslips.spec.ts`

| ID | Check | Expected | Layer |
|---|---|---|---|
| M12-01 | Payslip generated after payroll run | GET /api/payslip → 200 + list | E2E |
| M12-02 | Employee can download own payslip | GET /api/payslip/{id} (own) → 200 + PDF | E2E |
| M12-03 | Employee cannot download other's payslip | GET /api/payslip/{other_id} (emp token) → 403 | unit |
| M12-04 | Salary register (Admin) | GET /api/salary-register → 200 + all employees | E2E |
| M12-05 | Payslip has correct pay period dates | payslip.periodStart + periodEnd correct | unit |

**Pass criteria:** All 5 checks PASS.

---

### Module 13 — Recruitment

**Source files:** `RecruitmentController.cs`, `e2e/recruitment.spec.ts`

| ID | Check | Expected | Layer |
|---|---|---|---|
| M13-01 | Create job posting | POST /api/recruitment → 201 | E2E |
| M13-02 | List job postings | GET /api/recruitment → 200 | E2E |
| M13-03 | Add candidate application | POST /api/recruitment/{id}/applications → 201 | E2E |
| M13-04 | Move candidate through pipeline stages | PUT /api/recruitment/{id}/applications/{app_id} → 200 | E2E |
| M13-05 | Close job posting | PUT /api/recruitment/{id}/close → 200 | E2E |

**Pass criteria:** All 5 checks PASS.

---

### Module 14 — Performance Management

**Source files:** `PerformanceController.cs`, `e2e/performance.spec.ts`

| ID | Check | Expected | Layer |
|---|---|---|---|
| M14-01 | Create appraisal cycle | POST /api/performance → 201 | E2E |
| M14-02 | Assign goals to employee | POST /api/performance/goals → 201 | E2E |
| M14-03 | Employee self-rates goals | PUT /api/performance/goals/{id} (self) → 200 | E2E |
| M14-04 | Manager rates employee | PUT /api/performance/goals/{id} (manager) → 200 | E2E |
| M14-05 | Final performance score computed | GET /api/performance/{id}/score → 200 + numeric score | unit |
| M14-06 | Employee can only rate own goals | PUT /api/performance/goals/{other_id} (emp) → 403 | unit |

**Pass criteria:** All 6 checks PASS.

---

### Module 15 — CRM / Sales

**Source files:** `SalesController.cs`, `e2e/sales.spec.ts`

| ID | Check | Expected | Layer |
|---|---|---|---|
| M15-01 | Create sales lead | POST /api/sales/leads → 201 | E2E |
| M15-02 | List leads (Sales role) | GET /api/sales/leads → 200 | E2E |
| M15-03 | Update lead stage | PUT /api/sales/leads/{id} → 200 | E2E |
| M15-04 | Create sales activity | POST /api/sales/activities → 201 | E2E |
| M15-05 | Sales report | GET /api/sales/report → 200 | E2E |
| M15-06 | Non-Sales role cannot access leads | GET /api/sales/leads (emp token) → 403 | E2E |

**Pass criteria:** All 6 checks PASS.

---

### Module 16 — Asset Management

**Source files:** `AssetsController.cs`, `e2e/assets.spec.ts`

| ID | Check | Expected | Layer |
|---|---|---|---|
| M16-01 | Create asset | POST /api/assets → 201 | E2E |
| M16-02 | Assign asset to employee | POST /api/assets/{id}/assign → 200 | E2E |
| M16-03 | Return asset | POST /api/assets/{id}/return → 200 | E2E |
| M16-04 | List assets (Admin) | GET /api/assets → 200 + list | E2E |
| M16-05 | Employee views own assigned assets | GET /api/my/assets → 200 | E2E |

**Pass criteria:** All 5 checks PASS.

---

### Module 17 — Notifications

**Source files:** `NotificationController.cs`, `e2e/smoke.spec.ts` (notification section)

| ID | Check | Expected | Layer |
|---|---|---|---|
| M17-01 | List notifications | GET /api/notifications → 200 + list | smoke |
| M17-02 | Filter unread notifications | GET /api/notifications?unreadOnly=true → 200 | smoke |
| M17-03 | Mark notification as read | PUT /api/notifications/{id}/read → 200 | E2E |
| M17-04 | Mark all as read | PUT /api/notifications/read-all → 200 | E2E |
| M17-05 | Email queue (SuperAdmin) | GET /api/email-queue → 200 | smoke |

**Pass criteria:** All 5 checks PASS.

---

### Module 18 — Documents & File Uploads

**Source files:** `EmployeeDocumentController.cs`, `e2e/uploads-downloads.spec.ts`  
**Test class:** `HRMS.Tests/Infrastructure/UploadValidationIntegrationTests.cs`

| ID | Check | Expected | Layer |
|---|---|---|---|
| M18-01 | Upload employee document (PDF) | POST /api/employee-documents → 201 | E2E |
| M18-02 | Download own document | GET /api/employee-documents/{id} (own) → 200 + file | E2E |
| M18-03 | Cannot download other's document | GET /api/employee-documents/{other_id} (emp) → 403 | unit |
| M18-04 | Rejected file type (exe, php) | POST /api/employee-documents (exe file) → 422 | unit |
| M18-05 | File size limit enforced | POST /api/employee-documents (>10MB) → 413 | unit |
| M18-06 | Uploads served by nginx directly | GET /uploads/{file} → 200 (nginx, no API hop) | smoke |

**Pass criteria:** All 6 checks PASS. No server-side path traversal in filenames.

---

### Module 19 — Reports & Analytics

**Source files:** `ReportController.cs`, `EmployeeReportController.cs`, `AttendanceReportController.cs`, `LeaveReportController.cs`, `PayrollReportController.cs`, `DashboardReportController.cs`, `e2e/reports.spec.ts`  
**Test class:** `HRMS.Tests/IDOR/ReportControllerIDORTests.cs`

| ID | Check | Expected | Layer |
|---|---|---|---|
| M19-01 | Employee report (Admin) | GET /api/reports/employees → 200 + CSV/JSON | E2E |
| M19-02 | Attendance report | GET /api/reports/attendance → 200 | E2E |
| M19-03 | Leave report | GET /api/reports/leave → 200 | E2E |
| M19-04 | Payroll report | GET /api/reports/payroll → 200 | E2E |
| M19-05 | Report IDOR — company A cannot pull company B report | GET /api/reports/employees?companyId=B (A token) → 403 | unit |
| M19-06 | Streaming Excel export (large dataset) | GET /api/reports/employees?format=xlsx → 200 + valid XLSX | E2E |
| M19-07 | Analytics dashboard summary | GET /api/analytics → 200 | E2E |

**Pass criteria:** All 7 checks PASS.

---

### Module 20 — Dashboard

**Source files:** `DashboardController.cs`, `e2e/smoke.spec.ts` (dashboard section)

| ID | Check | Expected | Layer |
|---|---|---|---|
| M20-01 | Dashboard summary loads (SuperAdmin) | GET /api/dashboard → 200 + summary stats | smoke |
| M20-02 | Dashboard loads for Admin (company-scoped) | GET /api/dashboard (admin token) → company stats only | E2E |
| M20-03 | Dashboard loads for Employee (personal stats) | GET /api/dashboard (emp token) → own stats only | E2E |
| M20-04 | Headcount, attendance today, pending leaves shown | All three fields present in response | E2E |
| M20-05 | Dashboard data scoped to caller's company | Admin A stats ≠ Admin B stats | smoke |

**Pass criteria:** All 5 checks PASS.

---

### Module 21 — Company A vs Company B Tenant Isolation Security Test

**Source files:** `HRMS.Tests/Security/TenantRepositoryTests.cs`, `HRMS.Tests/Security/CompanyBranchIdorTests.cs`, `HRMS.Tests/Security/TrainingEnrollmentIdorTests.cs`, `HRMS.Tests/Security/PayrollAttendanceTenantTests.cs`  
**E2E accounts:** e2e.adminA / e2e.adminB (Companies 9001 / 9002 from `e2e/e2e_seed.sql`)

| ID | Isolation check | Attack vector | Expected | Layer |
|---|---|---|---|---|
| M21-01 | Admin A cannot list Company B employees | GET /api/employees (adminA token) | Only company 9001 employees | smoke + unit |
| M21-02 | Admin B cannot list Company A employees | GET /api/employees (adminB token) | Only company 9002 employees | smoke + unit |
| M21-03 | Admin A cannot access Company B branches (IDOR) | GET /api/companies/9002/branches (adminA) | 403 or 404 | smoke + unit |
| M21-04 | Admin A payslip cannot reference Company B employee | GET /api/payslip/{B_emp_id} (adminA) | 403 | unit |
| M21-05 | Admin A attendance cannot reference Company B employee | GET /api/attendance?employeeId={B_emp_id} (adminA) | 403 / empty | unit |
| M21-06 | Admin A reports scoped to company 9001 only | GET /api/reports/employees (adminA) | No company 9002 rows | unit |
| M21-07 | EF Core global query filter verified in DB | GenericRepository.GetAllAsync with tenant A | Returns zero B rows | unit |
| M21-08 | RequireTenantForWriteAttribute blocks cross-tenant write | PUT /api/employees/{B_emp_id} (adminA) | 403 | unit |
| M21-09 | Training enrollment IDOR | EnrollAsync(enrolleeId from B, callerCompanyId=A) | null / throws | unit |
| M21-10 | Company branch IDOR (GetBranchAsync) | GetBranchAsync(B_branch_id, callerCompanyId=A) | null | unit |

**Pass criteria:** All 10 isolation checks PASS. Any BREACH is a go-live blocker.

---

## Summary gate

| # | Module | Min pass rate | Blocker on FAIL |
|---|---|---|---|
| 1 | Login & Session | 12/12 | ✅ Yes |
| 2 | JWT / Token | 8/8 | ✅ Yes |
| 3 | RBAC | 8/8 | ✅ Yes |
| 4 | Employee | 12/12 | ✅ Yes |
| 5 | Attendance | 9/9 | ✅ Yes |
| 6 | Leave | 9/9 | ✅ Yes |
| 7 | Holiday | 5/5 | No |
| 8 | Shift | 5/5 | No |
| 9 | Department | 6/6 | No |
| 10 | Designation | 4/4 | No |
| 11 | Payroll | 9/9 | ✅ Yes |
| 12 | Payslip | 5/5 | ✅ Yes |
| 13 | Recruitment | 5/5 | No |
| 14 | Performance | 6/6 | No |
| 15 | CRM / Sales | 6/6 | No |
| 16 | Assets | 5/5 | No |
| 17 | Notifications | 5/5 | No |
| 18 | Documents | 6/6 | ✅ Yes |
| 19 | Reports | 7/7 | ✅ Yes |
| 20 | Dashboard | 5/5 | No |
| 21 | Tenant Isolation | 10/10 | ✅ Yes |
| — | Phase 8 smoke | 67/67 | ✅ Yes |
| — | Phase 8 DB validation | 42/42 | ✅ Yes |
| — | Playwright E2E 625 | 625/625 | ✅ Yes |
| — | .NET 934 unit tests | 934/934 | ✅ Yes |

**Go-live requires:** 0 FAILs on all ✅ blocker modules + all E2E and unit test counts at target.

---

*Phase 9 plan generated 2026-08-04 from source analysis of 706 files, all 25 E2E spec files, and Phase 8 runbook (67 smoke + 42 DB checks).*
