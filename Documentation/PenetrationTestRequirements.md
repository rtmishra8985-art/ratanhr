# Penetration Test Requirements
**HRMS v2.0.0** | Addresses Specification Gap #5

---

## Why Static Analysis Alone Is Insufficient

The Phase 1 audit was a static code review only. Static analysis cannot detect:

| Risk Category | Examples | Why Static Analysis Misses It |
|--------------|---------|-------------------------------|
| Runtime SSRF | Webhook delivery, ClamAV callbacks, SMTP relay abuse | URL validation logic only visible at runtime with actual HTTP calls |
| Business-logic abuse | Leave manipulation, payroll override via API sequencing, double-spend on leave balance | Requires authenticated multi-step session replay |
| Auth token leakage | Browser caching of JWTs, referrer header leakage, CORS misconfiguration in prod | Requires a live browser + proxy in the deployed environment |
| Timing attacks | Token comparison in `PasswordResetService`, TOTP validation | Requires timing measurements against a running instance |
| Infrastructure misconfig | Open ports, exposed PostgreSQL, Redis without auth, Grafana default password | Requires external network scan |
| Second-order injection | Stored XSS that fires on admin dashboard, CSV injection in Excel export | Requires end-to-end flow from input to rendered output |

---

## Pen Test Sign-Off Policy

| Trigger | Required Before | Scope |
|---------|----------------|-------|
| Initial production launch | First tenant goes live | External black-box + authenticated grey-box |
| ≥ 10 tenants onboarded | Reaching 10th tenant | Full scope re-test |
| Major architectural change | Deploying new feature | Targeted re-test of changed surface |
| Annual | Calendar year | Full scope re-test |
| Post-incident | After any confirmed breach | Targeted re-test of exploited vectors |

> **Current status:** Pen test not yet performed. This is a declared pre-launch risk (see [ThreatModel.md](ThreatModel.md) — Residual Accepted Risks). The go-live gate in the audit's Go/No-Go is **not cleared** until pen test sign-off is obtained.

---

## Scope Definition

### In-Scope

| Surface | Notes |
|---------|-------|
| HRMS API — all endpoints | Including Swagger/OpenAPI discovery |
| Nginx reverse proxy layer | Headers, TLS config, rate-limit bypass |
| Authentication flows | Login, MFA, refresh token, password reset |
| Multi-tenant IDOR | Cross-company data access attempts |
| File upload handling | Path traversal, malicious file types, ZIP bombs |
| Webhook delivery | SSRF via attacker-controlled webhook URLs |
| Hangfire dashboard | Network restriction bypass, job injection |
| Grafana / Prometheus | Default credentials, metrics information disclosure |
| Background jobs | Timing, job queuing manipulation |

### Out of Scope (First Test)

| Surface | Reason |
|---------|--------|
| PostgreSQL direct access | Protected by Docker network; no external port |
| Redis direct access | Protected by Docker network |
| Third-party email provider (SMTP) | Out of HRMS team's control |
| Aadhaar UIDAI integration | Not implemented — out of scope |
| Client browser / end-user device | Not owned by the service operator |

---

## Test Methodology

### Phase 1 — Reconnaissance (1 day)

- Enumerate all API endpoints via Swagger (`/swagger/v1/swagger.json`)
- Identify all authentication mechanisms
- Map JWT claim structure
- Identify all file upload endpoints
- Map all external service calls (SMTP, ClamAV, webhook delivery)

### Phase 2 — Unauthenticated Testing (1 day)

| Test | Tool | Pass Criteria |
|------|------|--------------|
| TLS version / cipher suite | `testssl.sh` | TLS 1.2+ only; no BEAST/POODLE ciphers |
| Security headers | `OWASP ZAP` passive scan | All 7 headers present in every response |
| CORS misconfiguration | Manual `curl` with `Origin: evil.com` | No `Access-Control-Allow-Origin: *` on auth endpoints |
| Rate-limit bypass | Distributed IP rotation (k6) | Rate limit fires within declared window |
| Open port scan | `nmap -sV` | Only 80/443 exposed externally |

### Phase 3 — Authenticated Testing — Employee Role (1 day)

| Test | Pass Criteria |
|------|--------------|
| IDOR: Access another employee's payslip | 403 or 404 returned |
| IDOR: Access another employee's leave requests | 403 or 404 returned |
| IDOR: View another company's employees | 0 results or 403 |
| Privilege escalation: Employee → Admin API call | 403 Forbidden |
| JWT algorithm confusion (alg: none) | 401 Unauthorized |
| JWT company claim tamper (modify CompanyId in payload) | 401 Unauthorized (signature invalid) |

### Phase 4 — Authenticated Testing — Admin Role (1 day)

| Test | Pass Criteria |
|------|--------------|
| Cross-tenant data access via `?companyId=N` parameter | Returns only caller's tenant data |
| SSRF via webhook URL field | Internal URLs (10.x.x.x, 172.16.x.x) blocked |
| File upload — path traversal (`../../etc/passwd`) | 400 Bad Request or normalised path |
| File upload — executable file (`.php`, `.sh`, `.exe`) | 400 Bad Request |
| File upload — ZIP bomb | Request rejected before memory exhaustion |
| Stored XSS via employee name → admin dashboard | Escaped output; no script execution |
| Business logic: payroll double-run | 409 Conflict (Redis lock) |
| Business logic: negative leave balance via API | Validation error; balance not corrupted |
| CSV injection in Excel export | Formulae not evaluated; plain-text cells |
| Timing attack on password reset token comparison | Constant-time comparison (verify via timing measurements) |

### Phase 5 — Infrastructure (0.5 day)

| Test | Pass Criteria |
|------|--------------|
| Grafana default credentials (`admin:admin`, `admin:changeme`) | Login rejected |
| Prometheus `/metrics` accessible externally | 403 or blocked by nginx |
| Hangfire dashboard accessible without auth | 401 or 403 |
| Docker containers running as root | `docker inspect` shows non-root user |

### Phase 6 — Reporting (0.5 day)

Deliverables:
- Executive summary with CVSS scores for all findings
- Technical detail for each finding: steps to reproduce, evidence, remediation guidance
- Retesting confirmation for any critical/high findings remediated before report delivery
- **Go/No-Go sign-off statement** for production launch

---

## Acceptance Criteria for Go-Live Pen Test Sign-Off

| Finding Severity | Required State |
|-----------------|---------------|
| Critical | Zero open findings |
| High | Zero open findings, OR documented accepted risk with CISO sign-off |
| Medium | Remediation plan with target date ≤ 30 days post-launch |
| Low / Informational | Logged in backlog; no go-live gate |

---

## Recommended External Pen Test Providers

The pen test must be performed by a party external to the development team. Recommended qualifications:

- CREST-accredited or OSCP-certified testers
- Experience with ASP.NET Core / .NET multi-tenant SaaS applications
- Experience with OWASP ASVS Level 2 assessment

---

## Estimated Effort

| Scope | Effort | Estimated Cost Range |
|-------|--------|---------------------|
| Full scope (as above) | 5 person-days | USD 8 000 – 15 000 |
| Re-test of specific findings only | 1–2 person-days | USD 2 000 – 4 000 |

---

*Pen test requirements approved: 2026-07-24. Next scheduled test: before first production tenant goes live.*
