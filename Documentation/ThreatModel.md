# Threat Model
**HRMS v2.0.0** | Addresses Specification Gap #3

---

## Overview

This threat model uses the STRIDE framework applied to the HRMS multi-tenant SaaS architecture. It defines the attacker personas, their capabilities, and the corresponding mitigations. All severity ratings in the audit must be read against this model.

---

## System Boundaries

```
[Browser / Mobile Client]
        |
       TLS
        |
  [Nginx Reverse Proxy]  ←── DDoS / rate-limit boundary
        |
  [ASP.NET Core 8 API]  ←── Authentication / authorisation boundary
     /         \
[PostgreSQL]  [Redis]   ←── Data isolation boundary
        |
  [Hangfire Workers]    ←── Background job boundary
        |
  [External Services]   ←── SMTP · ClamAV · Webhook receivers
```

---

## Attacker Personas

| ID | Persona | Capability | Goal |
|----|---------|-----------|------|
| **P1** | Competing Tenant Admin | Authenticated user with valid JWT for Tenant A | Access or corrupt Tenant B's data (IDOR) |
| **P2** | Rogue Internal Admin | HR Admin within the same tenant | Escalate privileges; exfiltrate PII; cover tracks |
| **P3** | External Attacker (Unauthenticated) | No credentials; internet access only | Gain initial foothold via injection, auth bypass, or brute-force |
| **P4** | Compromised Hangfire Worker | Code execution within the worker process | Abuse background-job context to access all-tenant data without a company filter |
| **P5** | Malicious Employee (Self) | Valid JWT with Employee role | View other employees' payslips, leave, or PII via IDOR |
| **P6** | Supply-Chain Attacker | Control over a NuGet package or CI runner | Inject malicious code into the build pipeline |

---

## STRIDE Threat Register

### Spoofing

| ID | Threat | Attacker | Likelihood | Impact | Mitigation |
|----|--------|---------|-----------|--------|------------|
| S-01 | JWT forged using weak algorithm | P3 | Low | Critical | RS256 signing; key stored in environment secret; `JwtService` validates algorithm |
| S-02 | Session fixation via stolen refresh token | P3/P5 | Medium | High | Refresh tokens are single-use (rotated on use); stored hashed in DB; 30-day TTL |
| S-03 | Password reset token replay | P3 | Low | High | Tokens are single-use + 1-hour TTL; `UsedAt` column prevents replay |
| S-04 | MFA bypass (TOTP code reuse) | P3 | Low | High | TOTP codes are single-use; `TotpUsedCode` checked in `MfaController` |

### Tampering

| ID | Threat | Attacker | Likelihood | Impact | Mitigation |
|----|--------|---------|-----------|--------|------------|
| T-01 | Tenant injection — shift creation with foreign CompanyId | P1 | **High** | Critical | `ShiftController` overrides `dto.CompanyId` from JWT claims (ISSUE-002 fix) |
| T-02 | Payslip tampering via direct DB reference | P1/P5 | Medium | High | `payslips.company_id` scoped by global query filter; compound index on `(company_id, employee_id, period)` |
| T-03 | File path traversal during upload/delete | P1 | Medium | High | `Path.GetFullPath` + `StartsWith(uploadsRoot)` in `FileStorageService` (MED-10 verified) |
| T-04 | Audit log deletion | P2 | Low | Critical | Audit logs are append-only; no `DELETE` endpoint on `AuditLogs`; SuperAdmin cannot delete logs via API |
| T-05 | Excel upload with formula injection | P2/P5 | Medium | Medium | ClosedXML strips formulae on cell read; no formula evaluation at server |

### Repudiation

| ID | Threat | Attacker | Likelihood | Impact | Mitigation |
|----|--------|---------|-----------|--------|------------|
| R-01 | Admin denies changing employee salary | P2 | Medium | High | `AuditLogs` records before/after values, user ID, IP, and timestamp for every mutation |
| R-02 | Payroll run disputed | P2 | Low | High | Hangfire job history records job ID, caller, and timestamp; `PayrollLock` table records lock acquisition |

### Information Disclosure

| ID | Threat | Attacker | Likelihood | Impact | Mitigation |
|----|--------|---------|-----------|--------|------------|
| I-01 | Cross-tenant employee data via IDOR on Leave endpoint | P1 | **High** | Critical | Post-fetch company check in `LeaveController` (HIGH-2 — partially mitigated; push to DB query is Sprint 1 debt) |
| I-02 | PII (Aadhaar/PAN) returned in standard employee detail | P1/P5 | **High** | Critical | `EmployeePiiDto` separates PII; `EmployeeDetailDto` ignores PII fields (MED-9) |
| I-03 | Temporary password in Serilog structured log | P4 | High | High | **Active blocker** — `Log.Warning(tempPassword)` must be replaced before go-live (MED-2) |
| I-04 | Grafana default password exposes monitoring system | P3 | Medium | High | `:-changeme` fallback must be removed; `GRAFANA_ADMIN_PASSWORD` must be required (MED-16) |
| I-05 | JWT claims readable from browser (no httpOnly) | P3 | Low | Medium | JWTs returned in JSON body, not cookies; stored in `localStorage` by SPA — documented risk accepted |
| I-06 | PII in application logs via Serilog destructuring | P4 | Medium | High | `EmployeePiiDto` fields tagged `[Sensitive]`; Serilog destructuring policy masks them |

### Denial of Service

| ID | Threat | Attacker | Likelihood | Impact | Mitigation |
|----|--------|---------|-----------|--------|------------|
| D-01 | Brute-force login | P3 | High | High | Rate limit: 10 req/min/IP on `/api/auth/login` (Redis-backed) |
| D-02 | Concurrent payroll generation race | P2 | Medium | High | Redis distributed lock in `PayrollService.GenerateAsync`; 409 Conflict returned (HIGH-3 verified) |
| D-03 | Unbounded report export (OOM) | P2 | Medium | Medium | OpenXML streaming; paginated `GetPagedAsync` prevents full-table loads |
| D-04 | Large Excel attendance upload (OOM) | P2 | Medium | Medium | 10 MB file size limit enforced; ClosedXML in-memory (Sprint 1 debt: replace with streaming reader) |
| D-05 | Hangfire job flood | P4 | Low | High | Hangfire concurrency workers capped; job deduplication on payroll lock |

### Elevation of Privilege

| ID | Threat | Attacker | Likelihood | Impact | Mitigation |
|----|--------|---------|-----------|--------|------------|
| E-01 | Admin creates SuperAdmin account | P2 | Low | Critical | Role assignment gated to SuperAdmin only; `[Authorize(Roles="SuperAdmin")]` on user-role endpoints |
| E-02 | Company ID claim tampered in JWT | P1 | Low | Critical | RS256-signed JWT; claim cannot be tampered without the private key |
| E-03 | SSRF via webhook delivery URL | P2 | Medium | High | **Not mitigated in current codebase** — webhook URLs are not validated against an allowlist. Sprint 1 item. |
| E-04 | Insecure Direct Object Reference on Sales data (SuperAdmin sentinel -1) | P1 | High | High | `CallerCompanyIdOrNull` used in `SalesController`; returns `null` for SuperAdmin (ISSUE-001 fixed) |

---

## Severity Re-Rating Against This Model

The original audit ranked HIGH-2 (Leave IDOR) lower than HIGH-10 (k6 load tests). **Corrected rating under this threat model:**

| Finding | Original Rating | Corrected Rating | Reason |
|---------|----------------|-----------------|--------|
| HIGH-2 Leave IDOR | HIGH | **CRITICAL** | Tenants share the same deployment; cross-tenant leave data is a regulatory breach (DPDP/GDPR) |
| HIGH-10 k6 Load Tests | HIGH | HIGH (unchanged) | Performance gap, not a data-safety gap |
| MED-2 Temp password log | MEDIUM | **CRITICAL** | Credentials in log stores are accessible to any log-reader (P4); active blocker |
| MED-9 PII in EmployeeDetailDto | MEDIUM | **HIGH** | Regulatory (DPDP/GDPR) violation on any authenticated API call |

---

## Threat-Adjusted Go-Live Score

The audit's Go-Live score of 61/100 was computed without a threat model. Against this model, the threat-adjusted score is:

| Dimension | Raw Score | Threat-Adjusted Score | Adjustments |
|-----------|-----------|-----------------------|-------------|
| Tenant Isolation | 70 | 58 | Leave IDOR (I-01) re-rated CRITICAL; still partially mitigated |
| PII Protection | 65 | 52 | PII in EmployeeDetailDto (I-02) re-rated HIGH |
| Credential Safety | 55 | 40 | Temp password logging (I-03) is an active CRITICAL blocker |
| Denial of Service | 80 | 80 | No change — mitigations adequate |
| Privilege Escalation | 85 | 82 | SSRF (E-03) unmitigated; Sprint 1 |
| **Overall** | **61** | **52** | **Go-Live: NO until blockers resolved** |

---

## Mitigations Required Before Go-Live (Threat-Model Driven)

| Threat ID | Description | Blocker? |
|-----------|-------------|---------|
| I-03 | Remove temp password from `Log.Warning` | ✅ Yes |
| I-02 | Add `EmployeePiiDto`; ignore PII in `EmployeeDetailDto` | ✅ Yes |
| I-01 | Push Leave IDOR check into DB query | ✅ Yes (re-rated CRITICAL) |
| I-04 | Remove Grafana `:-changeme` password fallback | ✅ Yes |
| E-03 | Validate webhook URLs against allowlist | Sprint 1 |

---

## Residual Accepted Risks

| Risk | Rationale |
|------|-----------|
| JWT in localStorage (I-05) | HttpOnly cookie would require CSRF protection; current SPA architecture accepts this tradeoff; mitigated by short JWT TTL (30 min) |
| ClosedXML in-memory Excel (D-04) | Acceptable for current scale (<1,000 employees/tenant); Sprint 1 debt |
| No external pen test | Static analysis only for initial launch; pen test required before reaching 10 tenants (see [PenetrationTestRequirements.md](PenetrationTestRequirements.md)) |

---

*Threat model authored: 2026-07-24. Review annually and on any architectural change.*
