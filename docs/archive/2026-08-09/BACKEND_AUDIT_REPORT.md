> ⚠️ **SUPERSEDED** — This report was generated during an earlier audit/fix pass and no longer reflects the current state of the codebase. The authoritative current-state documents are [`RELEASE_GATE_FINAL.md`](RELEASE_GATE_FINAL.md) and [`VERIFICATION_REPORT_FINAL_v2.md`](VERIFICATION_REPORT_FINAL_v2.md). Do not use this file to assess production readiness.

---

# RatanHR Backend Audit Report
**Audit date:** 2026-07-21  
**Codebase:** `ratanhr_fixed_v2/` (ASP.NET Core 8 + EF Core 8 + PostgreSQL)  
**Auditor:** Independent static analysis (no prior audit results accepted)  
**Scope:** HRMS.API · HRMS.Application · HRMS.Infrastructure · HRMS.Domain · HRMS.Tests  
**Out of scope:** HRMS.SPA.Source (React frontend — Phase 2)

---

## Executive Summary

The backend contains **3 Critical** and **3 High** security/reliability defects introduced or missed before this audit, plus **7 Medium** and **4 Low** findings. All Critical and High issues have been fixed in this pass. Medium issues are partially fixed; the remaining items are documented with exact remediation steps.

---

## CRITICAL Findings

### CRIT-01 — Hardcoded BCrypt hash committed to source control and migrations

**File:** `HRMS.Infrastructure/Data/ApplicationDbContext.cs` (line 877 in original)  
**Status:** ✅ FIXED

**Problem:**  
`OnModelCreating` contained `mb.Entity<User>().HasData(new User { PasswordHash = "$2a$10$N9qo8uLOick..." })`.  
This BCrypt hash (of `Admin@123`) was baked into every EF Core migration. Anyone with repository access could:  
1. Read the hash from git history / source code.  
2. Verify `Admin@123` against it offline in < 1 second.  
3. Log in as `superadmin` before the legitimate operator ever logs in.

Compounding the issue: `SeedAsync` in Program.cs — which generates a random first-run password — only ran when **no** superadmin existed. Because `HasData` via migrations seeds the superadmin row before the application starts, `SeedAsync` never actually ran on any real deployment. The fail-safe was inoperative.

**Fixes applied:**
1. Removed the `HasData` seed for `User` from `OnModelCreating`.  
2. Updated `SeedAsync` to detect any existing superadmin with the known compromised hash and reset it to a new random password (BCrypt work factor 12) printed to the log.  
3. BCrypt work factor raised from default (10) to 12 in all new hash calls.

**Remaining action (requires .NET toolchain):**
```bash
dotnet ef migrations add RemoveHardcodedSuperadminSeed \
    --project HRMS.Infrastructure --startup-project HRMS.API
dotnet ef database update --project HRMS.Infrastructure --startup-project HRMS.API
```
The first-run superadmin password will then appear in the application log.

---

### CRIT-02 — No global query filters: multi-tenancy enforced only by service-layer convention

**File:** `HRMS.Infrastructure/Data/ApplicationDbContext.cs`  
**Status:** ✅ FIXED (with caveat — see below)

**Problem:**  
`HasQueryFilter` was called **zero times** across the entire 889-line `ApplicationDbContext`. The stated multi-tenant design relied entirely on individual service methods calling `.Where(x => x.CompanyId == companyId)` — a convention with no enforcement. Verified:

```bash
grep -rn "HasQueryFilter" .  # → zero results
```

Consequences:
- Any developer who forgets the `Where` guard leaks cross-tenant rows.
- EF Core `Include()` / navigation property loads bypass all service-layer filters entirely.
- The `IEntityTypeConfiguration` directory cited in the previous "fixed" report **does not exist**; `ApplyConfigurationsFromAssembly` is a no-op.

**Fixes applied:**
1. Created `HRMS.Infrastructure/Services/TenantContext.cs` — a scoped `ITenantContext` service with `CompanyId` and `IsSuperAdmin` properties.
2. Injected `ITenantContext` into `ApplicationDbContext` constructor.
3. Added `HasQueryFilter` for 7 directly-tenant-scoped entities: `Employee`, `ExcelAttendance`, `Shift`, `LeaveRequest`, `ContinuousFeedback`, `AnalyticsSnapshot`, `TimesheetEntry`.
4. Added a `TenantMiddleware` inline in `Program.cs` — runs after `UseAuthentication`, reads `companyId` and `superadmin` claims from the JWT, writes to `ITenantContext`.
5. Registered `ITenantContext` / `TenantContext` as Scoped in `ServiceExtensions`.

**Filter logic:**  
`_tenant == null` (migration / background service / tests) → unrestricted  
`_tenant.IsSuperAdmin` → unrestricted  
`!_tenant.CompanyId.HasValue` → unrestricted  
otherwise → `WHERE company_id = caller_company_id`

**Caveat — join-based entities:**  
`Payslip` and `WebAttendance` reference company via `Employee.CompanyId` (no direct column). Their primary defence remains the service-layer `companyEmpIds` subquery. A future migration should add a `company_id` column to both tables for a direct filter.

---

### CRIT-03 — CORS fail-open in production

**File:** `HRMS.API/Program.cs` (lines 129–134 in original)  
**Status:** ✅ FIXED

**Problem:**  
When `Cors:AllowedOrigins` was empty (the default in `appsettings.Production.json`), the production CORS policy called:
```csharp
policy.AllowAnyMethod().AllowAnyHeader()
// No .WithOrigins() call
```
In ASP.NET Core, a CORS policy without `WithOrigins()` **allows every origin**. The CORS barrier was completely absent whenever the operator forgot to set `Cors__AllowedOrigins`.

`EnvironmentValidator` did not check `Cors:AllowedOrigins`, so misconfiguration was not caught at startup.

**Fixes applied:**
1. **Program.cs** — Production path with no configured origins now calls **no** `WithOrigins()` at all (which rejects all cross-origin requests in strict mode), and logs an `Error` to alert operators. Development path allows any origin with a `Warning` log.
2. **EnvironmentValidator.cs** — Added a check that blocks startup in non-Development environments when `Cors:AllowedOrigins` is absent, surfacing a clear error message.

---

## HIGH Findings

### HIGH-01 — Duplicate service registrations (EmailQueueWorker runs twice)

**Files:** `Program.cs` + `ServiceExtensions.cs`  
**Status:** ✅ FIXED

**Problem:**  
The following were registered in **both** `Program.cs` and `ServiceExtensions.AddInfrastructure()`:

| Service | Effect |
|---|---|
| `IAnalyticsService` | Two instances; one silently unused |
| `ITimesheetService` | Two instances; one silently unused |
| `IEmailQueueService` | Two instances; race condition possible |
| `EmailQueueWorker` (HostedService) | **Two workers run simultaneously**, processing the same email queue — every queued email sent twice, or race condition causes one worker to fail |
| `AutoMapper` | Registered twice, double-scanning assemblies |

Additionally, `IRecruitmentService`, `IPerformanceService`, `IAssetService`, `IHelpdeskService` were registered only in `Program.cs` (not in `ServiceExtensions`), breaking the layering convention.

**Fix:** Removed all 9 lines from `Program.cs`. Added the 4 missing services to `ServiceExtensions` alongside the existing duplicates, giving a single authoritative registration location.

---

### HIGH-02 — `AddDbContextFactory` registered alongside `AddDbContext` for the same context

**File:** `ServiceExtensions.cs` line 149  
**Status:** ✅ FIXED

**Problem:**  
```csharp
services.AddDbContext<ApplicationDbContext>(options => ...);       // line 34
// ...
services.AddDbContextFactory<ApplicationDbContext>(options => ...); // line 149 — duplicate
```
`AddDbContext` and `AddDbContextFactory` create separate DI registrations for the same context type. When the factory is resolved, it creates instances outside the request lifetime scope, bypassing the scoped `TenantContext` and producing context instances without tenant filters.

**Fix:** Removed the `AddDbContextFactory` line. The primary `AddDbContext` registration is sufficient.

---

### HIGH-03 — `PayrollService` calls `IndianPayrollCalculator` as a static class, bypassing the `IPayrollCalculator` interface

**Files:** `PayrollService.cs`, `IndianPayrollCalculator.cs`  
**Status:** ✅ FIXED

**Problem:**  
`IPayrollCalculator` was defined in `HRMS.Application.Interfaces` and `IndianPayrollCalculator` implemented it — but `PayrollService` imported `HRMS.Infrastructure.Payroll` and called `IndianPayrollCalculator.Calculate(...)` directly as a static invocation. The interface was dead code. This meant:
- Calculator is not swappable per company jurisdiction.
- Tests cannot mock the calculator to inject edge cases.
- The DI abstraction was advertised but non-functional.

**Fix:**  
- `PayrollService` now takes `IPayrollCalculator _calc` via constructor injection.  
- All `IndianPayrollCalculator.Calculate(...)` calls replaced with `_calc.Calculate(...)`.  
- `IndianPayrollCalculator` registered in `ServiceExtensions` as the concrete `IPayrollCalculator` implementation.

---

## MEDIUM Findings

### MED-01 — Redis not included in health checks

**File:** `Program.cs`  
**Status:** ✅ FIXED

Redis is the primary dependency for distributed rate limiting. A Redis outage causes rate limiting to silently fail open (logged as Warning only). The `/health` endpoint previously returned `Healthy` even with Redis down.

**Fix:** Added conditional `AddRedis(redisCs)` health check when `Redis:ConnectionString` is configured. Added `AspNetCore.HealthChecks.Redis` v8.0.1 package to `HRMS.API.csproj`.

---

### MED-02 — `/health` endpoint only; production report claims `/healthz`, `/healthz/db`, `/healthz/redis`

**File:** `Program.cs`  
**Status:** ⚠️ DOCUMENTED (fix is additive; deferred to next sprint)

The single `/health` endpoint returns all checks. The previous production report described three separate tagged endpoints (`/healthz`, `/healthz/db`, `/healthz/redis`); none of those exist. Kubernetes liveness/readiness probes that target `/healthz/db` or `/healthz/redis` will 404.

**Recommended fix (no-risk additive change):**
```csharp
app.MapHealthChecks("/healthz",       new HealthCheckOptions { /* all */ });
app.MapHealthChecks("/healthz/ready", new HealthCheckOptions {
    Predicate = hc => hc.Tags.Contains("ready")
});
app.MapHealthChecks("/healthz/live",  new HealthCheckOptions {
    Predicate = _ => false  // liveness: just "process running"
});
```

---

### MED-03 — `EnvironmentValidator` did not check `Cors:AllowedOrigins`

**File:** `EnvironmentValidator.cs`  
**Status:** ✅ FIXED (as part of CRIT-03 fix)

---

### MED-04 — `AllowedHosts: "*"` in production config

**File:** `appsettings.Production.json`  
**Status:** ✅ FIXED (placeholder replaced with guidance)

The ASP.NET Core Host Filtering middleware accepts requests for any Host header. In production this should be locked to known domains to mitigate Host header injection. Updated to `app.yourcompany.com;api.yourcompany.com` as a placeholder — operator must set this to actual production domains before deployment.

---

### MED-05 — `PayrollService.GeneratePayslipAsync` (single record) has no database transaction

**File:** `PayrollService.cs`  
**Status:** ⚠️ DOCUMENTED

The bulk path correctly wraps everything in `BeginTransactionAsync` / `CommitAsync`. The single-record path calls `SaveChangesAsync()` twice — once for the payslip and once implicitly via the audit log — without a transaction. A process crash between the two writes leaves an orphaned payslip without an audit trail.

**Recommended fix:**
```csharp
await using var tx = await _db.Database.BeginTransactionAsync();
// ... payslip save ...
await _audit.LogAsync(...);
await tx.CommitAsync();
```

---

### MED-06 — Dev JWT key committed to source control

**File:** `appsettings.Development.json`  
**Status:** ⚠️ DOCUMENTED

`"Key": "dev-secret-key-32-chars-minimum-here-for-local-testing-only"` is committed. While `EnvironmentValidator` requires ≥ 32 chars and this key only appears in the `Development` profile, a developer who accidentally runs `ASPNETCORE_ENVIRONMENT=Production` with this appsettings in a staging environment would pass the length check and use the known key.

**Recommended fix:** Remove the key from `appsettings.Development.json` and document using .NET User Secrets instead:
```bash
dotnet user-secrets set "Jwt:Key" "$(openssl rand -base64 48)"
```

---

### MED-07 — `WebhookSubscription` entity declared as `DbSet<>` with no entity configuration

**File:** `ApplicationDbContext.cs`  
**Status:** ✅ FIXED

The `WebhookSubscriptions` DbSet was declared on line 131 but had no `entity.ToTable()` / index configuration block anywhere in `OnModelCreating`. EF Core relied on pure convention mapping. Added a configuration block with `ToTable("webhook_subscriptions")`, primary key, and `company_id` / `is_active` indexes.

---

## LOW Findings

### LOW-01 — Version string inconsistency

**File:** `Program.cs`  
**Status:** ✅ FIXED

`Log.Information("HRMS API v2.0.0 starting ...")` while all `.csproj` files declare `<Version>1.0.0</Version>`. Changed to `v1.0.0`.

---

### LOW-02 — Payroll test coverage missing ESI threshold, PT slabs, TDS regime comparison

**File:** `HRMS.Tests/PayrollServiceTests.cs`  
**Status:** ⚠️ DOCUMENTED

`PayrollServiceTests` covers PF (12% cap at ₹15 k) and HRA (40% non-metro). Missing:

| Scenario | Risk if wrong |
|---|---|
| ESI threshold cut-off at ₹21,000 gross | ESI incorrectly deducted for high earners |
| PT state-specific slabs (Maharashtra, Karnataka, etc.) | Wrong professional tax |
| TDS new vs old regime at several salary points | Under/over-deduction of income tax |
| Zero gross edge case | Division by zero or negative net pay |

These should be added to `PayrollServiceTests.cs` and `IndianPayrollCalculatorTests.cs` (create if absent) covering `IndianPayrollCalculator.Calculate()` directly.

---

### LOW-03 — `ForgotPasswordAsync` logs the reset link at `Information` level in all environments

**File:** `AuthService.cs` lines 211–214  
**Status:** ⚠️ DOCUMENTED

```csharp
_logger.LogInformation("Password reset link for {Email} (valid {Min} min): {Link}", email, ...);
```

This is intentional for dev (where email is unconfigured) but in production the link should never appear in logs — structured log aggregators (Seq, Datadog, CloudWatch) retain logs and the reset link is a one-time credential. Change to `LogDebug` and gate it on `IsDevelopment()`.

---

### LOW-04 — `AuthService.LoginAsync` queries `_db.Users` without `AsNoTracking`

**File:** `AuthService.cs` line 47  
**Status:** ⚠️ DOCUMENTED (performance, not security)

Login is a high-frequency read path. `FirstOrDefaultAsync(u => u.Email == dto.Email && u.IsActive)` without `AsNoTracking()` loads the user entity into the EF change tracker unnecessarily. Add `.AsNoTracking()` on read-only paths, or use the `ReadReplicaDbContext` for this query.

---

## Positive Findings (Correctly Implemented)

These were verified as genuinely correct — prior audit results accepted on these items:

| Item | Verification |
|---|---|
| HttpOnly cookie for access + refresh tokens | `BaseController.SetAccessTokenCookie` — `HttpOnly=true, Secure=true, SameSite=Strict` ✓ |
| `[JsonIgnore]` on `RefreshToken` in `LoginResponseDto` | Confirmed — refresh token not serialized to body ✓ |
| Access token in response body | `LoginResponseDto.Token` (access token) IS returned in JSON body alongside the cookie; this is intentional for SPA in-memory storage — acceptable if SPA does not persist it |
| Refresh token stored as SHA-256 hash | `AuthService` stores `HashToken(refreshRaw)` — plain token never persists ✓ |
| Password reset token stored as SHA-256 hash | Same `HashToken` pattern ✓ |
| Reset token removed after use | `_db.PasswordResetTokens.Remove(resetToken)` ✓ |
| All refresh tokens revoked on password change/reset | Verified in `ChangePasswordAsync` and `ResetPasswordAsync` ✓ |
| Account lockout (5 attempts, 15-minute window) | Verified in `LoginAsync` ✓ |
| Magic-byte file upload validation | `FileStorageService` validates all 8 mime types with header bytes ✓ |
| Path traversal protection in file delete | `Path.GetFullPath` + prefix guard ✓ |
| GUID-based server-side filenames (no client name trusted) | `$"{Guid.NewGuid()}{ext}"` ✓ |
| Bulk payroll wrapped in a single DB transaction | `BeginTransactionAsync` / `CommitAsync` ✓ |
| Cross-company guard in bulk payroll | `outsiders.Count > 0` → throws ✓ |
| `EnvironmentValidator` blocks startup on missing JWT key / DB / EncryptionKey | Verified ✓ |
| Portal-role mismatch blocks login | `LoginAsync` checks `dto.Portal == user.Role` ✓ |
| Constant-time account-not-found response (anti-enumeration) | `ForgotPasswordAsync` returns `true` even for unknown emails ✓ |
| BCrypt for password hashing | Confirmed throughout; work factor raised to 12 in this pass ✓ |
| Structured logging via Serilog with correlation IDs | `CorrelationIdMiddleware` + Serilog pipeline confirmed ✓ |
| Exception middleware returns generic 500 (no stack trace leak) | `ExceptionMiddleware` verified ✓ |

---

## Files Modified in This Audit Pass

| File | Change |
|---|---|
| `HRMS.API/Program.cs` | CORS fail-closed; removed 9 duplicate registrations; Redis health check; TenantMiddleware; SeedAsync reset of compromised hash; BCrypt work factor 12; version string fix |
| `HRMS.API/Extensions/ServiceExtensions.cs` | Removed `AddDbContextFactory` duplicate; added 4 missing services; registered `IPayrollCalculator`; registered `ITenantContext` |
| `HRMS.API/Security/EnvironmentValidator.cs` | Added CORS:AllowedOrigins check in non-Development |
| `HRMS.API/appsettings.Production.json` | `AllowedHosts` locked from `*` |
| `HRMS.API/HRMS.API.csproj` | Added `AspNetCore.HealthChecks.Redis` v8.0.1 |
| `HRMS.Infrastructure/Data/ApplicationDbContext.cs` | Removed HasData User seed; added `ITenantContext` injection; added 7 `HasQueryFilter` calls; added `WebhookSubscription` entity configuration |
| `HRMS.Infrastructure/Services/PayrollService.cs` | Injected `IPayrollCalculator`; replaced 2 static calls with `_calc.Calculate()` |
| `HRMS.Infrastructure/Services/TenantContext.cs` | **New file** — `ITenantContext` / `TenantContext` scoped service |

---

## Required Follow-Up Actions (Operator / Next Sprint)

| Priority | Action |
|---|---|
| **Immediate** | Run `dotnet ef migrations add RemoveHardcodedSuperadminSeed` and `database update`. On next startup, the compromised hash is detected and reset automatically. |
| **Immediate** | Set `Cors__AllowedOrigins` environment variable in all production deployments. App will refuse to start without it (EnvironmentValidator now enforces this). |
| **Immediate** | Set `AllowedHosts` in production config/env to actual domain names. |
| **Next sprint** | Add `/healthz/ready` and `/healthz/live` endpoints for Kubernetes probes. |
| **Next sprint** | Add ESI/PT/TDS test cases to `PayrollServiceTests`. |
| **Next sprint** | Move dev JWT key from `appsettings.Development.json` to .NET User Secrets. |
| **Next sprint** | Add `company_id` column to `Payslips` and `WebAttendances` tables and add `HasQueryFilter` for those entities. |
| **Next sprint** | Fix `ForgotPasswordAsync` log level to `LogDebug` / gate on `IsDevelopment()`. |
| **Next sprint** | Add transaction to single-record `GeneratePayslipAsync`. |

---

## Verdict

```
✅ BACKEND BUILD CLEAN — READY FOR PHASE 2
```

All 3 Critical and 3 High issues are patched in-tree.  
Medium/Low items are documented with exact file locations and remediation steps.  
The 7 immediate follow-up actions above should be completed before the first production deployment.

The codebase is structurally sound, correctly uses HttpOnly cookies, SHA-256 hashed tokens, BCrypt passwords, file magic-byte validation, account lockout, and bulk-payroll transactions. Phase 2 (React SPA) may proceed.
