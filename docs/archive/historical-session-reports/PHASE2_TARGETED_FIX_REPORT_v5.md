# Phase 2 Targeted Fix Report (Phase 1 recommendation follow-up)

## 1. Tooling status — BLOCKED
| Command | Result |
|---|---|
| `dotnet --version` | `bash: dotnet: command not found` |
| `docker --version` | `bash: docker: command not found` |

This environment is a gVisor-based sandbox (`4.19.0-gvisor`) with no Docker socket and no
package manager path to install a .NET SDK or a container runtime. Nix/Replit config changes
cannot create a Docker daemon here. No build/test/EF commands were executed.

## 2. Migration / schema drift — PASS (static verification only)
Migrations present (linear, 2 nodes):
1. `20260810080843_MySqlBaselineSchema`
2. `20260810101800_AddPayslipsCompanyForeignKey`

- Each has exactly one `[Migration(...)]` Designer file; no branch, no duplicate timestamps.
- `ApplicationDbContextModelSnapshot.cs` was diffed line-by-line against the latest
  migration's `BuildTargetModel`: the only differences are the class header/attribute lines
  (`ModelSnapshot` vs `[Migration]` partial class, `BuildModel` vs `BuildTargetModel`).
  The model bodies are byte-identical → snapshot is in sync, no pending model changes.
- EF ProductVersion consistent at 8.0.8 across snapshot and both migrations.
- No live/staging MySQL is reachable from here, so runtime drift vs a deployed DB is
  **UNVERIFIED**. No new migration generated (correct per instructions).

## 3. TODO resolution — Option (b), deferred with tracking note
File: `HRMS.Infrastructure/Data/ReadReplicaDbContext.cs`

Findings: read-replica routing is **not** a no-op stub in application code. `ServiceExtensions.cs:53-78`
already reads `Database:ReplicaConnection` / `Database:EnableReadReplica` and registers
`ReadReplicaDbContext`; when disabled or unset it falls back to the primary connection.
The only missing piece is physical MySQL replication at the infrastructure layer, which is a
deployment concern, not code. Implementing replica infrastructure was explicitly out of scope.

Action: replaced the `TODO` with an explicit DEFERRED note stating why it is deferred, what is
already implemented, exactly which two settings enable it, and a tracking id
`HRMS-INFRA-READ-REPLICA`. No behavioural code change.

## 4. Legacy-UI vs SPA coverage (72 legacy pages; nothing deleted or modified)

| Legacy page | SPA equivalent |
|---|---|
| login.html | /login |
| admin-dashboard.html, emp-dashboard.html | /dashboard |
| view-employees.html, add-employee.html, edit-employee.html, view-emp-details.html, add-emp-details.html | /employees, /employees/:id |
| departments.html | /departments |
| holidays.html | /holidays |
| leave.html, leave-admin.html | /leave |
| leave-adjustments.html | no SPA equivalent found |
| payroll.html, add-payroll.html | /payroll |
| bulk-payroll.html | no SPA equivalent found |
| emp-payslip.html | no dedicated route (payslip handled inside /payroll) |
| view-attendance.html, emp-web-attendance.html, web-attendance-admin.html | /attendance |
| upload-attendance.html | no SPA equivalent found |
| biometric-dashboard.html, biometric-logs.html, biometric-realtime.html, biometric-settings.html, biometric-sync-history.html | /biometric |
| biometric-devices.html | /biometric/devices |
| performance-dashboard.html, performance-cycles.html, performance-goals.html, performance-reviews.html, performance-feedback.html | /performance |
| recruitment-dashboard.html, recruitment-candidates.html, recruitment-interviews.html, recruitment-offers.html, recruitment-requisitions.html | /recruitment |
| reports-attendance.html, reports-employee.html, reports-leave.html, reports-payroll.html, reports-salary-register.html | /reports |
| sales-dashboard.html, sales-customers.html, sales-leads.html, sales-followups.html, sales-meetings.html, sales-quotations.html, sales-tasks.html, sales-visits.html, sales-reports.html | /sales |
| appreciation.html, emp-appreciation.html | no SPA equivalent found |
| notifications.html | no dedicated route (in-app notification UI in Layout) |
| change-password.html, forgot-password.html, reset-password.html | no SPA equivalent found (password flows) |
| access-denied.html | no dedicated route (guard renders inline) |
| admin-users.html, admin-permissions.html | no SPA equivalent found (partially /settings) |
| add-company.html, edit-company.html, view-company.html, company-docs.html, upload-logo.html | no SPA equivalent found (partially /settings) |
| superadmin-login.html, superadmin-dashboard.html, superadmin-companies.html, superadmin-manage-admins.html, superadmin-superadmins.html, superadmin-permissions.html | no SPA equivalent found (entire super-admin console) |
| webhooks.html | no SPA equivalent found |
| SPA-only (no legacy page): /timesheet, /assets, /helpdesk, /org-chart, /training, /expenses, /travel, /onboarding, /shifts, /designations, /analytics, /audit-log, /settings, employee transfers/promotions/exit | — |

Largest gaps: super-admin console (6 pages), company management (5), auth/password flows (3),
admin users & permissions (2), bulk/upload utilities (2), appreciation (2), webhooks (1).
Reported only — retain/deprecate decision left to you.

## 5. New blockers
- No .NET SDK and no Docker runtime in this sandbox (gVisor, no daemon socket). Items requiring
  `dotnet ef migrations list|script` and any build/test remain unexecuted.
- No reachable staging MySQL, so live schema drift cannot be confirmed.

## 6. Files changed
- `HRMS.Infrastructure/Data/ReadReplicaDbContext.cs` (comment only)
- `PHASE2_TARGETED_FIX_REPORT_v5.md` (new, this report)

PHASE 2 TARGETED FIX STATUS: BLOCKED (item 1 and the runtime half of item 2); items 3 and 4 complete.
