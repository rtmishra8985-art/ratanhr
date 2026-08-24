// Fixed: M3 — e2e tests for MFA setup wizard in Settings
import { test, expect } from '@playwright/test';

test.describe('Settings — MFA Setup', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/settings');
  });

  test('should show MFA card', async ({ page }) => {
    await expect(page.getByRole('heading', { name: /two-factor authentication/i })).toBeVisible();
  });

  test('should show Set Up MFA button', async ({ page }) => {
    await expect(page.getByRole('button', { name: /set up mfa/i })).toBeVisible();
  });

  test('should show QR step after clicking Set Up MFA (mocked API)', async ({ page }) => {
    // Intercept the setup endpoint
    await page.route('**/api/auth/mfa/setup', (route) =>
      route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          data: {
            qrCodeUri: 'otpauth://totp/HRMS:test@example.com?secret=JBSWY3DPEHPK3PXP&issuer=HRMS',
            manualEntryKey: 'JBSWY3DPEHPK3PXP',
          },
          success: true,
        }),
      })
    );

    await page.getByRole('button', { name: /set up mfa/i }).click();
    await expect(page.getByText(/scan this qr code/i)).toBeVisible({ timeout: 5000 });
    await expect(page.getByText(/JBSWY3DPEHPK3PXP/)).toBeVisible();
    await expect(page.getByPlaceholder(/6-digit code/i)).toBeVisible();
  });
});
