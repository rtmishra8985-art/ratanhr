# RatanHR HRMS — Evidence Index

This index separates fresh evidence observed during the 2026-08-02 local review from historical claims reported in the uploaded source documentation and from validation that still requires approved staging, infrastructure, or client participation.

**Evidence handling:** Store supporting files in the approved evidence repository. Redact secrets, tokens, cookies, private keys, email content, and personal data.

| Evidence ID | Requirement | Test or validation performed | Environment | Date/time | Tester | Result | Evidence location | Notes and limitations |
|---|---|---|---|---|---|---|---|---|
| E-SRC-001 | Source package | ZIP integrity test using `unzip -tq` | Local review workspace | 2026-08-02 | Replit review | PASS | Uploaded ZIP; command output retained in review record | Proves archive integrity only; does not prove application runtime. |
| E-SRC-002 | Source package | Inspected archive contents for application projects, tests, CI, staging, deployment, and readiness documentation | Local review workspace | 2026-08-02 | Replit review | PASS | `Staging/FRESH_VALIDATION_2026-08-02.md` | Presence of files is not approval of their claims. |
| E-SRC-003 | Source package identity | SHA-256 recorded for exact uploaded ZIP | Local review workspace | 2026-08-02 | Replit review | RECORDED | `78f54a0fe47559fd47d120f8591380981ab6dada5c65ff1dc4cee9ee1909adc7` | Historical reports contain a different archive identity; use this hash for the uploaded archive. |
| E-SRC-004 | Automated checks | Attempted local `dotnet test` execution | Local review workspace | 2026-08-02 | Replit review | NOT RUN | Review command record | .NET SDK unavailable in this workspace; no test result inferred. |
| E-SRC-005 | Baseline and privacy review | Checked migration reference, staging configuration references, health route references, private-key candidates, and `.env` files in the uploaded archive | Local review workspace | 2026-08-02 | Replit review | PASS — SOURCE CHECK | Review command record | Does not prove live migration history, runtime configuration, connectivity, or monitoring. |
| E-VAL-001 | Automated checks | Backend restore, Release build, and test suite | Disposable .NET SDK 8.0.416 container | 2026-08-02 | Replit validation | PASS | `/tmp/ratanhr-backend-validation.log` | 934 passed, 0 failed, 0 skipped; 0 build warnings/errors. |
| E-VAL-002 | Frontend checks | Bun install, typecheck, unit tests, lint, and production build with `PORT=3001` | Local extracted source | 2026-08-02 | Replit validation | PASS | `/tmp/ratanhr-frontend-*.log` | 76 tests passed; build sourcemap notices were non-fatal. |
| E-VAL-003 | Build/dependency checks | API/frontend Docker builds, Compose interpolation, Bun audit, and NuGet vulnerable-package audit | Disposable local validation | 2026-08-02 | Replit validation | PASS | `/tmp/ratanhr-docker-build.log`; `/tmp/ratanhr-bun-audit.log`; `/tmp/ratanhr-dotnet-audit.log` | No vulnerable packages reported; no production resources used. |
| E-VAL-004 | Runtime health/security | Historical disposable staging stack report | Historical local validation | 2026-08-01 (as documented) | As named in source report | REPORTED | `Staging/STAGING_DATABASE_VALIDATION_REPORT.md` | Historical evidence retained separately from the fresh run. |
| E-VAL-005 | Database migrations | Historical dedicated migration image report | Historical local validation | 2026-08-01 (as documented) | As named in source report | REPORTED, CURRENTNESS RECONCILED BELOW | `Staging/STAGING_DATABASE_VALIDATION_REPORT.md` | Fresh dedicated image retry was blocked by NuGet; fresh schema verification used a transparent disposable application runner. |
| E-REC-001 | Recovery | Disposable MySQL backup/restore, MySQL restart/reconnect, Redis restart/reconnect, schema encoding/timezone, and MailHog reachability | Disposable local recovery network | 2026-08-02 | Replit validation | PASS WITH LIMITATIONS | `Staging/RECOVERY_VALIDATION_2026-08-02.md` | MySQL dump emitted a `PROCESS` privilege/tablespace warning; encryption, retention, RPO/RTO, API/frontend recovery, Hangfire recovery, and rollback remain unproven. |
| E-MON-001 | Monitoring and ownership | Reviewed alert rules and Alertmanager routing; created sanitized ownership matrix | Local source review | 2026-08-02 | Replit review | PENDING | `Staging/MONITORING_OWNERSHIP_MATRIX_2026-08-02.md` | No alert service, destination, named owner, escalation evidence, or controlled alert delivery was available; placeholder/no-op receivers remain. |
| E-UAT-001 | Client UAT | Reconciled planned UAT scenario areas and access requirements | Local review workspace | 2026-08-02 | Replit review | BLOCKED / PENDING | `Staging/CLIENT_UAT_DISPOSITION_2026-08-02.md` | 16 planned areas, 0 executed; no client approval inferred. |
| E-VAL-006 | Cleanup | Removed disposable containers, volumes, network, temporary Compose file, generated keys, and passwords | Local Docker runtime | 2026-08-02 | Replit validation | PASS | `/tmp/ratanhr-final-cleanup.log` | No staging validation resources remain. |
| E-FRESH-001 | Archive integrity and safety | `unzip -tq`, SHA-256, file count, and candidate secret-file scan | Local review workspace | 2026-08-02 | Replit validation | PASS / RECORDED | `Staging/FRESH_VALIDATION_2026-08-02.md` | 1,262 paths; no committed `.env*` or key/certificate candidates. |
| E-FRESH-002 | Staging configuration | Package validation script with generated staging-only values | Disposable local validation | 2026-08-02 | Replit validation | PASS | `Staging/FRESH_VALIDATION_2026-08-02.md` | Compose isolation, auto-migration false, biometric live sync false, and non-memory Hangfire verified. |
| E-FRESH-003 | Database connectivity and schema | MySQL/Redis network clients plus disposable application migration runner | Disposable isolated staging network | 2026-08-02 | Replit validation | PASS | `Staging/FRESH_VALIDATION_2026-08-02.md` | Eight migration rows, protected migration, nullable `company_id`, charset/collation/timezone verified. |
| E-FRESH-004 | API/frontend/security runtime | Four health endpoints, frontend, MailHog, headers, and representative unauthenticated routes | Disposable isolated staging network | 2026-08-02 | Replit validation | PASS WITH DOCKER HEALTHCHECK LIMITATION | `Staging/FRESH_VALIDATION_2026-08-02.md` | In-container health checks were limited by Docker `setns`; independent network probes passed. |
| E-FRESH-005 | Prescribed migration image | Dedicated `dotnet-ef` image build retry | Disposable local validation | 2026-08-02 | Replit validation | BLOCKED — TEMPORARY NUGET NETWORK FAILURE | `Staging/FRESH_VALIDATION_2026-08-02.md` | No source migration was edited and no production resource was accessed. |
| E-REP-001 | Automated checks | Uploaded reports state backend, frontend, dependency, SAST, and build results | Reported controlled validation | 2026-08-01 (as documented) | As named in source reports | REPORTED, NOT INDEPENDENTLY REPRODUCED | `ratanhr-source/Staging/STAGING_SMOKE_TEST_CHECKLIST.md`; `ratanhr-source/FINAL_PRODUCTION_READINESS_REPORT.md` | Reports contain conflicting backend totals (931, 933, 934); rerun exact release candidate. |
| E-REP-002 | Authentication | Negative login and refresh-without-cookie checks reported as HTTP 401 | Reported staging validation | 2026-08-01 (as documented) | As named in source checklist | REPORTED PASS | `ratanhr-source/Staging/STAGING_SMOKE_TEST_CHECKLIST.md` | Does not cover successful approved-role sessions. |
| E-REP-003 | Authenticated workflows | SuperAdmin/Admin/Employee login, session, RBAC, IDOR, tenant/branch, export/download checks listed as blocked | Reported staging validation | 2026-08-01 (as documented) | As named in source checklist | BLOCKED | `ratanhr-source/Staging/STAGING_SMOKE_TEST_CHECKLIST.md` | Approved credentials and fixtures unavailable in supplied evidence. |
| E-REP-004 | Email | SMTP delivery, failure, retry, recovery, duplicate, and attachment checks listed as blocked | Reported staging validation | 2026-08-01 (as documented) | As named in source checklist | BLOCKED | `ratanhr-source/Staging/STAGING_SMOKE_TEST_CHECKLIST.md` | Requires staging SMTP inspection. |
| E-REP-005 | Hangfire/jobs | Redis-backed startup and registered job server described; authenticated job inspection remains blocked | Reported staging validation | 2026-08-01 (as documented) | As named in source checklist | PARTIALLY REPORTED / BLOCKED | `ratanhr-source/Staging/STAGING_SMOKE_TEST_CHECKLIST.md` | Does not establish job success, retry, duplicate prevention, recovery, or scope isolation. |
| E-REP-006 | Backup/restore | Prior restore/drill documents supplied | Reported source documentation | Dates vary by document | As named in source documents | REPORTED, CURRENTNESS UNVERIFIED | `ratanhr-source/Documentation/BackupGuide.md`; `ratanhr-source/Documentation/DRDrillReport.md`; `ratanhr-source/docs/backup-restore.md` | Must be repeated or revalidated for the actual staging/release environment. |
| E-REP-007 | Infrastructure | Deployment, monitoring, TLS, SMTP, backup, and escalation guides supplied | Source documentation | Dates vary by document | As named in source documents | DOCUMENTED / PENDING OWNER EVIDENCE | `ratanhr-source/Documentation/DeploymentGuide.md`; `ratanhr-source/Documentation/MonitoringGuide.md` | Documentation is not proof that client infrastructure is configured. |
| E-REP-008 | Biometric | Source documents state live biometric sync remains disabled and vendor/hardware validation is blocked | Reported source documentation | 2026-08-01 (as documented) | As named in source reports | BLOCKED / DISABLED | `ratanhr-source/Biometric/BIOMETRIC_RELEASE_DECISION.md`; `ratanhr-source/Biometric/BIOMETRIC_VENDOR_VALIDATION.md` | Required only if biometric capability is in release scope; do not enable prematurely. |
| E-EXT-001 | Approved accounts | SuperAdmin, Admin, and Employee owners, approvals, environment, and dates | Staging | TBD | QA / client owner | PENDING EXTERNAL VALIDATION | To be supplied by client/QA | Never use production or guessed credentials. |
| E-EXT-002 | Authenticated workflows | Full runbook sections 3–7 | Staging | TBD | QA/security tester | PENDING EXTERNAL VALIDATION | Evidence repository | Requires approved sessions and two-tenant/branch fixtures. |
| E-EXT-003 | Email/retry | Full runbook section 8 | Staging | TBD | QA/operations | PENDING EXTERNAL VALIDATION | Evidence repository | Record message IDs and job IDs only. |
| E-EXT-004 | Hangfire/jobs | Full runbook section 9 | Staging | TBD | QA/operations | PENDING EXTERNAL VALIDATION | Evidence repository | Requires authenticated inspection. |
| E-EXT-005 | Backup/restore/rollback | Full runbook section 10 | Staging | TBD | Operations/DBA | PENDING EXTERNAL VALIDATION | Evidence repository | Record RPO, RTO, restore time, integrity, and cleanup. |
| E-EXT-006 | Infrastructure | DNS/TLS/SMTP/secrets/monitoring/backup/migration evidence | Client infrastructure | TBD | Infrastructure owner | PENDING EXTERNAL VALIDATION | Evidence repository | No secret values in evidence. |
| E-EXT-007 | Client UAT | Business workflow scenarios, defect retests, and explicit approval | Staging | TBD | Client UAT owner | PENDING EXTERNAL VALIDATION | UAT record/sign-off | Client approval cannot be inferred from engineering results. |
| E-EXT-008 | Required approvals | Approval matrix completed and signed | Governance | TBD | Named owners | PENDING EXTERNAL VALIDATION | Approval references | All required roles must be resolved before go-live. |

## Evidence status summary

- **Observed freshly:** archive integrity, package structure, staging configuration, MySQL/Redis/MailHog connectivity, eight migration rows and protected schema, Docker API build, disposable runtime health/security probes, and cleanup.
- **Reported in supplied documentation:** selected source/build/security/runtime results; these require exact-artifact reconciliation where counts or hashes conflict.
- **Observed freshly:** disposable MySQL/Redis recovery restart paths, fixture backup/restore, schema encoding/timezone, and MailHog reachability; recovery limitations are recorded separately.
- **Still pending or blocked:** prescribed dedicated migration-image rerun, approved accounts, authenticated staging, tenant/branch isolation, RBAC/IDOR, exports/downloads, authenticated SMTP workflows, Hangfire behavior, encrypted backup/retention/RPO/RTO/rollback, monitoring delivery/ownership, infrastructure, UAT, and approvals.
- **Final UAT/approval report:** `FINAL_UAT_GO_LIVE_APPROVAL_REPORT.md`; zero client UAT scenarios were executed in this workspace and final deployment approval is blocked.

## Authoritative latest evidence — 2026-08-02

The following entries supersede older conflicting historical counts for the
uploaded candidate while retaining those older records as historical context.

| Evidence ID | Requirement | Test or validation performed | Environment | Result | Evidence location | Notes and limitations |
|---|---|---|---|---|---|---|
| E-FRESH-007 | Source package | ZIP integrity, SHA-256, path count, excluded-content and complete private-key scan | Local review workspace | PASS / RECORDED | `Staging/STAGING_SMOKE_TEST_CHECKLIST.md` | Uploaded candidate hash `b3c10aedb643bcea50818e2531dabe63944b4fb7fafe5f9220479b4889d8c6ef`; 1,265 paths; no secret-like filenames or complete key blocks. |
| E-FRESH-008 | Backend | Runtime image build and corrected disposable SDK test execution | Disposable .NET SDK 8.0.416 container | PASS | `Staging/STAGING_SMOKE_TEST_CHECKLIST.md` | 934 passed, 0 failed, 0 skipped. |
| E-FRESH-009 | Frontend | Bun install, typecheck, tests, lint, and production build | Local extracted source | PASS | `Staging/STAGING_SMOKE_TEST_CHECKLIST.md` | 76 tests passed; build sourcemap notices were non-fatal. |
| E-FRESH-010 | Database/migrations | Dedicated migration image execution and sanitized schema assertions | Disposable MySQL container/network | PASS | `Staging/STAGING_DATABASE_VALIDATION_REPORT.md` | 8 history rows; protected migration exactly once; nullable `leave_types.company_id`; `utf8mb4/utf8mb4_unicode_ci`. |
| E-FRESH-011 | Staging configuration | Generated-value Compose validation | Disposable local validation | PASS | `Staging/STAGING_SMOKE_TEST_CHECKLIST.md` | Loopback-only bindings and safety settings verified; no source secrets used. |
| E-FRESH-012 | Runtime reachability | Full documented-port staging runtime attempt | Local workspace | BLOCKED — workspace port conflict | `Staging/STAGING_SMOKE_TEST_CHECKLIST.md` | `127.0.0.1:8081` was already occupied; cleanup completed and no runtime pass was inferred. |

### Current evidence summary

Freshly observed for this candidate: archive safety, Compose validation,
backend image build, 934 backend tests, frontend checks, dedicated migration
execution, eight migration rows, protected migration, nullable schema column,
and schema encoding. The required authenticated, client, infrastructure,
monitoring, production recovery, and approval gates remain blocked or pending.

---

## Final authoritative evidence addendum — 2026-08-02

`Staging/FINAL_READINESS_ADDENDUM_2026-08-02.md` is authoritative for the
current uploaded archive. The current archive hash is
`c540b4b3046d1ec2f47c301b9a022732a8d144ac64b6f0d9782a893efa22ee44` with 1,265
paths. Current source/archive checks pass, but this review executed no
authenticated staging checks and no client UAT. Current overall decision:
**NOT READY FOR RELEASE**. Prior evidence with different hashes or totals is
historical and must be reconciled before release use.

## Final-task evidence addendum — 2026-08-02

The latest final-task evidence is consolidated in
`Staging/FINAL_READINESS_ADDENDUM_2026-08-02.md`:

- Disposable staging Compose interpolation and isolation checks: **PASS**.
- Frontend typecheck, 76 tests, lint, and production build: **PASS**.
- Source migration/configuration and excluded-content checks: **PASS**.
- Backend execution: **NOT RUN** because the .NET SDK is unavailable here.
- Authenticated staging, SMTP/email, Hangfire, recovery restart flows, client
  UAT, monitoring ownership, and approvals: **BLOCKED/PENDING**.

The final decision remains **NOT READY FOR RELEASE**.
