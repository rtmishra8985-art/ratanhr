> ⚠️ **SUPERSEDED** — This report was generated during an earlier audit/fix pass and no longer reflects the current state of the codebase. The authoritative current-state documents are [`RELEASE_GATE_FINAL.md`](RELEASE_GATE_FINAL.md) and [`VERIFICATION_REPORT_FINAL_v2.md`](VERIFICATION_REPORT_FINAL_v2.md). Do not use this file to assess production readiness.

---

# Bug Fix Changelog v5 — Production Hardening Pass

**Date:** 2026-07-20  
**Pass:** v5 (Automated Audit Fix)

---

## Fixes Applied

### BF5-01 — AuthController: NullReferenceException in ChangePassword [HIGH]

**File:** `HRMS.API/Controllers/Authentication/AuthController.cs`

**Issue:** `ChangePassword` used `int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value)` with the
null-forgiving operator (`!`). While `[Authorize]` guarantees authentication, a malformed JWT with a
missing `NameIdentifier` claim would cause a `NullReferenceException` at runtime rather than a graceful
auth failure.

**Fix:** Replaced with `UserId` from `BaseController`, which handles the null case by returning `0` and
is consistent with all other controllers in the project.

---

### BF5-02 — AutoMapper: DateTime Constructor Exception in Payslip Mapping [HIGH]

**File:** `HRMS.Application/Mapping/HrmsAutoMapperProfile.cs`

**Issue:** `new DateTime(s.Year, s.Month, 1)` throws `ArgumentOutOfRangeException` if the stored
`Payslip.Month` or `Payslip.Year` values fall outside the valid DateTime range (month: 1–12,
year: 1–9999). A corrupted database row would crash the entire payslip listing endpoint.

**Fix:** Extracted `SafeMonthYear()` helper that validates the range and catches the exception,
returning `"Period {year}/{month} (invalid)"` instead of propagating a 500 error.

---

### BF5-03 — SwaggerBasicAuthMiddleware: Unlogged FormatException [MEDIUM]

**File:** `HRMS.API/Middleware/SwaggerBasicAuthMiddleware.cs`

**Issue:** Malformed Base64 in the Authorization header was caught by a bare `catch` block that
returned HTTP 400 silently. No logging made it impossible to detect brute-force or attack patterns.

**Fix:** 
- Injected `ILogger<SwaggerBasicAuthMiddleware>` via constructor.
- Narrowed catch to `FormatException` (malformed base64 only).
- Added `LogWarning` with the remote IP for security monitoring.
- Added explicit `WriteAsync("Malformed Authorization header.")` body for API clients.

---

### BF5-04 — RedisDistributedRateLimiter: No Fail-Safe on Redis Outage [HIGH]

**File:** `HRMS.Infrastructure/Redis/RedisDistributedRateLimiter.cs`

**Issue:** Any Redis connection failure threw an unhandled `RedisException`, which propagated up the
rate-limiting middleware and returned HTTP 500 to all clients — a Redis outage would take down the
entire API.

**Fix:**
- Wrapped Redis pipeline in `try/catch (RedisException)` and `catch (Exception)`.
- On failure, the limiter **fails open** (allows the request through) and logs a `Warning`.
- Added `ILogger?` parameter to the constructor for observability.
- Added comment explaining that nginx-level rate limiting remains active as a backup.

---

### BF5-05 — CompanyService: Hardcoded "India" Country Default [MEDIUM]

**File:** `HRMS.Infrastructure/Services/CompanyService.cs`

**Issue:** Both `CreateAsync` and `UpdateAsync` defaulted `Country` to `"India"` when the DTO field
was null. For an international HRMS system this produces incorrect data for non-Indian companies and
silently overrides an intentional null/empty value.

**Fix:** Changed the default to `string.Empty` so the database stores what was actually provided.
If a default country is required for a specific deployment, configure it via `appsettings.json`
rather than hardcoding.

---

### BF5-06 — TimesheetPage: Implicit `any` in apiFetch Return Type [LOW]

**File:** `HRMS.SPA.Source/src/pages/timesheet/TimesheetPage.tsx`

**Issue:** `res.json().catch(() => ({}))` returned an untyped empty object. TypeScript inferred the
entire `apiFetch` return as `any`, which silently disabled type-checking for all callers.

**Fix:** Added explicit `Record<string, unknown>` type annotation on the `json` variable and
return expression, restoring full TypeScript type safety.

---

### BF5-07 — profileHelpers: ProfileLike/UserProfile Interface Mismatch [MEDIUM]

**File:** `HRMS.SPA.Source/src/utils/profileHelpers.ts`

**Issue:** `UserProfile` in `domain.ts` uses `companyName` and `branchName` fields, but `ProfileLike`
only declared `company` and `branch`. `getCompany()` and `getBranch()` helpers therefore returned
`"Unknown Company"` / `"Unknown Branch"` when passed a real `UserProfile` object, causing blank
company/branch display in the Navbar and profile cards.

**Fix:**
- Added `companyName?` and `branchName?` to `ProfileLike` (alongside legacy `company`/`branch`).
- Updated `getCompany()` to check `companyName` first, then fall back to `company`.
- Updated `getBranch()` to check `branchName` first, then fall back to `branch`.

---

### BF5-08 — JwtService Tests: Wrong Issuer String [LOW]

**Files:** `HRMS.Tests/JwtServiceTests.cs`, `HRMS.Tests/JwtTokenClaimsTests.cs`

**Issue:** Test JWT configs used `Jwt:Issuer = "HRMS.Tests"` instead of `"HRMS.API"`. The `JwtService`
validates the issuer on the validation path, so test-generated tokens used a different issuer than
production. While tests were self-consistent (same config for generate + validate), this divergence
made cross-environment token-rejection tests imprecise.

**Fix:** Updated both test config builders to use `"HRMS.API"` as the issuer, matching production.
Test isolation is maintained by the different signing key — tokens signed with the test key are
rejected by any other key regardless of issuer.

---

## Summary

| # | File | Severity | Type |
|---|------|----------|------|
| BF5-01 | AuthController.cs | High | NullReferenceException |
| BF5-02 | HrmsAutoMapperProfile.cs | High | Runtime exception |
| BF5-03 | SwaggerBasicAuthMiddleware.cs | Medium | Missing logging |
| BF5-04 | RedisDistributedRateLimiter.cs | High | No fail-safe / outage risk |
| BF5-05 | CompanyService.cs | Medium | Hardcoded default |
| BF5-06 | TimesheetPage.tsx | Low | Type safety |
| BF5-07 | profileHelpers.ts | Medium | Interface mismatch / broken UI |
| BF5-08 | JwtServiceTests.cs | Low | Test config divergence |

**All 8 findings fixed. No production blockers remain from this pass.**
