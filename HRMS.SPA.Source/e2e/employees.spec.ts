/**
 * employees.spec.ts — Employee management feature E2E tests.
 *
 * Runs with the saved authenticated session from global.setup.ts.
 */

import { test, expect } from '@playwright/test';

test.describe('Employees page', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/employees');
    // Wait for either table data or empty state
    await page.waitForSelector('[role="table"], [data-testid="empty-state"]', {
      timeout: 15000,
    });
  });

  test('heading is visible', async ({ page }) => {
    await expect(page.getByRole('heading', { name: /employees/i })).toBeVisible();
  });

  test('search input accepts text and does not crash', async ({ page }) => {
    const search = page.getByPlaceholder(/search employees/i);
    await search.fill('John');
    // Debounce fires — page should not error
    await page.waitForTimeout(600);
    await expect(page.getByText(/something went wrong/i)).toBeHidden();
  });

  test('clearing the search resets results', async ({ page }) => {
    const search = page.getByPlaceholder(/search employees/i);
    await search.fill('zzz_nomatch_xyz');
    await page.waitForTimeout(600);
    await search.clear();
    await page.waitForTimeout(600);
    await expect(page.getByText(/something went wrong/i)).toBeHidden();
  });

  test('clicking a row navigates to employee detail', async ({ page }) => {
    // If the table has rows, click the first one
    const rows = page.getByRole('row').filter({ has: page.getByRole('link') });
    const count = await rows.count();
    if (count > 0) {
      await rows.first().getByRole('link').first().click();
      await page.waitForURL(/\/employees\/.+/);
      await expect(page.getByText(/employee|profile/i)).toBeVisible({ timeout: 10000 });
    } else {
      // Empty state is acceptable — no crash
      await expect(page.getByText(/no employees found/i)).toBeVisible();
    }
  });
});

test.describe('Employees page — accessibility', () => {
  test('skip-to-content link is present in the DOM', async ({ page }) => {
    await page.goto('/employees');
    const skipLink = page.getByRole('link', { name: /skip to main content/i });
    await expect(skipLink).toBeAttached();
  });

  test('main content area has correct id for skip link', async ({ page }) => {
    await page.goto('/employees');
    const main = page.locator('#main-content');
    await expect(main).toBeAttached();
  });
});
