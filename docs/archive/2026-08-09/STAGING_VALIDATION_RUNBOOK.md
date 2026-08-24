# RatanHR HRMS — Controlled Staging Validation Runbook

**Purpose:** Execute the release gate without accessing production or exposing secrets.  
**Environment:** Isolated staging only.  
**Status:** READY TO RUN; fresh isolated technical validation recorded; not a staging sign-off.  
**Companion documents:** `GO_LIVE_READINESS.md`, `EVIDENCE_INDEX.md`, `APPROVAL_MATRIX.md`

## Fresh validation note — 2026-08-02

The exact uploaded source archive was exercised in disposable local staging
resources. Fresh evidence is recorded in
`Staging/FRESH_VALIDATION_2026-08-02.md` and indexed in `EVIDENCE_INDEX.md`.
The following technical checks passed:

- staging Compose isolation and required baseline settings;
- MySQL connectivity, `utf8mb4` charset, `utf8mb4_unicode_ci` collation,
  `+05:30` timezone, and `hrms_staging` selection;
- Redis authenticated `PING` and `SET`/`GET` round-trip;
- all eight EF migration-history rows, including
  `20260801000001_AddCompanyIdToLeaveTypes`;
- nullable `leave_types.company_id`;
- `/health`, `/healthz`, `/healthz/live`, and `/healthz/ready`;
- frontend and MailHog reachability;
- security headers and representative unauthenticated `401` responses.

The prescribed dedicated migration-image procedure remains blocked by a
temporary NuGet network failure while installing `dotnet-ef`. A one-shot
application migration runner was used transparently against disposable MySQL
only to verify the schema; the source baseline remains
`Database__AutoMigrate=false`. Docker in-container health-check processes were
limited by the workspace runtime's namespace restrictions, so independent
network probes are recorded separately from Compose health status.

Authenticated accounts, tenant/branch fixtures, role and IDOR testing, HRMS
workflows, email retry behavior, Hangfire inspection, backup/restore,
infrastructure evidence, client UAT, and formal approvals remain
`PENDING`/`BLOCKED`. This runbook is not a release approval.

## 1. Rules and prerequisites

### Required staging resources

- A deployed staging release candidate with its immutable build identifier/digest.
- A staging database and cache/job store isolated from production.
- One approved SuperAdmin, one approved Admin, and one approved Employee account.
- Two sanitized tenant/company records, with at least two branches and test employees in each.
- Staging-only SMTP test service with inbox and delivery-failure inspection.
- Authenticated access to the staging Hangfire dashboard or equivalent job inspection.
- A current encrypted staging backup and a documented rollback target.
- Named testers for QA, security/privacy, infrastructure/operations, and client UAT.

### Evidence rules

- Use UTC timestamps in `YYYY-MM-DDTHH:mm:ssZ` format.
- Use evidence IDs such as `E-STG-001`, `E-EMAIL-001`, or `E-BACKUP-001`.
- Store screenshots, sanitized response bodies, logs, export hashes, and job/message IDs in the approved evidence repository.
- Redact passwords, access tokens, refresh tokens, cookies, email bodies, PII, private keys, connection strings, and secret values.
- For each test record: tester, role, timestamp, environment, route/workflow, fixture IDs, expected result, observed result, PASS/FAIL/BLOCKED, and evidence ID.
- A blocked or not-tested row is not a pass.

## 2. Preflight and fixture setup

1. Confirm the release candidate identifier and record it in the evidence index.
2. Confirm the staging hostname and verify that it is not a production hostname.
3. Confirm staging secrets are different from production secrets without recording either value.
4. Confirm HTTPS/TLS, health endpoints, database connectivity, cache connectivity, and migration status.
5. Confirm biometric live sync is disabled unless separately approved.
6. Create or verify sanitized Tenant A and Tenant B.
7. Create Branch A1 and Branch A2 under Tenant A and Branch B1 under Tenant B.
8. Create non-sensitive employee, leave, attendance, payroll, document, and ticket fixtures in the correct scope.
9. Confirm the three approved accounts and record their owner, approval, environment, and date in the evidence index.
10. Confirm the SMTP test inbox, failure-control mechanism, Hangfire inspection, backup reference, and rollback target.

## 3. Approved account verification

For each account:

1. Confirm the account owner and approval record.
2. Confirm the account is staging-only and has no production reuse.
3. Authenticate with the supplied approved credential through the normal login flow.
4. Record only status code, role, session evidence ID, and timestamp; never record the credential or token.
5. Confirm the account lands on the correct role-specific surface.
6. If the account is forced to change its initial password, record completion without recording either password.

Required accounts:

| Role | Minimum scope | Approval evidence |
|---|---|---|
| SuperAdmin | Platform-level staging administration | Account owner, approval, environment, date |
| Admin | Tenant A and permitted branch scope | Account owner, approval, environment, date |
| Employee | Self/employee scope and permitted branch | Account owner, approval, environment, date |

## 4. Authentication and session tests

Run each scenario for the appropriate staging account:

| ID | Scenario | Expected result |
|---|---|---|
| AUTH-01 | Valid SuperAdmin login | Successful authentication; correct role and session established |
| AUTH-02 | Valid Admin login | Successful authentication; tenant/branch scope established |
| AUTH-03 | Valid Employee login | Successful authentication; employee scope established |
| AUTH-04 | Invalid password | `401` or documented equivalent; no session |
| AUTH-05 | Refresh with valid session | New valid session; old/revocation behavior matches design |
| AUTH-06 | Refresh without required cookie/session | Unauthorized; no session |
| AUTH-07 | Expired access session | Unauthorized; no protected data returned |
| AUTH-08 | Logout | Session/cookies revoked or cleared; protected request fails afterward |
| AUTH-09 | Unauthorized route | `401`/`403` as documented; no data leakage |
| AUTH-10 | Employee to Admin route | Forbidden; no admin data or side effect |
| AUTH-11 | Admin to SuperAdmin route | Forbidden; no platform data or side effect |
| AUTH-12 | Login rate limit | Threshold and recovery behavior match policy |
| AUTH-13 | MFA status/flow, if enabled | Correct status and enforcement |
| AUTH-14 | CSRF/cookie attributes, where applicable | Secure, HttpOnly, SameSite, and origin protections match policy |

## 5. Tenant and branch isolation

Use Tenant A and Tenant B data, then use Branch A1/A2 data within Tenant A:

1. Read each tenant's list and detail endpoints using the authorized account.
2. Attempt direct URL access to the other tenant's known fixture identifiers.
3. Attempt identifier substitution in query, route, form, JSON, multipart, and export parameters.
4. Attempt create/update/delete against an unauthorized tenant or branch.
5. Attempt to infer another scope through counts, search, pagination, sort, autocomplete, error messages, reports, analytics, notifications, or timing.
6. Verify unauthorized responses are consistent with the security design and do not disclose object existence where it should be hidden.
7. Confirm permitted Admin actions remain limited to the assigned tenant/branches.
8. Confirm Employee actions cannot modify another employee or restricted branch data.

Required result: no unauthorized view, edit, delete, export, download, or reliable inference.

## 6. RBAC and IDOR matrix

For SuperAdmin, Admin, and Employee, test object-level `view`, `create`, `update`, `delete`, `export`, and `download` actions for:

- Companies/tenants and branches
- Users and employees
- Attendance and leave
- Payroll and reports
- Documents and files
- Notifications and helpdesk records
- Any additional release-scope module

For every action:

1. Perform the authorized action.
2. Repeat with a manipulated identifier from another tenant.
3. Repeat with a branch identifier outside the user's scope.
4. Repeat by changing identifiers in the browser URL or request payload.
5. Verify the expected `2xx`, `401`, `403`, or `404` response.
6. Verify the database and visible UI show no unauthorized side effect.

Record expected behavior, observed behavior, result, and evidence reference.

## 7. Export and download isolation

1. Create or identify an export containing only Tenant A / authorized branch fixtures.
2. Verify the export contains no Tenant B or unauthorized Branch A2 records.
3. Record a cryptographic hash of the sanitized export, not its contents.
4. Attempt to alter file IDs, object paths, query parameters, download URLs, and API identifiers.
5. Attempt to use a file reference belonging to another tenant or branch.
6. Revoke or expire a download link, then retry it.
7. Confirm expired, revoked, and unauthorized links fail safely without content or metadata leakage.
8. Verify download response headers and content type match policy.
9. Repeat for payslips, company documents, reports, and any generated attachment.

## 8. Email delivery and retry

1. Confirm staging SMTP endpoint, sender, TLS mode, and failure-control mechanism without recording credentials.
2. Trigger a known successful message using a staging-only inbox.
3. Record provider message ID, application correlation ID, job ID, timestamp, and delivery status; do not record email content.
4. Make the provider unavailable or use its documented controlled-failure mechanism.
5. Trigger a message and observe failure logging, retry count, backoff, and retry limit.
6. Confirm the failed message does not duplicate beyond the documented policy.
7. Restore provider availability and verify recovery.
8. Verify invalid-recipient behavior and safe logging.
9. Verify generated attachments are authorized, complete, and isolated.
10. Verify queue/job records do not contain secrets or full message content.

## 9. Hangfire and background jobs

1. Confirm the staging job server is connected to the intended staging storage.
2. Confirm recurring jobs are registered and have the expected schedule.
3. Trigger a controlled successful job and record its job ID and final state.
4. Trigger a controlled failure and verify exception handling, retry count, backoff, and terminal state.
5. Verify recovery after the dependency becomes available.
6. Submit the same logical work twice and verify duplicate prevention or documented idempotency.
7. Verify dashboard authorization: Employee denied, Admin limited as designed, SuperAdmin permitted.
8. Verify job arguments, logs, exports, and generated files cannot cross tenant or branch boundaries.
9. Confirm monitoring detects failed jobs and that the alert route is tested.

## 10. Backup, restore, and rollback

1. Inventory the latest staging backups: timestamp, size, encryption status, retention expiry, and storage location reference.
2. Verify the backup is separate from the live staging database and is restorable.
3. Start a controlled restore into a disposable isolated staging database.
4. Record restore start/end timestamps, restoration time, and backup timestamp/recovery point.
5. Run migration history, row-count, referential-integrity, tenant/branch, login, health, and representative workflow checks after restore.
6. Record recovery time objective and recovery point objective achieved.
7. Deploy the prior application release or execute the documented application rollback in staging.
8. Verify health and representative workflows after rollback.
9. Record unresolved risks, failed checks, and cleanup confirmation.

## 11. Infrastructure readiness checks

The infrastructure/operations owner must attach evidence for:

- DNS records and routing
- TLS certificate chain, hostname, expiry, and renewal test
- SMTP provider, sender, SPF, DKIM, and DMARC
- Production secret inventory, ownership, access control, and rotation plan
- Database migration and rollback procedure
- Health checks and dependency readiness
- Monitoring dashboards, alert rules, alert delivery, and log retention
- Error tracking and escalation contacts
- Backup schedule, encryption, retention, restore owner, and last test

Do not paste secret values into the evidence package.

## 12. Client UAT

The client UAT owner runs business scenarios for:

- Employee onboarding and profile updates
- Attendance and corrections
- Leave request, approval, balance, and notifications
- Payroll preparation, locking, calculation, and payslip/report review
- Tenant/branch administration
- Document upload/download
- Helpdesk and notifications
- Any biometric workflow included in the contracted scope

For each scenario record tester, date, result, defects, retest result, approval, and evidence ID. UAT is not approved until the client explicitly signs it.

## 13. Cleanup and closeout

1. Delete temporary users, files, exports, messages, jobs, and fixture data according to the staging retention policy.
2. Revoke temporary links and sessions.
3. Confirm no production data or credential was used.
4. Update `EVIDENCE_INDEX.md` with every result and limitation.
5. Update `APPROVAL_MATRIX.md` only from actual owner responses.
6. Recalculate the go/no-go decision. Any failed or blocked critical row remains a release blocker.
