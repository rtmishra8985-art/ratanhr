/**
 * leave.spec.ts — Leave management module E2E tests.
 * Covers leave listing, balance cards, apply-leave flow,
 * filter/tab interaction, and API error states.
 */

import { test, expect } from '@playwright/test';

test.describe('Leave page', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/leave');
    await page.waitForSelector('[role="table"], [data-testid="empty-state"], .min-h-\\[400px\\]', {
      timeout: 15_000,
    });
  });

  test('page heading is visible', async ({ page }) => {
    await expect(page.getByRole('heading', { name: /leave/i })).toBeVisible();
  });

  test('leave balance cards are rendered', async ({ page }) => {
    // Balance summary cards (Casual Leave, Sick Leave, etc.) should appear
    const balance = page.getByText(/casual leave|sick leave|earned leave|leave balance/i).first();
    await expect(balance).toBeVisible({ timeout: 10_000 });
  });

  test('Apply Leave button is present', async ({ page }) => {
    const applyBtn = page.getByRole('button', { name: /apply leave|request leave/i });
    await expect(applyBtn).toBeVisible({ timeout: 10_000 });
  });

  test('clicking Apply Leave opens a dialog or navigates', async ({ page }) => {
    const applyBtn = page.getByRole('button', { name: /apply leave|request leave/i });
    await applyBtn.click();
    // Expect either a dialog or a form to appear
    const dialog = page.getByRole('dialog');
    const form   = page.getByRole('form').or(page.locator('form'));
    await expect(dialog.or(form).first()).toBeVisible({ timeout: 8_000 });
  });

  test('leave table or empty state renders', async ({ page }) => {
    const table = page.getByRole('table');
    const empty = page.getByText(/no leave|no records|no data/i);
    await expect(table.or(empty).first()).toBeVisible({ timeout: 10_000 });
  });

  test('status filter/tabs work without crashing', async ({ page }) => {
    // Try clicking a filter tab (All / Pending / Approved / Rejected)
    const tabs = page.getByRole('tab').or(
      page.getByRole('button', { name: /pending|approved|rejected|all/i })
    );
    const count = await tabs.count();
    if (count > 0) {
      await tabs.first().click();
      await page.waitForTimeout(600);
      await expect(page.getByText(/something went wrong/i)).toBeHidden();
    }
  });

  test('API error state shows retry button', async ({ page }) => {
    await page.route('**/api/leave**', route =>
      route.fulfill({ status: 500, body: JSON.stringify({ message: 'Server error' }) })
    );
    await page.reload();
    await expect(page.getByRole('button', { name: /try again|retry/i })).toBeVisible({ timeout: 10_000 });
  });

  test('no JavaScript errors on load', async ({ page }) => {
    const errors: string[] = [];
    page.on('console', msg => { if (msg.type() === 'error') errors.push(msg.text()); });
    await page.goto('/leave');
    await page.waitForTimeout(2_000);
    expect(errors.filter(e => !e.includes('favicon'))).toHaveLength(0);
  });
});

test.describe('Leave page — accessibility', () => {
  test('skip-to-content link is attached', async ({ page }) => {
    await page.goto('/leave');
    await expect(page.getByRole('link', { name: /skip to main content/i })).toBeAttached();
  });

  test('main content area has correct id', async ({ page }) => {
    await page.goto('/leave');
    await expect(page.locator('#main-content')).toBeAttached();
  });
});
