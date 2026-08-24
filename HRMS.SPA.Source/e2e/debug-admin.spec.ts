import { test } from '@playwright/test';

test.use({ storageState: { cookies: [], origins: [] } });

test('debug admin dashboard render', async ({ page }) => {
  page.on('console', (msg) => {
    if (msg.text().includes('DEBUG')) console.log('[console]', msg.text());
  });
  page.on('response', (res) => {
    if (res.url().includes('/api/dashboard') || res.url().includes('/api/profile')) {
      console.log('[response]', res.status(), res.url());
    }
  });

  await page.goto('/login');
  await page.getByRole('tab', { name: 'Admin', exact: true }).click();
  await page.getByLabel(/email/i).fill('fixcheck.admin@test.com');
  await page.getByRole('textbox', { name: /password/i }).fill('FinalCheck@321');
  await page.getByRole('button', { name: /sign in/i }).click();
  await page.waitForURL((url) => !url.pathname.includes('/login'), { timeout: 15000 });
  await page.waitForTimeout(3000);
});
