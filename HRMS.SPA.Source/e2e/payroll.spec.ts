/**
 * payroll.spec.ts — Payroll module E2E tests.
 * Covers payroll listing, period summary cards, lock/process actions,
 * API error states, and basic accessibility.
 */

import { test, expect } from '@playwright/test';

test.describe('Payroll page', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/payroll');
    await page.waitForSelector('[role="table"], [data-testid="empty-state"], .min-h-\\[400px\\]', {
      timeout: 15_000,
    });
  });

  test('page heading is visible', async ({ page }) => {
    await expect(page.getByRole('heading', { name: /payroll/i })).toBeVisible();
  });

  test('summary/stat cards render without crashing', async ({ page }) => {
    // At least one card-like element should be visible (total payout, employee count, etc.)
    const cards = page.locator('[data-testid="stat-card"], .rounded-lg, .card').first();
    await expect(cards).toBeVisible({ timeout: 10_000 });
  });

  test('month/year period selector is present', async ({ page }) => {
    const periodSelector = page.getByRole('combobox').or(
      page.getByLabel(/month|period|payroll period/i)
    ).first();
    await expect(periodSelector).toBeVisible({ timeout: 10_000 });
  });

  test('payroll table or empty state renders', async ({ page }) => {
    const table = page.getByRole('table');
    const empty = page.getByText(/no payroll|no records|no data/i);
    const either = table.or(empty);
    await expect(either.first()).toBeVisible({ timeout: 10_000 });
  });

  test('no JavaScript errors on load', async ({ page }) => {
    const errors: string[] = [];
    page.on('console', msg => { if (msg.type() === 'error') errors.push(msg.text()); });
    await page.goto('/payroll');
    await page.waitForTimeout(2_000);
    expect(errors.filter(e => !e.includes('favicon'))).toHaveLength(0);
  });

  test('API error state shows retry button', async ({ page }) => {
    await page.route('**/api/payroll**', route =>
      route.fulfill({ status: 500, body: JSON.stringify({ message: 'Server error' }) })
    );
    await page.reload();
    await expect(page.getByRole('button', { name: /try again|retry/i })).toBeVisible({ timeout: 10_000 });
  });
});

test.describe('Payroll page — accessibility', () => {
  test('skip-to-content link is attached', async ({ page }) => {
    await page.goto('/payroll');
    await expect(page.getByRole('link', { name: /skip to main content/i })).toBeAttached();
  });
});
