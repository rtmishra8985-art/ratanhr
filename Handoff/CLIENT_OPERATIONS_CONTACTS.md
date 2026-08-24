# CLIENT OPERATIONS CONTACTS
**Project:** RatanHR HRMS  
**Version:** 2.0.0  
**Date:** 2026-08-01  
**Status:** TEMPLATE — Client must populate all fields before go-live

---

> **INSTRUCTIONS FOR CLIENT:**  
> Complete every section below before the go-live date. Share this completed document with the RatanHR support team and store it securely in your internal IT runbook. Review and update contacts quarterly.

---

## 1. Client Organisation

| Field | Value |
|---|---|
| Organisation Name | ______________________________ |
| Production URL | `https://hrms.yourdomain.com` |
| IT Department Head | ______________________________ |
| Primary Technical Contact | ______________________________ |
| Billing / Commercial Contact | ______________________________ |

---

## 2. Primary Support Contact

> First point of contact for all operational issues.

| Field | Value |
|---|---|
| Name | ______________________________ |
| Role | IT Manager / System Administrator |
| Email | ______________________________ |
| Phone (business hours) | ______________________________ |
| Phone (emergency / out-of-hours) | ______________________________ |
| Working hours | e.g. Mon–Fri 09:00–18:00 IST |
| Backup contact (when unavailable) | ______________________________ |

---

## 3. Escalation Path

| Level | Role | Name | Contact | Response SLA |
|---|---|---|---|---|
| L1 | Helpdesk / First Response | | | 1 hour (business hours) |
| L2 | System Administrator | | | 4 hours |
| L3 | IT Manager | | | 8 hours |
| L4 | CTO / Technical Director | | | 24 hours |
| L5 | Vendor (RatanHR Support) | support@ratanhr.com | +91-XXXXXXXXXX | Per SLA agreement |

**Escalation trigger:**
- L1 → L2: Issue unresolved after 1 hour
- L2 → L3: Issue unresolved after 4 hours or affects >10 users
- L3 → L4: Data loss risk, security breach, or system down >8 hours
- L4 → L5: Critical production outage requiring vendor intervention

---

## 4. Infrastructure Contacts

### 4.1 Cloud / Hosting Provider

| Field | Value |
|---|---|
| Provider Name | e.g. AWS, Azure, GCP, DigitalOcean |
| Account ID / Customer Number | ______________________________ |
| Support Portal | ______________________________ |
| Support Phone | ______________________________ |
| Support Tier | e.g. Business / Enterprise |
| Account Owner Contact | ______________________________ |

### 4.2 Domain Registrar

| Field | Value |
|---|---|
| Registrar Name | e.g. GoDaddy, Cloudflare, Namecheap |
| Account Login | **Store in password manager — do not document here** |
| Admin Contact | ______________________________ |
| Domain Expiry Date | ______________________________ |
| Auto-renewal enabled | ☐ Yes  ☐ No |

### 4.3 DNS Provider

| Field | Value |
|---|---|
| Provider | e.g. Cloudflare, Route 53, Cloudflare |
| Admin Contact | ______________________________ |
| Access | **Credentials in password manager** |

### 4.4 SSL/TLS Certificate

| Field | Value |
|---|---|
| Certificate Provider | e.g. Let's Encrypt, DigiCert |
| Certificate Type | DV / OV / EV |
| Expiry Date | ______________________________ |
| Auto-renewal enabled | ☐ Yes  ☐ No |
| Alert email for expiry | ______________________________ |

---

## 5. Email / SMTP Contacts

| Field | Value |
|---|---|
| SMTP Provider | e.g. SendGrid, AWS SES, Mailgun |
| Provider Support URL | ______________________________ |
| Account Owner | ______________________________ |
| From address | `noreply@yourdomain.com` |
| Transactional email daily limit | ______________________________ |
| Bounce/complaint alerts sent to | ______________________________ |

---

## 6. Monitoring & Alerting Contacts

| Alert Type | Recipient Name | Email | Phone | Hours |
|---|---|---|---|---|
| API down / health check fail | | | | 24/7 |
| High error rate (>1%) | | | | Business |
| Database disk > 80% | | | | Business |
| SSL cert expiry < 30 days | | | | Business |
| Security alert / intrusion | | | | 24/7 |
| Backup failure | | | | Business |

**PagerDuty / OpsGenie service key:** Store in password manager — do not document here.

---

## 7. Database Contacts

| Field | Value |
|---|---|
| DBA Name | ______________________________ |
| DBA Email | ______________________________ |
| DBA Phone | ______________________________ |
| MySQL root password location | Password manager — [Vault Path] |
| Backup storage location | ______________________________ |
| Backup verification contact | ______________________________ |

---

## 8. Security & Compliance Contacts

| Role | Name | Email |
|---|---|---|
| Data Protection Officer (DPO) | | |
| Security Incident Lead | | |
| GDPR / DPDP Compliance Officer | | |
| External Pen Test Vendor | | |

**Security incident hotline:** ______________________________  
**Regulatory reporting contact (CERT-In, etc.):** ______________________________

---

## 9. Vendor (RatanHR) Support

| Channel | Details |
|---|---|
| Email | support@ratanhr.com |
| Phone | **CLIENT ACTION REQUIRED** — obtain from contract |
| Support portal | **CLIENT ACTION REQUIRED** — obtain from contract |
| Contract reference | ______________________________ |
| Support hours | Per SLA agreement |
| Critical issue hotline | **CLIENT ACTION REQUIRED** |

---

## 10. Change Management

| Field | Value |
|---|---|
| Change approval authority | ______________________________ |
| Change window | e.g. Saturday 22:00–02:00 IST |
| Change notification lead time | 48 hours minimum |
| Rollback decision authority | ______________________________ |
| Post-change verification contact | ______________________________ |

---

## 11. Backup and Disaster Recovery

| Field | Value |
|---|---|
| Backup owner | ______________________________ |
| Recovery Time Objective (RTO) | 60 minutes |
| Recovery Point Objective (RPO) | 24 hours |
| DR drill schedule | Quarterly |
| Last DR drill date | ______________________________ |
| Last backup verification date | ______________________________ |
| Offsite backup storage | ______________________________ |

---

## Document Control

| Version | Date | Author | Change |
|---|---|---|---|
| 1.0 | 2026-08-01 | RatanHR Engineering | Initial template |
| | | | |

**Review schedule:** Quarterly — next review: ______________________________  
**Document owner:** ______________________________
