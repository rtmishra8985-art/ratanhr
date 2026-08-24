# Migrations Directory — IMPORTANT READ BEFORE ADDING MIGRATIONS

## FIX DB-01 — Migration Directory Structure

This project was rebased from a multi-provider history to MySQL-only in July 2026.
`HRMS.Infrastructure.csproj` enforces the following compile rules:

```xml
<Compile Remove="Migrations/*.cs" />      <!-- excludes ALL root-level migration files -->
<Compile Remove="Migrations/**/*.cs" />   <!-- excludes ALL recursive subdirectory files -->
<Compile Include="Migrations/MySql/**/*.cs" /> <!-- RE-ADDS only MySql/ subdirectory -->
```

### What This Means

| Directory | Compiled? | Purpose |
|-----------|-----------|---------|
| `Migrations/` (root `.cs` files) | ❌ **NO** | Historical archive — pre-MySQL migration set. Do NOT touch. |
| `Migrations/MySql/` | ✅ **YES** | **Active production migrations for MySQL 8.4** |

### ⚠️ CRITICAL — New Migrations MUST go in `Migrations/MySql/`

If you run `dotnet ef migrations add MyNewMigration` from the repository root **without**
specifying `--output-dir`, EF Core writes the new file to `Migrations/` — where it is
**silently excluded from compilation and will never run**.

**Always use the `--output-dir` flag:**

```bash
dotnet ef migrations add MyNewMigration \
  --project HRMS.Infrastructure \
  --startup-project HRMS.API \
  --output-dir Migrations/MySql
```

### Verify Your Migration Chain

After adding a migration, confirm EF Core sees it:

```bash
dotnet ef migrations list \
  --project HRMS.Infrastructure \
  --startup-project HRMS.API
```

The list should start with `20260726000001_MySqlInitialSchema` and include your new migration.
If your migration is absent from the list, it was written to the wrong directory.

### Root-Level Files (Historical Archive)

Files directly in `Migrations/` (not `Migrations/MySql/`) are **not compiled**.
They represent the pre-MySQL migration history and are kept for audit trail only.
Do NOT delete them. Do NOT edit them. Do NOT add new files here.
