# RatanHR HRMS — Final UAT and Go-Live Approval Report

**Assessment date:** 2026-08-02  
**Assessment environment:** Isolated local validation using disposable staging resources; no production access  
**Release decision:** **NOT READY FOR PRODUCTION RELEASE**

## Executive result

The final readiness activities could not be fully completed because approved staging accounts, client participants, and infrastructure-owner evidence were not available. The exact uploaded archive was validated in an isolated disposable stack with generated staging-only values. The API, frontend, MailHog, Redis connectivity, health endpoints, security headers, unauthenticated `401` responses, MySQL settings, and the eight migration rows were exercised. The prescribed dedicated database migration image could not install `dotnet-ef` after a NuGet network failure; a transparent one-shot application migration runner verified the migrations against disposable MySQL without changing the source baseline.

The following safe local checks were completed:

- Uploaded source ZIP integrity: **PASS**
- Exact uploaded ZIP SHA-256 recorded: `78f54a0fe47559fd47d120f8591380981ab6dada5c65ff1dc4cee9ee1909adc7`
- Source package structure inspected: **PASS**
- Required migration reference present in source: **PASS**
- Staging auto-migration disabled by configuration references: **PASS**
- Biometric live synchronization disabled by configuration references: **PASS**
- Health route references present for `/health`, `/healthz`, `/healthz/live`, and `/healthz/ready`: **PASS**
- Private-key files and `.env` files in the uploaded archive: **NONE FOUND**
- Backend restore/build/tests: **PASS** — 934 passed, 0 failed, 0 skipped; 0 build warnings/errors
- Frontend typecheck/tests/lint/build: **PASS** — 76 tests passed; lint passed; production build passed with `PORT=3001`
- API and frontend Docker builds: **PASS**
- Compose interpolation: **PASS**
- Bun dependency audit: **PASS** — no vulnerabilities found
- NuGet vulnerable-package audit: **PASS** — no vulnerable packages reported
- Runtime health/security probes: **PASS WITH ENVIRONMENT LIMITATION** for API health endpoints, frontend HTTP 200, MailHog HTTP 200, Redis AUTH round-trip, security headers, and representative unauthenticated `401` responses; Docker in-container health-check execs were limited by the workspace runtime

These checks do not replace live staging validation.

## Baseline preservation

| Baseline requirement | Result | Limitation |
|---|---|---|
| Migration `20260801000001_AddCompanyIdToLeaveTypes` remains present | **PASS — SOURCE CHECK AND SCHEMA CHECK** | Source reference remains present and the protected migration row was observed in disposable `__EFMigrationsHistory`. |
| All 8 MySQL migrations remain applied | **PASS — FRESH DISPOSABLE SCHEMA CHECK** | Eight rows were queried after the one-shot application migration runner completed. The dedicated migration-image procedure remains blocked by NuGet. |
| `Database__AutoMigrate=false` remains unchanged | **PASS — SOURCE CHECK** | Runtime environment value still requires staging confirmation. |
| `Biometric__EnableLiveSync=false` remains unchanged | **PASS — SOURCE CHECK** | Runtime environment value still requires staging confirmation. |
| MySQL operational | **PASS — RUNTIME CONNECTIVITY** | Disposable MySQL 8.0.46 accepted connections; charset, collation, timezone, selected database, migration history, and protected column were verified. |
| Redis operational | **PASS — RUNTIME CONNECTIVITY** | API health reported Redis healthy and Hangfire announced a Redis-backed server; direct container health commands were affected by a Docker runtime `setns` issue. |
| API operational | **PASS — RUNTIME PROBE** | `/health`, `/healthz`, `/healthz/live`, and `/healthz/ready` returned HTTP 200. |
| Hangfire operational | **PARTIAL** | Redis-backed server announced successfully; authenticated dashboard, job execution, retry, duplicate, recovery, and scope checks remain pending. |
| No database reset/replacement performed | **PASS** | No database was accessed or changed. |
| No applied migration edited | **PASS — REVIEW SCOPE** | No source migration was edited during this review. |

## Staging validation summary

| Gate | Result | Required evidence |
|---|---|---|
| Backend restore/build | **PASS** | Exact candidate restored and built in .NET SDK 8.0.416 container with 0 warnings/errors. |
| Backend unit/integration tests | **PASS** | 934 passed, 0 failed, 0 skipped. |
| Frontend typecheck/tests/lint/build | **PASS** | Typecheck, 76 tests, lint, and production build passed. |
| Dependency audit and SAST | **PARTIAL** | Bun and NuGet audits passed; supported SAST invocation still requires a clean rerun. |
| Privacy/logging scan | **PENDING** | No sensitive values were recorded; fresh supported scanner evidence still required. |
| E2E/Playwright and safe k6 smoke tests | **PENDING** | Run only documented safe tests against isolated staging. |
| Database migrations/history/column/collation | **PASS WITH PROCEDURE BLOCKER** | Fresh disposable schema verification passed through the application migration runner; the prescribed dedicated `dotnet-ef` image still needs a retry after NuGet access is restored. |
| API health/readiness/liveness | **PASS — DISPOSABLE RUNTIME** | All four endpoints returned HTTP 200. |
| Security headers/401/403/version disclosure | **PARTIAL PASS** | Security headers and representative unauthenticated `401` responses were observed; role-specific 403 and full version-disclosure checks remain pending. |
| Authenticated role workflows | **BLOCKED** | Approved SuperAdmin, Admin, and Employee staging access unavailable. |
| HRMS workflows | **BLOCKED** | Requires authenticated staging sessions and seeded fixtures. |
| Tenant/branch/RBAC/IDOR | **BLOCKED** | Requires two sanitized tenants, branch fixtures, and authenticated probes. |
| Export/download isolation | **BLOCKED** | Requires generated staging files and authorized/unauthorized download probes. |

## Authentication and authorization results

The following remain **BLOCKED — approved staging access unavailable**:

- SuperAdmin, Admin, and Employee login
- Forced password change
- Valid, invalid, and expired access-token handling
- Refresh-token rotation
- Logout and session invalidation
- MFA, if enabled
- Login rate limiting
- CSRF retrieval/validation
- Secure cookie flags
- Role restrictions
- Tenant/company/branch isolation
- IDOR protection

No production credentials were used or requested.

## HRMS workflow results

The following remain **BLOCKED — authenticated staging and fixtures unavailable**:

- Employee create/list/get/update/self-view
- Attendance check-in/check-out/history
- Leave apply/approve/reject/balance
- Payroll calculation, LOP, finalization, and locking
- Payslip generation/retrieval/download
- Department, designation, shift, and holiday CRUD
- Recruitment
- Performance cycles, goals, and reviews
- Notifications
- Reports, filters, search, pagination, and exports
- Biometric read-only endpoints
- Verification that biometric live synchronization and data mutation remain disabled

Every workflow must later verify persistence, authorization, forbidden-action non-mutation, and safe cleanup.

## Client UAT

### UAT totals

| UAT group | Total scenarios | PASS | FAIL | BLOCKED | NOT APPLICABLE |
|---|---:|---:|---:|---:|---:|
| SuperAdmin/Admin | 0 executed | 0 | 0 | 0 | 0 |
| Employee | 0 executed | 0 | 0 | 0 | 0 |
| **Total** | **0 executed** | **0** | **0** | **0** | **0** |

UAT is **PENDING**. No client tester, date, completed scenario, defect retest, or explicit client approval was available.

Required SuperAdmin/Admin scenarios include login/password change, employee management, organization setup, attendance, leave, payroll/payslips, recruitment, performance, notifications, reports/exports, role restrictions, and company/branch isolation.

Required Employee scenarios include login/logout, self-view, attendance, history, leave/balance, payslip access/download, notifications, supported dashboards/reports, and blocked admin actions.

Each future UAT row must record scenario, role, expected result, actual result, status, sanitized evidence, client feedback, defect reference, and approval status. Findings must be classified as critical blocker, high, medium, low, change request, or approved exception.

## Final approval checklist

Only the statuses `APPROVED`, `PENDING`, `BLOCKED`, and `NOT APPLICABLE` are valid.

| Approval area | Status | Approver role/date | Evidence or outstanding condition |
|---|---|---|---|
| Business owner | PENDING | TBD | Client business acceptance required. |
| HR process | PENDING | TBD | UAT required. |
| Payroll and payslips | PENDING | TBD | UAT and report review required. |
| Attendance | PENDING | TBD | UAT and staging workflow evidence required. |
| Leave | PENDING | TBD | UAT and approval-flow evidence required. |
| Employee management | PENDING | TBD | UAT required. |
| Recruitment | PENDING | TBD | UAT required if in release scope. |
| Performance | PENDING | TBD | UAT required if in release scope. |
| Notifications and email | BLOCKED | TBD | Staging SMTP and retry evidence unavailable. |
| Reports and exports | BLOCKED | TBD | Authenticated tenant/branch and file-isolation evidence unavailable. |
| SuperAdmin/Admin/Employee access | BLOCKED | TBD | Approved staging accounts unavailable. |
| RBAC | BLOCKED | TBD | Authenticated role probes unavailable. |
| Tenant and branch isolation | BLOCKED | TBD | Two-tenant fixtures and authenticated probes unavailable. |
| Privacy and logging | PENDING | TBD | Fresh supported scan and owner review required. |
| Security scan | PENDING | TBD | Bun/NuGet audits passed; supported SAST/privacy evidence and owner approval remain required. |
| Infrastructure | PENDING | TBD | Current DNS, TLS, routing, secrets ownership, and operations evidence required. |
| Backup and restore | PENDING | TBD | Current backup inventory and isolated restore evidence required. |
| Monitoring and alerting | PENDING | TBD | Controlled alert tests and owner/destination evidence required. |
| Support and incident contacts | PENDING | TBD | Named contacts and escalation path required. |
| Go-live date | PENDING | TBD | Set only after all gates pass. |
| Maintenance window | PENDING | TBD | Set only after release approval. |
| Rollback owner | PENDING | TBD | Named operations owner required. |
| Final production deployment approval | BLOCKED | TBD | Cannot be approved while required validation is blocked or pending. |

## Infrastructure recovery readiness

| Area | Result | Required staging evidence |
|---|---|---|
| Database backup/restore | **PARTIAL — NOT SIGN-OFF** | Disposable fixture backup/restore passed with a documented MySQL `PROCESS` privilege/tablespace warning. Encrypted backup, freshness, retention, RPO/RTO, migration compatibility after restore, rollback, and owner evidence remain pending. |
| Redis restart/reconnect | **PASS FOR DISPOSABLE MARKER** | Authenticated Redis marker survived restart/reconnect; queued-job, retry, duplicate, lost-job, and production recovery behavior remain pending. |
| Hangfire recovery | **PENDING** | Worker restart, failure visibility, retry, recovery, and duplicate prevention. |
| API restart/health | **PARTIAL** | Initial runtime health passed; restart/session/error recovery was not completed. |
| Frontend dependency recovery | **PENDING** | API outage/recovery behavior and no stale/unauthorized data. |
| Container/network recovery | **PARTIAL** | Disposable recovery network and containers were isolated and cleaned up; API/frontend restart, full network assertions, and production ownership evidence remain pending. |

## Monitoring and alerting readiness

**PENDING EXTERNAL VALIDATION.** The following signals require owner, threshold, destination, recovery action, and controlled staging alert evidence:

- API, liveness, readiness, database, and Redis health
- Hangfire/job failure
- Authentication failures and rate limiting
- Error rate and latency
- Disk, memory, and container restarts
- Backup failures and email-delivery failures
- Security events
- Alert ownership, destination, escalation, and on-call/support contact

No production incidents were created.

The sanitized ownership matrix is `Staging/MONITORING_OWNERSHIP_MATRIX_2026-08-02.md`.
The supplied Alertmanager configuration contains placeholder/no-op receivers;
no monitoring owner, destination, escalation, or alert-delivery approval is
inferred.

## Security and privacy result

**NOT READY FOR PRODUCTION RELEASE.** Local source inspection found no private-key or `.env` files in the uploaded archive, but the following require fresh staging/build evidence:

- No secrets, passwords, or tokens in source, build output, or logs
- No unnecessary email, salary, payslip, or personal data in logs
- CSRF and secure cookie behavior
- Server-side RBAC and tenant/branch enforcement
- Export/download authorization
- Dependency audit and SAST critical/high findings
- Privacy findings fixed or formally approved with mitigation

## Remaining access and approvals required

1. Approved staging-only SuperAdmin, Admin, and Employee accounts.
2. Isolated staging MySQL, Redis, API, frontend, and Hangfire services.
3. Two sanitized tenants and branch-scoped test fixtures.
4. Staging SMTP inspection and controlled failure capability.
5. Authenticated Hangfire/job inspection.
6. Current backup access and disposable restore environment.
7. Monitoring/alert destinations and operations owner.
8. Client UAT participants and explicit approval.
9. Named business, HR, payroll, attendance, leave, employee-management, recruitment, performance, security/privacy, infrastructure, support, and final release approvers.

## Final status

**NOT READY FOR PRODUCTION RELEASE**

Production must not be approved while any required validation is blocked, failed, untested, lacks evidence, or lacks required client approval.

---

## Authoritative exact-candidate disposition — 2026-08-02

This section is the final disposition for the uploaded source candidate. It
supersedes conflicting historical check counts and the earlier temporary
NuGet procedure blocker. No production system or credential was used.

| Area | Latest result |
|---|---|
| Source archive integrity | PASS; uploaded candidate SHA-256 `b3c10aedb643bcea50818e2531dabe63944b4fb7fafe5f9220479b4889d8c6ef`; 1,265 paths |
| Source safety scan | PASS; no secret-like filenames, excluded build directories, or complete private-key blocks |
| Backend | PASS; runtime image built and 934 disposable SDK tests passed, 0 failed, 0 skipped |
| Frontend | PASS; typecheck, 76 tests, lint, and production build passed |
| Compose/configuration | PASS; isolated generated-value validation passed and required safety settings remained false |
| Database/migrations | PASS for disposable verification; dedicated migration image executed, 8 rows present, protected migration exactly once, nullable `leave_types.company_id`, `utf8mb4/utf8mb4_unicode_ci` |
| Current full runtime | BLOCKED by workspace port ownership during the documented-port attempt; disposable resources were cleaned up |
| Authenticated staging | BLOCKED — approved staging access unavailable |
| RBAC, IDOR, tenant/branch isolation | BLOCKED — approved accounts and two sanitized scopes unavailable |
| HRMS workflow validation | BLOCKED — authenticated staging fixtures unavailable |
| Email and Hangfire | BLOCKED — authenticated triggers and inspection evidence unavailable |
| Recovery | READY WITH BLOCKERS; disposable database/cache checks passed with limitations, production controls unproven |
| Monitoring/ownership | PENDING; names, destinations, escalation, and alert-delivery evidence unavailable |
| Client UAT | BLOCKED/PENDING; 0 of 16 planned areas executed |
| Approvals | BLOCKED/PENDING; no approval inferred |

### Final release decision

`NOT READY FOR PRODUCTION RELEASE`

The source package is ready for controlled staging validation, not for client
production approval. The remaining access and approval requirements are listed
in `GO_LIVE_READINESS.md`, `EVIDENCE_INDEX.md`, and `APPROVAL_MATRIX.md`.

---

## Final authoritative readiness addendum — 2026-08-02

The authoritative current review is recorded in
`Staging/FINAL_READINESS_ADDENDUM_2026-08-02.md`. Current totals are 0
authenticated staging checks executed and 16 planned client-UAT areas with 0
executed, 0 PASS, 0 FAIL, and 16 BLOCKED/PENDING. Historical automated and
disposable-runtime results remain labeled as historical and are not promoted to
current evidence.

**Final status: NOT READY FOR RELEASE.**

## Final-task execution addendum — 2026-08-02

The latest detailed disposition is in
`Staging/FINAL_READINESS_ADDENDUM_2026-08-02.md`. Current final-task totals are
0 authenticated staging checks executed and 16 client-UAT areas planned with 0
executed and 16 blocked/pending. Email, Hangfire, recovery, client, monitoring,
and approval evidence remains incomplete. Final status: **NOT READY FOR
RELEASE**.
