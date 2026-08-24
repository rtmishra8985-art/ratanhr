# Supplementary SQL Files — Execution Order & Purpose

> **Primary deployment path:** EF Core migrations only.
> Run `docker compose -f docker-compose.prod.yml up migrate` or `dotnet ef database update`.
> The supplementary SQL files below are **additive** — they add indexes, soft-delete columns,
> and data fixes that are either already included in later EF migrations or need to be
> applied once to existing databases that pre-date those migrations.

---

## Decision Tree

```
Fresh install?
  └─ YES → Use EF Core migrations only. Do NOT run any .sql file below.
  └─ NO  → Upgrading a live database that predates 2026-07-28?
              └─ YES → Apply the files below in the order listed.
              └─ NO  → All index/column changes are already in EF migrations ≥ 20260728.
                        No supplementary SQL needed.
```

---

## File Inventory and Execution Order

> All files use `IF NOT EXISTS` / `IF NOT EXISTS` guards. They are idempotent —
> safe to run more than once on the same database.

| Order | File | Purpose | Run against |
|-------|------|---------|-------------|
| 1 | `db_setup_additions.sql` | Adds supplementary columns not in the original bootstrap schema | MySQL (`hrms_db`) |
| 2 | `db_softdelete_fix.sql` | Adds `is_deleted` / `deleted_at` columns to tables that used hard-delete | MySQL (`hrms_db`) |
| 3 | `db_indexes_fix.sql` | Composite multi-tenant indexes for tenant-scoped queries | MySQL (`hrms_db`) |
| 4 | `db_performance.sql` | FK-supporting indexes and read-heavy query optimisations | MySQL (`hrms_db`) |
| 5 | `db_crm.sql` | CRM/Sales schema additions (clients, deals, activities) | MySQL (`hrms_db`) |
| 6 | `db_recruitment.sql` | Recruitment additions (job_requisitions extended fields) | MySQL (`hrms_db`) |

**Files that are HISTORICAL / DO NOT EXECUTE:**

| File | Reason |
|------|--------|
| `db_setup.sql` | PostgreSQL TIMESTAMPTZ syntax — incompatible with MySQL. Documentation reference only. |
| `bootstrap_only_db_setup.sql` | PostgreSQL syntax. Documentation reference only. |

---

## How to Apply (upgrading a live database)

```bash
# Connect to the MySQL database
MYSQL="mysql -h <host> -u hrms_user -p hrms_db"

# Apply in order
$MYSQL < db_setup_additions.sql
$MYSQL < db_softdelete_fix.sql
$MYSQL < db_indexes_fix.sql
$MYSQL < db_performance.sql
$MYSQL < db_crm.sql
$MYSQL < db_recruitment.sql

echo "Supplementary SQL applied successfully."
```

Then run EF Core migrations to bring the schema up to the latest version:

```bash
dotnet ef database update --project HRMS.Infrastructure --startup-project HRMS.API
```

---

## Long-term recommendation

These supplementary SQL files should be migrated into EF Core migrations in the next
major release cycle so there is a single deployment path with full rollback support.
Track as technical debt item in the backlog.
