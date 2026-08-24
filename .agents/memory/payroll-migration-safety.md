---
name: Payroll migration safety
description: Durable rule for schema changes involving payslip uniqueness and existing payroll records.
---

Database migrations that enforce payroll uniqueness must not delete or rewrite
existing payslip rows. They should fail clearly when duplicates exist and leave
reconciliation to an explicit, auditable operational procedure.

**Why:** Payroll rows are production records; automatic duplicate deletion can
destroy legally or financially significant history and violate the no-destructive
production-data rule.

**How to apply:** Rehearse uniqueness migrations against clean and duplicate
fixtures before release, and validate the duplicate-reconciliation procedure
before retrying a failed migration.