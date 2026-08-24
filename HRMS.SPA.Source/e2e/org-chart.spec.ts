// Fixed: M5 — e2e tests for Org Chart page
import { test, expect } from '@playwright/test';

test.describe('Org Chart Page', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/org-chart');
  });

  test('should display the Org Chart page', async ({ page }) => {
    await expect(page.getByRole('heading', { name: /org chart/i })).toBeVisible();
  });

  test('sidebar should have an Org Chart link', async ({ page }) => {
    await page.goto('/dashboard');
    const link = page.getByRole('link', { name: /org chart/i });
    await expect(link).toBeVisible();
    await link.click();
    await expect(page).toHaveURL(/\/org-chart/);
  });
});
