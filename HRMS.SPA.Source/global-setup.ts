/**
 * global-setup.ts — Playwright globalSetup hook
 *
 * Runs once before any project (including the setup project).
 * Responsibilities:
 *   1. Load .env.e2e and fail fast if any required variable is missing.
 *   2. Create the playwright/.auth/ directory.
 *   3. Log in as each of the 6 staging E2E roles via POST /api/auth/login.
 *   4. Save each session's cookies + localStorage to playwright/.auth/<role>.json
 *      so that test projects can reuse them without logging in again.
 *
 * The API sets HttpOnly cookies (hrms_access_token, hrms_refresh_token).
 * Using context.request.post() captures those cookies into the browser
 * context's cookie jar; context.storageState() then serialises them.
 *
 * ── URL convention ─────────────────────────────────────────────────────────
 *   E2E_API_URL   → .NET API base URL  (e.g. http://127.0.0.1:8082)
 *                   Used HERE for POST /api/auth/login.
 *   E2E_BASE_URL  → SPA / browser base URL  (e.g. http://127.0.0.1:3000)
 *                   Used by playwright.config.ts as the browser baseURL.
 *
 * FIX (E2E-GLOBALSETUP-001): Previously used E2E_BASE_URL (frontend URL) for
 * API calls, causing HTTP 401 because the SPA does not expose /api/auth/login.
 * Now uses E2E_API_URL with fallback to the legacy default port 8082.
 *
 * ⚠️  This file MUST NOT import test fixtures (test, expect, page, etc.).
 *     It uses chromium directly from @playwright/test.
 */

import * as path from 'path';
import * as fs from 'fs';
import { chromium } from '@playwright/test';
import dotenv from 'dotenv';

// ── Types ──────────────────────────────────────────────────────────────────

interface RoleCredential {
  name: string;
  email: string;
  password: string;
}

// ── Helpers ────────────────────────────────────────────────────────────────

function requireEnv(key: string): string {
  const v = process.env[key];
  if (!v) throw new Error(`globalSetup: required env var "${key}" is not set. Did you create .env.e2e from .env.e2e.template?`);
  return v;
}

// ── Main ───────────────────────────────────────────────────────────────────

export default async function globalSetup(): Promise<void> {
  // 1. Load .env.e2e — must exist next to playwright.config.ts
  const envPath = path.resolve(process.cwd(), '.env.e2e');
  if (!fs.existsSync(envPath)) {
    throw new Error(
      `globalSetup: .env.e2e not found at ${envPath}\n` +
      'Copy .env.e2e.template to .env.e2e and fill in real staging values.\n' +
      'Add .env.e2e to .gitignore — never commit real credentials.',
    );
  }
  dotenv.config({ path: envPath });

  // 2. Validate all required variables up-front
  const REQUIRED_VARS = [
    'E2E_SUPERADMIN_EMAIL', 'E2E_SUPERADMIN_PASS',
    'E2E_ADMIN_A_EMAIL',    'E2E_ADMIN_A_PASS',
    'E2E_EMPLOYEE_A_EMAIL', 'E2E_EMPLOYEE_A_PASS',
    'E2E_ADMIN_B_EMAIL',    'E2E_ADMIN_B_PASS',
    'E2E_EMPLOYEE_B_EMAIL', 'E2E_EMPLOYEE_B_PASS',
    'E2E_AUDITOR_EMAIL',    'E2E_AUDITOR_PASS',
  ] as const;

  const missing = REQUIRED_VARS.filter(k => !process.env[k]);
  if (missing.length > 0) {
    throw new Error(
      `globalSetup: missing required env vars in .env.e2e:\n  ${missing.join('\n  ')}`,
    );
  }

  // FIX E2E-GLOBALSETUP-001: Use E2E_API_URL for API login calls.
  // E2E_BASE_URL is the FRONTEND/SPA URL (used by the browser for page navigation).
  // E2E_API_URL is the BACKEND API URL (used here for authentication requests).
  // Previously both were conflated, causing 401 because the SPA has no /api/auth/login.
  const apiURL = process.env.E2E_API_URL ?? 'http://127.0.0.1:8082';

  // 3. Create playwright/.auth/ directory
  const authDir = path.resolve(process.cwd(), 'playwright/.auth');
  fs.mkdirSync(authDir, { recursive: true });

  // 4. Role definitions
  const roles: RoleCredential[] = [
    { name: 'superAdmin', email: requireEnv('E2E_SUPERADMIN_EMAIL'), password: requireEnv('E2E_SUPERADMIN_PASS') },
    { name: 'adminA',     email: requireEnv('E2E_ADMIN_A_EMAIL'),    password: requireEnv('E2E_ADMIN_A_PASS')    },
    { name: 'employeeA',  email: requireEnv('E2E_EMPLOYEE_A_EMAIL'), password: requireEnv('E2E_EMPLOYEE_A_PASS') },
    { name: 'adminB',     email: requireEnv('E2E_ADMIN_B_EMAIL'),    password: requireEnv('E2E_ADMIN_B_PASS')    },
    { name: 'employeeB',  email: requireEnv('E2E_EMPLOYEE_B_EMAIL'), password: requireEnv('E2E_EMPLOYEE_B_PASS') },
    { name: 'auditor',    email: requireEnv('E2E_AUDITOR_EMAIL'),    password: requireEnv('E2E_AUDITOR_PASS')    },
  ];

  // 5. Log in as each role and save storage state
  console.log(`\n[globalSetup] Authenticating E2E roles against API: ${apiURL}`);

  for (const role of roles) {
    const stateFile = path.join(authDir, `${role.name}.json`);

    const browser = await chromium.launch();
    try {
      // baseURL here is the API URL — we are making direct API requests, not browsing the SPA.
      const context = await browser.newContext({ baseURL: apiURL });

      // POST /api/auth/login — the server sets HttpOnly cookies in the response.
      const response = await context.request.post('/api/auth/login', {
        headers: { 'Content-Type': 'application/json' },
        data: { email: role.email, password: role.password },
      });

      if (!response.ok()) {
        let detail = '';
        try { detail = await response.text(); } catch { /* ignore */ }
        throw new Error(
          `[globalSetup] Login FAILED for role "${role.name}" (${role.email})\n` +
          `  HTTP ${response.status()} ${response.statusText()}\n` +
          `  API URL: ${apiURL}\n` +
          `  Body: ${detail.slice(0, 300)}\n\n` +
          '  Possible causes:\n' +
          '  • The API is not running at ' + apiURL + ' (check E2E_API_URL in .env.e2e)\n' +
          '  • e2e_seed.sql has not been applied to the staging database\n' +
          '  • The password in .env.e2e does not match the BCrypt hash in the DB\n' +
          '  • The account has IsActive = 0 or MustChangePassword = 1\n' +
          '  • CORS is blocking the request (check Cors__AllowedOrigins in API config)',
        );
      }

      await context.storageState({ path: stateFile });
      console.log(`  ✓  ${role.name.padEnd(12)} → playwright/.auth/${role.name}.json`);
    } finally {
      await browser.close();
    }
  }

  console.log('[globalSetup] All 6 sessions saved. Auth setup complete.\n');
}
