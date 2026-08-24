/**
 * smoke.spec.ts — Fast smoke tests that verify every major route loads
 * without errors, a blank page, or a JavaScript crash.
 *
 * These run against an authenticated session (see global.setup.ts).
 * They are intentionally shallow — deep functional tests live in the
 * feature-specific spec files.
 */

import { test, expect } from '@playwright/test';

const ROUTES = [
  { path: '/dashboard',   heading: /dashboard/i },
  { path: '/employees',   heading: /employees/i },
  { path: '/attendance',  heading: /attendance/i },
  { path: '/leave',       heading: /leave/i },
  { path: '/payroll',     heading: /payroll/i },
  { path: '/recruitment', heading: /recruitment/i },
  { path: '/performance', heading: /performance/i },
  { path: '/assets',      heading: /asset/i },
  { path: '/helpdesk',    heading: /helpdesk/i },
  { path: '/settings',    heading: /settings/i },
  // RHR-013 FIX: this list predated several pages added later (per App.tsx's
  // own fix-history comments) and had no smoke coverage at all for them.
  { path: '/timesheet',    heading: /timesheet/i },
  { path: '/reports',      heading: /reports/i },
  { path: '/org-chart',    heading: /org chart/i },
  { path: '/training',     heading: /training/i },
  { path: '/expenses',     heading: /expense/i },
  { path: '/travel',       heading: /travel/i },
  { path: '/onboarding',   heading: /onboarding/i },
  { path: '/shifts',       heading: /shifts/i },
  { path: '/biometric',    heading: /biometric/i },
  { path: '/departments',  heading: /departments/i },
  { path: '/designations', heading: /designations/i },
  { path: '/holidays',     heading: /holiday/i },
  { path: '/sales',        heading: /sales/i },
  { path: '/analytics',    heading: /analytics/i },
  { path: '/audit-log',    heading: /audit log/i },
  { path: '/webhooks',     heading: /webhooks/i },
] as const;

for (const route of ROUTES) {
  test(`${route.path} — page loads without errors`, async ({ page }) => {
    const consoleErrors: string[] = [];
    page.on('console', (msg) => {
      if (msg.type() === 'error') consoleErrors.push(msg.text());
    });

    await page.goto(route.path);

    // Page title is visible
    const heading = page.getByRole('heading', { name: route.heading });
    await expect(heading).toBeVisible({ timeout: 15000 });

    // No JS errors in the console
    expect(
      consoleErrors.filter((e) => !e.includes('favicon')),
      `Console errors on ${route.path}: ${consoleErrors.join(', ')}`,
    ).toHaveLength(0);

    // No "Something went wrong" error boundary
    await expect(
      page.getByText(/something went wrong/i),
    ).toBeHidden();
  });
}

test('404 page renders for unknown route', async ({ page }) => {
  await page.goto('/this-does-not-exist-xyz');
  await expect(page.getByText(/not found|404/i)).toBeVisible({ timeout: 10000 });
});
