import { test, expect } from '@playwright/test';

/**
 * Backend-free smoke tests. All API traffic is mocked so the suite is
 * deterministic and runnable without docker-compose.e2e.yml.
 */
test.beforeEach(async ({ page }) => {
  await page.route('**/api/**', (route) =>
    route.fulfill({
      status: 401,
      contentType: 'application/json',
      body: JSON.stringify({ message: 'Unauthorized (mocked offline smoke)' }),
    }),
  );
});

test('index.html is served and the app root mounts', async ({ page }) => {
  const response = await page.goto('/', { waitUntil: 'domcontentloaded' });
  expect(response?.status()).toBe(200);
  await expect(page.locator('#root')).toBeAttached();
});

test('unauthenticated visit resolves to the login screen', async ({ page }) => {
  await page.goto('/', { waitUntil: 'domcontentloaded' });
  await page.waitForLoadState('networkidle');
  await expect(page.locator('#root')).not.toBeEmpty();
});

test('SPA fallback serves the app for deep links', async ({ page }) => {
  const response = await page.goto('/employees', { waitUntil: 'domcontentloaded' });
  expect(response?.status()).toBe(200);
  await expect(page.locator('#root')).toBeAttached();
});

test('no uncaught page errors during bootstrap', async ({ page }) => {
  const errors: string[] = [];
  page.on('pageerror', (e) => errors.push(e.message));
  await page.goto('/', { waitUntil: 'domcontentloaded' });
  await page.waitForLoadState('networkidle');
  expect(errors).toEqual([]);
});
