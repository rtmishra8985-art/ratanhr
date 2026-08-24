# RatanHR HRMS — Approval Matrix

**Purpose:** Record explicit approval for the release gate.  
**Current state:** No approval is inferred or fabricated. This checklist uses only the permitted statuses: `APPROVED`, `PENDING`, `BLOCKED`, and `NOT APPLICABLE`.

## Required approvals

| Approval role | Name | Scope of approval | Status | Date | Evidence / approval reference | Outstanding conditions |
|---|---|---|---|---|---|---|
| Business owner | TBD | Business readiness, contracted workflows, operational acceptance, and business risk acceptance | PENDING | TBD | TBD | Complete client UAT and resolve business defects. |
| HR process owner | TBD | HR workflow acceptance | PENDING | TBD | TBD | Complete client UAT. |
| Payroll/payslip owner | TBD | Payroll calculation, locking, finalization, reports, and payslip acceptance | PENDING | TBD | TBD | Complete payroll UAT and report review. |
| Attendance owner | TBD | Attendance visibility, correction, and employee attendance acceptance | PENDING | TBD | TBD | Complete attendance UAT. |
| Leave owner | TBD | Leave application, approval, rejection, balances, and notifications | PENDING | TBD | TBD | Complete leave UAT. |
| Employee-management owner | TBD | Employee creation, management, self-view, and access scope | PENDING | TBD | TBD | Complete employee-management UAT. |
| Recruitment owner | TBD | Recruitment workflow acceptance, if in release scope | PENDING | TBD | TBD | Complete recruitment UAT or mark NOT APPLICABLE with justification. |
| Performance owner | TBD | Performance cycle, goals, and review acceptance, if in release scope | PENDING | TBD | TBD | Complete performance UAT or mark NOT APPLICABLE with justification. |
| Security/privacy owner | TBD | Authentication, authorization, tenant/branch isolation, IDOR, export/download controls, privacy scan, and residual security risk | BLOCKED | TBD | TBD | Authenticated staging evidence and fresh security/privacy scans required. |
| Infrastructure/operations owner | TBD | Staging/production infrastructure, DNS, TLS, SMTP, monitoring, alerting, backups, restore, rollback, secrets, migrations, and escalation | PENDING | TBD | TBD | Provide current infrastructure evidence and complete recovery/monitoring validation. |
| Notifications/email owner | TBD | Email delivery, retry, recovery, and notification acceptance | BLOCKED | TBD | TBD | Staging SMTP inspection unavailable. |
| Reports/exports owner | TBD | Reports, exports, download authorization, and scope isolation | BLOCKED | TBD | TBD | Authenticated staging files and cross-scope probes unavailable. |
| Access/RBAC owner | TBD | SuperAdmin, Admin, Employee access and server-side RBAC | BLOCKED | TBD | TBD | Approved staging accounts unavailable. |
| Tenant/branch isolation owner | TBD | Tenant/company/branch isolation and IDOR acceptance | BLOCKED | TBD | TBD | Two sanitized tenants and authenticated probes unavailable. |
| Backup/restore owner | TBD | Backup freshness, encryption, restore integrity, RPO/RTO, and rollback | PENDING | TBD | TBD | Disposable fixture restore passed with a documented tablespace privilege limitation; current encrypted-backup, retention, RPO/RTO, rollback, and owner evidence remain required. |
| Monitoring/alerting owner | TBD | Health, failure, security, backup, email, and escalation alerts | PENDING | TBD | TBD | Ownership matrix is recorded, but names, destinations, escalation, and controlled alert evidence remain required. |
| Support/incident owner | TBD | Support contacts, escalation path, and incident ownership | PENDING | TBD | TBD | Named contacts and escalation reference required. |
| Client UAT owner | TBD | Client business scenario execution, defect retest, and explicit UAT acceptance | PENDING | TBD | TBD | 16 planned areas were dispositioned with 0 executed; provide a named client participant, staging access, scenario evidence, defect retests, and explicit approval. |
| Final release approver | TBD | Final go/no-go decision after all technical, operational, security, business, and client gates are complete | BLOCKED | TBD | TBD | All blocked/pending gates must close; evidence index must be reconciled. |

## How to complete this matrix

For each row, the owner must provide:

1. Full name and role.
2. Exact scope being approved.
3. One allowed status: `APPROVED`, `PENDING`, `BLOCKED`, or `NOT APPLICABLE`.
4. Date and timezone.
5. A durable evidence or approval reference.
6. Any outstanding conditions, owner, and due date.

An email or verbal statement may be referenced only if it is stored in the approved governance location and can be retrieved by the release team. Do not paste personal data, credentials, tokens, or confidential message content into this file.

## Approval rules

- Any `PENDING` or `BLOCKED` approval blocks production release.
- `NOT APPLICABLE` requires a written scope justification and owner confirmation.
- Client UAT approval must come from the client UAT owner; it cannot be substituted by engineering or QA.
- Security/privacy approval must address cross-tenant, branch, RBAC, IDOR, export, and download isolation evidence.
- Infrastructure approval must cover current production configuration readiness, not only the existence of deployment documentation.
- The final release approver must review `GO_LIVE_READINESS.md` and `EVIDENCE_INDEX.md` after all evidence is attached.

## Current decision

**Final release approval: BLOCKED.**  
**Production go-live: NOT READY.**

Fresh technical evidence is recorded in `Staging/FRESH_VALIDATION_2026-08-02.md`,
with recovery, monitoring, and UAT dispositions in the `Staging/` records dated
2026-08-02. Production go-live remains blocked until authenticated staging
evidence, infrastructure checks, encrypted recovery evidence, client UAT, the
prescribed migration-image procedure, and all required approvals are complete.

---

## Authoritative exact-candidate disposition — 2026-08-02

The dedicated migration-image procedure is now **PASS** for disposable MySQL:
the image built and executed, eight migration-history rows were observed, the
protected migration appeared exactly once, and the nullable
`leave_types.company_id` column plus `utf8mb4/utf8mb4_unicode_ci` encoding were
verified. This closes the temporary NuGet/procedure blocker for disposable
validation only.

The exact candidate also passed the backend runtime-image build, 934 backend
tests, frontend typecheck, 76 frontend tests, lint, production build, source
safety scan, and staging Compose validation. The documented-port runtime
attempt was blocked by workspace ownership of `127.0.0.1:8081` and was cleaned
up; no runtime pass is inferred from it.

No approval row changes are authorized by these technical checks. All rows
remain at their explicit `APPROVED`, `PENDING`, `BLOCKED`, or `NOT APPLICABLE`
values above. In particular, authenticated role evidence, tenant/branch and
RBAC/IDOR evidence, workflow evidence, SMTP/Hangfire evidence, current
recovery controls, monitoring ownership, client UAT, and final release approval
remain unresolved.

**Authoritative final release approval: BLOCKED.**
**Production go-live: NOT READY.**

---

## Final authoritative readiness addendum — 2026-08-02

No approval-row status is changed by the current source/archive review. No
approver names, dates, owners, client approval, infrastructure approval, or
security/privacy approval were supplied. The exact current candidate evidence
and blockers are in `Staging/FINAL_READINESS_ADDENDUM_2026-08-02.md`.

**Final release approval: BLOCKED.**
**Final status: NOT READY FOR RELEASE.**

## Final-task execution addendum — 2026-08-02

The final-task execution did not provide any new approver, owner, client
approval, authenticated staging evidence, email/Hangfire evidence, recovery
approval, or monitoring approval. No approval-row status is changed.
**Final release approval remains BLOCKED** and the release remains **NOT READY
FOR RELEASE**.
