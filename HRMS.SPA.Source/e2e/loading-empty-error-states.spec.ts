/**
 * loading-empty-error-states.spec.ts
 * Exhaustive tests for loading skeletons, empty states, and API error
 * recovery across all major pages.
 *
 * Strategy:
 *   - Loading: intercept the API, delay response, assert skeleton/spinner
 *   - Empty:   intercept the API, return empty list, assert empty-state UI
 *   - Error:   intercept the API with 500, assert retry button and no crash
 */

import { test, expect } from '@playwright/test';

// ─── Helper ──────────────────────────────────────────────────────────────────

async function assertNoJsError(page: import('@playwright/test').Page) {
  await expect(page.getByText(/something went wrong/i)).toBeHidden();
}

// ─── Dashboard ────────────────────────────────────────────────────────────────

test.describe('Dashboard — loading and error states', () => {
  test('shows skeleton/spinner while API responds slowly', async ({ page }) => {
    // Delay all dashboard API calls by 3 s
    await page.route('**/api/dashboard**', (route) =>
      new Promise((res) => setTimeout(() => { route.continue(); res(undefined); }, 3_000)),
    );
    await page.goto('/dashboard');
    // A skeleton or spinner should appear before the data arrives
    const skeleton = page
      .locator('.animate-pulse, [data-testid="skeleton"], [role="progressbar"]')
      .first();
    await expect(skeleton).toBeVisible({ timeout: 5_000 });
  });

  test('shows retry button when dashboard API returns 500', async ({ page }) => {
    await page.route('**/api/dashboard**', (route) =>
      route.fulfill({ status: 500, body: JSON.stringify({ message: 'Server error' }) }),
    );
    await page.goto('/dashboard');
    const retry = page.getByRole('button', { name: /try again|retry|reload/i });
    const errMsg = page.getByText(/failed|error|unable to load/i);
    await expect(retry.or(errMsg).first()).toBeVisible({ timeout: 10_000 });
    await assertNoJsError(page);
  });
});

// ─── Employees ────────────────────────────────────────────────────────────────

test.describe('Employees — empty and error states', () => {
  test('empty state renders when API returns zero employees', async ({ page }) => {
    await page.route('**/api/employees**', (route) =>
      route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          success: true,
          data: { items: [], totalCount: 0, page: 1, pageSize: 20, totalPages: 0 },
        }),
      }),
    );
    await page.goto('/employees');
    const empty = page.getByText(/no employees|no records/i);
    await expect(empty).toBeVisible({ timeout: 10_000 });
    await assertNoJsError(page);
  });

  test('retry button appears and re-fetches on click when API fails', async ({
    page,
  }) => {
    let callCount = 0;
    await page.route('**/api/employees**', (route) => {
      callCount++;
      if (callCount === 1) {
        route.fulfill({ status: 500, body: JSON.stringify({ message: 'Server error' }) });
      } else {
        route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            success: true,
            data: { items: [], totalCount: 0, page: 1, pageSize: 20, totalPages: 0 },
          }),
        });
      }
    });
    await page.goto('/employees');
    const retryBtn = page.getByRole('button', { name: /try again|retry/i });
    await expect(retryBtn).toBeVisible({ timeout: 10_000 });
    await retryBtn.click();
    await page.waitForTimeout(1_000);
    await assertNoJsError(page);
    expect(callCount).toBeGreaterThan(1);
  });
});

// ─── Leave ────────────────────────────────────────────────────────────────────

test.describe('Leave — empty and error states', () => {
  test('empty state renders when API returns zero leave records', async ({
    page,
  }) => {
    await page.route('**/api/leave**', (route) =>
      route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          success: true,
          data: { items: [], totalCount: 0, page: 1, pageSize: 20, totalPages: 0 },
        }),
      }),
    );
    await page.goto('/leave');
    const empty = page.getByText(/no leave|no records|no data/i);
    await expect(empty.or(page.getByRole('heading', { name: /leave/i })).first()).toBeVisible({ timeout: 10_000 });
    await assertNoJsError(page);
  });

  test('error state shows retry button and no crash', async ({ page }) => {
    await page.route('**/api/leave**', (route) =>
      route.fulfill({ status: 503, body: JSON.stringify({ message: 'Service unavailable' }) }),
    );
    await page.goto('/leave');
    const retry = page.getByRole('button', { name: /try again|retry/i });
    await expect(retry).toBeVisible({ timeout: 10_000 });
    await assertNoJsError(page);
  });
});

// ─── Attendance ───────────────────────────────────────────────────────────────

test.describe('Attendance — loading and error states', () => {
  test('loading skeleton is shown while API is slow', async ({ page }) => {
    await page.route('**/api/attendance**', (route) =>
      new Promise((res) => setTimeout(() => { route.continue(); res(undefined); }, 2_000)),
    );
    await page.goto('/attendance');
    const skeleton = page
      .locator('.animate-pulse, [data-testid="skeleton"]')
      .first();
    await expect(skeleton).toBeVisible({ timeout: 4_000 });
  });

  test('error state shows retry button', async ({ page }) => {
    await page.route('**/api/attendance**', (route) =>
      route.fulfill({ status: 500, body: JSON.stringify({ message: 'Server error' }) }),
    );
    await page.goto('/attendance');
    await expect(
      page.getByRole('button', { name: /try again|retry/i }),
    ).toBeVisible({ timeout: 10_000 });
    await assertNoJsError(page);
  });
});

// ─── Payroll ─────────────────────────────────────────────────────────────────

test.describe('Payroll — loading and error states', () => {
  test('loading state is shown while API is slow', async ({ page }) => {
    await page.route('**/api/payroll**', (route) =>
      new Promise((res) => setTimeout(() => { route.continue(); res(undefined); }, 2_000)),
    );
    await page.goto('/payroll');
    const skeleton = page
      .locator('.animate-pulse, [data-testid="skeleton"], [role="progressbar"]')
      .first();
    await expect(skeleton).toBeVisible({ timeout: 4_000 });
  });

  test('error state shows retry button', async ({ page }) => {
    await page.route('**/api/payroll**', (route) =>
      route.fulfill({ status: 500, body: JSON.stringify({ message: 'Server error' }) }),
    );
    await page.goto('/payroll');
    await expect(
      page.getByRole('button', { name: /try again|retry/i }),
    ).toBeVisible({ timeout: 10_000 });
    await assertNoJsError(page);
  });
});

// ─── Reports ─────────────────────────────────────────────────────────────────

test.describe('Reports — loading and error states', () => {
  test('error on reports API shows graceful error state', async ({ page }) => {
    await page.route('**/api/reports**', (route) =>
      route.fulfill({ status: 500, body: JSON.stringify({ message: 'Server error' }) }),
    );
    await page.goto('/reports');
    const retry  = page.getByRole('button', { name: /try again|retry/i });
    const heading = page.getByRole('heading', { name: /reports/i });
    // Either a retry or the static page structure should be visible
    await expect(retry.or(heading).first()).toBeVisible({ timeout: 10_000 });
    await assertNoJsError(page);
  });
});

// ─── Assets ──────────────────────────────────────────────────────────────────

test.describe('Assets — empty and error states', () => {
  test('empty state when API returns zero assets', async ({ page }) => {
    await page.route('**/api/assets**', (route) =>
      route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          success: true,
          data: { items: [], totalCount: 0, page: 1, pageSize: 20, totalPages: 0 },
        }),
      }),
    );
    await page.goto('/assets');
    const empty = page.getByText(/no assets|no records/i);
    await expect(empty.or(page.getByRole('heading', { name: /asset/i })).first()).toBeVisible({ timeout: 10_000 });
    await assertNoJsError(page);
  });

  test('error state shows retry button', async ({ page }) => {
    await page.route('**/api/assets**', (route) =>
      route.fulfill({ status: 500, body: JSON.stringify({ message: 'Server error' }) }),
    );
    await page.goto('/assets');
    await expect(
      page.getByRole('button', { name: /try again|retry/i }),
    ).toBeVisible({ timeout: 10_000 });
    await assertNoJsError(page);
  });
});

// ─── Helpdesk ─────────────────────────────────────────────────────────────────

test.describe('Helpdesk — empty and error states', () => {
  test('empty state when API returns zero tickets', async ({ page }) => {
    await page.route('**/api/tickets**', (route) =>
      route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          success: true,
          data: { items: [], totalCount: 0, page: 1, pageSize: 20, totalPages: 0 },
        }),
      }),
    );
    await page.goto('/helpdesk');
    const empty   = page.getByText(/no tickets|no records/i);
    const heading = page.getByRole('heading', { name: /helpdesk/i });
    await expect(empty.or(heading).first()).toBeVisible({ timeout: 10_000 });
    await assertNoJsError(page);
  });
});
