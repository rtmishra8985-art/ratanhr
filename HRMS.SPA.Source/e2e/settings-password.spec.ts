// Fixed: S1 — e2e tests for Change Password card in Settings
import { test, expect } from '@playwright/test';

test.describe('Settings — Change Password', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/settings');
  });

  test('should show Change Password card', async ({ page }) => {
    await expect(page.getByRole('heading', { name: /change password/i })).toBeVisible();
  });

  test('should show the three password fields', async ({ page }) => {
    await expect(page.getByLabel(/current password/i)).toBeVisible();
    await expect(page.getByLabel(/new password/i)).toBeVisible();
    await expect(page.getByLabel(/confirm new password/i)).toBeVisible();
  });

  test('should show validation error when passwords do not match', async ({ page }) => {
    await page.getByLabel(/current password/i).fill('OldPass@1');
    await page.getByLabel(/^new password/i).fill('NewPass@1');
    await page.getByLabel(/confirm new password/i).fill('Different@1');
    await page.getByRole('button', { name: /change password/i }).click();
    await expect(page.getByText(/do not match/i)).toBeVisible();
  });

  test('should show validation error for weak new password', async ({ page }) => {
    await page.getByLabel(/current password/i).fill('OldPass@1');
    await page.getByLabel(/^new password/i).fill('short');
    await page.getByLabel(/confirm new password/i).fill('short');
    await page.getByRole('button', { name: /change password/i }).click();
    await expect(page.getByText(/at least 8/i)).toBeVisible();
  });
});
