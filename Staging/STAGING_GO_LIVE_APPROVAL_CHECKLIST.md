# RatanHR HRMS — Sanitized Go-Live Approval Checklist

**Validation date:** 2026-08-01  
**Environment:** Isolated staging validation only  
**Overall release decision:** `NOT READY`

## Status rules

Only these statuses are used:

- `APPROVED` — approval or evidence was explicitly provided and verified.
- `PENDING` — required approval or evidence has not been supplied.
- `BLOCKED` — the check cannot be executed until a required staging dependency or approved access is supplied.
- `NOT APPLICABLE` — the item does not apply to this release.

No approver names, dates, credentials, cookies, tokens, private keys, connection strings, or personal data are recorded in this checklist.

## Client and release approvals

| Approval area | Status | Approver role | Date | Sanitized evidence / remaining requirement |
|---|---|---|---|---|
| Business owner approval | PENDING | Not supplied | Not supplied | Written business-owner approval is required. |
| SuperAdmin access approval | BLOCKED | Not supplied | Not supplied | Approved staging-only SuperAdmin access and completed forced password change are required. |
| Admin access approval | BLOCKED | Not supplied | Not supplied | Approved staging-only Admin account is required. |
| Employee access approval | BLOCKED | Not supplied | Not supplied | Approved staging-only Employee account is required. |
| Employee management approval | BLOCKED | Not supplied | Not supplied | Authenticated create, list, detail, update, self-view, upload, transfer, exit, validation, and authorization evidence is required. |
| Attendance approval | BLOCKED | Not supplied | Not supplied | Authenticated check-in/out, duplicate handling, history, edits, reason, date-window, and scope evidence is required. |
| Leave approval | BLOCKED | Not supplied | Not supplied | Authenticated apply, history, balance, approve, reject, authorization, and scope evidence is required. |
| Payroll and payslip approval | BLOCKED | Not supplied | Not supplied | Authenticated calculation, LOP, generation, retrieval, download, finalization, locking, and authorization evidence is required. |
| Recruitment approval | BLOCKED | Not supplied | Not supplied | Authenticated supported recruitment workflow and tenant-scope evidence is required. |
| Performance approval | BLOCKED | Not supplied | Not supplied | Authenticated cycle, goals, reviews, role, and tenant-scope evidence is required. |
| Notifications and email approval | BLOCKED | Not supplied | Not supplied | Staging SMTP sink/inbox access and authenticated trigger, delivery, retry, duplicate, and recipient evidence are required. |
| Reports and exports approval | BLOCKED | Not supplied | Not supplied | Authenticated report filters, pagination/search, export, download, role, and tenant authorization evidence is required. |
| RBAC and security approval | BLOCKED | Not supplied | Not supplied | Approved role sessions are required for allow/deny, IDOR, CSRF, cookie, refresh, logout, expiry, MFA, and rate-limit checks. |
| Tenant and branch-isolation approval | BLOCKED | Not supplied | Not supplied | Two approved company/tenant scopes with sanitized fixtures are required. |
| Privacy and logging approval | PENDING | Not supplied | Not supplied | Fresh dependency, SAST, and privacy scans are clean; privacy-owner approval was not supplied. |
| Monitoring and alerting approval | PENDING | Not supplied | Not supplied | Monitoring, alert routing, and operational ownership evidence is required. |
| Backup and restore approval | PENDING | Not supplied | Not supplied | Backup/restore evidence or an explicit approved exception is required. |
| Support and incident-contact approval | PENDING | Not supplied | Not supplied | Completed support and escalation contacts are required. |
| Go-live date and maintenance window | PENDING | Not supplied | Not supplied | Client-approved date and maintenance window are required. |
| Rollback owner | PENDING | Not supplied | Not supplied | Named rollback owner and validated rollback procedure are required. |
| Final production deployment approval | PENDING | Not supplied | Not supplied | Cannot be granted while required staging evidence or approvals remain outstanding. |

## Infrastructure evidence disposition

| Evidence area | Status | Current sanitized result |
|---|---|---|
| Backend restore/build | APPROVED | Locked restore and runtime image build passed in isolated containers. |
| Backend automated tests | APPROVED | 934 passed, 0 failed, 0 skipped. |
| Frontend typecheck | APPROVED | TypeScript check passed. |
| Frontend tests | APPROVED | 76 tests passed across 4 test files. |
| Frontend lint | APPROVED | Lint passed with zero warnings/errors. |
| Frontend production build | APPROVED | Production bundle built; only non-fatal existing sourcemap notices were emitted. |
| Dependency audit | APPROVED | 0 critical, high, moderate, or low findings. |
| SAST scan | APPROVED | 0 findings. |
| Privacy/logging scan | APPROVED | 0 findings from the fresh privacy/security scan. |
| Staging compose validation | APPROVED | Compose interpolation passed with temporary validation placeholders. |
| Migration baseline | APPROVED | Protected `20260801000001_AddCompanyIdToLeaveTypes` file remains present; no applied migration was edited. |
| Automatic migration setting | APPROVED | `Database__AutoMigrate=false` remains configured. |
| Biometric live-sync setting | APPROVED | `Biometric__EnableLiveSync=false` remains configured. |
| Database connectivity and migration history | BLOCKED | Requires a running isolated staging database; no staging environment was available for this validation run. |
| MySQL charset/collation | BLOCKED | Requires a running isolated staging database and sanitized query evidence. |
| Redis connectivity | BLOCKED | Requires a running isolated staging Redis service. |
| Hangfire initialization and job execution | BLOCKED | Requires running staging Redis/API, controlled jobs, and authenticated inspection. |
| API health endpoints | BLOCKED | Requires a running staging API on the documented isolated port. |
| Security headers in running staging | BLOCKED | Requires a running staging API request. |
| Staging port isolation | APPROVED | Compose declares loopback-only ports distinct from production; runtime confirmation remains unavailable. |
| Production resources untouched | APPROVED | No production endpoint, database, volume, credential, or compose file was accessed. |
| Cleanup | APPROVED | No staging users, records, jobs, emails, containers, volumes, or temporary staging credentials were created. Local validation build outputs were removed after checks. |

## Required before release sign-off

1. Approved staging-only SuperAdmin, Admin, and Employee access.
2. Completed forced password change for the SuperAdmin account.
3. Two company/tenant scopes with sanitized records for isolation and IDOR checks.
4. Running isolated staging services on the documented ports.
5. MailHog, Mailtrap, or equivalent staging-only SMTP inbox access.
6. Authenticated Hangfire dashboard access or equivalent sanitized job-result evidence.
7. Authenticated API and frontend evidence for every blocked checklist row.
8. Client approvals, privacy-owner review, monitoring/alerting ownership, backup/restore disposition, support contacts, maintenance window, rollback owner, and final production approval.

## Final decision

`NOT READY`

The reproducible source, build, test, configuration, and security checks passed. Final release sign-off is not claimed because authenticated staging evidence, email/background-job evidence, tenant-isolation fixtures, and required client/infrastructure approvals remain unavailable.

## Current Validation Addendum — 2026-08-02

The following statuses supersede earlier infrastructure-runtime statuses in
this checklist. They reflect a disposable local stack only and do not
constitute client staging approval:

| Evidence area | Current status | Current sanitized result |
|---|---|---|
| Backend restore/build/tests | APPROVED | 934 passed, 0 failed, 0 skipped; 0 build warnings/errors. |
| Frontend checks | APPROVED | Typecheck, 76 tests, lint, and production build passed. |
| API/frontend Docker builds | APPROVED | Both images built successfully. |
| Dependency audits | APPROVED | Bun and NuGet audits reported no vulnerabilities/findings. |
| Compose validation | APPROVED | Temporary staging-only interpolation passed. |
| API health/readiness/liveness | APPROVED | All four API health endpoints returned HTTP 200 in the disposable stack. |
| Frontend and MailHog reachability | APPROVED | Both returned HTTP 200. |
| Security headers | APPROVED | HSTS, CSP, X-Content-Type-Options, and X-Frame-Options observed. |
| Redis/Hangfire startup | PENDING | Redis-backed Hangfire announced successfully; authenticated dashboard, job execution, retry, duplicate, recovery, and scope evidence remain outstanding. |
| Database migration history/schema | BLOCKED | Migration image could not install `dotnet-ef` after three NuGet network retries; verify all eight migrations and `leave_types.company_id` after retry. |
| Authenticated staging/UAT/infrastructure approvals | BLOCKED/PENDING | Approved role accounts, client UAT, current recovery/monitoring evidence, and named approvals remain unavailable. |
| Disposable cleanup | APPROVED | All temporary containers, volumes, network, generated values, and temporary files were removed. |

## Updated final decision

`NOT READY`

The source/build/test and limited disposable-runtime checks passed where
executed. This does not close the release gate. Production remains blocked
until the migration verification, authenticated staging workflows, tenant and
branch isolation, SMTP and Hangfire behavior, recovery/monitoring evidence,
client UAT, and required approvals are complete.

---

## Final Readiness Execution Disposition — 2026-08-02

This is the current approval disposition for the uploaded source package. Only
the statuses `APPROVED`, `PENDING`, `BLOCKED`, and `NOT APPLICABLE` are used.
No approval is fabricated.

| Approval area | Status | Current disposition |
|---|---|---|
| Source/build/automated checks | PENDING | Exact release-candidate rerun is required; the local workspace lacked the .NET SDK and extracted frontend dependencies. |
| Database/migration/connectivity | BLOCKED | Isolated staging database and sanitized query evidence unavailable. |
| Redis/Hangfire recovery and jobs | BLOCKED | Isolated staging Redis/API and authenticated inspection unavailable. |
| API health/security headers | BLOCKED | Running isolated staging API unavailable. |
| SuperAdmin/Admin/Employee access | BLOCKED | Approved staging accounts unavailable. |
| RBAC/IDOR/tenant/branch isolation | BLOCKED | Approved sessions and two sanitized tenant scopes unavailable. |
| HRMS workflow acceptance | BLOCKED | Authenticated staging sessions and fixtures unavailable. |
| SMTP/email approval | BLOCKED | Staging SMTP sink/inbox and controlled failure access unavailable. |
| Reports/exports/downloads | BLOCKED | Authenticated file and scope-isolation evidence unavailable. |
| Backup/restore/rollback | PENDING | Current backup and disposable restore evidence not supplied. |
| Monitoring/alerting | PENDING | Controlled alert evidence, owners, destinations, and escalation not supplied. |
| Client UAT | PENDING | No client tester, scenario result, defect retest, or explicit approval supplied. |
| Final production deployment approval | BLOCKED | Required validation and approvals remain outstanding. |

### Final decision

`NOT READY FOR PRODUCTION RELEASE`

See the package-root `FINAL_UAT_GO_LIVE_APPROVAL_REPORT.md` for UAT totals,
infrastructure recovery disposition, monitoring requirements, security/privacy
status, blockers, cleanup status, and remaining approvals.