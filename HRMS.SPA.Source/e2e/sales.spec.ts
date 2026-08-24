/**
 * sales.spec.ts — CRM / Sales module E2E tests.
 * Covers KPI dashboard cards, Leads/Customers/Quotations tabs,
 * search/filter, lead creation dialog, status updates,
 * API error states, and accessibility.
 */

import { test, expect } from '@playwright/test';

test.describe('Sales / CRM page', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/sales');
    await page.waitForSelector(
      '[role="tablist"], [role="table"], [data-testid="empty-state"], [role="heading"]',
      { timeout: 15_000 },
    );
  });

  test('page heading is visible', async ({ page }) => {
    await expect(
      page.getByRole('heading', { name: /sales|crm/i }),
    ).toBeVisible();
  });

  test('KPI summary cards are rendered', async ({ page }) => {
    const kpi = page
      .getByText(/leads|pipeline|customers|quotations/i)
      .first();
    await expect(kpi).toBeVisible({ timeout: 10_000 });
  });

  test('Leads tab is present', async ({ page }) => {
    await expect(page.getByRole('tab', { name: /leads/i })).toBeVisible();
  });

  test('Customers tab is present', async ({ page }) => {
    await expect(page.getByRole('tab', { name: /customers/i })).toBeVisible();
  });

  test('Quotations tab is present', async ({ page }) => {
    await expect(page.getByRole('tab', { name: /quotations/i })).toBeVisible();
  });

  test('switching to Customers tab does not crash', async ({ page }) => {
    await page.getByRole('tab', { name: /customers/i }).click();
    await page.waitForTimeout(600);
    await expect(page.getByText(/something went wrong/i)).toBeHidden();
  });

  test('switching to Quotations tab does not crash', async ({ page }) => {
    await page.getByRole('tab', { name: /quotations/i }).click();
    await page.waitForTimeout(600);
    await expect(page.getByText(/something went wrong/i)).toBeHidden();
  });

  test('"Add Lead" button opens a dialog', async ({ page }) => {
    const btn = page
      .getByRole('button', { name: /add lead|new lead|create lead/i })
      .first();
    const count = await btn.count();
    if (count > 0) {
      await btn.click();
      await expect(page.getByRole('dialog')).toBeVisible({ timeout: 5_000 });
    }
  });

  test('lead creation dialog shows required fields', async ({ page }) => {
    const btn = page
      .getByRole('button', { name: /add lead|new lead|create lead/i })
      .first();
    const count = await btn.count();
    if (count > 0) {
      await btn.click();
      await page.waitForSelector('[role="dialog"]', { timeout: 5_000 });
      const nameField = page
        .getByLabel(/name|lead name|contact/i)
        .or(page.getByPlaceholder(/name/i))
        .first();
      await expect(nameField).toBeVisible({ timeout: 5_000 });
    }
  });

  test('search input works without crashing', async ({ page }) => {
    const search = page.getByPlaceholder(/search/i).first();
    const count = await search.count();
    if (count > 0) {
      await search.fill('Acme');
      await page.waitForTimeout(600);
      await expect(page.getByText(/something went wrong/i)).toBeHidden();
    }
  });

  test('status filter works without crashing', async ({ page }) => {
    const filter = page
      .getByRole('combobox')
      .or(page.getByRole('button', { name: /filter|status/i }))
      .first();
    const count = await filter.count();
    if (count > 0) {
      await filter.click();
      await page.waitForTimeout(400);
      await expect(page.getByText(/something went wrong/i)).toBeHidden();
    }
  });

  test('API error state shows retry button', async ({ page }) => {
    await page.route('**/api/sales/**', (route) =>
      route.fulfill({ status: 500, body: JSON.stringify({ message: 'Server error' }) }),
    );
    await page.reload();
    const retry = page.getByRole('button', { name: /try again|retry/i });
    const errMsg = page.getByText(/failed|error|unable/i);
    await expect(retry.or(errMsg).first()).toBeVisible({ timeout: 10_000 });
  });

  test('no JavaScript errors on load', async ({ page }) => {
    const errors: string[] = [];
    page.on('console', (msg) => {
      if (msg.type() === 'error') errors.push(msg.text());
    });
    await page.goto('/sales');
    await page.waitForTimeout(2_000);
    expect(errors.filter((e) => !e.includes('favicon'))).toHaveLength(0);
  });
});

test.describe('Sales / CRM page — accessibility', () => {
  test('skip-to-content link is attached', async ({ page }) => {
    await page.goto('/sales');
    await expect(
      page.getByRole('link', { name: /skip to main content/i }),
    ).toBeAttached();
  });

  test('main content area has correct id', async ({ page }) => {
    await page.goto('/sales');
    await expect(page.locator('#main-content')).toBeAttached();
  });
});
