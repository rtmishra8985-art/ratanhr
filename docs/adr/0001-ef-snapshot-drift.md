# ADR-0001 — EF Core model-snapshot drift is not a release gate

- **Status:** Accepted
- **Date:** 2026-08-09
- **Deciders:** Release engineering / backend owners
- **Supersedes:** the blocking CI step "Verify migration snapshot is in sync with the model"

## Context

`dotnet ef migrations has-pending-model-changes --context ApplicationDbContext`
exits `1`. The command compares the live `ApplicationDbContext` model against
`ApplicationDbContextModelSnapshot.cs`.

Two facts drive this decision:

1. **The snapshot is legacy.** It was generated early in the project's life and
   was never regenerated after the team moved to hand-authored SQL migrations
   (MySQL-specific DDL: generated columns, fulltext/prefix indexes, partitions
   and collation choices that the EF scaffolder cannot express faithfully).
   The snapshot declares, for example, 3 of ~22 columns for `payslips`, and the
   same sparse pattern repeats across most of the 81 mapped entities.
2. **The "fix" is destructive.** A disposable probe migration generated to close
   the drift was **14,134 lines** and contained `DropColumn` / `DropTable` /
   `RenameColumn` operations against columns that exist in production. It was
   reviewed and **discarded**; nothing was applied.

Re-baselining the snapshot (deleting the migration history and generating a
single "InitialCreate" from the current model) would also lose the MySQL-native
DDL that the hand-authored migrations carry, and would invalidate
`__EFMigrationsHistory` on every existing deployment.

## Decision

1. Hand-authored SQL migrations under `HRMS.Infrastructure/Migrations/` are the
   **single source of truth** for database schema.
2. `has-pending-model-changes` is demoted to an **advisory** CI step
   (`continue-on-error: true`, emits a `::warning`). It is **not** a release gate.
3. It is replaced by a **blocking** gate that tests the property we actually
   care about: *the migration chain must apply cleanly to an empty database and
   produce a usable schema.* See `.github/workflows/ci.yml` →
   "Verify migrations apply cleanly to a fresh database (blocking)".
4. Schema changes are made by writing a migration by hand and adding coverage in
   `HRMS.Tests`; nobody runs `dotnet ef migrations add` against this context.

## Consequences

- **Positive:** no destructive auto-generated migration can reach a release; the
  gate now fails only for defects that would actually break a deployment.
- **Positive:** the check still runs, so the drift stays visible in every CI log.
- **Negative:** EF's compile-time safety net for "you changed the model but
  forgot the migration" is gone. Mitigation: schema-touching PRs must include a
  migration, and the fresh-database gate plus the integration tests in
  `HRMS.Tests` fail if the model expects a column the migrations never create.

## Revisit criteria

Reopen this ADR if the team migrates to EF-generated migrations wholesale, or
at the next major-version upgrade of EF Core, whichever comes first.
