# Compliance Framework
**HRMS v2.0.0** | Addresses Specification Gap #1

---

## Applicable Regulatory Regimes

This system is designed to be deployable in multiple jurisdictions. The table below declares the active compliance baseline for each deployment tier. **The deploying organisation must select and confirm their applicable regime in `.env` via the `COMPLIANCE_REGIME` variable before going live.**

| Regime | Jurisdiction | Applies When |
|--------|-------------|--------------|
| **India DPDP Act 2023** | India | All deployments where any employee is an Indian resident or where personal data of Indian citizens is processed |
| **GDPR** | EU / EEA | All deployments where any employee is an EU/EEA resident |
| **ISO 27001:2022** | Global | Applied as the baseline information-security control framework for all deployments regardless of jurisdiction |
| **SOC 2 Type II** | US SaaS tenants | Required when SaaS tenants are US-domiciled businesses demanding SOC 2 attestation |
| **UIDAI Guidelines** | India | Required when Aadhaar numbers are stored or processed (see Aadhaar section below) |

> **Default baseline:** ISO 27001:2022 + India DPDP Act 2023. Override with `COMPLIANCE_REGIME=gdpr` for EU deployments.

---

## Point-by-Point Applicability

### 1. Jurisdiction Coverage

| Configuration variable | Purpose |
|------------------------|---------|
| `COMPLIANCE_REGIME` | `dpdp` (default), `gdpr`, `iso27001`, `soc2` |
| `DATA_RESIDENCY_REGION` | `in-south-1` (India), `eu-west-1` (EU), `us-east-1` (US) |
| `EMPLOYEE_JURISDICTIONS` | Comma-separated ISO-3166-1 alpha-2 country codes of employee populations |

The deploying organisation is responsible for declaring these values truthfully before production go-live. The system enforces encryption and audit controls regardless of regime; regime selection controls which additional obligations are active (e.g. data-subject access requests, erasure timelines).

---

### 2. Data Retention — 36-Month Target

The `36-month` audit-log retention target referenced in the audit (LOW-9) is classified as:

| Classification | Basis |
|---------------|-------|
| **Legal requirement** | India DPDP Act 2023 — data principals may exercise access/correction rights; records must be retained long enough to service those requests |
| **Business preference** | Payroll and statutory compliance in India requires records for 3 years under the Payment of Wages Act and EPF Act |
| **Override** | EU/GDPR deployments must reduce retention to the minimum necessary (typically 12–18 months for HR data); set `AUDIT_LOG_RETENTION_MONTHS=18` |

The `TokenCleanupService` enforces retention for tokens. Audit-log retention is enforced by the `AuditLogRetentionService` background job (Hangfire, daily at 03:00 UTC). See [Runbook.md](Runbook.md) for operational details.

---

### 3. Aadhaar / PAN Handling — UIDAI Guidelines

| Data Element | Storage | Access Control | UIDAI Compliant |
|-------------|---------|---------------|-----------------|
| `AadhaarNumber` | AES-256 encrypted at rest | `PII_VIEWER` role only via `EmployeePiiDto` | ✅ — not stored in plain text; masked in logs |
| `PanNumber` | AES-256 encrypted at rest | `PII_VIEWER` role only via `EmployeePiiDto` | ✅ |
| `BankAccountNumber` | AES-256 encrypted at rest | `PII_VIEWER` role only via `EmployeePiiDto` | ✅ |

**UIDAI-specific obligations:**
- Aadhaar numbers **must not** be used as a primary key or shared identifier across systems — ✅ HRMS uses internal `EmployeeId` as the primary key.
- Aadhaar numbers **must not** be printed on payslips or exported in reports — ✅ The `PayslipDto` and all report DTOs exclude PII fields.
- Authentication using Aadhaar biometrics requires an AUA/KUA licence — **out of scope** for this system; HRMS stores the number for statutory compliance only, not for authentication.

---

### 4. Data Subject Rights (DPDP / GDPR)

| Right | DPDP 2023 | GDPR Art. | How HRMS Satisfies It |
|-------|-----------|-----------|----------------------|
| Right to Access | S. 11 | Art. 15 | `GET /api/employees/{id}/pii` (PII_VIEWER role) |
| Right to Correction | S. 12 | Art. 16 | `PUT /api/employees/{id}` — all fields updatable by HR Admin |
| Right to Erasure | S. 13 | Art. 17 | Soft-delete preserves statutory records; hard-delete available for non-statutory data via SuperAdmin |
| Right to Grievance | S. 14 | Art. 77 | Contact: Data Protection Officer — configure `DPO_EMAIL` in `.env` |
| Breach Notification | S. 8(6) | Art. 33 | Incident runbook in [Runbook.md](Runbook.md) — target: notify within 72 hours |

---

### 5. ISO 27001:2022 Control Mapping (Key Controls)

| Control | Domain | HRMS Implementation |
|---------|--------|---------------------|
| A.5.15 | Access control | RBAC with JWT claims; `ICompanyOwned` global query filters |
| A.5.23 | Information security for cloud services | Secrets via environment variables; TLS enforced |
| A.8.3 | Information access restriction | PII accessible only via `EmployeePiiDto`; role-gated |
| A.8.5 | Secure authentication | BCrypt pw12, MFA (TOTP), rate-limited login |
| A.8.10 | Information deletion | Soft-delete + `AuditLogRetentionService` |
| A.8.12 | Data leakage prevention | AES-256 PII encryption; no PII in logs (Serilog destructuring) |
| A.8.15 | Logging | `AuditLogs` table + Serilog structured logs + OpenTelemetry |
| A.8.24 | Use of cryptography | AES-256 (PII), BCrypt (passwords), RS256 (JWT) |

---

### 6. SOC 2 Type II Readiness (for US SaaS Tenants)

| Trust Service Criterion | Status | Notes |
|------------------------|--------|-------|
| CC6.1 Logical access | ✅ Ready | RBAC, MFA, session management |
| CC6.2 Access provisioning | ✅ Ready | Admin-controlled user creation; `MustChangePassword` on creation |
| CC6.7 Data transmission | ✅ Ready | TLS 1.2+; HSTS |
| CC7.2 System monitoring | ✅ Ready | Prometheus + Grafana + Serilog + OpenTelemetry |
| CC8.1 Change management | ⚠️ Partial | CI/CD pipeline present; formal change-approval process is the tenant's responsibility |
| A1.2 Performance monitoring | ✅ Ready | Prometheus metrics; Grafana dashboards |

---

## Compliance Contacts

| Role | Variable | Required Before Go-Live |
|------|----------|------------------------|
| Data Protection Officer | `DPO_EMAIL` | Yes — for DPDP/GDPR breach notification |
| Legal Counsel | (external) | Yes — for jurisdiction confirmation |
| ISO 27001 Lead | (external) | Yes — for ISO audit scoping |

---

## Annual Review

This document must be reviewed:
- Annually
- When a new jurisdiction's employees are onboarded
- When a material regulatory change is enacted (e.g. DPDP Act implementing rules, GDPR supervisory authority guidance)
- Before any new SaaS tenant is onboarded in a previously uncovered jurisdiction

*Last reviewed: 2026-07-24*
