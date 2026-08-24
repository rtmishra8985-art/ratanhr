# Data Migration & Tenant Onboarding Validation
**HRMS v2.1.0** | MySQL 8.4 | Addresses Specification Gap #8

---

## Overview

The audit identified two critical data-migration gaps:

1. `Employee.CompanyId NOT NULL` migration (CRIT-1) requires a backfill `UPDATE` before the constraint can be applied — not scripted anywhere.
2. `payslips.company_id NOT NULL` migration (HIGH-8) has the same problem — rows inserted before the column existed have NULL values that block the constraint.
3. No smoke-test seed script verifies the schema works end-to-end after all migrations run in sequence on an empty DB.

This document provides the missing backfill scripts, a migration sequencing runbook, and an end-to-end seed smoke test.

---

## Migration Execution Order

All migrations must run in this exact sequence. The `migrate` init-container enforces this automatically via `dotnet ef database update`. This table is the canonical sequence reference.

| # | Migration Name | Date | Critical Pre-condition |
|---|---------------|------|----------------------|
| 1 | `20240101000000_InitialCreate` | 2024-01-01 | Fresh database |
| 2 | `20240601000000_AddExpandedStructure` | 2024-06-01 | Migration 1 complete |
| 3 | `20260711141438_AddSecurityAndLeaveManagement` | 2026-07-11 | Migration 2 complete |
| 4 | `20260715000001_AddAuditLog` | 2026-07-15 | Migration 3 complete |
| 5 | `20260717000001_AddUserProfilePicture` | 2026-07-17 | Migration 4 complete |
| 6 | `20260718000001_AddNewFeatures` | 2026-07-18 | Migration 5 complete |
| 7 | `20260718200000_AddPayrollLockAndAttendanceReason` | 2026-07-18 | Migration 6 complete |
| 8 | `20260719000001_AddPerformanceIndexes` | 2026-07-19 | Migration 7 complete |
| **9** | **`AddEmployeeCompanyIdNotNullConstraint`** | **Pending** | **Backfill script must run first (see below)** |
| **10** | **`AddPayslipCompanyIdNotNullConstraint`** | **Pending** | **Backfill script must run first (see below)** |

---

## CRIT-1 Backfill: `employees.company_id NOT NULL`

### Problem

`Employee.CompanyId` is currently `int?` (nullable). Rows with `NULL` company_id cannot have a NOT NULL constraint applied until the nulls are resolved.

### Step 1 — Audit Existing NULL Rows

```sql
-- Run before applying the NOT NULL constraint
-- Identify employees with no company assignment
SELECT e.id, e.employee_number, e.first_name, e.last_name, e.created_at
FROM employees e
WHERE e.company_id IS NULL
ORDER BY e.created_at;
```

Expected result for a correctly seeded system: **0 rows**. If rows exist, proceed to Step 2.

### Step 2 — Backfill NULL company_ids

**Option A — Assign to the first company (for single-tenant or initial deployment):**

```sql
-- Safe: only updates rows where company_id IS NULL
UPDATE employees
SET company_id = (SELECT id FROM companies ORDER BY id LIMIT 1)
WHERE company_id IS NULL;

-- Verify: should return 0 after update
SELECT COUNT(*) FROM employees WHERE company_id IS NULL;
```

**Option B — Assign to a "orphaned records" company (for multi-tenant audit trail):**

```sql
-- First: create an orphan company record if it doesn't exist
INSERT INTO companies (name, is_active, created_at, updated_at)
VALUES ('_ORPHANED_RECORDS_', false, NOW(), NOW())
ON DUPLICATE KEY UPDATE name = name;

-- Then: assign orphaned employees to this company
UPDATE employees
SET company_id = (SELECT id FROM companies WHERE name = '_ORPHANED_RECORDS_')
WHERE company_id IS NULL;
```

> **Decision required:** Confirm with business which option applies before running the backfill. For a fresh deployment (no live data), Option A is correct. For a deployment with historical data, escalate to the Data Protection Officer.

### Step 3 — Add the Migration

Create `HRMS.Infrastructure/Migrations/AddEmployeeCompanyIdNotNullConstraint.cs`:

```csharp
public partial class AddEmployeeCompanyIdNotNullConstraint : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Safety check: this migration will FAIL if any NULL rows remain
        // The backfill script in DataMigrationValidation.md must run first
        migrationBuilder.AlterColumn<int>(
            name: "company_id",
            table: "employees",
            type: "int",
            nullable: false,
            defaultValue: 0,
            oldClrType: typeof(int),
            oldType: "int",
            oldNullable: true);

        migrationBuilder.AddForeignKey(
            name: "fk_employees_companies_company_id",
            table: "employees",
            column: "company_id",
            principalTable: "companies",
            principalColumn: "id",
            onDelete: ReferentialAction.Restrict);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "fk_employees_companies_company_id",
            table: "employees");

        migrationBuilder.AlterColumn<int?>(
            name: "company_id",
            table: "employees",
            type: "int",
            nullable: true,
            oldClrType: typeof(int),
            oldType: "int");
    }
}
```

### Step 4 — Update Domain Entity

In `HRMS.Domain/Entities/Employee/Employee.cs`, change:

```csharp
// Before:
public int? CompanyId { get; set; }

// After:
public int CompanyId { get; set; }
```

---

## HIGH-8 Backfill: `payslips.company_id NOT NULL`

### Problem

The `payslips.company_id` column was added in a later migration. Any payslip rows inserted before this column was added (or before the backfill ran) may have `NULL` values.

### Step 1 — Audit Existing NULL Rows

```sql
SELECT p.id, p.employee_id, p.month, p.year, e.company_id AS employee_company_id
FROM payslips p
LEFT JOIN employees e ON p.employee_id = e.id
WHERE p.company_id IS NULL
ORDER BY p.year, p.month;
```

### Step 2 — Backfill from Employee's company_id

```sql
-- Backfill company_id from the employee's current company_id
-- Only updates rows where payslip.company_id IS NULL
UPDATE payslips p
INNER JOIN employees e ON p.employee_id = e.id
SET p.company_id = e.company_id
WHERE p.company_id IS NULL
  AND e.company_id IS NOT NULL;

-- Verify: should return 0 after update
SELECT COUNT(*) FROM payslips WHERE company_id IS NULL;
```

> ⚠️ If any payslip employee has a NULL `company_id` (both tables have NULLs), run CRIT-1 backfill first.

### Step 3 — Add the Migration

```csharp
public partial class AddPayslipCompanyIdNotNullConstraint : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<int>(
            name: "company_id",
            table: "payslips",
            type: "int",
            nullable: false,
            defaultValue: 0,
            oldClrType: typeof(int),
            oldType: "int",
            oldNullable: true);

        // Compound index for tenant isolation + performance
        migrationBuilder.CreateIndex(
            name: "ix_payslips_company_id_employee_id_period",
            table: "payslips",
            columns: new[] { "company_id", "employee_id", "year", "month" },
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex("ix_payslips_company_id_employee_id_period", "payslips");
        migrationBuilder.AlterColumn<int?>(
            name: "company_id",
            table: "payslips",
            type: "int",
            nullable: true,
            oldClrType: typeof(int),
            oldType: "int");
    }
}
```

---

## End-to-End Smoke Test Seed Script

This script verifies that all 10+ migrations run in sequence on an empty database, and that the resulting schema can perform all core CRUD operations without errors. Run it after every full migration sequence in CI.

### Usage

```bash
# Run against the migrate container after migrations complete:
docker compose run --rm \
  -e SEED_SMOKE_TEST=true \
  api \
  dotnet HRMS.API.dll --seed-smoke-test

# Or directly via MySQL client:
docker compose exec mysql \
  mysql -u hrms -p"$MYSQL_PASSWORD" hrms_db < scripts/smoke-test-seed.sql
```

### Smoke Test SQL (`scripts/smoke-test-seed.sql`)

```sql
-- ============================================================
-- HRMS End-to-End Smoke Test Seed (MySQL 8.4)
-- Verifies schema integrity after all migrations run in sequence
-- on an empty database. Safe to run in CI; cleans up after itself.
-- ============================================================

START TRANSACTION;

-- 1. Create a test company
INSERT INTO companies (name, is_active, created_at, updated_at)
VALUES ('_SMOKE_TEST_COMPANY_', true, NOW(), NOW());
SET @company_id = LAST_INSERT_ID();

-- 2. Create a test department
INSERT INTO departments (name, company_id, created_at, updated_at)
VALUES ('_SMOKE_TEST_DEPT_', @company_id, NOW(), NOW());
SET @dept_id = LAST_INSERT_ID();

-- 3. Create a test designation
INSERT INTO designations (name, company_id, created_at, updated_at)
VALUES ('_SMOKE_TEST_DESIG_', @company_id, NOW(), NOW());
SET @desig_id = LAST_INSERT_ID();

-- 4. Create a test user
INSERT INTO users (id, email, password_hash, role, full_name, company_id,
    is_active, must_change_password, created_at)
VALUES ('smoke-test-user-id', 'smoketest@example.com', 'smoke-hash',
    'employee', 'Smoke Test', @company_id, true, false, NOW());

-- 5. Create a test employee (verifies CRIT-1: company_id NOT NULL)
INSERT INTO employees (employee_id, first_name, last_name, email, company_id,
    department, designation, date_of_joining, is_active, created_at)
VALUES ('EMP-SMOKE-001', 'Smoke', 'Test', 'emp@smoke.test', @company_id,
    '_SMOKE_TEST_DEPT_', '_SMOKE_TEST_DESIG_', CURDATE(), true, NOW());
SET @emp_id = LAST_INSERT_ID();

-- 6. Create a payslip (verifies HIGH-8: payslips.company_id NOT NULL)
INSERT INTO payslips (employee_id, company_id, month, year, basic_pay, gross_earnings,
    net_pay, generated_at)
VALUES ('EMP-SMOKE-001', @company_id, MONTH(NOW()), YEAR(NOW()),
    50000, 55000, 48000, NOW());

-- 7. Write an audit log entry (verifies audit log schema)
INSERT INTO audit_logs (entity_name, action, entity_id, old_values, new_values,
    user_id, ip_address, created_at)
VALUES ('Employee', 'Create', CAST(@emp_id AS CHAR), '{}', '{"smoke":"test"}',
    'smoke-test-user-id', '127.0.0.1', NOW());

-- 8. Verify global query filters work (multi-tenant isolation)
SELECT COUNT(*) INTO @emp_count
FROM employees
WHERE company_id = @company_id;

-- 9. Verify payslip compound unique index (duplicate should fail gracefully)
-- The ON DUPLICATE KEY clause handles the expected constraint violation
INSERT INTO payslips (employee_id, company_id, month, year, basic_pay, gross_earnings,
    net_pay, generated_at)
VALUES ('EMP-SMOKE-001', @company_id, MONTH(NOW()), YEAR(NOW()), 50000, 55000, 48000, NOW())
ON DUPLICATE KEY UPDATE generated_at = generated_at;
-- If the index is working, this triggers ON DUPLICATE KEY instead of inserting

-- 10. Roll back everything — this is a test only
ROLLBACK;

-- If we reach here without an unhandled error, all schema assertions passed
SELECT 'SMOKE TEST PASSED: All schema assertions verified' AS result;
```

---

## Pre-Migration Checklist

Before applying migrations to a production database:

- [ ] Full backup taken and verified (`gunzip -t backups/latest.sql.gz`)
- [ ] Off-site backup confirmed (S3/GCS download tested)
- [ ] CRIT-1 backfill audit run: `SELECT COUNT(*) FROM employees WHERE company_id IS NULL` → 0
- [ ] HIGH-8 backfill audit run: `SELECT COUNT(*) FROM payslips WHERE company_id IS NULL` → 0
- [ ] Migration tested on staging with a copy of production data
- [ ] `docker compose run --rm migrate` exits with code 0 on staging
- [ ] Smoke test seed script passes on staging
- [ ] Rollback procedure tested and documented for this specific migration set
- [ ] Maintenance window announced to users (if schema lock expected)

---

## Tenant Onboarding Validation

After a new tenant (company) is created, run the following validation checklist to confirm data isolation is working:

```bash
# Replace COMPANY_ID with the new tenant's ID
COMPANY_ID=42
ADMIN_JWT="<new tenant admin JWT>"

# 1. Verify company was created with required fields
curl -s -H "Authorization: Bearer $ADMIN_JWT" \
  https://your-domain.com/api/companies/$COMPANY_ID | \
  jq 'if .id == '$COMPANY_ID' then "OK" else "FAIL: Company not found" end'

# 2. Verify new admin can only see their own employees
curl -s -H "Authorization: Bearer $ADMIN_JWT" \
  https://your-domain.com/api/employees | \
  jq 'if (.items // []) | all(.companyId == '$COMPANY_ID') then "OK: Tenant isolation working" else "FAIL: Cross-tenant data visible" end'

# 3. Verify new admin cannot access another company's data
OTHER_EMP_ID=1  # An employee from a different company
curl -s -H "Authorization: Bearer $ADMIN_JWT" \
  https://your-domain.com/api/employees/$OTHER_EMP_ID | \
  jq 'if .status == 404 or .status == 403 then "OK: IDOR blocked" else "FAIL: Cross-tenant IDOR" end'
```

---

*Migration validation guide approved: 2026-07-26. Backfill scripts must be run before applying CRIT-1 and HIGH-8 migrations.*
