// Fixed: B2 — e2e tests for Reports page
import { test, expect } from '@playwright/test';

test.describe('Reports Page', () => {
  test.beforeEach(async ({ page }) => {
    // Assume already authenticated via storageState fixture
    await page.goto('/reports');
  });

  test('should display the Reports page with 5 tabs', async ({ page }) => {
    await expect(page.getByRole('heading', { name: /reports/i })).toBeVisible();
    await expect(page.getByRole('tab', { name: /attendance/i })).toBeVisible();
    await expect(page.getByRole('tab', { name: /payroll/i })).toBeVisible();
    await expect(page.getByRole('tab', { name: /leave/i })).toBeVisible();
    await expect(page.getByRole('tab', { name: /employee/i })).toBeVisible();
    await expect(page.getByRole('tab', { name: /salary register/i })).toBeVisible();
  });

  test('should show date range pickers on each tab', async ({ page }) => {
    for (const tab of ['Attendance', 'Payroll', 'Leave']) {
      await page.getByRole('tab', { name: new RegExp(tab, 'i') }).click();
      await expect(page.getByLabel('From')).toBeVisible();
      await expect(page.getByLabel('To')).toBeVisible();
      await expect(page.getByRole('button', { name: /export excel/i })).toBeVisible();
    }
  });

  test('sidebar should have a Reports link', async ({ page }) => {
    await page.goto('/dashboard');
    const link = page.getByRole('link', { name: /reports/i });
    await expect(link).toBeVisible();
    await link.click();
    await expect(page).toHaveURL(/\/reports/);
  });
});
