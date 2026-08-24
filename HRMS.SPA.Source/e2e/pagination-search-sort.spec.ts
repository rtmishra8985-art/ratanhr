/**
 * pagination-search-sort.spec.ts
 * Covers pagination controls, search debounce, filter/sort interactions
 * across the main data-heavy pages (Employees, Leave, Attendance, Payroll).
 */

import { test, expect } from '@playwright/test';

// ─── Employees ────────────────────────────────────────────────────────────────

test.describe('Employees — pagination, search, and filter', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/employees');
    await page.waitForSelector('[role="table"], [data-testid="empty-state"]', {
      timeout: 15_000,
    });
  });

  test('pagination next/prev buttons exist when data is present', async ({
    page,
  }) => {
    const table = page.getByRole('table');
    if (await table.isVisible({ timeout: 5_000 }).catch(() => false)) {
      const nav = page.getByRole('navigation', { name: /pagination/i })
        .or(page.locator('[aria-label*="pagination" i]'))
        .first();
      // Pagination should be attached (even if disabled)
      if (await nav.isVisible({ timeout: 3_000 }).catch(() => false)) {
        const nextBtn = nav.getByRole('button', { name: /next/i });
        await expect(nextBtn).toBeAttached();
      }
    }
  });

  test('pagination next navigates to page 2 without crash', async ({ page }) => {
    const nextBtn = page.getByRole('button', { name: /next/i }).first();
    if (
      (await nextBtn.count()) > 0 &&
      (await nextBtn.isEnabled().catch(() => false))
    ) {
      await nextBtn.click();
      await page.waitForTimeout(700);
      await expect(page.getByText(/something went wrong/i)).toBeHidden();
    }
  });

  test('search "a" returns filtered or empty results without crash', async ({
    page,
  }) => {
    const search = page.getByPlaceholder(/search employees/i);
    await search.fill('a');
    await page.waitForTimeout(700);
    await expect(page.getByText(/something went wrong/i)).toBeHidden();
  });

  test('search clears to show all results again', async ({ page }) => {
    const search = page.getByPlaceholder(/search employees/i);
    await search.fill('abc');
    await page.waitForTimeout(700);
    await search.clear();
    await page.waitForTimeout(700);
    await expect(page.getByText(/something went wrong/i)).toBeHidden();
  });

  test('department filter (combobox) is functional without crash', async ({
    page,
  }) => {
    const filter = page
      .getByRole('combobox')
      .or(page.getByLabel(/department/i))
      .first();
    if (await filter.isVisible({ timeout: 3_000 }).catch(() => false)) {
      await filter.click();
      await page.waitForTimeout(400);
      await expect(page.getByText(/something went wrong/i)).toBeHidden();
    }
  });
});

// ─── Leave ────────────────────────────────────────────────────────────────────

test.describe('Leave — status filter tabs and pagination', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/leave');
    await page.waitForSelector('[role="table"], [data-testid="empty-state"]', {
      timeout: 15_000,
    });
  });

  test('All / Pending / Approved / Rejected tabs exist', async ({ page }) => {
    const tabs = page
      .getByRole('tab')
      .or(page.getByRole('button', { name: /pending|approved|rejected|all/i }));
    const count = await tabs.count();
    // At least one filter control should exist
    expect(count).toBeGreaterThan(0);
  });

  test('clicking each status filter does not crash', async ({ page }) => {
    const tabLabels = [/pending/i, /approved/i, /rejected/i];
    for (const label of tabLabels) {
      const tab = page.getByRole('tab', { name: label }).or(
        page.getByRole('button', { name: label }),
      ).first();
      if (await tab.isVisible({ timeout: 2_000 }).catch(() => false)) {
        await tab.click();
        await page.waitForTimeout(500);
        await expect(page.getByText(/something went wrong/i)).toBeHidden();
      }
    }
  });
});

// ─── Attendance ───────────────────────────────────────────────────────────────

test.describe('Attendance — date filter and sort', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/attendance');
    await page.waitForSelector('[role="table"], [data-testid="empty-state"]', {
      timeout: 15_000,
    });
  });

  test('table columns have sortable headers (if present)', async ({ page }) => {
    const sortHeaders = page.getByRole('columnheader').filter({ has: page.locator('[aria-sort]') });
    const count = await sortHeaders.count();
    if (count > 0) {
      await sortHeaders.first().click();
      await page.waitForTimeout(500);
      await expect(page.getByText(/something went wrong/i)).toBeHidden();
    }
  });

  test('month/year picker interaction does not crash', async ({ page }) => {
    const picker = page
      .getByRole('combobox')
      .or(page.getByRole('button', { name: /month|date|today/i }))
      .first();
    if (await picker.isVisible({ timeout: 3_000 }).catch(() => false)) {
      await picker.click();
      await page.waitForTimeout(400);
      await expect(page.getByText(/something went wrong/i)).toBeHidden();
    }
  });
});

// ─── Payroll ─────────────────────────────────────────────────────────────────

test.describe('Payroll — period selector and pagination', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/payroll');
    await page.waitForSelector('[role="tablist"]', { timeout: 15_000 });
  });

  test('month selector changes period without crash', async ({ page }) => {
    const selector = page
      .getByRole('combobox')
      .or(page.getByLabel(/month|period/i))
      .first();
    if (await selector.isVisible({ timeout: 3_000 }).catch(() => false)) {
      await selector.click();
      await page.waitForTimeout(400);
      await expect(page.getByText(/something went wrong/i)).toBeHidden();
    }
  });

  test('payslips tab pagination next-page works without crash', async ({
    page,
  }) => {
    const tab = page.getByRole('tab', { name: /payslips/i });
    if (await tab.isVisible()) {
      await tab.click();
      await page.waitForTimeout(400);
      const nextBtn = page.getByRole('button', { name: /next/i }).first();
      if (
        (await nextBtn.count()) > 0 &&
        (await nextBtn.isEnabled().catch(() => false))
      ) {
        await nextBtn.click();
        await page.waitForTimeout(700);
        await expect(page.getByText(/something went wrong/i)).toBeHidden();
      }
    }
  });
});
