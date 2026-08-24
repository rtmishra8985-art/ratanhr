// Fixed: M2 — e2e tests for Expenses page
import { test, expect } from '@playwright/test';

test.describe('Expenses Page', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/expenses');
  });

  test('should display the Expenses page', async ({ page }) => {
    await expect(page.getByRole('heading', { name: /expense claims/i })).toBeVisible();
    await expect(page.getByRole('tab', { name: /my claims/i })).toBeVisible();
  });

  test('should show Submit Expense button', async ({ page }) => {
    await expect(page.getByRole('button', { name: /submit expense/i })).toBeVisible();
  });

  test('Submit Expense dialog should open', async ({ page }) => {
    await page.getByRole('button', { name: /submit expense/i }).click();
    await expect(page.getByRole('dialog')).toBeVisible();
    await expect(page.getByRole('heading', { name: /submit expense claim/i })).toBeVisible();
    await expect(page.getByLabel(/title/i)).toBeVisible();
    await expect(page.getByLabel(/amount/i)).toBeVisible();
  });

  test('sidebar should have an Expenses link', async ({ page }) => {
    await page.goto('/dashboard');
    const link = page.getByRole('link', { name: /expenses/i });
    await expect(link).toBeVisible();
    await link.click();
    await expect(page).toHaveURL(/\/expenses/);
  });
});
