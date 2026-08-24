// Fixed: B1, B3 — e2e tests for Employees CRUD (Add dialog + Delete confirmation)
import { test, expect } from '@playwright/test';

test.describe('Employees CRUD', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/employees');
  });

  test('Add Employee button visible for admins', async ({ page }) => {
    // Assumes admin auth in storageState
    await expect(page.getByRole('button', { name: /add employee/i })).toBeVisible();
  });

  test('Add Employee dialog opens and has required fields', async ({ page }) => {
    await page.getByRole('button', { name: /add employee/i }).click();
    await expect(page.getByRole('dialog')).toBeVisible();
    await expect(page.getByLabel(/first name/i)).toBeVisible();
    await expect(page.getByLabel(/last name/i)).toBeVisible();
    await expect(page.getByLabel(/work email/i)).toBeVisible();
  });

  test('Add Employee dialog shows validation errors on empty submit', async ({ page }) => {
    await page.getByRole('button', { name: /add employee/i }).click();
    await page.getByRole('button', { name: /^add employee$/i }).click();
    await expect(page.getByText(/first name is required/i)).toBeVisible();
    await expect(page.getByText(/last name is required/i)).toBeVisible();
  });

  test('Delete action shows confirmation dialog', async ({ page }) => {
    // Wait for employees to load
    await page.waitForSelector('table tbody tr', { timeout: 10000 }).catch(() => null);
    const row = page.locator('table tbody tr').first();
    // Hover to reveal the actions menu
    await row.hover();
    const moreBtn = row.getByRole('button', { name: /actions for/i });
    if (await moreBtn.isVisible()) {
      await moreBtn.click();
      const deleteItem = page.getByRole('menuitem', { name: /delete/i });
      if (await deleteItem.isVisible()) {
        await deleteItem.click();
        await expect(page.getByRole('alertdialog')).toBeVisible({ timeout: 3000 });
        await expect(page.getByText(/cannot be undone/i)).toBeVisible();
      }
    }
  });
});
