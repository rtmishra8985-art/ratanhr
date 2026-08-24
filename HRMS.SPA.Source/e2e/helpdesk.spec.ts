/**
 * helpdesk.spec.ts — Helpdesk / ticketing E2E tests.
 */

import { test, expect } from '@playwright/test';

test.describe('Helpdesk page', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/helpdesk');
    await page.waitForSelector('[role="table"], .min-h-\\[400px\\]', { timeout: 15000 });
  });

  test('heading is visible', async ({ page }) => {
    await expect(page.getByRole('heading', { name: /helpdesk/i })).toBeVisible();
  });

  test('"New Ticket" button is present', async ({ page }) => {
    await expect(page.getByRole('button', { name: /new ticket/i })).toBeVisible();
  });

  test('summary cards are rendered', async ({ page }) => {
    await expect(page.getByText(/open tickets|in progress|resolved/i).first()).toBeVisible({
      timeout: 10000,
    });
  });

  test('error state shows a retry button when API fails', async ({ page }) => {
    // Intercept the tickets API and return a 500
    await page.route('**/api/tickets**', (route) =>
      route.fulfill({ status: 500, body: JSON.stringify({ message: 'Internal server error' }) }),
    );
    await page.reload();
    await expect(page.getByRole('button', { name: /try again/i })).toBeVisible({ timeout: 10000 });
    await expect(page.getByText(/server error/i)).toBeVisible();
  });
});
