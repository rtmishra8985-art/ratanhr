// ============================================================
// HRMS k6 Smoke Test — CI Gate (FIX-2)
//
// Lightweight smoke test designed to run on every CI build.
// 10 VUs × 30 seconds — completes in ~45s including setup/teardown.
//
// Pass/fail thresholds mirror PerformanceSLA.md targets but are
// evaluated at low concurrency — this validates correctness and
// baseline latency, not peak throughput (use load-test.js for that).
//
// Usage:
//   k6 run -e BASE_URL=http://localhost:8080 k6/smoke-test.js
//
// The full load test (15 min, 3500 VUs) is in k6/load-test.js
// and is run manually before each production release.
// ============================================================

import http from 'k6/http';
import { check, sleep, group } from 'k6';
import { Trend, Rate, Counter } from 'k6/metrics';

// ── Custom metrics ───────────────────────────────────────────
const loginDuration    = new Trend('login_duration',    true);
const employeeDuration = new Trend('employee_duration', true);
const leaveDuration    = new Trend('leave_duration',    true);
const healthDuration   = new Trend('health_duration',   true);
const errorRate        = new Rate('error_rate');
const requestCount     = new Counter('request_count');

// ── Smoke test configuration ─────────────────────────────────
// 10 VUs × 30 s = light load; passes in all healthy environments.
export const options = {
  vus:      10,
  duration: '30s',

  // Thresholds intentionally generous at low VU count.
  // P95 must be below 800ms (vs 500ms in the full SLA) to account
  // for Docker Compose cold-start overhead in CI.
  thresholds: {
    http_req_duration:  ['p(95)<800', 'p(99)<2000'],
    http_req_failed:    ['rate<0.01'],     // < 1% errors
    login_duration:     ['p(95)<600'],
    employee_duration:  ['p(95)<800'],
    leave_duration:     ['p(95)<800'],
    error_rate:         ['rate<0.01'],
  },
};

const BASE_URL      = __ENV.BASE_URL      || 'http://localhost:8080';
const ADMIN_EMAIL   = __ENV.ADMIN_EMAIL   || 'superadmin@hrms.local';
const ADMIN_PASSWORD = __ENV.ADMIN_PASSWORD || '';

// ── Per-VU token cache ────────────────────────────────────────
let token = null;

function login() {
  const res = http.post(
    `${BASE_URL}/api/auth/login`,
    JSON.stringify({ email: ADMIN_EMAIL, password: ADMIN_PASSWORD, portal: 'superadmin' }),
    { headers: { 'Content-Type': 'application/json' }, tags: { endpoint: 'login' } },
  );
  loginDuration.add(res.timings.duration);
  requestCount.add(1);
  const ok = check(res, {
    'login: status 200': (r) => r.status === 200,
    'login: has token':  (r) => {
      try { return !!r.json('data.token'); } catch { return false; }
    },
  });
  if (!ok) errorRate.add(1);
  else errorRate.add(0);

  try { return res.json('data.token') || null; } catch { return null; }
}

function authHeaders() {
  return {
    'Content-Type':  'application/json',
    'Authorization': token ? `Bearer ${token}` : '',
  };
}

// ── Main VU loop ──────────────────────────────────────────────
export default function () {
  if (!token) token = login();
  if (!token) { sleep(1); return; }

  group('health', () => {
    const res = http.get(`${BASE_URL}/healthz`, { tags: { endpoint: 'health' } });
    healthDuration.add(res.timings.duration);
    requestCount.add(1);
    const ok = check(res, { 'healthz: 200': (r) => r.status === 200 });
    errorRate.add(ok ? 0 : 1);
  });

  sleep(0.5);

  group('employees', () => {
    const res = http.get(
      `${BASE_URL}/api/employees?page=1&pageSize=10`,
      { headers: authHeaders(), tags: { endpoint: 'employees-list' } },
    );
    employeeDuration.add(res.timings.duration);
    requestCount.add(1);
    const ok = check(res, {
      'employees: 200':         (r) => r.status === 200,
      'employees: has data key': (r) => {
        try { return r.json('data') !== undefined; } catch { return false; }
      },
    });
    errorRate.add(ok ? 0 : 1);
  });

  sleep(0.5);

  group('leave types', () => {
    const res = http.get(
      `${BASE_URL}/api/leave/types`,
      { headers: authHeaders(), tags: { endpoint: 'leave-types' } },
    );
    leaveDuration.add(res.timings.duration);
    requestCount.add(1);
    const ok = check(res, { 'leave-types: 200': (r) => r.status === 200 });
    errorRate.add(ok ? 0 : 1);
  });

  sleep(1);
}

// ── Pre-test: assert the stack is up ─────────────────────────
export function setup() {
  const res = http.get(`${BASE_URL}/healthz`);
  if (!check(res, { 'setup: healthz 200': (r) => r.status === 200 })) {
    throw new Error(
      `Stack is not healthy before smoke test. ` +
      `GET /healthz returned HTTP ${res.status}. Aborting.`,
    );
  }
  console.log(`[smoke-test] Stack healthy at ${BASE_URL}. Starting VUs.`);
  return {};
}

// ── Post-test summary ─────────────────────────────────────────
export function teardown(data) {
  console.log('[smoke-test] Complete. Check thresholds above for pass/fail.');
}
