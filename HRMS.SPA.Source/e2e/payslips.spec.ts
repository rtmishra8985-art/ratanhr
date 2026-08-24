/**
 * payslips.spec.ts — Payslips tab within the Payroll page E2E tests.
 * The payslips are rendered as a tab inside /payroll.
 * Covers listing, download action, pagination, empty state,
 * and API error recovery.
 */

import { test, expect } from '@playwright/test';

test.describe('Payslips tab (within Payroll)', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/payroll');
    await page.waitForSelector('[role="tablist"]', { timeout: 15_000 });
    // Navigate to Payslips tab
    const payslipsTab = page.getByRole('tab', { name: /payslips/i });
    await expect(payslipsTab).toBeVisible({ timeout: 10_000 });
    await payslipsTab.click();
    await page.waitForTimeout(500);
  });

  test('Payslips tab is present and clickable', async ({ page }) => {
    await expect(page.getByRole('tab', { name: /payslips/i })).toBeVisible();
  });

  test('payslips table or empty state renders', async ({ page }) => {
    const table = page.getByRole('table');
    const empty = page.getByText(/no payslips|no records|process payroll/i);
    await expect(table.or(empty).first()).toBeVisible({ timeout: 10_000 });
  });

  test('download button is present per payslip row (when data exists)', async ({
    page,
  }) => {
    const rows = page.getByRole('row').filter({ hasText: /download|pdf/i });
    const count = await rows.count();
    if (count > 0) {
      const downloadBtn = rows
        .first()
        .getByRole('button', { name: /download|pdf/i });
      await expect(downloadBtn).toBeVisible();
    }
  });

  test('intercepted download request does not crash the page', async ({
    page,
  }) => {
    // Intercept download requests so the test stays self-contained
    await page.route('**/api/payslips/**download**', (route) =>
      route.fulfill({
        status: 200,
        contentType: 'application/pdf',
        body: Buffer.from('%PDF-1.4'),
        headers: {
          'Content-Disposition': 'attachment; filename="payslip.pdf"',
        },
      }),
    );
    const downloadBtn = page
      .getByRole('button', { name: /download|pdf/i })
      .first();
    const btnCount = await downloadBtn.count();
    if (btnCount > 0) {
      await downloadBtn.click();
      await page.waitForTimeout(1_000);
      await expect(page.getByText(/something went wrong/i)).toBeHidden();
    }
  });

  test('pagination controls change page without crash (when multiple pages)', async ({
    page,
  }) => {
    const nextBtn = page.getByRole('button', { name: /next/i }).first();
    const count = await nextBtn.count();
    if (count > 0 && (await nextBtn.isEnabled())) {
      await nextBtn.click();
      await page.waitForTimeout(600);
      await expect(page.getByText(/something went wrong/i)).toBeHidden();
    }
  });

  test('API error on payslips shows retry button', async ({ page }) => {
    await page.route('**/api/payslips**', (route) =>
      route.fulfill({ status: 500, body: JSON.stringify({ message: 'Server error' }) }),
    );
    await page.reload();
    // Navigate back to the tab after reload
    await page.waitForSelector('[role="tablist"]', { timeout: 10_000 });
    const tab = page.getByRole('tab', { name: /payslips/i });
    if (await tab.isVisible()) {
      await tab.click();
    }
    await expect(
      page.getByRole('button', { name: /try again|retry/i }),
    ).toBeVisible({ timeout: 10_000 });
  });
});

test.describe('Payslips — accessibility', () => {
  test('skip-to-content link is attached', async ({ page }) => {
    await page.goto('/payroll');
    await expect(
      page.getByRole('link', { name: /skip to main content/i }),
    ).toBeAttached();
  });
});
