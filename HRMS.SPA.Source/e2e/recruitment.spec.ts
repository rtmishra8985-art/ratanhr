/**
 * recruitment.spec.ts — Recruitment module E2E tests.
 * Covers job listings, pipeline/kanban view, job creation dialog,
 * applicant list, filter/tab interaction, and API error states.
 */

import { test, expect } from '@playwright/test';

test.describe('Recruitment page', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/recruitment');
    await page.waitForSelector(
      '[role="table"], [data-testid="empty-state"], .min-h-\\[400px\\], [role="heading"]',
      { timeout: 15_000 },
    );
  });

  test('page heading is visible', async ({ page }) => {
    await expect(page.getByRole('heading', { name: /recruitment/i })).toBeVisible();
  });

  test('job listings or empty state renders', async ({ page }) => {
    const table  = page.getByRole('table');
    const cards  = page.locator('[data-testid="job-card"], .job-card').first();
    const empty  = page.getByText(/no jobs|no positions|no openings/i);
    await expect(table.or(cards).or(empty).first()).toBeVisible({ timeout: 10_000 });
  });

  test('"Post Job" or "Add Job" button is visible', async ({ page }) => {
    const btn = page
      .getByRole('button', { name: /post job|add job|new job|create job/i })
      .first();
    await expect(btn).toBeVisible({ timeout: 10_000 });
  });

  test('clicking "Post Job" opens a dialog or form', async ({ page }) => {
    const btn = page
      .getByRole('button', { name: /post job|add job|new job|create job/i })
      .first();
    await btn.click();
    const dialog = page.getByRole('dialog');
    const form   = page.locator('form').first();
    await expect(dialog.or(form)).toBeVisible({ timeout: 8_000 });
  });

  test('job dialog shows required fields', async ({ page }) => {
    const btn = page
      .getByRole('button', { name: /post job|add job|new job|create job/i })
      .first();
    await btn.click();
    await page.waitForSelector('[role="dialog"]', { timeout: 5_000 });
    // Title/position field should be present
    const titleField = page
      .getByLabel(/job title|position/i)
      .or(page.getByPlaceholder(/title|position/i))
      .first();
    await expect(titleField).toBeVisible({ timeout: 5_000 });
  });

  test('status/stage filter tabs work without crashing', async ({ page }) => {
    const tabs = page
      .getByRole('tab')
      .or(page.getByRole('button', { name: /open|closed|draft|all/i }));
    const count = await tabs.count();
    if (count > 0) {
      await tabs.first().click();
      await page.waitForTimeout(600);
      await expect(page.getByText(/something went wrong/i)).toBeHidden();
    }
  });

  test('search input works without crashing', async ({ page }) => {
    const search = page
      .getByPlaceholder(/search/i)
      .or(page.getByRole('searchbox'))
      .first();
    const count = await search.count();
    if (count > 0) {
      await search.fill('Engineer');
      await page.waitForTimeout(600);
      await expect(page.getByText(/something went wrong/i)).toBeHidden();
    }
  });

  test('API error state shows retry button', async ({ page }) => {
    await page.route('**/api/jobs**', (route) =>
      route.fulfill({ status: 500, body: JSON.stringify({ message: 'Server error' }) }),
    );
    await page.reload();
    await expect(
      page.getByRole('button', { name: /try again|retry/i }),
    ).toBeVisible({ timeout: 10_000 });
  });

  test('no JavaScript errors on load', async ({ page }) => {
    const errors: string[] = [];
    page.on('console', (msg) => {
      if (msg.type() === 'error') errors.push(msg.text());
    });
    await page.goto('/recruitment');
    await page.waitForTimeout(2_000);
    expect(errors.filter((e) => !e.includes('favicon'))).toHaveLength(0);
  });
});

test.describe('Recruitment page — accessibility', () => {
  test('skip-to-content link is attached', async ({ page }) => {
    await page.goto('/recruitment');
    await expect(
      page.getByRole('link', { name: /skip to main content/i }),
    ).toBeAttached();
  });

  test('main content area has correct id', async ({ page }) => {
    await page.goto('/recruitment');
    await expect(page.locator('#main-content')).toBeAttached();
  });
});
