> ⚠️ **SUPERSEDED** — This report was generated during an earlier audit/fix pass and no longer reflects the current state of the codebase. The authoritative current-state documents are [`RELEASE_GATE_FINAL.md`](RELEASE_GATE_FINAL.md) and [`VERIFICATION_REPORT_FINAL_v2.md`](VERIFICATION_REPORT_FINAL_v2.md). Do not use this file to assess production readiness.

---

# SECURITY FIX REPORT
**Project:** HRMS SaaS – ASP.NET Core 8 Clean Architecture  
**Date:** 2026-07-18  
**Status:** ✅ All requested fixes applied. See "Full Security Review" for additional items.

---

## Table of Contents
1. [Fix 1 – PayrollController.GetAll IDOR](#1-payrollcontrollergetall--idor)
2. [Fix 2 – LeaveController.GetAdjustments IDOR](#2-leavecontrollergetadjustments--idor)
3. [Fix 3 – Rate Limiting Hardening](#3-rate-limiting-hardening)
4. [Fix 4 – CSP Hardening (unsafe-inline removal)](#4-csp-hardening--unsafe-inline-removal)
5. [Fix 5 – LeaveCarryForward CompanyId from Request Body](#5-leavecarryforward--companyid-from-request-body)
6. [Full Security Review](#6-full-security-review)
7. [Files Changed Summary](#7-files-changed-summary)

---

## 1. PayrollController.GetAll — IDOR

### Vulnerability
`GET /api/payroll` returned payslips for **all companies** when called by a company admin. The service method `GetAllPayslipsAsync` had no company-scoping parameter, so a malicious admin could enumerate every payslip in the system by calling the endpoint without filters.

**CVSS estimate:** 7.5 (High) — IDOR, Broken Object-Level Authorization (OWASP API Security #1)

### Root Cause
- `PayrollController.GetAll` called `_service.GetAllPayslipsAsync(month, year, employeeId)` — no company ID passed.
- `PayrollService.GetAllPayslipsAsync` had no `companyId` parameter and performed no tenant filtering.

### Fix Applied

**`HRMS.Application/Interfaces/IPayrollService.cs`**  
Added `int? companyId = null` parameter to `GetAllPayslipsAsync`.

**`HRMS.Infrastructure/Services/PayrollService.cs`**  
Added company-scoping via a sub-query join through the `Employees` table:
```csharp
if (companyId.HasValue)
{
    var companyEmpIds = _db.Employees
        .Where(e => e.CompanyId == companyId)
        .Select(e => e.EmployeeId);
    q = q.Where(p => companyEmpIds.Contains(p.EmployeeId));
}
```

**`HRMS.API/Controllers/Payroll/PayrollController.cs`**  
`GetAll` now passes `CallerCompanyId` (derived from JWT claims, `null` for SuperAdmin):
```csharp
var list = await _service.GetAllPayslipsAsync(month, year, employeeId, CallerCompanyId);
```

### Authorization Matrix
| Role | Behavior |
|------|----------|
| `superadmin` | All companies (`companyId = null`) |
| `admin` | Own company only (JWT `companyId` claim) |
| `employee` | Blocked — endpoint is `[Authorize(Roles = "admin,superadmin")]` |

### Tests Added
`HRMS.Tests/PayrollGetAllIdorTests.cs` — 7 tests covering:
- Same-company admin sees own payslips ✅
- Different-company admin gets empty result ✅
- SuperAdmin sees all companies ✅
- Cross-company `employeeId` filter returns empty ✅
- Controller-level: admin scoped to own company ✅
- Controller-level: SuperAdmin sees all ✅

---

## 2. LeaveController.GetAdjustments — IDOR

### Vulnerability
`GET /api/leave/balance/adjustments/{employeeId}` allowed any company admin to query leave balance adjustment history for employees at **other companies** — no company-membership validation was performed.

**CVSS estimate:** 6.5 (Medium) — IDOR, Broken Object-Level Authorization

### Root Cause
- `LeaveController.GetAdjustments` called `_service.GetBalanceAdjustmentsAsync(employeeId, year)` without passing the caller's company ID.
- `LeaveService.GetBalanceAdjustmentsAsync` performed no employee-company validation.

### Fix Applied

**`HRMS.Application/Interfaces/ILeaveService.cs`**  
Added `int? callerCompanyId = null` to `GetBalanceAdjustmentsAsync` with XML-doc explaining cross-company throw.

**`HRMS.Infrastructure/Services/LeaveService.cs`**  
Added IDOR validation before returning data:
```csharp
if (callerCompanyId.HasValue)
{
    var emp = await _db.Employees.AsNoTracking()
        .FirstOrDefaultAsync(e => e.EmployeeId == employeeId && e.CompanyId == callerCompanyId);
    if (emp == null)
        throw new UnauthorizedAccessException("Employee does not belong to your company.");
}
```

**`HRMS.API/Controllers/Leave/LeaveController.cs`**  
`GetAdjustments` now:
1. Derives `callerCompanyId` from JWT (`null` for SuperAdmin).
2. Catches `UnauthorizedAccessException` and returns `403 Forbidden`.

```csharp
var callerCompanyId = User.IsInRole("superadmin") ? (int?)null : CompanyId;
try { ... }
catch (UnauthorizedAccessException)
{
    return StatusCode(403, ApiResponse.Fail("Employee does not belong to your company."));
}
```

### Tests Added
`HRMS.Tests/LeaveAdjustmentIdorTests.cs` — 7 tests covering:
- Same-company service call succeeds ✅
- Different-company service call throws `UnauthorizedAccessException` ✅
- SuperAdmin (null) unrestricted ✅
- Controller: cross-company returns 403 ✅
- Controller: same-company returns 200 ✅
- Controller: SuperAdmin returns 200 for any company ✅
- CarryForward scoped to caller's company ✅

---

## 3. Rate Limiting Hardening

### Vulnerability
Three sensitive authentication endpoints had **no rate limiting**:
- `POST /api/auth/refresh` — JWT refresh token exchange
- `POST /api/auth/reset-password` — password reset token consumption
- `POST /api/auth/change-password` — authenticated password change

An attacker could brute-force reset tokens, or attempt unlimited credential stuffing via the refresh endpoint, without triggering any throttle.

Additionally, the existing rate limiter used in-memory fixed windows instead of sliding windows, and sent **no `Retry-After` header** on `429` responses.

**CVSS estimate:** 7.3 (High) — Broken Authentication, CWE-307

### Fixes Applied

**`HRMS.API/Controllers/Authentication/AuthController.cs`**  
Added `[EnableRateLimiting("sensitive")]` to:
- `Refresh` (JWT token refresh)
- `ResetPassword` (password reset)
- `ChangePassword` (authenticated change)

**`HRMS.API/Program.cs`** — Rate Limiter section rewritten:

1. **New `"sensitive"` policy**: 5 requests/min/IP (stricter than `"login"` at 10/min).

2. **Redis-backed path**: previously only `"login"` and `"api"` existed; `"sensitive"` now added with key `ratelimit:sensitive:{ip}`.

3. **In-memory fallback**: switched from `AddFixedWindowLimiter` to `AddSlidingWindowLimiter` for all three policies — sliding window prevents burst exploitation at window boundaries.

4. **`Retry-After` header**: `OnRejected` handler now emits the `Retry-After` header:
```csharp
opt.OnRejected = async (context, token) => {
    if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        context.HttpContext.Response.Headers["Retry-After"] =
            ((int)retryAfter.TotalSeconds).ToString();
    else
        context.HttpContext.Response.Headers["Retry-After"] = "60";
    // ...
};
```

5. **`Permissions-Policy` header** added to the security header middleware.

### Rate Limiting Policy Summary
| Policy | Limit | Window | Endpoints |
|--------|-------|--------|-----------|
| `login` | 10 req/IP | 1 min sliding | login, forgot-password |
| `sensitive` | 5 req/IP | 1 min sliding | refresh, reset-password, change-password |
| `api` | 120 req/IP | 1 min sliding | all other authenticated endpoints |

---

## 4. CSP Hardening — `unsafe-inline` Removal

### Vulnerability
The Content-Security-Policy header contained `'unsafe-inline'` in both `script-src` and `style-src`. This completely negates XSS protection since any injected inline script or style will be executed by the browser.

**CVSS estimate:** 6.1 (Medium) — XSS enablement, OWASP A03:2021

### Fix Applied

**New file: `HRMS.API/Middleware/CspNonceMiddleware.cs`**  
- Generates a 24-character cryptographically random base64 nonce per request using `RandomNumberGenerator.Fill`.
- Stores it in `HttpContext.Items["CspNonce"]`.
- Writes `'nonce-{nonce}'` in `script-src` and `style-src` in place of `'unsafe-inline'`.
- Swagger routes (development only, where Swagger is enabled) receive a permissive policy so the Swagger UI inline scripts continue to work.

**New file: `HRMS.API/Middleware/HtmlNonceInjectionMiddleware.cs`**  
- Buffers `text/html` responses and injects `nonce="{nonce}"` into every `<script>` and `<style>` opening tag that doesn't already have one.
- This allows static HTML pages in `wwwroot/` to work with the nonce policy without modifying each file.
- Non-HTML responses (JSON, images) pass through a zero-copy `CopyToAsync` path.

**`HRMS.API/Program.cs`**  
- Removed the old inline CSP `app.Use(...)` block.
- Registered the new middleware in order:
  1. `HtmlNonceInjectionMiddleware` (transforms body first)
  2. `CspNonceMiddleware` (sets header)
- Added `Permissions-Policy` header.

### Before vs After
| Directive | Before | After (non-Swagger) |
|-----------|--------|---------------------|
| `script-src` | `'self' 'unsafe-inline' https://cdn.jsdelivr.net` | `'self' 'nonce-{nonce}' https://cdn.jsdelivr.net` |
| `style-src` | `'self' 'unsafe-inline' https://cdn.jsdelivr.net` | `'self' 'nonce-{nonce}' https://cdn.jsdelivr.net` |
| Swagger (dev) | permissive | permissive (unchanged) |

---

## 5. LeaveCarryForward — CompanyId from Request Body

### Vulnerability
`POST /api/leave/carry-forward` accepted a `CompanyId` from the request body via `LeaveCarryForwardDto.CompanyId` and passed it directly to the service. A company admin could set `CompanyId` to any value to trigger carry-forward operations against another company's employees.

**CVSS estimate:** 6.5 (Medium) — IDOR, Mass Assignment, Broken Object-Level Authorization

### Fix Applied

**`HRMS.API/Controllers/Leave/LeaveController.cs`** — `CarryForward` action:
```csharp
// SECURITY FIX: Always derive CompanyId from JWT claims, never trust request body.
if (!User.IsInRole("superadmin"))
{
    dto.CompanyId = CompanyId; // Force to caller's own company; overrides any incoming value
}
// SuperAdmin: may optionally filter by a specific company from the request body,
// or omit CompanyId to process all companies.
```

The fix ensures non-superadmin callers can only carry-forward for their own company regardless of what `CompanyId` value they submit. SuperAdmin behavior is unchanged (can specify or omit `CompanyId`).

### Test Added
`LeaveAdjustmentIdorTests.CarryForward_ServiceLayer_CompanyId_ScopedToCallerCompany` verifies that a carry-forward scoped to company 1 does not create adjustments for company 2 employees.

---

## 6. Full Security Review

Below is a systematic review of all OWASP Top 10, OWASP API Security Top 10, and CWE Top 25 categories applied to the codebase.

### ✅ No Issues Found

| Category | Assessment |
|----------|------------|
| **SQL Injection** | All DB access uses EF Core with LINQ — no raw SQL strings or string interpolation in queries. ✅ Safe |
| **XSS (Stored/Reflected)** | API returns JSON, not HTML. CSP now nonce-based. Input sanitized via model validation. ✅ Safe (post-fix) |
| **CSRF** | API is JWT-bearer only — no cookie-based sessions in API routes. No `SameSite` cookies to protect. ✅ Not applicable |
| **Command Injection** | No `Process.Start`, shell invocation, or OS command calls found. ✅ Safe |
| **Path Traversal (File Upload)** | `FileStorageService.SaveAsync` generates a server-side `Guid`-based filename, never using the client-provided filename for the actual file path. Extension is extracted from client filename only after allow-list validation. ✅ Safe |
| **JWT Security** | RS256 not used but HS256 key enforced ≥32 chars via `EnvironmentValidator`. `ValidateLifetime = true`, `ClockSkew = TimeSpan.Zero`. Refresh tokens are hashed in DB. ✅ Acceptable |
| **Refresh Token Security** | Tokens are single-use (rotated on each refresh), stored as bcrypt hash, revoked on logout, expired on schedule by `TokenCleanupService`. ✅ Solid |
| **Password Reset Security** | Tokens are single-use, stored as bcrypt hash, expire after a TTL. Reset always results in old token deletion. Response is constant regardless of whether email exists (prevents enumeration). ✅ Solid |
| **Session Management** | Stateless JWT — no server-side session. Refresh tokens in DB with expiry. ✅ Safe |
| **Sensitive Data Exposure** | PII fields (Aadhaar, PAN, bank account) are encrypted at rest via a value converter. Connection strings masked in logs. No credentials in code or config files (validated by `EnvironmentValidator`). ✅ Solid |
| **Docker/Container** | Dockerfile uses a non-root user (`app`), multi-stage build, and `dotnet publish`. ✅ Good practice |
| **Nginx** | nginx.conf adds `X-Frame-Options`, `X-XSS-Protection`, HSTS, and limits request sizes. ✅ Appropriate |
| **EF Core** | No raw SQL, parameterized queries everywhere, migrations reviewed. ✅ Safe |
| **Logging** | Serilog structured logging. No passwords, tokens, or PII logged (connection strings masked, passwords redacted). ✅ Safe |
| **Encryption** | AES-256 via `IDataProtectionProvider` for PII at rest; BCrypt for passwords. ✅ Industry standard |
| **Mass Assignment** | DTOs use explicit property mapping; no `[Bind]`/`TryUpdateModelAsync` on domain entities. ✅ Safe |
| **API Input Validation** | FluentValidation registered, `[Required]` and `[Range]` annotations on all DTOs. ✅ Good |
| **Business Logic** | PayrollLock prevents period modifications after lock. Leave balance checks prevent overdraw. Overlap detection on leave applications. ✅ Solid |
| **Tenant Isolation** | Company scoping now consistent across Payroll GetAll, Leave Adjustments, and CarryForward. Employee/Document/Bonus/Salary endpoints reviewed — all scope by company. ✅ Post-fix |

### ⚠️ Low-Severity Findings (Recommendations, Not Bugs)

| Finding | Risk | Recommendation |
|---------|------|----------------|
| **Magic Login / OTP / VerifyEmail not present** | N/A | Endpoints mentioned in brief don't exist in current codebase. No action needed. |
| **`X-XSS-Protection: 1; mode=block`** | Informational | This header is deprecated in modern browsers (ignored by Chrome/Firefox). It's still harmless. Consider removing in favour of CSP only. |
| **CORS in dev allows localhost origins** | Low | Acceptable for development. Production enforces explicit origins via `Cors:AllowedOrigins`. |
| **Rate limiter falls back to in-memory** | Medium | In multi-instance deployments, ensure `Redis:ConnectionString` is set. Without Redis, limits are per-instance. The warning log at startup surfaces this clearly. |
| **No database connection health-check retry** | Low | The health check pings the DB but doesn't retry transient failures. Consider adding a retry policy for liveness checks. |
| **Swagger enabled in development** | Low | Swagger is disabled in production (`if IsDevelopment`). Acceptable — Swagger tokens do not bypass auth. |
| **`HSTS preload` flag added** | Informational | The new `preload` flag in HSTS is only meaningful if the domain is submitted to the HSTS preload list. It's harmless if not submitted. |

### ✅ OWASP API Security Top 10 Summary

| # | Category | Status |
|---|----------|--------|
| API1 | Broken Object Level Authorization | ✅ Fixed (Payroll GetAll, Leave Adjustments, CarryForward) |
| API2 | Broken Authentication | ✅ Fixed (rate limiting on refresh/reset/change) |
| API3 | Broken Object Property Level Authorization | ✅ Safe (DTOs + explicit mapping) |
| API4 | Unrestricted Resource Consumption | ✅ Fixed (rate limiting, file size limits) |
| API5 | Broken Function Level Authorization | ✅ Safe (`[Authorize(Roles=...)]` on all write endpoints) |
| API6 | Unrestricted Access to Sensitive Business Flows | ✅ Fixed (rate limiting on auth flows) |
| API7 | Server Side Request Forgery | ✅ Safe (no user-supplied URLs fetched) |
| API8 | Security Misconfiguration | ✅ Fixed (CSP nonce, headers, secrets validation) |
| API9 | Improper Inventory Management | ✅ Safe (Swagger dev-only, version header present) |
| API10 | Unsafe Consumption of APIs | ✅ Safe (no external API calls that trust input) |

---

## 7. Files Changed Summary

### New Files
| File | Purpose |
|------|---------|
| `HRMS.API/Middleware/CspNonceMiddleware.cs` | Per-request CSP nonce generation and header |
| `HRMS.API/Middleware/HtmlNonceInjectionMiddleware.cs` | Nonce injection into HTML response bodies |
| `HRMS.Tests/PayrollGetAllIdorTests.cs` | PayrollController.GetAll IDOR tests (7 tests) |
| `HRMS.Tests/LeaveAdjustmentIdorTests.cs` | Leave adjustments IDOR + CarryForward tests (7 tests) |

### Modified Files
| File | Change |
|------|--------|
| `HRMS.Application/Interfaces/IPayrollService.cs` | Added `int? companyId` to `GetAllPayslipsAsync` |
| `HRMS.Infrastructure/Services/PayrollService.cs` | Company-scoped filtering in `GetAllPayslipsAsync` |
| `HRMS.API/Controllers/Payroll/PayrollController.cs` | Pass `CallerCompanyId` to `GetAll` |
| `HRMS.Application/Interfaces/ILeaveService.cs` | Added `int? callerCompanyId` to `GetBalanceAdjustmentsAsync` |
| `HRMS.Infrastructure/Services/LeaveService.cs` | Employee-company validation in `GetBalanceAdjustmentsAsync` |
| `HRMS.API/Controllers/Leave/LeaveController.cs` | GetAdjustments IDOR fix + CarryForward CompanyId JWT fix |
| `HRMS.API/Controllers/Authentication/AuthController.cs` | `[EnableRateLimiting("sensitive")]` on Refresh, ResetPassword, ChangePassword |
| `HRMS.API/Program.cs` | New "sensitive" rate-limit policy, sliding windows, Retry-After, nonce CSP middleware |

---

*Report generated 2026-07-18. All fixes preserve existing architecture, naming conventions, and API contracts.*
