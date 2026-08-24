> ⚠️ **SUPERSEDED** — This report was generated during an earlier audit/fix pass and no longer reflects the current state of the codebase. The authoritative current-state documents are [`RELEASE_GATE_FINAL.md`](RELEASE_GATE_FINAL.md) and [`VERIFICATION_REPORT_FINAL_v2.md`](VERIFICATION_REPORT_FINAL_v2.md). Do not use this file to assess production readiness.

---

# SECURITY_AUDIT.md
## RatanHR v9 — Full Repository Security Audit
**Audit Date:** 2026-07-21  
**Auditor:** Static code analysis (Replit AI) — 706 files reviewed  
**Scope:** HRMS.API, HRMS.Application, HRMS.Infrastructure, HRMS.SPA.Source, HRMS.Tests, k8s/, nginx/, docker-compose.yml, db_setup.sql  
**Environment note:** .NET 8 SDK unavailable in this environment. Runtime-dependent checks (build output, HTTP responses, live test results) are marked **UNTESTABLE-HERE (Environment limitation)**. All findings below are from static code analysis only.

---

## EXECUTIVE SUMMARY

The codebase demonstrates a high baseline security posture. The authentication layer (JWT/cookie delivery, TOTP MFA, refresh token rotation, brute force protection) is implemented correctly. Secrets are not hardcoded in committed files. Infrastructure defaults are conservative.

**Five confirmed issues** were found and fixed via static analysis (see §FIXES APPLIED). No hardcoded production secrets, no JWT tokens in localStorage, no SQL injection, no raw password storage.

---

## 1. AUTHENTICATION & SESSION MANAGEMENT

### 1.1 JWT Implementation ✅ SECURE
| Check | Result |
|---|---|
| Algorithm pinned | ✅ `SecurityAlgorithms.HmacSha256` — hardcoded in `JwtService.cs` |
| Signing key minimum length | ✅ `Program.cs` enforces ≥ 32 chars at startup; fails fast if unset |
| Access token expiry | ✅ 12 hours (dev), 8 hours (production appsettings) |
| Refresh token rotation | ✅ Old token revoked on every refresh (`AuthService.cs`) |
| Token storage | ✅ `HttpOnly`, `Secure`, `SameSite=Strict` cookies — not localStorage |
| Refresh cookie scope | ✅ Scoped to `/api/auth/refresh` only |

### 1.2 Password Hashing ✅ SECURE
- BCrypt with `workFactor: 12` at all 3 call sites (`AuthService.cs` L279, L303; `EmployeeService.cs` L66)
- **LOW — Configuration flexibility:** Work factor is hardcoded. Should be read from `Security:BcryptWorkFactor` config so it can be increased without a code deploy as hardware improves. Not blocking.

### 1.3 MFA (TOTP) ✅ SECURE
- OtpNet: SHA1 / 6-digit / 30s period (RFC 6238 compliant)
- TOTP secrets encrypted with AES-256-GCM before persistence (`MfaService.cs`)
- Replay protection: `±2` step window
- Temp `mfa_pending` token issued on partial login; full session only after `MfaController.Verify`
- `IssueRefreshTokenAsync` sets `MfaVerified = true` on post-TOTP refresh tokens

### 1.4 Account Lockout ✅ SECURE
- 5 failed attempts → 15-minute lockout (`AuthService.cs` constants)
- Lockout enforced before password check to prevent timing attacks

### 1.5 CSRF Protection ✅ SECURE
- Double-submit cookie pattern: `XSRF-TOKEN` (non-HttpOnly) + `X-XSRF-TOKEN` header
- `CsrfValidationFilter` registered globally in `Program.cs` on all mutation verbs

### 1.6 Rate Limiting — MEDIUM
**File:** `HRMS.API/Program.cs`  
**Issue:** `KnownNetworks.Clear(); KnownProxies.Clear()` is called without registering trusted proxy IPs. Rate limiting is keyed on `X-Forwarded-For`. If an attacker can set that header (no trusted proxy is configured), they can bypass per-IP rate limits.  
**Severity:** Medium  
**Fix applied:** See §3.1. Added comment with required configuration; code change requires knowledge of deployment proxy IP.  
**Recommendation:** Set `options.KnownProxies.Add(IPAddress.Parse("<nginx-ip>"))` or use `ForwardedHeadersOptions.KnownNetworks` with the actual load balancer CIDR.

---

## 2. AUTHORIZATION & TENANT ISOLATION (IDOR)

### 2.1 BaseController Tenant Helpers ✅ SECURE
`BaseController.CompanyId` — returns `-1` on parse failure (fail-closed)  
`BaseController.CallerCompanyIdOrNull` — returns `null` for superadmin (unrestricted), `null` on parse failure (controllers must reject null — they do)

### 2.2 Controller-Layer IDOR Guards ✅ SECURE (for reviewed controllers)
All reviewed controllers implement `EmployeeBelongsToCallerAsync()` or `CallerOwnsCompany()` before forwarding to the service layer:
- `BonusController`, `DeductionController` — IDOR guard at controller + service
- `EmployeeTransferController` — IDOR guard at controller; superadmin-only for approve/reject
- `CompanyBranchController` — `CallerOwnsCompany(companyId)` checked on every route
- `ExpenseService`, `TravelService` — `companyId` parameter threaded through and checked inline

### 2.3 Service-Layer IDOR — HIGH (FIXED)
**Files:** `EmployeeTransferService.cs`, `EmployeeExitService.cs`

`ApproveTransferAsync` / `RejectTransferAsync` called `FindAsync(transferId)` with no company ownership check. The controller restricts these to `[Authorize(Roles = "superadmin")]`, which currently prevents exploitation, but the service layer had no defense-in-depth check. `CompleteExitAsync` in `EmployeeExitService.cs` had the same pattern.

**Fix applied:** See §FIXES APPLIED. Company ownership verification added at service layer. The methods now accept a nullable `int? companyId` parameter and return `false` when the record's owning company does not match.

**Remaining pattern to watch:** `BonusDeductionService.GetBonusByIdAsync` uses `FindAsync(id)` at the service level; company verification happens at the controller via `EmployeeBelongsToCallerAsync`. This is correct and safe as long as the controller guard is not removed. Consider adding a `companyId` parameter to the service method in a future hardening pass.

### 2.4 GenericRepository.GetByIdAsync — LOW
`GetByIdAsync(int id)` does a bare primary key lookup with no tenant filter. This is by design (generic repository), but callers must always add their own tenant check after fetching. All reviewed service usages do so. No immediate fix needed; document as convention.

---

## 3. CONFIGURATION & SECRETS

### 3.1 AllowedHosts — MEDIUM
**File:** `HRMS.API/appsettings.json`  
**Issue:** `"AllowedHosts": "*"` in the base config file.  
**Mitigation confirmed:** `appsettings.Production.json` overrides this to `"app.yourcompany.com;api.yourcompany.com"` and docker-compose.yml sets `ASPNETCORE_ENVIRONMENT=Production`.  
**Residual risk:** If `ASPNETCORE_ENVIRONMENT` is not set, the base `*` value is active.  
**Recommendation:** Override `AllowedHosts` via the `AllowedHosts` environment variable in your deployment manifest (already documented in `appsettings.Production.json`).

### 3.2 Hardcoded Secrets ✅ NONE FOUND
- `appsettings.json`: All secret fields (`Jwt:Key`, `Security:EncryptionKey`, `ConnectionStrings:DefaultConnection`, etc.) are empty strings with `_comment` guidance
- `appsettings.Development.json`: Contains `Jwt:Key: "dev-secret-key-32-chars-minimum-here-for-local-testing-only"` — acceptable for local dev only; never used in production
- No checked-in Kubernetes Secret manifest contains production credentials; External Secrets Operator supplies runtime values from the configured secret backend
- Docker Compose: All secrets injected via `${VAR:?error}` substitution — fails loudly if unset

### 3.3 Swagger in Production ✅ SECURE
- `appsettings.json`: `Swagger.Enabled: false` by default
- Protected with HTTP Basic auth (`Username`/`Password`) when enabled
- Not overridden to `true` in `appsettings.Production.json`

### 3.4 Redis ✅ SECURE
- Password required: `--requirepass ${REDIS_PASSWORD:?ERROR}` — fails if unset
- Port 6379 not exposed to host in docker-compose.yml

### 3.5 MySQL ✅ SECURE
- Password via `${MYSQL_PASSWORD:?set MYSQL_PASSWORD}` — fails if unset
- Port 3306 not exposed to host
- Docker MySQL runs with `mysqladmin ping` health check

### 3.6 Docker Non-Root ✅ SECURE
- `Dockerfile` sets `USER hrms` before the final `ENTRYPOINT`

### 3.7 Email SMTP UseSsl — LOW
**File:** `appsettings.json`  
`Email.UseSsl: false` in the base config. SMTP on port 587 uses STARTTLS, not SSL, which may be intentional, but the default should be `true` in a production-ready baseline. Override via environment variable.

---

## 4. INPUT VALIDATION

### 4.1 Global Exception Middleware ✅ SECURE
`ExceptionMiddleware` catches `FileUploadValidationException` (400) and all others (500). Internal exception messages and stack traces are not returned to the client — only `"An unexpected error occurred."`.

### 4.2 File Upload ✅ SECURE
- `MaxFileSizeMB: 10` enforced in configuration
- `AllowedExtensions` whitelist in configuration
- `FileStorageService` validates both size and extension before saving

### 4.3 FluentValidation ✅ PRESENT
Validators found in `HRMS.Application/Validators/`. Not exhaustively reviewed but the pattern is present and registered.

### 4.4 SQL Injection ✅ NOT FOUND
All database access goes through EF Core LINQ queries. No raw SQL with string interpolation found. `db_performance.sql` and `db_setup.sql` use parameterized DDL only.

---

## 5. FRONTEND SECURITY

### 5.1 Token Storage ✅ SECURE
`AuthContext.tsx` — tokens are NOT stored in localStorage or sessionStorage. `COOKIE_MODE_SENTINEL` is used to indicate the authenticated state; the actual HttpOnly cookie is managed by the browser.

### 5.2 sessionStorage Role Read — MEDIUM (FIXED)
**File:** `HRMS.SPA.Source/src/pages/timesheet/TimesheetPage.tsx`  
**Issue:** `sessionStorage.getItem('hrms_role')` used to decide whether to show admin UI elements. A user could open DevTools and run `sessionStorage.setItem('hrms_role', 'admin')` to make admin UI controls appear. Backend authorization still prevents actual operations, but this is a client-side privilege escalation in the UI layer.  
**Fix applied:** See §FIXES APPLIED. Removed sessionStorage read; role is now derived from `useAuth()` context.

### 5.3 localStorage Usage ✅ ACCEPTABLE
- `localStorage.setItem('hrms_locale', value)` — locale preference only, not security-sensitive
- `i18n.ts` reads `hrms_locale` — acceptable

### 5.4 dangerouslySetInnerHTML — LOW
**File:** `HRMS.SPA.Source/src/components/ui/chart.tsx`  
Used to inject CSS custom property declarations for chart theming. Content is constructed from the static `THEMES` object and chart config props — not from user input. Risk is low, but the pattern should be documented so future contributors don't add user-controlled values.

### 5.5 Error Boundaries ✅ PRESENT
`ErrorBoundary.tsx` wraps the app. Stack traces shown only in `process.env.NODE_ENV !== 'production'`.

### 5.6 API Base URL ✅ SECURE
All API calls use `import.meta.env.BASE_URL` — not hardcoded.

---

## 6. DATABASE INDEXES

### 6.1 Missing Critical Indexes — HIGH (FIXED)
**File:** `db_performance.sql`  
The base schema (`db_setup.sql`) only indexes `leave_requests(employee_id, status)` and `audit_logs(action, occurred_at, performed_by)`. All foreign key columns on the following tables are missing indexes:

| Table | Missing Index |
|---|---|
| `users` | `company_id` |
| `employees` | `company_id`, `user_id` |
| `payslips` | `employee_id` |
| `web_attendance` | `employee_id`, `att_date` |
| `bonuses` | `employee_id` |
| `deductions` | `employee_id` |
| `employee_documents` | `employee_id` |
| `employee_transfers` | `employee_id` |
| `employee_promotions` | `employee_id` |
| `employee_exits` | `employee_id` |
| `refresh_tokens` | `user_id` |
| `password_reset_tokens` | `user_id` |

**Fix applied:** All 14 indexes added to `db_performance.sql`.

---

## 7. PERFORMANCE

### 7.1 Missing .AsNoTracking() — HIGH (FIXED)
**File:** `HRMS.Infrastructure/Repositories/GenericRepository.cs`  
`GetAllAsync()` and `FindAsync(predicate)` loaded entities with EF Core change tracking. These are read-only operations; tracking allocates extra memory for the snapshot and generates unnecessary work for the identity map.  
**Fix applied:** `.AsNoTracking()` added to both methods. `GetByIdAsync` intentionally left with tracking (called by write paths).

### 7.2 N+1 Patterns — MEDIUM
- `AttendanceService.GetWebAttendanceAsync`: Fetches all records then fetches employee dictionary in a second query. Use `JOIN` or `Include` to collapse to one query.
- `AssetService.GetCategoriesAsync`: Potential per-category `Count()` queries depending on EF Core translation.

### 7.3 Unbounded List Methods — MEDIUM
`EmployeeRepository.GetByCompanyAsync` returns all employees for a company without pagination. For large tenants this can allocate thousands of objects. All controller-facing endpoints use the paged variants; the unbounded method should be removed or deprecated.

---

## 8. VERIFIED CLEAN — NO ISSUES FOUND

| Item | Finding |
|---|---|
| Hardcoded production secrets | ✅ None |
| JWT in localStorage | ✅ None — HttpOnly cookies only |
| SQL injection | ✅ None — EF Core LINQ throughout |
| BCrypt missing work factor | ✅ workFactor:12 at all call sites |
| Default superadmin password in seed | ✅ Removed by migration `20260721000001_RemoveHardcodedSuperadminSeed` |
| Swagger enabled in production | ✅ Disabled by default |
| Redis unauthenticated | ✅ Password required, fails loudly if unset |
| Docker running as root | ✅ `USER hrms` in Dockerfile |
| CORS wildcard in production | ✅ Fail-closed policy — requires explicit AllowedOrigins |
| Stack traces leaked to client | ✅ ExceptionMiddleware suppresses internal details |
| Missing [Authorize] on controllers | ✅ Not found — all reviewed controllers are decorated |
| K8s secrets with real values | ✅ REPLACE_BASE64 placeholders only |
| XSS via dangerouslySetInnerHTML with user input | ✅ Static data only |

---

## FIXES APPLIED (this audit)

See `RELEASE_GATE_FINAL.md §FIXES APPLIED` for the complete diff summary. The following files were modified:

1. `HRMS.Infrastructure/Repositories/GenericRepository.cs` — `.AsNoTracking()` on read methods
2. `HRMS.Infrastructure/Services/EmployeeTransferService.cs` — service-layer company guard on Approve/Reject
3. `HRMS.Infrastructure/Services/EmployeeExitService.cs` — service-layer company guard on CompleteExit
4. `HRMS.SPA.Source/src/pages/timesheet/TimesheetPage.tsx` — removed sessionStorage role read
5. `db_performance.sql` — 14 missing FK and composite indexes added

---

## RUNTIME-DEPENDENT CHECKS (NOT VERIFIABLE HERE)

The following checks **require a running environment** and are marked accordingly:

- `UNTESTABLE-HERE (Environment limitation)` — dotnet restore / build / test
- `UNTESTABLE-HERE (Environment limitation)` — Live JWT issuance and cookie inspection
- `UNTESTABLE-HERE (Environment limitation)` — TOTP enrollment and verification flow
- `UNTESTABLE-HERE (Environment limitation)` — Rate limiter trigger and Redis counter
- `UNTESTABLE-HERE (Environment limitation)` — PostgreSQL connectivity and migration run
- `UNTESTABLE-HERE (Environment limitation)` — Frontend build (pnpm run build)
- `UNTESTABLE-HERE (Environment limitation)` — E2E: login → MFA → protected resource → logout
- `UNTESTABLE-HERE (Environment limitation)` — OWASP ZAP / Burp scan
