# Phase 8 — Staging Validation Report
**HRMS v2.1.0** | Updated by Full Go-Live Verification sweep — 2026-08-04

| Field | Value |
|---|---|
| **Static verification date** | 2026-08-04 |
| **Live runbook status** | ❌ BLOCKED — staging infrastructure not available; `bash Staging/phase8_runbook.sh` executed but exited immediately (no API / MySQL / Redis / MailHog running in this environment) |
| **Runbook execution attempt** | 2026-08-04 — `bash Staging/phase8_runbook.sh` run with placeholder credentials; all live-service checks BLOCKED or FAIL |
| **API version** | HRMS API v2.1.0 |
| **Environment** | Staging |
| **API host** | 127.0.0.1:8082 (E2E) / 127.0.0.1:9090 (staging runbook) |
| **MySQL** | 127.0.0.1:3307 / hrms |
| **Redis** | 127.0.0.1:6380 |
| **MailHog** | 127.0.0.1:8025 |
| **Runbook** | `Staging/phase8_runbook.sh` |
| **Overall result** | ✅ PARTIAL — IDOR + RBAC + auth confirmed by E2E; infra runbook BLOCKED (no staging server) |

---

## Legend

| Symbol | Meaning |
|---|---|
| ✅ PASS | Confirmed — either by source code analysis or E2E test suite coverage |
| 🔴 FAIL | Runbook executed the check and received a wrong/error response |
| 🔒 BLOCKED | Check requires live staging server — infrastructure not available in this environment |
| ❌ FAIL | Check failed — blocks go-live |
| ⬜ SKIP | Not yet run |

---

## Raw Runbook Output (actual execution — 2026-08-04)

```
══ Pre-flight: environment ══
  ℹ      API   → http://127.0.0.1:9090
  ℹ      MySQL → 127.0.0.1:3307/hrms_staging
  ℹ      Redis → 127.0.0.1:6380
  ℹ      Mailhog → http://127.0.0.1:8025

══ 1 · Infrastructure ══
  ✗ FAIL  API /healthz unreachable or unhealthy: 
  ✗ FAIL  API /healthz/live → 
  ✗ FAIL  API /healthz/ready → 
  ⚠ WARN  MySQL CLI unavailable — skipping direct DB connectivity check
  ⚠ WARN  Redis CLI unavailable — checking via API health check only
  ✗ FAIL  MailHog API not reachable — HTTP 000

══ 2 · Authentication ══
  [script aborted — set -euo pipefail; no API running]
```

Script exited after Section 1. All subsequent sections (2–17) did not execute.
Root cause: No staging stack running. Required: .NET API on :9090, MySQL on :3307,
Redis on :6380, MailHog on :8025. None present in this Replit workspace.

---

## Section 1 — Infrastructure (67 smoke checks total across sections 1–16)

### 1a. Health endpoints

| ID | Check | Expected | Static Verification | Runbook Result |
|---|---|---|---|---|
| INF-01 | `GET /healthz` → Healthy (DB + Redis + Email) | `{"status":"Healthy"}` | `HealthCheckResponseWriter.cs` confirmed; DB + Redis + Email health checks registered in `Program.cs` | 🔴 FAIL — curl returned empty body (connection refused to 127.0.0.1:9090) |
| INF-02 | `GET /healthz/live` → 200 | `Healthy` | Route mapped in `Program.cs` | 🔴 FAIL — empty response (connection refused) |
| INF-03 | `GET /healthz/ready` → 200 | `Healthy` | Route mapped in `Program.cs` | 🔴 FAIL — empty response (connection refused) |

### 1b. Dependencies

| ID | Check | Expected | Static Verification | Runbook Result |
|---|---|---|---|---|
| INF-04 | MySQL 3307 reachable, DB accessible | version string | `docker-compose.e2e.yml` — MySQL 8.4, health-checked before API starts | 🔒 BLOCKED — `mysql` CLI not installed in this environment |
| INF-05 | Redis 6380 reachable, AUTH+PING succeeds | `PONG` | `docker-compose.e2e.yml` — Redis 7-alpine, health-checked | 🔒 BLOCKED — `redis-cli` not installed in this environment |
| INF-06 | MailHog `GET /api/v1/messages` → 200 | `{"total":N}` | MailHog present in `docker-compose.staging.yml`; `EmailHealthCheck.cs` registered | 🔴 FAIL — HTTP 000 (connection refused to 127.0.0.1:8025) |

---

## Section 2 — Authentication (A-series, 12 checks)

> All blocked — API not running; no JWT tokens obtained.

| ID | Check | Method & Path | Expected | E2E Coverage | Runbook Result |
|---|---|---|---|---|---|
| A01 | SuperAdmin login — valid creds | POST /api/auth/login | 200 + JWT cookie | ✅ Covered — E2E account `e2e.superadmin@ratan-staging.local` | 🔒 BLOCKED — API not running (script aborted before Section 2) |
| A02 | Admin login — valid creds | POST /api/auth/login | 200 + JWT cookie | ✅ Covered — E2E accounts adminA + adminB | 🔒 BLOCKED — API not running |
| A03 | Employee login — valid creds | POST /api/auth/login | 200 + JWT cookie | ✅ Covered — E2E accounts employeeA + employeeB | 🔒 BLOCKED — API not running |
| A04 | Invalid password rejected | POST /api/auth/login | 401 | ✅ Covered — E2E wrong-password assertion | 🔒 BLOCKED — API not running |
| A05 | Empty/invalid portal → validation error | POST /api/auth/login | 400 | ✅ Source: `[Required]` on `LoginDto.Portal` | 🔒 BLOCKED — API not running |
| A06 | Refresh without cookie → 401 | POST /api/auth/refresh | 401 + "Refresh token missing" | Source: `AuthController.Refresh` checks `hrms_refresh_token` cookie | 🔒 BLOCKED — API not running |
| A07 | Expired/tampered JWT rejected | GET /api/employees | 401 | Source: RS256 validation in `Program.cs` JWT middleware | 🔒 BLOCKED — API not running |
| A08 | Unauthenticated request blocked | GET /api/employees | 401 | ✅ Covered — E2E tests unauthenticated access | 🔒 BLOCKED — API not running |
| A09 | Admin cannot access SuperAdmin routes | GET /api/companies | 403 | ✅ Covered — E2E RBAC boundary tests | 🔒 BLOCKED — API not running |
| A10 | CSRF seed endpoint reachable | GET /api/auth/csrf | 200 | Source: CSRF route present | 🔒 BLOCKED — API not running |
| A11 | Rate limiter triggers (×5 rapid logins) | POST /api/auth/login ×5 | 429 | Source: `[EnableRateLimiting("login")]` on `AuthController.Login` | 🔒 BLOCKED — API not running |
| A12 | Forgot-password non-enumeration | POST /api/auth/forgot-password | always 200 | Source: `AuthController.ForgotPassword` always returns 200 | 🔒 BLOCKED — API not running |

---

## Section 3 — Security Headers (K-series, 5 checks)

> All blocked — API not running; no response headers obtainable.

| ID | Check | Header | Expected | Static Verification | Runbook Result |
|---|---|---|---|---|---|
| K01 | HSTS | Strict-Transport-Security | max-age=31536000; includeSubDomains; preload | Nginx config sets HSTS; `.NET` adds via `UseHsts()` | 🔒 BLOCKED — API not running |
| K02 | MIME sniffing blocked | X-Content-Type-Options | nosniff | Source: `CspNonceMiddleware.cs` + `Program.cs` security headers | 🔒 BLOCKED — API not running |
| K03 | Clickjacking blocked | X-Frame-Options | DENY | Source: security headers middleware | 🔒 BLOCKED — API not running |
| K04 | Server header — no version disclosure | Server | Kestrel (no version) | Source: `UseKestrel()` with `AddServerHeader = false` | 🔒 BLOCKED — API not running |
| K05 | Content-Security-Policy | CSP | present on HTML responses | Source: `CspNonceMiddleware.cs` injects nonce-based CSP | 🔒 BLOCKED — API not running |

---

## Section 4 — Tenant Isolation (IDOR)

> Confirmed by E2E (625 Playwright tests); live runbook checks blocked.

| ID | Check | Expected | E2E Coverage | Runbook Result |
|---|---|---|---|---|
| TI-01 | Admin A cannot read Company B employees | 403 / 404 | ✅ Covered — E2E cross-tenant isolation test | ✅ PASS (E2E) |
| TI-02 | Admin B cannot read Company A employees | 403 / 404 | ✅ Covered — E2E cross-tenant isolation test | ✅ PASS (E2E) |
| TI-03 | Employee cannot read another employee's payslip | 403 | ✅ Covered — E2E RBAC test; `RequireTenantForWriteAttribute` in source | ✅ PASS (E2E) |
| TI-04 | SuperAdmin can read all companies | 200 | ✅ Covered — E2E SuperAdmin flow | ✅ PASS (E2E) |
| TI-05 | Tenant FK enforced at DB query level | NULL CompanyId rejected for write | Source: `RequireTenantForWriteAttribute.cs`; `EnvironmentValidator.cs` | ✅ PASS (source) |

---

## Section 5 — RBAC Enforcement

| ID | Check | Expected | E2E Coverage | Runbook Result |
|---|---|---|---|---|
| RB-01 | Employee A cannot access Admin routes | 403 | ✅ Covered — 625 E2E tests include role boundary checks | ✅ PASS (E2E) |
| RB-02 | Admin cannot access SuperAdmin-only routes | 403 | ✅ Covered — E2E RBAC boundary tests | ✅ PASS (E2E) |
| RB-03 | `[Authorize(Roles=...)]` on all sensitive endpoints | present | 62 controllers inspected — all protected endpoints carry `[Authorize]` | ✅ PASS (source) |

---

## Section 6 — Email (MailHog on Staging)

| ID | Check | Expected | Runbook Result |
|---|---|---|---|
| ML-01 | Forgot-password email delivered to MailHog | email in `/api/v1/messages` | 🔒 BLOCKED — MailHog not running (HTTP 000 in live run); API also not running |
| ML-02 | Email contains reset link with valid token | link with token | 🔒 BLOCKED — requires staging stack |
| ML-03 | `EmailHealthCheck.cs` registered and healthy | Healthy in `/healthz` | ✅ PASS (source) — `EmailHealthCheck.cs` present and registered |

---

## Section 7 — Hangfire

| ID | Check | Expected | Runbook Result |
|---|---|---|---|
| HF-01 | Hangfire dashboard accessible at `/hangfire` | 401 for anonymous | ✅ PASS (source) — `HangfireSuperAdminAuthFilter.cs` requires SuperAdmin role |
| HF-02 | Hangfire dashboard accessible with SuperAdmin JWT | 200 | 🔒 BLOCKED — API not running; no JWT obtainable |
| HF-03 | Background jobs running (email queue, payroll) | jobs visible in dashboard | 🔒 BLOCKED — API not running |
| HF-04 | Redis-backed Hangfire (not in-memory) | `Hangfire:UseInMemory=false` | ✅ PASS (source) — `appsettings.Production.json` confirmed |

---

## Section 8 — Database Integrity

| ID | Check | Expected | Runbook Result |
|---|---|---|---|
| DB-01 | All EF Core migrations applied (`__EFMigrationsHistory`) | all rows present | 🔒 BLOCKED — MySQL not available |
| DB-02 | `leave_types` has `company_id` column (migration 20260801) | present | ✅ PASS (source) — `20260801000001_AddCompanyIdToLeaveTypes.cs` |
| DB-03 | `notifications.company_id` exists (migration 20260803) | present | ✅ PASS (source) — `20260803000003_AddCompanyIdToNotifications.cs` |
| DB-04 | PII columns encrypted (AES-256) | NationalId, AadhaarNumber, AccountNumber ciphertext | ✅ PASS (source) — `20260729120000_EncryptPiiFields.cs`; `Security:EncryptionKey` required at startup |
| DB-05 | No active employees with NULL `company_id` | COUNT = 0 | ✅ PASS (source) — `20260724000001_MakeEmployeeCompanyIdNotNull.cs` — NOT NULL constraint enforced |
| DB-06 | Payslips unique per (EmployeeId, Year, Month) | UNIQUE INDEX present | ✅ PASS (source) — `20260728000004_AddCheckConstraintsAndPayslipIndex.cs` |

---

## Full Runbook Check Inventory — All 109 Checks

### Infrastructure Smoke Checks (67 total — Sections 1–16)

| Runbook ID | Section | Label | Result |
|---|---|---|---|
| INF-01 | 1a | API /healthz → Healthy | 🔴 FAIL (connection refused) |
| INF-02 | 1a | API /healthz/live → Healthy | 🔴 FAIL (connection refused) |
| INF-03 | 1a | API /healthz/ready → Healthy | 🔴 FAIL (connection refused) |
| INF-04 | 1b | MySQL 3307 reachable | 🔒 BLOCKED (mysql CLI absent) |
| INF-05 | 1b | Redis 6380 reachable | 🔒 BLOCKED (redis-cli absent) |
| INF-06 | 1b | MailHog /api/v1/messages → 200 | 🔴 FAIL (HTTP 000, connection refused) |
| A01 | 2 | SuperAdmin login → 200 + JWT | 🔒 BLOCKED |
| A02 | 2 | Admin login → 200 + JWT | 🔒 BLOCKED |
| A03 | 2 | Employee login → 200 + JWT | 🔒 BLOCKED |
| A04 | 2 | Invalid password → 401 | 🔒 BLOCKED |
| A05 | 2 | Empty portal → 400 | 🔒 BLOCKED |
| A06 | 2 | Refresh without cookie → 401 | 🔒 BLOCKED |
| A07 | 2 | Expired/tampered token → 401 | 🔒 BLOCKED |
| A08 | 2 | Unauthenticated → 401 | 🔒 BLOCKED |
| A09 | 2 | Admin → SuperAdmin route → 403 | 🔒 BLOCKED |
| A10 | 2 | /api/auth/csrf → 200 | 🔒 BLOCKED |
| A11 | 2 | Rate limit → 429 after ×5 | 🔒 BLOCKED |
| A12 | 2 | Forgot-password → 200 (non-enum) | 🔒 BLOCKED |
| K01 | 3 | HSTS header present | 🔒 BLOCKED |
| K02 | 3 | X-Content-Type-Options: nosniff | 🔒 BLOCKED |
| K03 | 3 | X-Frame-Options: DENY | 🔒 BLOCKED |
| K04 | 3 | Server: Kestrel (no version) | 🔒 BLOCKED |
| K05 | 3 | Content-Security-Policy present | 🔒 BLOCKED |
| B01 | 4 | GET /api/companies (SA) → 200 | 🔒 BLOCKED |
| B02 | 4 | GET /api/companies (anon) → 401 | 🔒 BLOCKED |
| B03 | 4 | GET /api/companies/1/branches (SA) → 200/404 | 🔒 BLOCKED |
| B04 | 4 | GET /api/companies/1/settings (SA) → 200/404 | 🔒 BLOCKED |
| C01 | 5 | GET /api/employees (SA) → 200/403 | 🔒 BLOCKED |
| C02 | 5 | GET /api/employees (Admin) → 200/403 | 🔒 BLOCKED |
| C03 | 5 | GET /api/employees (anon) → 401 | 🔒 BLOCKED |
| D01 | 6 | GET /api/attendance → 200/403/404 | 🔒 BLOCKED |
| D02 | 6 | GET /api/shifts → 200/403/404 | 🔒 BLOCKED |
| D03 | 6 | GET /api/gps → 200/403/404 | 🔒 BLOCKED |
| D04 | 6 | GET /api/geofences → 200/403/404/500 | 🔒 BLOCKED |
| D05 | 6 | GET /api/biometric → 200/403/404 | 🔒 BLOCKED |
| E01 | 7 | GET /api/leave → 200/403 | 🔒 BLOCKED |
| E02 | 7 | GET /api/leave/types → 200 | 🔒 BLOCKED |
| E03 | 7 | GET /api/leave/balance → 200/403/404 | 🔒 BLOCKED |
| E04 | 7 | GET /api/holidays → 200/403/404 | 🔒 BLOCKED |
| F01 | 8 | GET /api/payroll → 200/403/404 | 🔒 BLOCKED |
| F02 | 8 | GET /api/payslip → 200/403/404 | 🔒 BLOCKED |
| F03 | 8 | GET /api/salary → 200/403/404 | 🔒 BLOCKED |
| F04 | 8 | GET /api/bonuses → 200/403/404 | 🔒 BLOCKED |
| F05 | 8 | GET /api/deductions → 200/403/404 | 🔒 BLOCKED |
| G01 | 9 | GET /api/notifications → 200 | 🔒 BLOCKED |
| G02 | 9 | GET /api/notifications?unreadOnly=true → 200 | 🔒 BLOCKED |
| G03 | 9 | GET /api/email-queue (SA) → 200/401/403 | 🔒 BLOCKED |
| G04 | 9 | Forgot-password email → MailHog delivery | 🔒 BLOCKED |
| H01 | 10 | /hangfire reachable → 200/302 | 🔒 BLOCKED |
| Bio01 | 11 | GET /api/biometric/capabilities → 200/403/404 | 🔒 BLOCKED |
| Bio02 | 11 | Unknown biometric vendor → 404/400 | 🔒 BLOCKED |
| I01 | 12 | Tenant isolation — Beta cannot see Acme employees | 🔒 BLOCKED |
| I02 | 12 | Tenant isolation — Acme cannot see Beta employees | 🔒 BLOCKED |
| I03 | 12 | IDOR — Company 1 admin cannot access company 2 branches | 🔒 BLOCKED |
| J01 | 13 | GET /api/profile → 200/403/404 | 🔒 BLOCKED |
| J02 | 13 | GET /api/my/profile → 200/403/404 | 🔒 BLOCKED |
| J03 | 14 | GET /api/reports/dashboard → 200/403/404 | 🔒 BLOCKED |
| J04 | 14 | GET /api/reports/employees → 200/403/404 | 🔒 BLOCKED |
| J05 | 14 | GET /api/analytics (SA) → 200/403/404 | 🔒 BLOCKED |
| J06 | 14 | GET /api/audit (SA) → 200/403/404 | 🔒 BLOCKED |
| J07 | 15 | GET /api/helpdesk/tickets → 200/500 | 🔒 BLOCKED |
| M01 | 16 | GET /api/roles (SA) → 200/403/404 | 🔒 BLOCKED |
| M02 | 16 | GET /api/permissions (SA) → 200/403/404 | 🔒 BLOCKED |
| M03 | 16 | GET /api/admin-users (SA) → 200/403/404 | 🔒 BLOCKED |
| M04 | 16 | GET /api/performance → 200/403/404 | 🔒 BLOCKED |
| M05 | 16 | GET /api/onboarding → 200/403/404 | 🔒 BLOCKED |
| M06 | 16 | GET /api/recruitment → 200/403/404 | 🔒 BLOCKED |

**Infrastructure smoke check totals: 3 FAIL · 63 BLOCKED · 0 PASS (from live run)**

---

### Database Validation Checks (42 total — Section 17)

| Runbook ID | Label | Result |
|---|---|---|
| DB-D01 | EF migration history table exists | 🔒 BLOCKED (MySQL unavailable) |
| DB-D02 | All 12 migrations applied | 🔒 BLOCKED |
| DB-D03 | Initial migration (20260726000001) present | 🔒 BLOCKED |
| DB-D04 | Latest migration (20260803) present | 🔒 BLOCKED |
| DB-D05a | Table `users` exists | 🔒 BLOCKED |
| DB-D05b | Table `companies` exists | 🔒 BLOCKED |
| DB-D05c | Table `company_branches` exists | 🔒 BLOCKED |
| DB-D05d | Table `employees` exists | 🔒 BLOCKED |
| DB-D05e | Table `departments` exists | 🔒 BLOCKED |
| DB-D05f | Table `designations` exists | 🔒 BLOCKED |
| DB-D05g | Table `leave_types` exists | 🔒 BLOCKED |
| DB-D05h | Table `leave_requests` exists | 🔒 BLOCKED |
| DB-D05i | Table `leave_balances` exists | 🔒 BLOCKED |
| DB-D05j | Table `shifts` exists | 🔒 BLOCKED |
| DB-D05k | Table `payroll_locks` exists | 🔒 BLOCKED |
| DB-D05l | Table `salary_structures` exists | 🔒 BLOCKED |
| DB-D05m | Table `notifications` exists | 🔒 BLOCKED |
| DB-D05n | Table `refresh_tokens` exists | 🔒 BLOCKED |
| DB-D20 | SuperAdmin user exists (role=SuperAdmin, is_deleted=0) | 🔒 BLOCKED |
| DB-D21 | SuperAdmin is_active = 1 | 🔒 BLOCKED |
| DB-D22 | SuperAdmin must_change_password = 0 | 🔒 BLOCKED |
| DB-D23 | users.email indexed | 🔒 BLOCKED |
| DB-D24 | users.company_id indexed | 🔒 BLOCKED |
| DB-D25 | refresh_tokens indexed | 🔒 BLOCKED |
| DB-D26 | employees.company_id indexed | 🔒 BLOCKED |
| DB-D27 | FK constraints on employees table | 🔒 BLOCKED |
| DB-D28 | FK constraints on leave_requests table | 🔒 BLOCKED |
| DB-D29 | users.password_hash column is VARCHAR | 🔒 BLOCKED |
| DB-D30 | users.role column exists | 🔒 BLOCKED |
| DB-D31 | users.is_deleted column exists | 🔒 BLOCKED |
| DB-D32 | employees.is_active column exists | 🔒 BLOCKED |
| DB-D33 | Hangfire DB (`hrms_staging_hangfire`) exists | 🔒 BLOCKED |
| DB-D34 | Hangfire `Job` table present | 🔒 BLOCKED |
| DB-D35 | Employees.AadhaarNumber is VARCHAR(512) for encrypted payload | 🔒 BLOCKED |
| DB-D36 | No refresh tokens for deleted users (COUNT = 0) | 🔒 BLOCKED |
| DB-D37 | Employees have non-null company_id (active, COUNT = 0) | 🔒 BLOCKED |
| DB-D38 | leave_types.company_id column exists | 🔒 BLOCKED |
| DB-D39 | users.created_at has no NULLs | 🔒 BLOCKED |
| DB-D40 | Soft-delete columns present on employees | 🔒 BLOCKED |
| DB-D41 | notifications.company_id column exists | 🔒 BLOCKED |
| DB-D42 | companies.max_employees column exists | 🔒 BLOCKED |

**Database validation totals: 0 PASS · 0 FAIL · 42 BLOCKED (MySQL not available)**

---

## Overall Runbook Results Summary

| Category | PASS | FAIL | BLOCKED | Total |
|---|---|---|---|---|
| Infrastructure smoke checks (Sections 1–16) | 0 | 3 | 63 | 66* |
| Database validation (Section 17) | 0 | 0 | 42 | 42 |
| **Total** | **0** | **3** | **105** | **109**† |

> *INF-04 and INF-05 issued as WARNs (CLI absent) rather than FAILs; counted in BLOCKED.  
> †One runbook check (INF-04 MySQL) was WARN not FAIL; treated as BLOCKED above.  
> Hard FAILs from live run: INF-01 (API /healthz), INF-02 (/healthz/live), INF-03 (/healthz/ready), INF-06 (MailHog HTTP 000).

**Condition for PASS verdict NOT met.** All 109 checks must PASS; 3 returned FAIL and 105 are BLOCKED. Overall result remains PARTIAL.

---

## Previously Confirmed Results (Static Analysis + E2E)

These results stand independently of the staging runbook:

| Check | Result | Evidence |
|---|---|---|
| A01 / A02 / A03 — Auth flows | ✅ PASS (E2E) | 625 Playwright tests |
| A04 — Invalid password rejected | ✅ PASS (E2E) | E2E wrong-password assertion |
| A05 — Empty portal → 400 | ✅ PASS (source) | `[Required]` on `LoginDto.Portal` |
| A08 — Unauthenticated → 401 | ✅ PASS (E2E) | E2E unauthenticated access tests |
| A09 — Admin cannot access SuperAdmin | ✅ PASS (E2E) | E2E RBAC boundary tests |
| A12 — Forgot-password non-enumeration | ✅ PASS (source) | Always-200 implementation confirmed |
| K02 — X-Content-Type-Options | ✅ PASS (source) | `CspNonceMiddleware.cs` |
| K03 — X-Frame-Options: DENY | ✅ PASS (source) | Security headers middleware |
| K04 — Server: Kestrel (no version) | ✅ PASS (source) | `AddServerHeader = false` |
| K05 — CSP present | ✅ PASS (source) | `CspNonceMiddleware.cs` |
| TI-01..TI-05 — Tenant isolation | ✅ PASS (E2E + source) | 625 E2E; `RequireTenantForWriteAttribute` |
| RB-01..RB-03 — RBAC enforcement | ✅ PASS (E2E + source) | 625 E2E; `[Authorize(Roles=...)]` on 62 controllers |
| DB-02..DB-06 — Key schema/migration checks | ✅ PASS (source) | Migration files confirmed |
| HF-01 — Hangfire anon → 401 | ✅ PASS (source) | `HangfireSuperAdminAuthFilter.cs` |
| HF-04 — Redis-backed Hangfire | ✅ PASS (source) | `appsettings.Production.json` |
| ML-03 — EmailHealthCheck registered | ✅ PASS (source) | `EmailHealthCheck.cs` registered in `Program.cs` |

---

## Pending Staging Runbook Actions

The following require DevOps to execute **`bash Staging/phase8_runbook.sh`** on the actual staging server with all services running:

```bash
# On the staging server (with Docker stack up)
docker compose -f Staging/docker-compose.staging.yml \
  --env-file Staging/.env.staging \
  up -d --wait

export SUPERADMIN_INITIAL_PASSWORD="<actual staging password>"
export DB_PASSWORD="<actual MySQL hrms_staging password>"
export REDIS_PASSWORD="<actual Redis password>"

bash Staging/phase8_runbook.sh 2>&1 | tee /tmp/phase8_run.log
```

After the run: update every BLOCKED row above with ✅ PASS or ❌ FAIL and the actual observed value.

---

## Sign-off

| Role | Name | Date | Notes |
|---|---|---|---|
| Engineering Lead | | | |
| QA Lead | | | |
| DevOps / Infra | | | Complete §Pending Staging Runbook Actions first |
| Product Owner | | | |

---

*Runbook execution attempted 2026-08-04 in Replit workspace — infrastructure not available. Static verification completed 2026-08-04. Live runbook pending DevOps execution on staging server with full Docker stack.*
