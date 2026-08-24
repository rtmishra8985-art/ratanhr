# RatanHR Phase 1 Remediation Report

**Date:** 2026-08-06  
**Scope:** Developer-owned production blockers in the RatanHR HRMS codebase  
**Phase 2 status:** **Do not begin yet**

## Executive summary

The Phase 1 source audit and remediation pass is complete for the defects that
could be verified from the repository in this environment. The main verified
corrections cover payroll tenant assignment and transaction atomicity, audit
failure propagation, production configuration-key normalization, and removal
of destructive duplicate-row deletion from the payslip uniqueness migration.

The frontend validation suite passes. Backend Release build, restore, and test
execution could not be performed because the workspace does not contain the
.NET SDK (`dotnet: command not found`). This is an environment blocker, not a
test pass. Phase 1 therefore remains gated pending backend verification.

No production database, production infrastructure, DNS, TLS, client-owned
monitoring, or real secret value was accessed or modified.

## Blockers fixed

### 1. Single payslip generation is now tenant-safe and atomic

`PayrollService.GeneratePayslipAsync` now:

- scopes the employee lookup to `callerCompanyId` for normal tenant callers;
- derives the payslip company from the authenticated caller or target employee;
- does not trust the request DTO's optional `CompanyId`;
- stamps new and regenerated payslips with the authoritative company ID;
- writes the payslip and audit record within the same database transaction;
- rolls back when audit persistence fails.

The bulk generation path also stamps each generated or regenerated payslip
with the requested tenant or employee company and now rolls back all exceptions,
including audit failures.

### 2. Audit persistence failures are no longer silently swallowed

`AuditService.LogAsync` now propagates `SaveChangesAsync` failures. This is
required for callers that intentionally place business writes and audit writes
inside one transaction. The change does not weaken audit logging or replace
failed audit entries with fake success responses.

### 3. Production secret configuration names are normalized safely

The application startup maps the existing deployment-template environment names
to the hierarchical configuration keys actually consumed by the JWT and
encryption services:

- `JWT_PRIVATE_KEY_PEM` → `Jwt:PrivateKeyPem`
- `JWT_PUBLIC_KEY_PEM` → `Jwt:PublicKeyPem`
- `ENCRYPTION_KEY` → `Security:EncryptionKey`

Values are kept in configuration and are not printed, committed, or logged.
`EnvironmentValidator` accepts the application keys and legacy environment
aliases, while still failing startup when required values are absent.
Allowed-host and Redis validation also recognize the configuration names used
by the application.

### 4. Payslip uniqueness migration no longer deletes production rows

`20260806000001_AddUniquePayslipConstraint` now creates the unique index
directly. If duplicate rows already exist, MySQL rejects the index creation
and the migration remains unapplied. Operators must reconcile duplicates using
an explicit, auditable procedure; the migration itself does not delete payroll
data.

## Files changed

- `HRMS.API/Program.cs`
- `HRMS.API/Security/EnvironmentValidator.cs`
- `HRMS.Infrastructure/Services/AuditService.cs`
- `HRMS.Infrastructure/Services/PayrollService.cs`
- `HRMS.Infrastructure/Migrations/MySql/20260806000001_AddUniquePayslipConstraint.cs`
- `HRMS.Tests/Infrastructure/DockerEnvironmentValidationTests.cs`
- `HRMS.Tests/Payroll/PayrollAtomicityTests.cs`
- `PHASE_1_REMEDIATION_REPORT.md`

The production Compose file was inspected but not changed because the Phase 1
rules prohibit modifying production infrastructure.

## Tests added or updated

### Added: `PayrollAtomicityTests`

- `GeneratePayslip_StampsAuthenticatedTenantOnNewRow`
  - proves a newly generated payslip receives the authenticated tenant ID.
- `GeneratePayslip_AuditFailure_RollsBackPayslipWrite`
  - uses a failing audit double and verifies no payslip remains after rollback.

### Updated: `DockerEnvironmentValidationTests`

- verifies legacy environment secret names remain accepted for compatibility
  while application startup normalizes them to the keys consumed by services.

## Commands executed and results

### Backend commands

The required commands were identified from the Phase 1 brief:

```text
dotnet --info
dotnet restore HRMS.sln --locked-mode
dotnet build HRMS.sln --configuration Release --no-restore
dotnet test HRMS.sln --configuration Release --no-build --no-restore
```

**Result:** Not runnable in this workspace. `dotnet --info` returned:

```text
dotnet: command not found
```

Consequently, backend compilation, migration compilation, backend unit tests,
and backend integration tests remain **unverified**. No backend test result is
being represented as a pass.

### Frontend commands

Executed from `HRMS.SPA.Source`:

```text
bun install --frozen-lockfile
bun run typecheck
bun run lint
bun run test
bun run build:ci
```

**Result: PASS**

- Typecheck passed.
- ESLint passed with zero warnings allowed.
- Vitest: 4 test files passed, 76 tests passed.
- Production build passed.
- Vite emitted existing sourcemap-resolution warnings for several UI
  components; these did not fail the build.

### Static repository checks

```text
git diff --check
```

**Result: PASS** at the final source review point.

The running workspace workflows were also checked. The configured API-server
and mockup-sandbox workflows were running without new errors.

## Manual checks still required

These checks require the .NET SDK and representative infrastructure:

1. Run backend restore, Release build, and complete backend test suite.
2. Run the new payroll atomicity tests against both SQLite and the supported
   MySQL provider behavior.
3. Apply the uniqueness migration against a disposable database containing:
   - no duplicates;
   - duplicate payslips;
   - legacy rows requiring company backfill.
4. Verify migration history and schema snapshot consistency with
   `dotnet ef migrations list`.
5. Run two-company authorization, tenant-isolation, IDOR/BOLA,
   authentication/MFA, refresh-token, API-validation, and payroll suites.
6. Execute staging database validation and API smoke tests against isolated
   staging infrastructure.
7. Perform client-owned domain, TLS, SMTP, monitoring, backup, biometric
   hardware, UAT, and formal sign-off checks.

## Remaining risks

- Backend verification is blocked by the missing .NET SDK.
- Existing production databases may already contain duplicate payslips or
  legacy company identifiers. The revised migration intentionally fails rather
  than deleting data; reconciliation must be planned and audited before the
  index is applied.
- Existing audit and production-readiness documents identify staging,
  biometric, client infrastructure, monitoring, backup, and UAT work that
  cannot be verified from this workspace.
- The repository contains historical configuration and audit documentation.
  Operators must use the current startup validation and deployment runbook,
  not assume that a template value is a production secret.

## Phase 1 gate decision

**Phase 2 may not begin yet.**

The required remediation report now exists, and the frontend and static checks
pass. However, the Phase 1 gate requires backend build/test evidence and
backend security, migration, and payroll test evidence. Those commands could
not run because the .NET SDK is unavailable. Phase 2 should begin only after a
runtime with the required .NET SDK completes the backend validation suite and
the remaining manual/staging checks are recorded.