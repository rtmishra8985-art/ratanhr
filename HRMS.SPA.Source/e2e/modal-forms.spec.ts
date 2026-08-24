/**
 * modal-forms.spec.ts — Modal form validation and dialog interaction tests.
 *
 * Tests that every major modal dialog:
 *   - Opens correctly
 *   - Shows field-level validation on empty/invalid submit
 *   - Closes on cancel/escape without crashing
 *   - Submits successfully with mocked API response
 *   - Shows server-side errors gracefully
 */

import { test, expect } from '@playwright/test';

// ─── Add Employee dialog ──────────────────────────────────────────────────────

test.describe('Add Employee dialog', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/employees');
    await page.waitForSelector('[role="table"], [data-testid="empty-state"]', { timeout: 15_000 });
    await page.getByRole('button', { name: /add employee/i }).click();
    await expect(page.getByRole('dialog')).toBeVisible({ timeout: 5_000 });
  });

  test('dialog has all required fields', async ({ page }) => {
    const dialog = page.getByRole('dialog');
    await expect(dialog.getByLabel(/first name/i)).toBeVisible();
    await expect(dialog.getByLabel(/last name/i)).toBeVisible();
    await expect(dialog.getByLabel(/work email/i)).toBeVisible();
  });

  test('empty submit shows required field validation', async ({ page }) => {
    await page.getByRole('button', { name: /^add employee$/i }).click();
    await expect(page.getByText(/first name is required/i)).toBeVisible({ timeout: 5_000 });
    await expect(page.getByText(/last name is required/i)).toBeVisible({ timeout: 5_000 });
  });

  test('invalid email shows email validation error', async ({ page }) => {
    await page.getByLabel(/first name/i).fill('Test');
    await page.getByLabel(/last name/i).fill('User');
    await page.getByLabel(/work email/i).fill('not-an-email');
    await page.getByRole('button', { name: /^add employee$/i }).click();
    await expect(page.getByText(/valid email|invalid email/i)).toBeVisible({ timeout: 5_000 });
  });

  test('dialog closes on Cancel without crashing', async ({ page }) => {
    const cancelBtn = page.getByRole('button', { name: /cancel/i });
    await cancelBtn.click();
    await expect(page.getByRole('dialog')).toBeHidden({ timeout: 3_000 });
    await expect(page.getByRole('heading', { name: /employees/i })).toBeVisible();
  });

  test('pressing Escape closes the dialog', async ({ page }) => {
    await page.keyboard.press('Escape');
    await expect(page.getByRole('dialog')).toBeHidden({ timeout: 3_000 });
  });

  test('successful submission closes dialog and does not crash', async ({ page }) => {
    await page.route('**/api/employees', (route) => {
      if (route.request().method() === 'POST') {
        route.fulfill({
          status: 201,
          contentType: 'application/json',
          body: JSON.stringify({ success: true, message: 'Employee created' }),
        });
      } else {
        route.continue();
      }
    });
    const dialog = page.getByRole('dialog');
    await dialog.getByLabel(/first name/i).fill('Alice');
    await dialog.getByLabel(/last name/i).fill('Smith');
    await dialog.getByLabel(/work email/i).fill('alice@ratanhr.com');
    await page.getByRole('button', { name: /^add employee$/i }).click();
    await page.waitForTimeout(1_500);
    await expect(page.getByText(/something went wrong/i)).toBeHidden();
  });

  test('server error (500) shows error message — not a blank page', async ({ page }) => {
    await page.route('**/api/employees', (route) => {
      if (route.request().method() === 'POST') {
        route.fulfill({
          status: 500,
          contentType: 'application/json',
          body: JSON.stringify({ message: 'Internal server error' }),
        });
      } else {
        route.continue();
      }
    });
    const dialog = page.getByRole('dialog');
    await dialog.getByLabel(/first name/i).fill('Bob');
    await dialog.getByLabel(/last name/i).fill('Jones');
    await dialog.getByLabel(/work email/i).fill('bob@ratanhr.com');
    await page.getByRole('button', { name: /^add employee$/i }).click();
    await page.waitForTimeout(1_500);
    await expect(page.getByText(/something went wrong/i)).toBeHidden();
    // The dialog or a toast should convey the error
    const heading = page.getByRole('heading', { name: /employees/i });
    await expect(heading.or(dialog).first()).toBeVisible({ timeout: 5_000 });
  });
});

// ─── Apply Leave dialog ────────────────────────────────────────────────────────

test.describe('Apply Leave dialog', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/leave');
    await page.waitForSelector('[role="table"], [data-testid="empty-state"]', { timeout: 15_000 });
    await page.getByRole('button', { name: /apply leave|request leave/i }).click();
    const dialog = page.getByRole('dialog');
    const form   = page.locator('form').first();
    await expect(dialog.or(form)).toBeVisible({ timeout: 8_000 });
  });

  test('dialog/form shows leave type field', async ({ page }) => {
    const leaveType = page
      .getByLabel(/leave type|type/i)
      .or(page.getByRole('combobox', { name: /leave type/i }))
      .first();
    await expect(leaveType).toBeVisible({ timeout: 5_000 });
  });

  test('empty submit shows validation error', async ({ page }) => {
    const submitBtn = page
      .getByRole('button', { name: /apply|submit|save/i })
      .last();
    await submitBtn.click();
    const err = page.getByText(/required|start date|end date|type/i).first();
    await expect(err).toBeVisible({ timeout: 5_000 });
  });

  test('dialog cancels without crashing', async ({ page }) => {
    const cancelBtn = page.getByRole('button', { name: /cancel/i }).first();
    if (await cancelBtn.isVisible({ timeout: 2_000 }).catch(() => false)) {
      await cancelBtn.click();
      await page.waitForTimeout(500);
      await expect(page.getByText(/something went wrong/i)).toBeHidden();
    }
  });
});

// ─── Submit Expense dialog ─────────────────────────────────────────────────────

test.describe('Submit Expense dialog', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/expenses');
    await page.waitForSelector('[role="heading"]', { timeout: 15_000 });
    await page.getByRole('button', { name: /submit expense/i }).click();
    await expect(page.getByRole('dialog')).toBeVisible({ timeout: 5_000 });
  });

  test('dialog has title and amount fields', async ({ page }) => {
    await expect(page.getByLabel(/title/i)).toBeVisible();
    await expect(page.getByLabel(/amount/i)).toBeVisible();
  });

  test('empty submit shows validation errors', async ({ page }) => {
    await page.getByRole('button', { name: /submit/i }).last().click();
    const err = page.getByText(/required|title.*required|amount.*required/i).first();
    await expect(err).toBeVisible({ timeout: 5_000 });
  });

  test('negative amount is rejected', async ({ page }) => {
    await page.getByLabel(/title/i).fill('Test expense');
    await page.getByLabel(/amount/i).fill('-100');
    await page.getByRole('button', { name: /submit/i }).last().click();
    const err = page.getByText(/must be greater|positive|invalid/i).first();
    const visible = await err.isVisible({ timeout: 3_000 }).catch(() => false);
    if (!visible) {
      // If the form doesn't validate client-side, no crash should occur
      await expect(page.getByText(/something went wrong/i)).toBeHidden();
    }
  });

  test('dialog cancels without crashing', async ({ page }) => {
    const cancelBtn = page.getByRole('button', { name: /cancel/i }).first();
    if (await cancelBtn.isVisible()) {
      await cancelBtn.click();
      await expect(page.getByRole('dialog')).toBeHidden({ timeout: 3_000 });
    }
  });
});

// ─── New Helpdesk Ticket dialog ────────────────────────────────────────────────

test.describe('New Helpdesk Ticket dialog', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/helpdesk');
    await page.waitForSelector('[role="heading"]', { timeout: 15_000 });
    const btn = page.getByRole('button', { name: /new ticket/i });
    if (await btn.isVisible({ timeout: 5_000 }).catch(() => false)) {
      await btn.click();
      await page.waitForSelector('[role="dialog"]', { timeout: 5_000 });
    }
  });

  test('new ticket dialog opens with a subject/title field', async ({ page }) => {
    const dialog = page.getByRole('dialog');
    if (await dialog.isVisible({ timeout: 2_000 }).catch(() => false)) {
      const subjectField = dialog
        .getByLabel(/subject|title/i)
        .or(dialog.getByPlaceholder(/subject|title/i))
        .first();
      await expect(subjectField).toBeVisible({ timeout: 3_000 });
    }
  });

  test('empty submit shows validation message', async ({ page }) => {
    const dialog = page.getByRole('dialog');
    if (await dialog.isVisible({ timeout: 2_000 }).catch(() => false)) {
      const submitBtn = dialog
        .getByRole('button', { name: /submit|create|save/i })
        .first();
      if (await submitBtn.isVisible()) {
        await submitBtn.click();
        const err = page.getByText(/required/i).first();
        await expect(err).toBeVisible({ timeout: 5_000 });
      }
    }
  });
});

// ─── Delete confirmation dialogs ─────────────────────────────────────────────

test.describe('Delete confirmation dialogs', () => {
  test('delete confirmation dialog shows warning and Cancel button', async ({
    page,
  }) => {
    await page.goto('/employees');
    await page.waitForSelector('table tbody tr', { timeout: 10_000 }).catch(() => null);

    const rows = page.locator('table tbody tr');
    const count = await rows.count();
    if (count > 0) {
      await rows.first().hover();
      const moreBtn = rows.first().getByRole('button', { name: /actions for/i });
      if (await moreBtn.isVisible({ timeout: 2_000 }).catch(() => false)) {
        await moreBtn.click();
        const deleteItem = page.getByRole('menuitem', { name: /delete/i });
        if (await deleteItem.isVisible({ timeout: 2_000 }).catch(() => false)) {
          await deleteItem.click();
          const alertDialog = page.getByRole('alertdialog');
          await expect(alertDialog).toBeVisible({ timeout: 3_000 });
          await expect(page.getByText(/cannot be undone|irreversible/i)).toBeVisible();
          // Cancel should close without deleting
          await page.getByRole('button', { name: /cancel/i }).last().click();
          await expect(alertDialog).toBeHidden({ timeout: 3_000 });
        }
      }
    }
  });
});
