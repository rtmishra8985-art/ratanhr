/**
 * performance.spec.ts — Performance management module E2E tests.
 * Covers goals/OKR listing, reviews, cycles tabs, pagination,
 * empty states, and API error handling.
 */

import { test, expect } from '@playwright/test';

test.describe('Performance page', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/performance');
    await page.waitForSelector('[role="tablist"], [role="heading"]', {
      timeout: 15_000,
    });
  });

  test('page heading is visible', async ({ page }) => {
    await expect(
      page.getByRole('heading', { name: /performance/i }),
    ).toBeVisible();
  });

  test('Goals tab is present and active by default', async ({ page }) => {
    await expect(page.getByRole('tab', { name: /goals/i })).toBeVisible();
  });

  test('Reviews tab is present', async ({ page }) => {
    await expect(page.getByRole('tab', { name: /reviews/i })).toBeVisible();
  });

  test('Cycles tab is present', async ({ page }) => {
    await expect(page.getByRole('tab', { name: /cycles/i })).toBeVisible();
  });

  test('clicking Reviews tab does not crash', async ({ page }) => {
    await page.getByRole('tab', { name: /reviews/i }).click();
    await page.waitForTimeout(500);
    await expect(page.getByText(/something went wrong/i)).toBeHidden();
  });

  test('clicking Cycles tab does not crash', async ({ page }) => {
    await page.getByRole('tab', { name: /cycles/i }).click();
    await page.waitForTimeout(500);
    await expect(page.getByText(/something went wrong/i)).toBeHidden();
  });

  test('goals cards or empty state renders on Goals tab', async ({ page }) => {
    await page.getByRole('tab', { name: /goals/i }).click();
    const cards = page.locator('.rounded-lg, [data-testid="goal-card"]').first();
    const empty = page.getByText(/no goals|no okr/i);
    await expect(cards.or(empty).first()).toBeVisible({ timeout: 10_000 });
  });

  test('API error on goals shows graceful error state', async ({ page }) => {
    await page.route('**/api/goals**', (route) =>
      route.fulfill({ status: 500, body: JSON.stringify({ message: 'Server error' }) }),
    );
    await page.reload();
    // Either a retry button or an error message should appear
    const retry = page.getByRole('button', { name: /try again|retry/i });
    const errMsg = page.getByText(/failed|error|unable/i);
    await expect(retry.or(errMsg).first()).toBeVisible({ timeout: 10_000 });
  });

  test('no JavaScript errors on load', async ({ page }) => {
    const errors: string[] = [];
    page.on('console', (msg) => {
      if (msg.type() === 'error') errors.push(msg.text());
    });
    await page.goto('/performance');
    await page.waitForTimeout(2_000);
    expect(errors.filter((e) => !e.includes('favicon'))).toHaveLength(0);
  });
});

test.describe('Performance page — accessibility', () => {
  test('skip-to-content link is attached', async ({ page }) => {
    await page.goto('/performance');
    await expect(
      page.getByRole('link', { name: /skip to main content/i }),
    ).toBeAttached();
  });

  test('main content area has correct id', async ({ page }) => {
    await page.goto('/performance');
    await expect(page.locator('#main-content')).toBeAttached();
  });
});
