> ⚠️ This report is current. It supersedes all prior BUGFIX_CHANGELOG_* files except BUGFIX_CHANGELOG_AUDIT_V8.md (which remains the most recent backend-only audit pass).

# Bugfix Changelog — Final Remaining Items
**HRMS v1.0.3** | Date: 2026-08-01

---

## Summary

Four items left outstanding after the V8 audit pass have been resolved in this pass.

---

## FIX 1 — Backend broken core read path: `GetProfileAsync` missing `AsNoTracking`

**File:** `HRMS.Infrastructure/Services/AuthService.cs`  
**Severity:** Medium (performance — not a security defect)  
**Audit ref:** BACKEND_AUDIT_REPORT.md LOW-04

### Problem

`GetProfileAsync` queried `_db.Users` without `.AsNoTracking()`:
```csharp
var u = await _db.Users.FirstOrDefaultAsync(x => x.Id == userId);
```
This is a pure-read method — the `User` entity is never mutated; its fields are only mapped to `UserProfileDto`. Every call unnecessarily loaded the entity into the EF Core change tracker. This method is the hottest read path in the application: it is called by `ProfileController.GetProfile()` on every SPA page navigation to populate the header and guard routes.

The LOW-04 fix was previously applied to `Employee` lookups inside `LoginAsync` and `RefreshTokenAsync` (both confirmed), but the `User` lookup in `GetProfileAsync` was overlooked.

### Fix

Added `.AsNoTracking()` to the `_db.Users` query in `GetProfileAsync`:
```csharp
var u = await _db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == userId);
```

No change-tracking is needed here; EF Core saves change-tracking allocations and snapshot comparisons on every call to this endpoint.

---

## FIX 2 — `Microsoft.Extensions.Caching.Memory` 8.0.0 High advisory not pinned in `HRMS.Tests`

**File:** `HRMS.Tests/HRMS.Tests.csproj`  
**Severity:** High advisory (GHSA-qcnp-7r8w-h4x8)

### Problem

`HRMS.API.csproj` and `HRMS.Infrastructure.csproj` both explicitly reference version 8.0.1 (the patched release). However, `HRMS.Tests.csproj` had `<NoWarn>NU1603;NU1605</NoWarn>` — suppressing NuGet version-conflict warnings — with no direct pin for `Microsoft.Extensions.Caching.Memory`. This means a transitive resolution through test-host packages could silently downgrade to the vulnerable 8.0.0 with no visible warning.

### Fix

Added an explicit pin to `HRMS.Tests.csproj`:
```xml
<!-- SECURITY: Explicit transitive pin for Microsoft.Extensions.Caching.Memory.
     NU1603 is suppressed in this project which means the advisory version 8.0.0
     (GHSA-qcnp-7r8w-h4x8, High) could slip through undetected. Pinning to 8.0.1
     (the patched release) forces the floor across all transitive paths. -->
<PackageReference Include="Microsoft.Extensions.Caching.Memory" Version="8.0.1" />
```

**Note on dotnet build/test:** `dotnet` SDK is not present in this environment. The `.csproj` pins are syntactically correct and will take effect on the next `dotnet restore`. To confirm the resolved version after merging, run:
```
dotnet list package --include-transitive | grep Caching.Memory
```
The output should show 8.0.1 for all projects.

---

## FIX 3 — npm/bun audit: critical and high vulnerabilities resolved

**File:** `HRMS.SPA.Source/package.json`, `HRMS.SPA.Source/bun.lock`  
**Severity:** 1 Critical, 1 High, 3 Moderate (all resolved)

### Before

| Package | Constraint | Advisory | Severity |
|---------|-----------|---------|---------|
| vitest | ^2.0.0 | GHSA-5xrq-8626-4rwp (arbitrary file read/exec via Vitest UI server) | **Critical** |
| vite | ^5.4.2 | GHSA-fx2h-pf6j-xcff (server.fs.deny bypass — Windows) | **High** |
| vite | ^5.4.2 | GHSA-4w7w-66w2-5vf9 (path traversal in optimized deps) | Moderate |
| vite | ^5.4.2 | GHSA-v6wh-96g9-6wx3 (NTLMv2 hash disclosure — Windows) | Moderate |
| esbuild | (via vite) | GHSA-67mh-4wv8-2f99 (cross-origin dev-server requests) | Moderate |

### Fix

Updated `package.json` constraints:

| Package | Old | New | Resolved |
|---------|-----|-----|---------|
| `vitest` | `^2.0.0` | `^3.2.6` | 3.2.7 |
| `@vitest/coverage-v8` | `^2.0.0` | `^3.2.6` | 3.2.7 |
| `vite` | `^5.4.2` | `^6.4.3` | 6.4.3 |
| `@vitejs/plugin-react` | `^4.3.1` | `^4.7.0` | compatible |
| `@tailwindcss/vite` | `^4.0.6` | `^4.1.11` | compatible |

`bun.lock` regenerated with the resolved versions. `bun audit` confirms **no vulnerabilities found** after the update.

**Context on vite advisories:** GHSA-fx2h-pf6j-xcff and GHSA-v6wh-96g9-6wx3 are Windows-only (UNC path / alternate path bypass). GHSA-4w7w-66w2-5vf9 and GHSA-67mh-4wv8-2f99 affect the Vite dev server only, not production builds. vite 6.4.3 addresses all of them.

---

## FIX 4 — ~48 stale PRODUCTION_READY markdown reports marked as superseded

**Files:** 48 root-level markdown files  
**Severity:** Documentation hygiene

### Problem

Approximately 48 root-level markdown reports from earlier audit passes contained claims like "✅ PRODUCTION READY" or "Status: PRODUCTION READY". These were generated incrementally over the audit lifecycle and no longer reflect the current codebase state (several of the checks they claim to have passed had not yet been verified at code level, or were overtaken by subsequent findings).

### Fix

Prepended a standardised supersession banner to all 48 stale documents:

```
> ⚠️ **SUPERSEDED** — This report was generated during an earlier audit/fix pass
> and no longer reflects the current state of the codebase. The authoritative
> current-state documents are RELEASE_GATE_FINAL.md and
> VERIFICATION_REPORT_FINAL_v2.md. Do not use this file to assess production
> readiness.
```

**Not modified** (current/operational): `README.md`, `replit.md`, `RELEASE_GATE_FINAL.md`, `RELEASE_NOTES.md`, `CHANGELOG.md`, `RUNBOOK.md`, `UPGRADE_NOTES.md`, `GITHUB_SECRETS_SETUP.md`, `INTEGRATION_GUIDE.md`, `LIVE_VERIFICATION_SETUP.md`, `BUGFIX_CHANGELOG_AUDIT_V8.md`, `VERIFICATION_REPORT_FINAL_v2.md`.

---

## Open Items (not addressed in this pass)

| # | Item | Reason not addressed |
|---|------|---------------------|
| O1 | `dotnet test` not re-run | `dotnet` SDK absent from this environment. All `.csproj` changes are syntactically correct. Run `dotnet test` locally after merging. |
| O2 | `UpdateWebAttendanceStatusAsync` unguarded interface method | Currently dead code (zero controller callers). Recommendation per VERIFICATION_REPORT_FINAL_v2.md: either remove from `IAttendanceService` / `AttendanceService`, or add the `actorCompanyId` IDOR guard matching `EditWebAttendanceAsync`. Scoped to a follow-up task. |

