// ============================================================
// HRMS k6 Load Test — v2.0.0
// Implements the scenarios and pass/fail thresholds defined in
// Documentation/PerformanceSLA.md.
//
// Usage:
//   k6 run -e BASE_URL=https://api.yourdomain.com k6/load-test.js
//
// Pass/fail is determined by the thresholds block. k6 exits
// with code 1 if any threshold is breached — wire this into CI
// as a Go/No-Go gate before production deploys.
//
// Environment variables:
//   BASE_URL          API base (default: http://localhost:5000)
//   ADMIN_EMAIL       Tenant admin email  (default: admin@test.com)
//   ADMIN_PASSWORD    Tenant admin password
//   INCLUDE_PAYROLL   Set to "true" to exercise payroll endpoints (default: false)
// ============================================================

import http from 'k6/http';
import { check, sleep } from 'k6';
import { Trend, Rate } from 'k6/metrics';

// ── Custom metrics ───────────────────────────────────────────
const loginDuration    = new Trend('login_duration',    true);
const payrollDuration  = new Trend('payroll_duration',  true);
const employeeDuration = new Trend('employee_duration', true);
const leaveDuration    = new Trend('leave_duration',    true);

// ── Configuration ────────────────────────────────────────────
const BASE_URL       = __ENV.BASE_URL       || 'http://localhost:5000';
const ADMIN_EMAIL    = __ENV.ADMIN_EMAIL    || 'admin@test.com';
const ADMIN_PASSWORD = __ENV.ADMIN_PASSWORD || 'TestPassword123!';

// ── Pass/fail thresholds (from PerformanceSLA.md) ────────────
//
// These thresholds define the Go/No-Go signal for production clearance.
// All must pass at the 20-tenant load profile below.
//
export const options = {
  thresholds: {
    // Global: P95 ≤ 500 ms, P99 ≤ 1 000 ms  (PerformanceSLA.md)
    http_req_duration: ['p(95)<500', 'p(99)<1000'],

    // Error rate: ≤ 0.1% of requests may fail (5xx treated as error)
    http_req_failed: ['rate<0.001'],

    // Endpoint-specific SLAs
    login_duration:    ['p(95)<300'],    // Login ≤ 300 ms P95
    payroll_duration:  ['p(95)<30000'],  // Payroll generation ≤ 30 s P95
    employee_duration: ['p(95)<500'],    // Employee list ≤ 500 ms P95
    leave_duration:    ['p(95)<500'],    // Leave balance ≤ 500 ms P95
  },

  scenarios: {
    // ── Steady-state: 20 tenants × 135 req/min = 2 700 req/min ──────────
    // Uses constant-arrival-rate so concurrency is request-driven, not VU-driven.
    // Matches PerformanceSLA.md "Concurrent Request Profile" table.
    steady_state: {
      executor:          'constant-arrival-rate',
      rate:              2700,          // total req/min across all tenants
      timeUnit:          '1m',
      duration:          '15m',
      preAllocatedVUs:   50,
      maxVUs:            200,
      tags:              { scenario: 'steady_state' },
    },

    // ── Peak: month-end payroll run — ramp to 3 500 req/min ─────────────
    // Matches PerformanceSLA.md "Peak Load Profile" table.
    peak_payroll: {
      executor:   'ramping-arrival-rate',
      startTime:  '15m',              // begins after steady-state completes
      preAllocatedVUs: 100,
      maxVUs:     500,
      stages: [
        { duration: '2m',  target: 3500 }, // ramp to month-end peak
        { duration: '10m', target: 3500 }, // sustain peak (payroll window)
        { duration: '3m',  target: 0    }, // ramp down
      ],
      tags: { scenario: 'peak_payroll' },
    },
  },
};

// ── Helpers ───────────────────────────────────────────────────
function jsonHeaders(token) {
  const h = { 'Content-Type': 'application/json' };
  if (token) h['Authorization'] = `Bearer ${token}`;
  return h;
}

// ── Login once per VU and reuse the token ────────────────────
let cachedToken = null;

function login() {
  const res = http.post(
    `${BASE_URL}/api/auth/login`,
    JSON.stringify({ email: ADMIN_EMAIL, password: ADMIN_PASSWORD }),
    { headers: jsonHeaders(), tags: { endpoint: 'login' } },
  );
  loginDuration.add(res.timings.duration);
  check(res, { 'login 200': (r) => r.status === 200 });
  try {
    return res.json('data.token') || null;
  } catch { return null; }
}

// ── Main VU function ──────────────────────────────────────────
export default function () {
  if (!cachedToken) cachedToken = login();
  if (!cachedToken) { sleep(1); return; }

  const headers = jsonHeaders(cachedToken);

  // 1. List employees (most common read path — 10 req/min/tenant)
  const empRes = http.get(
    `${BASE_URL}/api/employees?page=1&pageSize=25`,
    { headers, tags: { endpoint: 'employees-list' } },
  );
  employeeDuration.add(empRes.timings.duration);
  check(empRes, { 'employees 200': (r) => r.status === 200 });

  sleep(1);

  // 2. Leave balance (employee self-service — 15 req/min/tenant)
  const leaveRes = http.get(
    `${BASE_URL}/api/leave/balance`,
    { headers, tags: { endpoint: 'leave-balance' } },
  );
  leaveDuration.add(leaveRes.timings.duration);
  check(leaveRes, { 'leave balance 200': (r) => r.status === 200 });

  sleep(1);

  // 3. Attendance check-in/out (60 req/min/tenant — highest frequency)
  const attendanceRes = http.get(
    `${BASE_URL}/api/attendance/today`,
    { headers, tags: { endpoint: 'attendance-today' } },
  );
  check(attendanceRes, { 'attendance 200 or 404': (r) => r.status === 200 || r.status === 404 });

  sleep(1);

  // 4. Dashboard (40 req/min/tenant — read-heavy)
  const dashRes = http.get(
    `${BASE_URL}/api/dashboard`,
    { headers, tags: { endpoint: 'dashboard' } },
  );
  check(dashRes, { 'dashboard 200': (r) => r.status === 200 });

  sleep(1);

  // 5. Payroll generation (heavy — only exercised in peak_payroll scenario)
  //    Set INCLUDE_PAYROLL=true to enable; disabled by default in steady_state.
  if (__ENV.INCLUDE_PAYROLL === 'true') {
    const year  = new Date().getFullYear();
    const month = new Date().getMonth() + 1;
    const payRes = http.post(
      `${BASE_URL}/api/payroll/generate`,
      JSON.stringify({ month, year, workingDays: 26, daysPresent: 26 }),
      { headers, tags: { endpoint: 'payroll-generate' } },
    );
    payrollDuration.add(payRes.timings.duration);
    // 200 (success) or 409 (lock held by concurrent run) are both acceptable — 5xx is not
    check(payRes, { 'payroll not 5xx': (r) => r.status < 500 });
  }

  sleep(2);
}

// ── Setup: validate connectivity before the test starts ──────
export function setup() {
  const res = http.get(`${BASE_URL}/healthz`);
  check(res, { 'healthz 200': (r) => r.status === 200 });
  if (res.status !== 200) {
    throw new Error(`Health check failed — aborting load test. Status: ${res.status}`);
  }
  console.log(`[load-test] Stack healthy at ${BASE_URL}. Starting scenarios.`);
  return {};
}

// ── Teardown summary ──────────────────────────────────────────
export function teardown(data) {
  console.log('[load-test] Complete. Inspect thresholds above for Go/No-Go signal.');
  console.log('[load-test] Expected: p(95)<500ms, p(99)<1000ms, error rate<0.1%');
  console.log('[load-test] If all thresholds PASS at 20-tenant profile → cleared for first tenant onboarding.');
}
