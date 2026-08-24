/**
 * assets.spec.ts — Asset Management module E2E tests.
 * Covers asset listing, summary cards, search/filter,
 * assign-asset dialog, API error states, and accessibility.
 */

import { test, expect } from '@playwright/test';

test.describe('Asset Management page', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/assets');
    await page.waitForSelector(
      '[role="table"], [data-testid="empty-state"], .min-h-\\[400px\\]',
      { timeout: 15_000 },
    );
  });

  test('page heading is visible', async ({ page }) => {
    await expect(
      page.getByRole('heading', { name: /asset management/i }),
    ).toBeVisible();
  });

  test('summary stat cards render without crashing', async ({ page }) => {
    // e.g. "Total Assets", "Assigned", "Available" cards
    const card = page
      .getByText(/total assets|assigned|available|in repair/i)
      .first();
    await expect(card).toBeVisible({ timeout: 10_000 });
  });

  test('asset table or empty state is rendered', async ({ page }) => {
    const table = page.getByRole('table');
    const empty = page.getByText(/no assets|no records/i);
    await expect(table.or(empty).first()).toBeVisible({ timeout: 10_000 });
  });

  test('search input accepts text and does not crash', async ({ page }) => {
    const search = page
      .getByPlaceholder(/search assets|search/i)
      .first();
    const count = await search.count();
    if (count > 0) {
      await search.fill('Laptop');
      await page.waitForTimeout(600);
      await expect(page.getByText(/something went wrong/i)).toBeHidden();
    }
  });

  test('clearing search resets results without crash', async ({ page }) => {
    const search = page.getByPlaceholder(/search/i).first();
    const count = await search.count();
    if (count > 0) {
      await search.fill('zzz_nomatch_xyz');
      await page.waitForTimeout(600);
      await search.clear();
      await page.waitForTimeout(600);
      await expect(page.getByText(/something went wrong/i)).toBeHidden();
    }
  });

  test('"Add Asset" or "Assign" button is visible for admins', async ({ page }) => {
    const btn = page
      .getByRole('button', { name: /add asset|assign asset|new asset/i })
      .first();
    await expect(btn).toBeVisible({ timeout: 10_000 });
  });

  test('clicking "Add Asset" opens a dialog', async ({ page }) => {
    const btn = page
      .getByRole('button', { name: /add asset|assign asset|new asset/i })
      .first();
    await btn.click();
    await expect(page.getByRole('dialog')).toBeVisible({ timeout: 5_000 });
  });

  test('asset dialog shows required fields', async ({ page }) => {
    const btn = page
      .getByRole('button', { name: /add asset|assign asset|new asset/i })
      .first();
    await btn.click();
    await page.waitForSelector('[role="dialog"]', { timeout: 5_000 });
    // Name or serial field should be present
    const nameField = page
      .getByLabel(/asset name|name|serial/i)
      .or(page.getByPlaceholder(/name|serial/i))
      .first();
    await expect(nameField).toBeVisible({ timeout: 5_000 });
  });

  test('API error state shows retry button', async ({ page }) => {
    await page.route('**/api/assets**', (route) =>
      route.fulfill({ status: 500, body: JSON.stringify({ message: 'Server error' }) }),
    );
    await page.reload();
    await expect(
      page.getByRole('button', { name: /try again|retry/i }),
    ).toBeVisible({ timeout: 10_000 });
  });

  test('no JavaScript errors on load', async ({ page }) => {
    const errors: string[] = [];
    page.on('console', (msg) => {
      if (msg.type() === 'error') errors.push(msg.text());
    });
    await page.goto('/assets');
    await page.waitForTimeout(2_000);
    expect(errors.filter((e) => !e.includes('favicon'))).toHaveLength(0);
  });
});

test.describe('Asset Management page — accessibility', () => {
  test('skip-to-content link is attached', async ({ page }) => {
    await page.goto('/assets');
    await expect(
      page.getByRole('link', { name: /skip to main content/i }),
    ).toBeAttached();
  });

  test('main content area has correct id', async ({ page }) => {
    await page.goto('/assets');
    await expect(page.locator('#main-content')).toBeAttached();
  });
});
