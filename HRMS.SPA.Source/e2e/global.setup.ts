/**
 * global.setup.ts — Setup project for Playwright.
 *
 * This file is the entry point for the "setup" project defined in
 * playwright.config.ts.  It runs AFTER globalSetup (global-setup.ts) has
 * already logged in as all 6 roles and written their storage-state files.
 *
 * Purpose of this project:
 *   • Assert that every expected auth-state file exists and is non-empty.
 *   • Provide a hard dependency for chromium / firefox / Mobile Chrome so
 *     those projects cannot start before auth is confirmed.
 *   • Give a clear, actionable failure message when an auth file is missing.
 *
 * Run in isolation (to verify auth without running the full suite):
 *   npx playwright test --project=setup
 */

import { test as setup, expect } from '@playwright/test';
import * as path from 'path';
import * as fs from 'fs';

// ── Auth-state files expected from globalSetup ────────────────────────────

const AUTH_ROLES = [
  'superAdmin',
  'adminA',
  'employeeA',
  'adminB',
  'employeeB',
  'auditor',
] as const;

// ── Verify each file exists and contains a valid storageState ─────────────

for (const role of AUTH_ROLES) {
  const authFile = path.resolve('playwright/.auth', `${role}.json`);

  setup(`auth state present: ${role}`, async () => {
    // File must exist
    expect(
      fs.existsSync(authFile),
      `Auth file missing for "${role}": ${authFile}\n` +
      'Ensure globalSetup (global-setup.ts) ran without errors and the ' +
      'staging API is reachable at the configured E2E_BASE_URL.',
    ).toBe(true);

    // File must be non-empty and valid JSON
    const raw = fs.readFileSync(authFile, 'utf-8');
    expect(raw.length, `Auth file for "${role}" is empty`).toBeGreaterThan(0);

    let state: { cookies?: unknown[]; origins?: unknown[] };
    expect(() => { state = JSON.parse(raw); }, `Auth file for "${role}" is not valid JSON`).not.toThrow();

    // Must contain at least one cookie (the HttpOnly hrms_access_token)
    const cookieCount = (state!.cookies ?? []).length;
    expect(
      cookieCount,
      `Auth file for "${role}" has no cookies — login may have failed silently.\n` +
      'Check the globalSetup output for HTTP errors.',
    ).toBeGreaterThan(0);
  });
}

// ── Sanity: confirm the staging API is alive ──────────────────────────────

setup('staging API health check', async ({ request }) => {
  // ROOT CAUSE FIX (Phase 2 remediation): this previously requested
  // `/api/healthz`, which does not exist. `nginx/nginx.conf` only proxies an
  // explicit `location = /healthz { proxy_pass http://hrms_api/healthz; }` —
  // there is no `/api/healthz` route, either on the API (`Program.cs` maps
  // `/health`, `/healthz`, `/healthz/ready`, `/healthz/live`, never under
  // `/api/`) or in nginx. Every request to `/api/healthz` was therefore
  // guaranteed to 404, and the test only ever "passed" because 404 had been
  // added to the accepted status list — masking a health check that never
  // actually verified anything. Pointing at the real `/healthz` route and
  // requiring a genuine 200 (ASP.NET Core's default HealthCheckOptions map
  // Healthy/Degraded -> 200, Unhealthy -> 503) restores real verification:
  // a down or unhealthy API now fails this check instead of silently passing.
  const response = await request.get('/healthz', {
    timeout: 10_000,
    failOnStatusCode: false,
  });

  // A connection-refused error bubbles as a network exception and fails the test.
  expect(
    response.status(),
    `Staging API health check at ${process.env.E2E_BASE_URL ?? 'http://127.0.0.1:8082'}/healthz ` +
    `returned ${response.status()} (expected 200/healthy). ` +
    'Is the .NET API running and are all registered health checks (DB, Redis, etc.) passing?',
  ).toBe(200);
});
