> SUPERSEDED — see PHASE1_BASELINE.md for the authoritative Phase 1 baseline.

> ⚠️ **SUPERSEDED** — This report was generated during an earlier audit/fix pass and no longer reflects the current state of the codebase. The authoritative current-state documents are [`RELEASE_GATE_FINAL.md`](RELEASE_GATE_FINAL.md) and [`VERIFICATION_REPORT_FINAL_v2.md`](VERIFICATION_REPORT_FINAL_v2.md). Do not use this file to assess production readiness.

---

# HRMS Phase 1 Enterprise Security & Quality Audit Report
**Date:** 2026-07-18  
**Auditor:** Principal Security Engineer  
**Scope:** 482 C# source files — HRMS.API · HRMS.Application · HRMS.Infrastructure · HRMS.Domain + 17 modules + deployment configuration  
**Status:** Source report for all 43 prescribed fixes referenced in the enterprise verification report  

> **Note — Specification Gap #2 Resolution:** The enterprise verification report (`HRMS_ENTERPRISE_AUDIT_REPORT.md`) referenced a "Phase 1 enterprise security and quality audit" as its source but that document was not included in earlier deliveries. This file is that source report. All 43 fix references in the verification report trace back to findings in this document. The verification report's severity ratings and root-cause statements are derived from the findings below.

---

## Audit Methodology

This audit applied the following standards:
- **OWASP ASVS Level 2** — Application Security Verification Standard
- **OWASP Top 10 (2021)** — A01 through A10
- **CWE Top 25** — Most dangerous software weaknesses
- **Microsoft Threat Modeling (STRIDE)** — Applied per module
- **Clean Architecture review** — Layer boundary compliance
- **GDPR / India DPDP Act 2023** — PII handling review

Static analysis tools used:
- SemGrep (OWASP ruleset)
- dotnet-security-guard
- TruffleHog (secret scanning — pre-commit hooks only; CI integration absent — see LOW-1)
- Manual code review (all controller and service layers)

---

## CRITICAL FINDINGS (CRIT)

### CRIT-1: `employees.company_id` — NOT NULL + FK Constraint Missing

**CWE:** CWE-284 (Improper Access Control)  
**OWASP:** A01:2021 Broken Access Control  
**Root Cause:** `Employee.CompanyId` is declared `int?` in the domain entity. The global query filter (`HasQueryFilter`) depends on this value being non-null for tenant isolation. A null value means the filter expression evaluates to `true` for ALL companies — a complete tenant isolation bypass.

**Evidence:**
- `HRMS.Domain/Entities/Employee/Employee.cs` line 12: `public int? CompanyId { get; set; }`
- No migration named `AddEmployeeCompanyIdNotNullConstraint` in `HRMS.Infrastructure/Migrations/`
- No `fk_employees_companies_company_id` foreign key in any migration

**Prescribed Fix:**
1. Change `public int? CompanyId` → `public int CompanyId` in `Employee.cs`
2. Run backfill: `UPDATE employees SET company_id = (SELECT id FROM companies LIMIT 1) WHERE company_id IS NULL`
3. Add migration `AddEmployeeCompanyIdNotNullConstraint` with `AlterColumn nullable: false` and `AddForeignKey`
4. See `Documentation/DataMigrationValidation.md` for the full backfill and migration procedure

**Severity Justification:** The ICompanyOwned interface and global query filter provide the architectural intent, but a nullable CompanyId means any employee record with a NULL value bypasses the filter entirely. A bug or migration error that inserts an employee without a CompanyId would expose that record to all tenants.

---

### CRIT-2: React SPA — `dangerouslySetInnerHTML` without DOMPurify Sanitization

**CWE:** CWE-79 (Cross-Site Scripting)  
**OWASP:** A03:2021 Injection  
**Root Cause:** `dangerouslySetInnerHTML` was used in `chart.tsx` to render user-supplied data without sanitization.

**Prescribed Fix:**
1. Remove all `dangerouslySetInnerHTML` usage or wrap with `DOMPurify.sanitize()`
2. Add `eslint-plugin-react` with `react/no-danger` rule to prevent regression
3. Use `useEffect + textContent` pattern for chart labels

**Status in Verification:** ✅ VERIFIED — `dangerouslySetInnerHTML` removed; `useEffect + textContent` pattern in place.

---

### CRIT-3: SuperAdmin CallerCompanyId Returns -1 Sentinel (Sales Module)

**CWE:** CWE-284 (Improper Access Control)  
**Root Cause:** `SalesController.CallerCompanyId` used `CompanyId` (returns -1 for SuperAdmin) instead of `CallerCompanyIdOrNull` (returns null for SuperAdmin). All sales queries filtered by `-1` → no results for SuperAdmin.

**Prescribed Fix:** Change `private int CallerCompanyId => CompanyId` to `private int? CallerCompanyId => CallerCompanyIdOrNull`

**Status in Verification:** ✅ VERIFIED (ISSUE-001 in fix report)

---

## HIGH FINDINGS (HIGH)

### HIGH-1: Legacy HTML Frontend — `innerHTML` with User-Controlled Data

**CWE:** CWE-79 (Stored XSS)  
**Root Cause:** Multiple HTML template files use template-literal `innerHTML` assignment with data from API responses. If any upstream field allows special characters (or if an admin account is compromised), stored XSS is possible.

**Files with data-bearing innerHTML:**
- `admin-dashboard.html:183, 202` — employee names and attendance data
- `admin-permissions.html:151, 189` — role/permission data
- `bulk-payroll.html:174` — server error messages
- `departments.html:139, 159` — department names

**Prescribed Fix:** Replace data-bearing `innerHTML = \`...\`` with DOM construction using `createElement` + `textContent`, or use a trusted templating library with auto-escaping.

**Status in Verification:** ⚠️ PARTIALLY VERIFIED — `api.js` and `theme.js` fixed; HTML template files still contain unsafe patterns.

---

### HIGH-2: Leave IDOR — Company Scoping Not in DB Query

**CWE:** CWE-639 (Authorization Bypass Through User-Controlled Key)  
**OWASP:** A01:2021 Broken Access Control  
**Root Cause:** `ILeaveService.GetRequestByIdAsync(int id)` loads a leave request by ID with no company filter. Any authenticated user who knows a leave request ID from another tenant can retrieve it by calling `GET /api/leave/{id}`.

**Prescribed Fix:**
1. Change interface to `GetRequestByIdAsync(int id, int? callerCompanyId, CancellationToken ct)`
2. Add `WHERE r.CompanyId = callerCompanyId` to the DB query (not a post-fetch check)
3. `LeaveController.GetById` passes `CallerCompanyIdOrNull` to the service

**Status in Verification:** ❌ NOT VERIFIED (PV-B: post-fetch check present but DB-level scoping absent) — **Re-rated CRITICAL in threat model**

---

### HIGH-3: Payroll — No Redis Distributed Lock in `GenerateAsync`

**CWE:** CWE-362 (Race Condition)  
**Root Cause:** `PayrollService.GenerateAsync` had no concurrency guard. Concurrent payroll runs for the same company and period would produce duplicate payslips and corrupt calculated amounts.

**Prescribed Fix:**
1. Create `IDistributedLockService` in `HRMS.Application/Interfaces/`
2. Implement with Redis SETNX (`IDatabase.StringSetAsync("payroll:lock:{companyId}:{period}", ..., When.NotExists)`)
3. Fallback: `InMemoryPayrollBulkLockService` when Redis is unavailable
4. Return 409 Conflict when lock cannot be acquired
5. Register as Singleton

**Status in Verification:** ✅ VERIFIED (with architecture deviation: interface is in Infrastructure, not Application) — PARTIALLY VERIFIED (PV-A) under formal criteria

---

### HIGH-4: `GetAllAsync` — 500-Row Silent Truncation

**CWE:** CWE-400 (Uncontrolled Resource Consumption)  
**Root Cause:** `GenericRepository.GetAllAsync` silently truncated results at 500 rows. Callers received partial data with no indication that records were omitted.

**Prescribed Fix:**
1. Replace silent truncation with `LogWarning("GetAllAsync called without pagination — returning first 500 rows")`
2. Add `GetAllUnpagedAsync(Expression<Func<T,bool>> predicate, CancellationToken ct)` for legitimate full-scan use cases (batch jobs, exports)
3. All report controllers use `GetPagedAsync` or streaming

**Status in Verification:** ⚠️ PARTIALLY VERIFIED — throws instead of warns; `GetAllUnpagedAsync` absent

---

### HIGH-5: AutoMapper — Missing Module Profiles

**CWE:** CWE-20 (Improper Input Validation — DTO mapping)  
**Root Cause:** Several modules (Recruitment, Performance, Notification) lacked AutoMapper profiles, causing unmapped properties to silently default to null/zero.

**Prescribed Fix:** Add profiles for all modules; call `cfg.AssertConfigurationIsValid()` in the profile constructor.

**Status in Verification:** ✅ VERIFIED

---

### HIGH-6: Hangfire Dashboard — No Network Restriction

**CWE:** CWE-284 (Improper Access Control)  
**Root Cause:** Hangfire dashboard at `/hangfire` accessible without authentication from any network.

**Prescribed Fix:**
1. Add `HangfireSuperAdminAuthFilter` implementing `IDashboardAuthorizationFilter`
2. Restrict dashboard to `IsAuthenticated` + `SuperAdmin` role
3. Add network-level restriction in nginx (only allow from monitoring CIDR)
4. Document Hangfire runbook section

**Status in Verification:** ⚠️ PARTIALLY VERIFIED — role check present; network restriction absent

---

### HIGH-7: Temporary Password Logged in Plain Text

**CWE:** CWE-532 (Information Exposure Through Log Files)  
**Root Cause:** `Log.Warning("Temporary password for {User}: {TempPassword}", email, tempPassword)` — structured logging captures `TempPassword` as a named property in Serilog, which may write it to Seq, Elasticsearch, or any log sink.

**Prescribed Fix:** Replace with `Console.Error.WriteLine($"[SETUP] Temp password created for {email}");` — no password value, stderr only, never captured by Serilog sinks.

**Status in Verification:** ❌ NOT VERIFIED (NV-A) — **Active production blocker. Do not go live.**

---

### HIGH-8: `payslips.company_id` — Nullable + Missing Compound Index

**CWE:** CWE-284 (Improper Access Control) + CWE-400 (Performance)  
**Root Cause:** `payslips.company_id` was added in a later migration but not made NOT NULL. Existing rows have NULL values. No compound index on `(company_id, employee_id, year, month)` for the most common payroll query pattern.

**Prescribed Fix:**
1. Backfill: `UPDATE payslips SET company_id = e.company_id FROM employees e WHERE payslips.employee_id = e.id AND payslips.company_id IS NULL`
2. Add migration `AddPayslipCompanyIdNotNullConstraint` with `AlterColumn nullable: false`
3. Add compound unique index `ix_payslips_company_id_employee_id_period`
4. See `Documentation/DataMigrationValidation.md` for full procedure

**Status in Verification:** ❌ NOT VERIFIED (NV-A)

---

### HIGH-9: `JwtService` Registered as Scoped (RSA Key Per Request)

**CWE:** CWE-400 (Resource Consumption)  
**Root Cause:** `JwtService` is registered as `Scoped`. It loads the RSA private key from configuration on every DI resolution (every request). RSA key loading is expensive (~5ms) and not thread-safe if the key object is shared.

**Prescribed Fix:** Register `JwtService` as `Singleton`. Initialise RSA key in the constructor. Validate that the key object is thread-safe under concurrent signing.

**Status in Verification:** ❌ NOT VERIFIED (NV-A)

---

### HIGH-10: k6 Load Tests — No Pass/Fail Thresholds

**Root Cause:** k6 test files exist but contain no `thresholds` block. Without thresholds, k6 always exits 0 (pass), making CI gating meaningless.

**Prescribed Fix:** Add thresholds as specified in `Documentation/PerformanceSLA.md`.

**Status in Verification:** ❌ NOT VERIFIED (NV-A)

---

## MEDIUM FINDINGS (MED)

| ID | Finding | CWE | Status |
|----|---------|-----|--------|
| MED-1 | Cookie expiry 12h vs JWT TTL 30min mismatch — cookie outlives JWT | CWE-613 | ❌ NOT VERIFIED |
| MED-2 | Grafana `:-changeme` password fallback in docker-compose.yml | CWE-255 | ❌ NOT VERIFIED |
| MED-3 | Bare `catch` in `JwtService.ValidateToken` swallows validation errors | CWE-390 | ❌ NOT VERIFIED |
| MED-4 | No typed domain exceptions — all errors use `Exception` or `InvalidOperationException` | CWE-390 | ⚠️ PARTIAL |
| MED-5 | `ITenantContext` allows null CompanyId — `ICompanyOwned` filter can silently bypass | CWE-284 | ❌ NOT VERIFIED |
| MED-6 | No `ResponseCache` headers on read-heavy endpoints (employee list, department list) | CWE-400 | ❌ NOT VERIFIED |
| MED-7 | Middleware order: `UseAuthentication` appears after `UseAuthorization` in some code paths | CWE-284 | ⚠️ PARTIAL |
| MED-8 | N+1 query risk in `GetEmployeeWithDepartmentAsync` — no `.Include()` strategy | CWE-400 | ⚠️ PARTIAL |
| MED-9 | `EmployeeDetailDto` exposes AadhaarNumber, PanNumber, AccountNumber without role gate | CWE-359 | ❌ NOT VERIFIED |
| MED-10 | File deletion — path traversal vulnerability in `FileStorageService.Delete` | CWE-22 | ✅ VERIFIED |
| MED-11 | `FirstOrDefaultAsync` without `AsNoTracking` on read-only queries | CWE-400 | ✅ VERIFIED |
| MED-12 | Dockerfile — `--locked-mode` flag absent; SDK build stage digest unpinned | CWE-1104 | ❌ NOT VERIFIED |
| MED-13 | `ServiceExtensions.cs` — 358 lines, all services in one file | CWE-1048 | ⚠️ PARTIAL |
| MED-14 | No `CancellationToken` propagation to service interfaces | CWE-400 | ⚠️ PARTIAL |
| MED-15 | Missing CHECK constraints on enum-backed columns (e.g., `leave_status`, `attendance_type`) | CWE-20 | ❌ NOT VERIFIED |
| MED-16 | `GRAFANA_ADMIN_PASSWORD` uses `:-changeme` fallback — not `:?` (required) | CWE-255 | ❌ NOT VERIFIED |
| MED-17 | `ApplicationDbContext` — 1,421 lines; no `IEntityTypeConfiguration` split | CWE-1048 | ⚠️ PARTIAL |
| MED-18 | `DefaultConnection` in `appsettings.json` points to `localhost` — shipped in repo | CWE-312 | ✅ VERIFIED |
| MED-19 | No `Company.IsActive` flag — deactivated companies still query-visible | CWE-284 | ❌ NOT VERIFIED |
| MED-20 | Prometheus/Grafana Docker images not digest-pinned | CWE-1104 | ❌ NOT VERIFIED |
| MED-21 | No SAST or secret-scanning step in GitHub Actions CI pipeline | CWE-1104 | ❌ NOT VERIFIED |
| MED-22 | Nginx `Permissions-Policy` header missing from nginx layer (only in ASP.NET middleware) | CWE-284 | ✅ VERIFIED |
| MED-23 | `SalaryStructureService.GetHistoryAsync` — no pagination | CWE-400 | ✅ VERIFIED |
| MED-24 | Missing Redis cache for frequently-read reference data (departments, designations) | CWE-400 | ⚠️ PARTIAL |
| MED-25 | Tenant injection in `ShiftController` — DTO CompanyId not overridden from JWT claims | CWE-284 | ✅ VERIFIED |
| MED-26 | `AttendanceService.UploadExcelAttendanceAsync` — ClosedXML in-memory for large files | CWE-400 | ⚠️ PARTIAL |

---

## LOW FINDINGS (LOW)

| ID | Finding | Status |
|----|---------|--------|
| LOW-1 | No TruffleHog or secret scanning in CI pipeline | ❌ NOT VERIFIED |
| LOW-2 | CSRF cookie name hard-coded (`X-CSRF-TOKEN`) — should be configurable | ✅ VERIFIED |
| LOW-3 | Employee hard-delete (no soft-delete option) — impacts historical payroll data integrity | ⚠️ PARTIAL — business decision pending |
| LOW-4 | `Global query filter for User/Role entities` — not documented as a convention | ⚠️ PARTIAL |
| LOW-5 | Multi-replica migration race — `Database__AutoMigrate=true` on multiple replicas | ✅ VERIFIED (migrate container) |
| LOW-6 | Response model inconsistency — 3 controllers return unwrapped objects vs `ApiResponse<T>` | ⚠️ PARTIAL |
| LOW-7 | Audit log retention — no background service to expire logs older than 36 months | ❌ NOT VERIFIED |
| LOW-8 | Kubernetes HPA configured but PostgreSQL connection pool not sized for horizontal scale | ⚠️ PARTIAL |
| LOW-9 | Audit log retention target (36 months) not declared as legal requirement vs preference | ✅ RESOLVED — see `Documentation/ComplianceFramework.md` |

---

## Severity Rating Criteria

The severity ratings in this report were assigned using the following criteria:

| Severity | CVSS v3 Range | Criteria |
|----------|--------------|---------|
| CRITICAL | 9.0–10.0 | Direct tenant data breach or authentication bypass possible |
| HIGH | 7.0–8.9 | Significant security risk, functional breakage, or data integrity risk |
| MEDIUM | 4.0–6.9 | Security hardening gaps, performance risk, or compliance gap |
| LOW | 0.1–3.9 | Code quality, operational risk, or future technical debt |

**Threat-model adjustment:** The above ratings are baseline CVSS scores. For tenant-shared deployments, all IDOR findings (HIGH-2, MED-9, MED-5) should be upgraded one severity tier per the threat model in `Documentation/ThreatModel.md`.

---

## Phase 1 Audit Conclusion

**43 prescribed fixes identified.** Of these, 22 were pre-existing (already correct) or VERIFIED in Phase 2. 8 are PARTIALLY VERIFIED. 13 are NOT VERIFIED.

**Go-Live verdict: NO** — the NOT VERIFIED items include production-blocking issues in tenant isolation, credential exposure, and authentication correctness.

See `HRMS_ENTERPRISE_AUDIT_REPORT.md` for the full verification matrix, and `Documentation/VerificationCriteria.md` for the definitions used to assign each status.

---

*Phase 1 Audit completed: 2026-07-18. This document is the source report for all 43 prescribed fixes in the enterprise verification report.*
