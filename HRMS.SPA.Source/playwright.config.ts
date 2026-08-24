/**
 * playwright.config.ts — RatanHR E2E test configuration
 *
 * All credentials are read from .env.e2e (loaded by globalSetup before any
 * project runs).  Never hard-code passwords here.
 *
 * Run targets:
 *   pnpm e2e                           — full suite (headless)
 *   pnpm e2e:headed                    — full suite (headed)
 *   pnpm e2e:ui                        — Playwright UI explorer
 *   npx playwright test --project=setup  — auth setup in isolation
 *   npx playwright test --project=chromium --project=firefox --project="Mobile Chrome"
 *
 * Auth state files (gitignored):
 *   playwright/.auth/superAdmin.json
 *   playwright/.auth/adminA.json
 *   playwright/.auth/employeeA.json
 *   playwright/.auth/adminB.json
 *   playwright/.auth/employeeB.json
 *   playwright/.auth/auditor.json
 */

import { defineConfig, devices } from '@playwright/test';

export default defineConfig({
  testDir: './e2e',

  /** Match all spec files but NOT the setup file */
  testMatch: '**/*.spec.ts',

  /**
   * globalSetup runs once before any project.
   * It loads .env.e2e, validates env vars, and logs in as all 6 roles,
   * saving each session to playwright/.auth/<role>.json.
   */
  globalSetup: './global-setup.ts',

  fullyParallel: true,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 2 : 0,
  workers: process.env.CI ? 1 : undefined,

  reporter: [
    ['list'],
    ['html', { outputFolder: 'playwright-report', open: 'never' }],
  ],

  use: {
    /** Staging API + SPA — set E2E_BASE_URL to override */
    baseURL: process.env.E2E_BASE_URL ?? 'http://127.0.0.1:3000',

    /**
     * Default session used by all test projects unless a spec overrides it.
     * Admin A can access all admin-level pages the smoke suite checks.
     */
    storageState: 'playwright/.auth/adminA.json',

    trace: 'on-first-retry',
    screenshot: 'only-on-failure',
    video: 'retain-on-failure',

    extraHTTPHeaders: {
      Accept: 'application/json',
    },
  },

  projects: [
    // ─── Setup project ────────────────────────────────────────────────────
    // globalSetup (global-setup.ts) does the actual API logins.
    // This project asserts the resulting auth-state files exist so that
    // the three browser projects can declare a hard `dependencies` on it.
    // Run in isolation with:  npx playwright test --project=setup
    {
      name: 'setup',
      testMatch: '**/global.setup.ts',
      use: {
        // No saved auth — setup verifies auth files that globalSetup wrote
        storageState: undefined,
      },
    },

    // ─── Desktop: Chromium ────────────────────────────────────────────────
    {
      name: 'chromium',
      use: {
        ...devices['Desktop Chrome'],
        storageState: 'playwright/.auth/adminA.json',
      },
      dependencies: ['setup'],
    },

    // ─── Desktop: Firefox ─────────────────────────────────────────────────
    {
      name: 'firefox',
      use: {
        ...devices['Desktop Firefox'],
        storageState: 'playwright/.auth/adminA.json',
      },
      dependencies: ['setup'],
    },

    // ─── Mobile: Chrome (Pixel 5) ─────────────────────────────────────────
    {
      name: 'Mobile Chrome',
      use: {
        ...devices['Pixel 5'],
        storageState: 'playwright/.auth/adminA.json',
      },
      dependencies: ['setup'],
    },
  ],
});
