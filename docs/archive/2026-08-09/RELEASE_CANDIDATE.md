# RatanHR HRMS — Release Candidate
## v1.0.0-rc1

**Prepared:** 2026-08-04  
**Status:** 🔒 PENDING — awaiting Phase 9 execution on staging server  
**RC promoted to GA when:** All go/no-go criteria met and all four roles sign off

---

## Git tag instructions

After `bash phase9_run.sh` prints **✅ PHASE 9 PASSED** and `bash phase9_cleanup.sh` prints **✅ CLEAN**:

```bash
# 1. Commit the cleanup
git add -A
git commit -m "chore: Phase 9 source cleanup for v1.0.0-rc1"

# 2. Tag the release candidate
git tag -a v1.0.0-rc1 -m "Release Candidate 1
- Phase 9 regression: all 1,709+ checks pass
- 934 .NET unit/integration tests pass
- 625 E2E tests pass (Chromium, Firefox, Mobile Chrome)
- Tenant isolation confirmed: Company A vs B
- Source cleaned, no secrets in tree"

# 3. Push
git push origin main --follow-tags

# 4. Verify
git show v1.0.0-rc1

# 5. When RC is approved for GA:
git tag -a v1.0.0 -m "RatanHR HRMS v1.0.0 GA" v1.0.0-rc1
git push origin v1.0.0
```

---

## Version history

| Version | Date | Key change |
|---|---|---|
| v2.1.0 | 2026-07-25 | Internal release gate — k8s JWT fix (RS256), build + 934 tests passing |
| v1.0.0-rc1 | 2026-08-04 | First public release candidate — full Phase 9 regression, deploy tooling |
| v1.0.0 | TBD | GA release — awaiting Phase 9 + sign-off |

---

## Phase gates 1–9 summary

| Phase | Description | Method | Status |
|---|---|---|---|
| **1** | Architecture & domain model | Code review, ERD review | ✅ PASS |
| **2** | Backend API completeness | Swagger parity, controller audit | ✅ PASS |
| **3** | Security hardening | Static analysis (706 files), OWASP checklist | ✅ PASS |
| **4** | Backend API deep audit | Integration tests, IDOR tests, tenant isolation | ✅ PASS |
| **5** | Payroll calculation accuracy | Edge-case tests, payroll arithmetic validation | ✅ PASS |
| **6** | Security audit (final) | PII encryption, RBAC, CSP, rate limiting | ✅ PASS |
| **7** | Frontend UX & E2E | 625 Playwright tests — Chromium, Firefox, Mobile Chrome | ✅ PASS |
| **8** | Staging smoke + DB validation | 67 smoke + 42 DB checks via `phase8_runbook.sh` | 🔒 PENDING (staging server required) |
| **9** | Full regression | All 21 modules, all layers, tenant security test | 🔒 PENDING (staging server required) |

> **Phases 8–9** are marked PENDING because they require a live server with MySQL, Redis, and the API running.  
> All code-verifiable checks in phases 8–9 have been confirmed by source analysis and the Phase 7 E2E suite.

---

## Detailed gate evidence

### Phase 1–3 — Architecture, API, Security
| Check | Evidence |
|---|---|
| 0 build errors, 0 warnings (`/warnaserror`) | `FINAL_UAT_GO_LIVE_APPROVAL_REPORT.md` |
| 934 unit + integration tests pass | `FINAL_UAT_GO_LIVE_APPROVAL_REPORT.md` |
| RS256 asymmetric JWT signing | `RELEASE_GATE_FINAL.md` — RS256 confirmed, HS256 removed |
| BCrypt work factor 12 (3 call sites) | Static analysis — `BcryptPasswordHasher` |
| AES-256-GCM PII field encryption | `PiiEncryptionIntegrationTests.cs` |
| CSRF double-submit globally applied | `CsrfMiddleware.cs` |
| Account lockout: 5 attempts → 15 min | `AccountLockoutService.cs` |
| Non-root Docker container (`USER hrms`) | `Dockerfile` line 16 |
| Digest-pinned base images | `Dockerfile` + `docker-compose.prod.yml` |
| TOTP secrets encrypted at rest | `MfaService.cs` |
| SQL injection: none found (EF Core LINQ) | Full static scan |
| Swagger disabled in production | `Program.cs` |

### Phase 4–6 — Integration, Payroll, Security audit
| Check | Evidence |
|---|---|
| Tenant isolation (EF Core global query filters) | `TenantRepositoryTests.cs` (15 tests) |
| IDOR protection on CompanyBranch | `CompanyBranchIdorTests.cs` (8 tests) |
| Training enrollment IDOR | `TrainingEnrollmentIdorTests.cs` |
| Payroll cross-tenant isolation | `PayrollAttendanceTenantTests.cs` |
| Report controller IDOR | `ReportControllerIDORTests.cs` |
| `RequireTenantForWriteAttribute` on all write routes | Static analysis |
| `[Authorize(Roles=...)]` on 62 controllers | Static analysis |
| PII fields encrypted at rest (Aadhaar, PAN, bank) | `PiiEncryptionIntegrationTests.cs` |
| Payroll calculation edge cases | `PayrollEdgeCaseTests.cs` |
| Leave balance arithmetic | `LeaveEdgeCaseTests.cs` |
| Attendance edge cases | `AttendanceEdgeCaseTests.cs` |
| Security headers (HSTS, CSP, X-Frame) | `CspNonceMiddleware.cs`, `nginx.conf` |

### Phase 7 — Frontend E2E
| Check | Evidence |
|---|---|
| 625/625 Chromium — 0 failed, 0 not run | `PHASE7_FRONTEND_UX_AUDIT.md` |
| 625/625 Firefox — 0 failed | `PHASE7_FRONTEND_UX_AUDIT.md` |
| 625/625 Mobile Chrome — 0 failed | `PHASE7_FRONTEND_UX_AUDIT.md` |
| 0 flaky tests | `PHASE7_FRONTEND_UX_AUDIT.md` |
| Auth, RBAC, tenant, payroll E2E flows | 25 spec files in `HRMS.SPA.Source/e2e/` |

### Phase 8 — Staging (requires server)
| Check | Status | Action |
|---|---|---|
| 67 smoke checks via `phase8_runbook.sh` | 🔒 PENDING | Run on staging server |
| 42 DB validation checks | 🔒 PENDING | Run on staging server |
| MailHog email delivery | 🔒 PENDING | Trigger forgot-password on staging |
| Hangfire `/hangfire` accessible | 🔒 PENDING | Verify on staging |

### Phase 9 — Full regression (requires server)
| Check | Status | Action |
|---|---|---|
| All 21 module regression checks | 🔒 PENDING | `bash phase9_run.sh` |
| Company A vs B tenant isolation (10 checks) | 🔒 PENDING | Included in phase9_run.sh §12 |
| Admin / Employee / Payroll / Sales workflows | 🔒 PENDING | Included in phase9_run.sh §13–16 |
| Browser console + server log sweep | 🔒 PENDING | Included in phase9_run.sh §17 |
| Source cleanup | 🔒 PENDING | `bash phase9_cleanup.sh` |

---

## Build numbers

| Component | Version | Build method |
|---|---|---|
| HRMS API | 1.0.0 | `dotnet publish HRMS.API -c Release /p:AssemblyVersion=1.0.0` |
| HRMS SPA | 1.0.0 | `bun run build:ci` (Vite) |
| Docker image | `hrms-api:1.0.0-rc1` | `docker compose build --build-arg GIT_SHA=$(git rev-parse --short HEAD)` |
| MySQL | 8.4 (digest-pinned) | `mysql:8.4@sha256:1d6b6a...` |
| Redis | 7.4-alpine (digest-pinned) | `redis:7.4-alpine@sha256:b1addb...` |
| .NET runtime | 8.0.16 (digest-pinned) | `aspnet:8.0.16@sha256:98ce95...` |

---

## Go / No-Go decision criteria

### ✅ GO conditions (all must be true)
- [ ] `bash phase9_run.sh` exits 0 — all sections PASS
- [ ] `bash phase9_cleanup.sh` exits 0 — source is clean
- [ ] `bash Staging/phase8_runbook.sh` exits 0 — PASS=109, FAIL=0
- [ ] MailHog receives test email from `/api/auth/forgot-password`
- [ ] Hangfire dashboard accessible at `/hangfire` with SuperAdmin login
- [ ] DNS A record pointing to production server
- [ ] TLS certificate issued and not expiring within 30 days
- [ ] `.env` filled — zero `<REQUIRED>` placeholders remain
- [ ] `BACKUP_ENCRYPTION_KEY` set and backup test succeeded
- [ ] All four sign-off roles have signed the table below

### ❌ NO-GO conditions (any one blocks release)
- Any FAIL in `phase9_run.sh` §12 (tenant isolation BREACH)
- Any FAIL in `dotnet test` (934 → must be ≥ 934 pass, 0 fail)
- Any FAIL in Playwright E2E (625 per browser → must all pass)
- Any FAIL in Phase 8 runbook (67 smoke + 42 DB checks)
- Any secret found in source tree by `phase9_cleanup.sh` §6
- Missing critical file found by `phase9_cleanup.sh` §7
- TLS certificate expired or expiring within 7 days
- SuperAdmin initial password not changed after first login

---

## Pre-production deployment checklist

| # | Action | Owner | Done |
|---|---|---|---|
| 1 | Execute `bash phase9_run.sh` → exit 0 | DevOps | ☐ |
| 2 | Execute `bash phase9_cleanup.sh` → exit 0 | DevOps | ☐ |
| 3 | Execute `bash Staging/phase8_runbook.sh` → PASS | DevOps | ☐ |
| 4 | Confirm MailHog receives test email | QA | ☐ |
| 5 | Confirm Hangfire dashboard at `/hangfire` | QA | ☐ |
| 6 | Generate production RSA keys (`scripts/generate-secrets.sh`) | DevOps | ☐ |
| 7 | Fill all `<REQUIRED>` fields in `.env` | DevOps | ☐ |
| 8 | Set `BACKUP_ENCRYPTION_KEY` and test backup | DevOps | ☐ |
| 9 | DNS A record → production server IP | DevOps | ☐ |
| 10 | TLS certificate issued via `certbot` | DevOps | ☐ |
| 11 | Execute `bash deploy.sh` on production server | DevOps | ☐ |
| 12 | Change SuperAdmin initial password on first login | QA | ☐ |
| 13 | Confirm production health: `curl https://DOMAIN/api/healthz` | QA | ☐ |
| 14 | Schedule backup cron job | DevOps | ☐ |
| 15 | Enable certificate auto-renewal hook | DevOps | ☐ |
| 16 | Set up monitoring alerts (5xx rate, disk, memory) | DevOps | ☐ |

---

## Final sign-off

> All four roles must sign before `v1.0.0-rc1` is promoted to `v1.0.0`.

| Role | Name | Signature | Date | Notes |
|---|---|---|---|---|
| Engineering Lead | | | | Phase 9 run completed, all tests pass |
| QA Lead | | | | Manual smoke on staging verified |
| DevOps / Infra | | | | deploy.sh run on production, health confirmed |
| Product Owner | | | | Feature acceptance sign-off |
| Security Officer | | | | Tenant isolation and secret scan confirmed |

---

## Post-release checklist

- [ ] Monitor production error rate for 24 hours after go-live
- [ ] Confirm first scheduled payroll run completes without errors
- [ ] Verify backup cron running: check `logs/backup.log` next morning
- [ ] Rotate SuperAdmin password within 24 hours of go-live
- [ ] Remove `E2E_*` seed accounts from production DB if they were accidentally seeded
- [ ] Tag `v1.0.0` from `v1.0.0-rc1` after 48-hour observation window

---

## Related documents

| Document | Purpose |
|---|---|
| `PHASE9_REGRESSION_PLAN.md` | Full regression plan — 21 modules, all checks |
| `phase9_run.sh` | One-command Phase 9 execution script |
| `phase9_cleanup.sh` | Source cleanup and verification |
| `PHASE8_STAGING_VALIDATION.md` | Phase 8 evidence (static + pending live) |
| `GO_LIVE_READINESS.md` | Full gate table phases 1–8 |
| `DEPLOYMENT.md` | Production deployment guide |
| `deploy.sh` | One-command deploy script |
| `rollback.sh` | Emergency rollback script |

---

*v1.0.0-rc1 prepared 2026-08-04 — Phase 9 pending execution on staging server*
