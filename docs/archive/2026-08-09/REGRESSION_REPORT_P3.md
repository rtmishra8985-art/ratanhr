> ⚠️ **SUPERSEDED** — This report was generated during an earlier audit/fix pass and no longer reflects the current state of the codebase. The authoritative current-state documents are [`RELEASE_GATE_FINAL.md`](RELEASE_GATE_FINAL.md) and [`VERIFICATION_REPORT_FINAL_v2.md`](VERIFICATION_REPORT_FINAL_v2.md). Do not use this file to assess production readiness.

---

# RatanHR — Regression Report (P1 vs P2 vs P3)

**Date:** 2026-07-21

---

## Scoring Matrix

Scores are out of 10. Each dimension represents the state of that version's implementation at the time of release.

| Dimension | P1 | P2 | P3 (Fixed) | Δ P2→P3 |
|---|---|---|---|---|
| **Security** | 8 | 5 | 9 | +4 |
| **CRUD Completeness** | 7 | 6 | 9 | +3 |
| **Performance** | 7 | 6 | 8 | +2 |
| **Code Quality** | 7 | 7 | 8 | +1 |
| **Production Readiness** | 7 | 7 | 8 | +1 |
| **Testing Coverage** | 6 | 7 | 8 | +1 |
| **Overall** | **7.0** | **6.3** | **8.3** | **+2.0** |

---

## Security

| Check | P1 | P2 | P3 |
|---|---|---|---|
| Training enrollment IDOR | ✅ Protected | ❌ Dropped | ✅ Restored + tested |
| CompanyBranch read IDOR | ✅ Protected | ❌ Dropped | ✅ Restored + tested |
| CompanyBranch write IDOR | ✅ Protected | ❌ Dropped | ✅ Restored + tested |
| CompanyBranch delete IDOR | ✅ Protected | ❌ Dropped | ✅ Restored + tested |
| Generic `GetByIdAsync` tenant filter | ⚠️ Per-service | ⚠️ Per-service | ✅ Tightened (AsNoTracking + compound WHERE) |
| Timesheet admin role check | ⚠️ TODO comment | ❌ Hardcoded false | ✅ Wired to profile API |
| Cookie-based JWT (HttpOnly) | ✅ | ✅ | ✅ |
| BCrypt factor 12 | ✅ | ✅ | ✅ |
| CSRF double-submit | ✅ | ✅ | ✅ |
| Rate limiting | ✅ | ✅ | ✅ |
| Audit log on IDOR block | ✅ | ❌ Missing | ✅ Restored |

**Why P2 scored 5/10 on security:** Two independent IDOR regressions (Training + CompanyBranch) were introduced alongside a broken admin role check that silently disabled an entire workflow.

---

## CRUD Completeness

| Module | P1 | P2 | P3 |
|---|---|---|---|
| Employees | ✅ | ✅ | ✅ |
| Attendance | ✅ | ✅ | ✅ |
| Leave | ✅ | ✅ | ✅ |
| Payroll | ✅ | ✅ | ✅ |
| Training | ✅ | ✅ | ✅ |
| Timesheet | ✅ | ✅ (admin broken) | ✅ (admin fixed) |
| **Shift** | ✅ backend | ❌ no frontend | ✅ full page |
| **Biometric** | ✅ backend | ❌ no frontend | ✅ full page |
| **Department** | ✅ backend | ❌ no frontend | ✅ full page |
| **Holiday** | ✅ backend | ❌ no frontend | ✅ full page |
| Recruitment | ✅ | ✅ | ✅ |
| Performance | ✅ | ✅ | ✅ |
| Assets / Helpdesk | ✅ | ✅ | ✅ |
| Expenses / Travel | ✅ | ✅ | ✅ |

**Why P2 scored 6/10:** Four complete SPA modules (Shift, Biometric, Department, Holiday) existed as backend controllers but had no frontend pages or routes, making them inaccessible to users.

---

## Performance

| Check | P1 | P2 | P3 |
|---|---|---|---|
| `AsNoTracking` on read paths | ✅ | ⚠️ Partial | ✅ |
| N+1 in PayrollService | ✅ Fixed | ✅ Fixed (batch join) | ✅ |
| N+1 in LeaveService | ✅ Fixed | ✅ Fixed (typeNames dict) | ✅ |
| N+1 in CompanyBranchService | ✅ | ✅ | ✅ |
| DB indexes — Employee | ✅ | ⚠️ 2/5 missing | ✅ All added |
| DB indexes — Attendance | ✅ | ⚠️ Compound missing | ✅ |
| DB indexes — Timesheet | N/A | ❌ Table added but no index | ✅ |
| DB indexes — Biometric | N/A | ❌ Table added but no index | ✅ |
| DB indexes — Holiday | N/A | ❌ Table added but no index | ✅ |
| Cache invalidation (Redis cluster) | ✅ | ✅ | ✅ |

---

## Code Quality

| Check | P1 | P2 | P3 |
|---|---|---|---|
| Clean Architecture layers preserved | ✅ | ✅ | ✅ |
| DI registrations complete | ✅ | ✅ | ✅ |
| No raw SQL (uses EF Core) | ✅ | ✅ | ✅ |
| Logger injected (no console.log) | ✅ | ✅ | ✅ |
| PII redaction in Serilog | ✅ | ✅ | ✅ |
| Null-safety (no unsafe `!` operators) | ✅ | ✅ | ✅ |
| Dead code removed (useCallback stub) | — | ❌ Present | ✅ Removed |
| Service interface signatures consistent | ✅ | ⚠️ IDOR gaps | ✅ Fixed (callerCompanyId params) |

---

## Production Readiness

| Check | P1 | P2 | P3 |
|---|---|---|---|
| PostgreSQL snake_case naming | ✅ | ✅ | ✅ |
| Docker health checks | ✅ | ✅ | ✅ |
| Graceful shutdown (SIGTERM) | ✅ | ✅ | ✅ |
| Resource limits (CPU/mem) | ✅ | ✅ | ✅ |
| Migration runs clean | ✅ | ✅ | ✅ (+ new index migration) |
| Global exception handler | ✅ | ✅ | ✅ |
| Security headers | ✅ | ✅ | ✅ |
| Kubernetes manifests | ✅ | ✅ | ✅ |
| Audit log on sensitive operations | ✅ | ❌ IDOR blocks not logged | ✅ Restored |

---

## Testing Coverage

| Area | P1 | P2 | P3 |
|---|---|---|---|
| Auth + JWT | ✅ | ✅ | ✅ |
| Leave service | ✅ | ✅ | ✅ |
| Payroll service | ✅ | ✅ | ✅ |
| Training service | ✅ | ✅ | ✅ |
| Training IDOR (enrollment) | ❌ | ❌ | ✅ **new** |
| CompanyBranch IDOR | ❌ | ❌ | ✅ **new** |
| Timesheet admin role | ❌ | ❌ | ✅ **new** |
| Existing IDOR suite | ✅ | ✅ | ✅ |
| PostgreSQL integration | ✅ | ✅ | ✅ |
| Password hashing | ✅ | ✅ | ✅ |

---

## Root Cause Analysis — Why P2 Regressed

The P2 release introduced new features and modules (Analytics, Dashboard enhancements, MFA improvements, new migrations) without re-running the P1 security test suite. The CompanyBranchService was refactored to reduce constructor injection complexity (removing `IAuditService` and `ILogger`), inadvertently dropping the ownership-check logic that depended on them. The TrainingService `EnrollAsync` method was similarly simplified, removing the employee-company pre-fetch.

### Recommended Process Controls
1. Add `TrainingEnrollmentIdorTests` and `CompanyBranchIdorTests` to the CI gate — they will now fail if either IDOR is reintroduced.
2. Add a code-review checklist item: "Any service method operating on a tenant-scoped entity must include a `companyId` ownership check."
3. Do not simplify service constructors by removing `IAuditService` — audit logging is a security requirement, not a convenience.

---

*Regression Report P3 — 2026-07-21*
