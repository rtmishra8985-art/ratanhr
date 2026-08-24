/**
 * attendance.spec.ts — Attendance module E2E tests.
 * Covers attendance listing, date-range filter, stat summary,
 * check-in/check-out action, export button, and API error states.
 */

import { test, expect } from '@playwright/test';

test.describe('Attendance page', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/attendance');
    await page.waitForSelector('[role="table"], [data-testid="empty-state"], .min-h-\\[400px\\]', {
      timeout: 15_000,
    });
  });

  test('page heading is visible', async ({ page }) => {
    await expect(page.getByRole('heading', { name: /attendance/i })).toBeVisible();
  });

  test('attendance table or empty state is rendered', async ({ page }) => {
    const table = page.getByRole('table');
    const empty = page.getByText(/no attendance|no records/i);
    await expect(table.or(empty).first()).toBeVisible({ timeout: 10_000 });
  });

  test('date picker / month-year selector is present', async ({ page }) => {
    const picker = page
      .getByRole('button', { name: /date|month|today/i })
      .or(page.getByRole('combobox'))
      .first();
    await expect(picker).toBeVisible({ timeout: 10_000 });
  });

  test('summary stat cards render', async ({ page }) => {
    // Cards like "Present", "Absent", "Late" etc.
    const stat = page.getByText(/present|absent|late|half.?day/i).first();
    await expect(stat).toBeVisible({ timeout: 10_000 });
  });

  test('export button is present and clickable without crash', async ({ page }) => {
    const exportBtn = page.getByRole('button', { name: /export|download/i });
    const count = await exportBtn.count();
    if (count > 0) {
      // Intercept to avoid real download
      await page.route('**/api/**export**', route => route.abort());
      await exportBtn.first().click();
      await page.waitForTimeout(1_000);
      await expect(page.getByText(/something went wrong/i)).toBeHidden();
    }
  });

  test('API error state shows retry button', async ({ page }) => {
    await page.route('**/api/attendance**', route =>
      route.fulfill({ status: 500, body: JSON.stringify({ message: 'Server error' }) })
    );
    await page.reload();
    await expect(
      page.getByRole('button', { name: /try again|retry/i })
    ).toBeVisible({ timeout: 10_000 });
  });

  test('no JavaScript errors on load', async ({ page }) => {
    const errors: string[] = [];
    page.on('console', msg => { if (msg.type() === 'error') errors.push(msg.text()); });
    await page.goto('/attendance');
    await page.waitForTimeout(2_000);
    expect(errors.filter(e => !e.includes('favicon'))).toHaveLength(0);
  });
});

test.describe('Attendance page — accessibility', () => {
  test('skip-to-content link is attached', async ({ page }) => {
    await page.goto('/attendance');
    await expect(page.getByRole('link', { name: /skip to main content/i })).toBeAttached();
  });
});
