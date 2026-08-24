# Performance SLA & Load Profile
**HRMS v2.0.0** | Addresses Specification Gap #4

---

## Declared SLA Targets

These targets must be met in production under the load profile defined below before go-live clearance. k6 load tests (HIGH-10) are written against these numbers — without targets, the tests have no pass/fail condition.

| Metric | Target | Hard Limit |
|--------|--------|-----------|
| API response time — P95 | ≤ 500 ms | 2 000 ms |
| API response time — P99 | ≤ 1 000 ms | 5 000 ms |
| Payroll generation (single company, 500 employees) | ≤ 30 s | 60 s |
| Attendance bulk upload (1 000-row Excel) | ≤ 10 s | 30 s |
| Report export (monthly payslip PDF, 500 rows) | ≤ 15 s | 45 s |
| Login endpoint | ≤ 300 ms (P95) | 1 000 ms |
| Availability (monthly) | ≥ 99.5 % | — |
| Error rate (5xx) | ≤ 0.1 % | 1 % |

---

## Baseline Load Profile

### Tenant Sizing

| Tier | Concurrent Tenants | Employees per Tenant (P99) | Employees per Tenant (Median) |
|------|-------------------|--------------------------|------------------------------|
| **Small** | 1–10 | 200 | 50 |
| **Medium** | 11–50 | 1 000 | 300 |
| **Large** | 51–200 | 5 000 | 1 500 |
| **Go-Live Target** | **≤ 20 tenants** | **≤ 1 000** | **≤ 300** |

> The go-live load target is **20 concurrent tenants × 300 employees (median)**. All SLA targets above must be validated at this load.

### Concurrent Request Profile (Steady-State, Business Hours)

| Endpoint Category | Req/min (per tenant) | Total Req/min (20 tenants) |
|-------------------|---------------------|--------------------------|
| Dashboard & reports (read-heavy) | 40 | 800 |
| Employee CRUD | 10 | 200 |
| Attendance check-in/out | 60 | 1 200 |
| Payroll operations | 5 | 100 |
| Leave requests | 15 | 300 |
| Auth (login, refresh) | 5 | 100 |
| **Total** | **135** | **2 700** |

### Peak Load Profile (Month-End Payroll)

| Scenario | Duration | Peak Req/min |
|----------|---------|-------------|
| Month-end payroll run (all tenants) | 30 min | 3 500 |
| Bulk attendance upload (start of month) | 1 hour | 4 000 |
| Annual leave sync (Jan 1 00:00 UTC) | 15 min | 5 000 |

---

## Pagination — Adequacy Evaluation

The audit noted (HIGH-4) that `GetAllAsync` had a 500-row silent cap. Given the load profile above:

| Query | Max Rows at P99 | Page Size | Pages to Load Full Set |
|-------|----------------|-----------|------------------------|
| Employee list | 1 000 | 50 | 20 |
| Attendance records (monthly) | 31 000 (1 000 emp × 31 days) | 100 | 310 — **streaming required** |
| Audit logs | 500 000 (annual) | 100 | **Export only; no UI pagination** |
| Payslip history | 120 (10 years × 12) | 25 | 5 |

**Conclusion:** Default page size of 25–50 is adequate for all UI-rendered lists. Attendance and audit log exports must use streaming (OpenXML streaming confirmed for reports — HIGH-4 partially verified).

---

## Caching — Adequacy Evaluation

| Cache Target | TTL | Cache Key | Adequate? |
|-------------|-----|-----------|-----------|
| Company settings | 10 min | `company:{id}:settings` | ✅ |
| Leave type list | 30 min | `company:{id}:leave-types` | ✅ |
| Department list | 30 min | `company:{id}:departments` | ✅ |
| Employee list (paginated) | **None** | — | ⚠️ Sprint 1: add 2-min cache with tag invalidation |
| Payroll calculation intermediates | **None** | — | ⚠️ Sprint 1: cache salary-structure lookups during bulk payroll |

---

## Distributed Lock — Adequacy Evaluation

| Lock Target | Implementation | Timeout | Adequate at Go-Live Load? |
|------------|---------------|---------|--------------------------|
| Payroll generation per company | Redis SETNX | 60 s | ✅ — 1 concurrent payroll run per company |
| Attendance bulk upload | **None** | — | ⚠️ Risk: concurrent uploads for same company may produce duplicate records. Sprint 1. |

---

## k6 Load Test Pass/Fail Conditions

The k6 tests (HIGH-10) must be configured with the following thresholds before they can produce a meaningful Go/No-Go signal:

```javascript
// k6/load-test.js — thresholds (add to existing test file)
export const options = {
  thresholds: {
    // P95 response time ≤ 500ms
    http_req_duration: ['p(95)<500', 'p(99)<1000'],
    // Error rate ≤ 0.1%
    http_req_failed: ['rate<0.001'],
    // Login endpoint ≤ 300ms P95
    'http_req_duration{endpoint:login}': ['p(95)<300'],
    // Payroll generation ≤ 30s
    'http_req_duration{endpoint:payroll-generate}': ['p(95)<30000'],
  },
  scenarios: {
    steady_state: {
      executor: 'constant-arrival-rate',
      rate: 2700,         // req/min steady-state (20 tenants)
      timeUnit: '1m',
      duration: '15m',
      preAllocatedVUs: 50,
      maxVUs: 200,
    },
    peak_payroll: {
      executor: 'ramping-arrival-rate',
      startTime: '15m',
      stages: [
        { duration: '2m', target: 3500 },  // ramp to peak
        { duration: '10m', target: 3500 }, // sustain peak
        { duration: '3m', target: 0 },     // ramp down
      ],
    },
  },
};
```

---

## Infrastructure Sizing for Go-Live Target

| Component | Minimum Spec (Go-Live) | Recommended |
|-----------|----------------------|-------------|
| API container | 2 vCPU, 2 GB RAM | 4 vCPU, 4 GB RAM |
| PostgreSQL | 4 vCPU, 8 GB RAM, 100 GB SSD | 8 vCPU, 16 GB RAM |
| Redis | 1 vCPU, 1 GB RAM | 2 vCPU, 2 GB RAM |
| Nginx | 1 vCPU, 512 MB RAM | 2 vCPU, 1 GB RAM |

> Horizontal scaling (HPA) is configured in `k8s/hpa.yaml` — triggers at 70% CPU. Ensure the DB connection pool (`Max Pool Size=100` in connection string) is set before enabling HPA.

---

## Latency Budget Breakdown (Payroll Generation)

Target: ≤ 30 s for 500 employees.

| Step | Budget | Notes |
|------|--------|-------|
| Acquire Redis lock | 50 ms | Timeout → 409 Conflict |
| Load salary structures (all employees) | 2 000 ms | Cacheable in Sprint 1 |
| Load attendance records (month) | 3 000 ms | Indexed by `(employee_id, date)` |
| Calculate net pay (500 employees, parallel) | 10 000 ms | CPU-bound; 4 vCPU |
| Write payslips (bulk insert) | 2 000 ms | `ExecuteUpdateAsync` batch |
| Release lock + publish event | 100 ms | |
| **Total** | **~17 s** | **≤ 30 s target met** |

---

*SLA targets approved: 2026-07-24. Must be re-evaluated when tenant count exceeds 20 or P99 employee count exceeds 1 000.*
