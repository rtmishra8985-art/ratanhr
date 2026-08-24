# STAGING GO-LIVE CHECKLIST — RatanHR HRMS
**Date prepared:** 2026-08-02  
**Target environment:** Isolated staging only. Never use this checklist against production.

Each item must be marked **✓ PASS** by a named engineer before proceeding to the next phase.  
Items marked **PENDING CLIENT** or **PENDING DEVOPS** must be resolved before staging sign-off.

---

## Source Code Status (completed — no further action needed)

| Fix | Status |
|---|---|
| Staging compose `service_started` → `service_healthy` | ✓ FIXED |
| Staging compose missing migrate service | ✓ FIXED |
| Alertmanager `noop` webhook removed; email receivers added | ✓ FIXED |
| Grafana dashboard "PostgreSQL" label → "MySQL" | ✓ FIXED |
| `nginx/nginx.conf` bash-default syntax corrected | ✓ FIXED |
| Frontend `build:ci` script added | ✓ FIXED |

---

## Phase 0 — Pre-requisites (DevOps)

| # | Item | Owner | Status | Sign-off |
|---|---|---|---|---|
| 0.1 | Linux host with Docker 24+ and Docker Compose V2 | DevOps | ☐ | |
| 0.2 | `Staging/.env.staging` populated from `Staging/staging.env.template` with staging-only values | DevOps | ☐ | |
| 0.3 | RSA key pair generated: `chmod +x scripts/generate-rsa-keys.sh && ./scripts/generate-rsa-keys.sh` | DevOps | ☐ | |
| 0.4 | AES-256 encryption key generated: `openssl rand -base64 32` | DevOps | ☐ | |
| 0.5 | `BACKUP_ENCRYPTION_KEY` generated and stored in secrets manager | DevOps | ☐ | |
| 0.6 | `SUPERADMIN_INITIAL_PASSWORD` generated and stored | DevOps | ☐ | |
| 0.7 | `ALERTMANAGER_EMAIL_TO` and `ALERTMANAGER_ONCALL_EMAIL` set in monitoring env | Client + DevOps | ☐ | |
| 0.8 | `.env.staging` validated — no placeholder values: `bash scripts/validate-staging.sh --env-file Staging/.env.staging` | DevOps | ☐ | |
| 0.9 | Required ports available: 3307, 6380, 8081, 8025, 1025, 3001 | DevOps | ☐ | |

---

## Phase 1 — Start Staging Stack (DevOps)

```bash
docker compose -f Staging/docker-compose.staging.yml --env-file Staging/.env.staging up -d
```

| # | Item | Owner | Status | Sign-off |
|---|---|---|---|---|
| 1.1 | MySQL container healthy: `docker inspect hrms_staging_db --format='{{.State.Health.Status}}'` → `healthy` | DevOps | ☐ | |
| 1.2 | Redis container healthy | DevOps | ☐ | |
| 1.3 | MailHog container healthy | DevOps | ☐ | |
| 1.4 | Migration service exits 0: `docker logs hrms_staging_migrate` shows "Migration complete" | DevOps | ☐ | |
| 1.5 | API container healthy (up to 90 s): `docker inspect hrms_staging_api --format='{{.State.Health.Status}}'` → `healthy` | DevOps | ☐ | |
| 1.6 | No ERROR-level log entries on API startup: `docker logs hrms_staging_api 2>&1 \| grep -i error` | DevOps | ☐ | |

---

## Phase 2 — Database Migration Verification (DevOps)

| # | Item | Owner | Status | Sign-off |
|---|---|---|---|---|
| 2.1 | All expected tables present in `hrms_staging` database | DevOps | ☐ | |
| 2.2 | Character encoding `utf8mb4` confirmed on all tables | DevOps | ☐ | |
| 2.3 | Run migration service a second time — exits 0, no duplicate records (idempotency) | DevOps | ☐ | |
| 2.4 | API `/health/ready` returns 200 | DevOps | ☐ | |

---

## Phase 3 — API and Frontend Health (DevOps + QA)

| # | Item | Owner | Status | Sign-off |
|---|---|---|---|---|
| 3.1 | `curl -sf http://127.0.0.1:8081/healthz` → `{"status":"Healthy"}` | DevOps | ☐ | |
| 3.2 | `curl -sf http://127.0.0.1:8081/health/live` → 200 | DevOps | ☐ | |
| 3.3 | `curl -sf http://127.0.0.1:8081/health/ready` → 200 | DevOps | ☐ | |
| 3.4 | Frontend loads at `http://127.0.0.1:3001` | QA | ☐ | |
| 3.5 | Frontend can reach API — no CORS errors in browser console | QA | ☐ | |
| 3.6 | Hangfire dashboard at `/hangfire` — anonymous access returns 401 | QA | ☐ | |
| 3.7 | ClamAV reachable from API container | DevOps | ☐ | |
| 3.8 | File upload scanning: upload test file, confirm scan result returned | QA | ☐ | |
| 3.9 | ClamAV fail-closed: stop ClamAV, attempt upload, confirm rejected with 503 | QA | ☐ | |
| 3.10 | API graceful shutdown: `docker stop hrms_staging_api` — no 5xx during drain | DevOps | ☐ | |

---

## Phase 4 — Authentication (QA / Security)

| # | Item | Owner | Status | Sign-off |
|---|---|---|---|---|
| 4.1 | SuperAdmin login with `SUPERADMIN_INITIAL_PASSWORD` → forced password-change prompt | QA | ☐ | |
| 4.2 | Change SuperAdmin password — subsequent login succeeds | QA | ☐ | |
| 4.3 | Invalid password → 401 (no account details leaked) | QA | ☐ | |
| 4.4 | Account lockout after 5 failed attempts | QA | ☐ | |
| 4.5 | Login rate-limit → 429 | QA | ☐ | |
| 4.6 | Admin login and Employee login | QA | ☐ | |
| 4.7 | MFA enrollment: scan QR, confirm setup | QA | ☐ | |
| 4.8 | MFA verification: login + TOTP code | QA | ☐ | |
| 4.9 | Invalid MFA code → 401 | QA | ☐ | |
| 4.10 | Refresh token rotation: old token rejected after use | QA | ☐ | |
| 4.11 | Logout invalidates refresh token | QA | ☐ | |
| 4.12 | Expired access token → 401 | QA | ☐ | |
| 4.13 | Missing / malformed token → 401 | QA | ☐ | |
| 4.14 | Secure cookie flags (`HttpOnly`, `Secure`, `SameSite=Strict`) present | QA | ☐ | |
| 4.15 | CSRF double-submit: mutation without CSRF header → 403 | QA | ☐ | |
| 4.16 | CORS: cross-origin from non-allowed origin → rejected | QA | ☐ | |
| 4.17 | Passwords / tokens / MFA secrets absent from API logs | Security | ☐ | |

---

## Phase 5 — Authorization and Tenant Isolation (QA / Security)

| # | Item | Owner | Status | Sign-off |
|---|---|---|---|---|
| 5.1 | SuperAdmin can access all companies | QA | ☐ | |
| 5.2 | Admin scoped to own company — cross-company resource → 403/404 | Security | ☐ | |
| 5.3 | Employee scoped to own records — cross-employee resource → 403/404 | Security | ☐ | |
| 5.4 | Cross-company IDOR attempt on `/api/employees/{id}` → 403/404 | Security | ☐ | |
| 5.5 | Cross-branch IDOR attempt | Security | ☐ | |
| 5.6 | Employee cannot access another employee's payslip | Security | ☐ | |
| 5.7 | Unauthorised document download → 403 | Security | ☐ | |
| 5.8 | Leave/attendance/expense — cross-tenant access rejected | Security | ☐ | |

---

## Phase 6 — HR and Payroll Workflows (QA)

| # | Item | Owner | Status | Sign-off |
|---|---|---|---|---|
| 6.1 | Company and department creation | QA | ☐ | |
| 6.2 | Employee onboarding with document upload | QA | ☐ | |
| 6.3 | Employee editing | QA | ☐ | |
| 6.4 | Leave application → approval → rejection | QA | ☐ | |
| 6.5 | Attendance entry and reporting | QA | ☐ | |
| 6.6 | Payroll configuration and calculation | QA | ☐ | |
| 6.7 | Payslip creation and PDF download | QA | ☐ | |
| 6.8 | Payroll, employee, and leave reports | QA | ☐ | |
| 6.9 | Notifications delivered | QA | ☐ | |
| 6.10 | Expense, recruitment, and performance flows | QA | ☐ | |

---

## Phase 7 — Email (QA / DevOps)

| # | Item | Owner | Status | Sign-off |
|---|---|---|---|---|
| 7.1 | Password reset email in MailHog at `http://127.0.0.1:8025` | QA | ☐ | |
| 7.2 | Welcome email on employee creation | QA | ☐ | |
| 7.3 | Leave approval / rejection email | QA | ☐ | |
| 7.4 | No duplicate email delivery | QA | ☐ | |
| 7.5 | SMTP failure: stop MailHog, trigger email — graceful failure, no crash | QA | ☐ | |
| 7.6 | Email queue drains on MailHog restart | QA | ☐ | |
| 7.7 | No passwords / tokens / PII in email bodies or API logs | Security | ☐ | |
| 7.8 | Real SMTP credential testing | PENDING CLIENT | ☐ | Awaiting SMTP credentials |

---

## Phase 8 — DNS and TLS (DevOps)

| # | Item | Owner | Status | Sign-off |
|---|---|---|---|---|
| 8.1 | Staging domain DNS resolves | PENDING DEVOPS | ☐ | |
| 8.2 | HTTP redirects to HTTPS | PENDING DEVOPS | ☐ | |
| 8.3 | TLS certificate valid | PENDING DEVOPS | ☐ | |
| 8.4 | HSTS header present | PENDING DEVOPS | ☐ | |
| 8.5 | TLS private key absent from source archive | ✓ PASS | ✓ | Confirmed by secret scan |
| 8.6 | `nginx.conf` syntax correct and consistent with template | ✓ PASS | ✓ | DEFECT-NGINX-CONF-01 fixed |
| 8.7 | Nginx entrypoint expands variables, validates config | ✓ PASS | ✓ | `entrypoint.sh` verified |

---

## Phase 9 — Monitoring (DevOps)

| # | Item | Owner | Status | Sign-off |
|---|---|---|---|---|
| 9.1 | Prometheus starts and config parses | DevOps | ☐ | |
| 9.2 | API metrics scraped at `/metrics` (internal only) | DevOps | ☐ | |
| 9.3 | Grafana starts and dashboard loads (MySQL label correct) | DevOps | ☐ | |
| 9.4 | Alertmanager starts | DevOps | ☐ | |
| 9.5 | Alert rules parse without error | DevOps | ☐ | |
| 9.6 | Alertmanager `noop` webhook removed | ✓ PASS | ✓ | DEFECT-ALERTMANAGER-01 fixed — email receivers in place |
| 9.7 | Test alert delivered to `ALERTMANAGER_EMAIL_TO` address | PENDING CLIENT | ☐ | Set ALERTMANAGER_EMAIL_TO |
| 9.8 | Escalation contacts documented | PENDING CLIENT | ☐ | |

---

## Phase 10 — Backup and Recovery (DevOps)

| # | Item | Owner | Status | Sign-off |
|---|---|---|---|---|
| 10.1 | Local encrypted backup: `bash scripts/mysql-backup.sh` | DevOps | ☐ | |
| 10.2 | Backup file is not plaintext | DevOps | ☐ | |
| 10.3 | Encryption key absent from backup file and logs | DevOps | ☐ | |
| 10.4 | Restore into disposable staging DB: `bash scripts/test-restore.sh` | DevOps | ☐ | |
| 10.5 | API health against restored DB | DevOps | ☐ | |
| 10.6 | MySQL restart and reconnect | DevOps | ☐ | |
| 10.7 | Redis restart and reconnect | DevOps | ☐ | |
| 10.8 | Hangfire recovery after restart | DevOps | ☐ | |
| 10.9 | Off-site S3 backup | PENDING DEVOPS | ☐ | Requires S3 credentials |

---

## Phase 11 — Biometric Scope

| # | Item | Owner | Status | Sign-off |
|---|---|---|---|---|
| 11.1 | Biometric live sync | DEFERRED BY SCOPE | ✓ | Stub services; pending vendor selection |

---

## Final Sign-Off Gate

All items in Phases 0–10 (except DEFERRED BY SCOPE) must be ✓ before production go-live.

| Gate | Status |
|---|---|
| All source-code defects fixed | ✓ COMPLETE |
| All PASS/FIXED items re-verified | ☐ |
| No FAIL, BLOCKED, or NOT TESTED items remain | ☐ |
| Formal UAT sign-off from client | PENDING CLIENT |
| Final go-live approval from project owner | PENDING CLIENT |
