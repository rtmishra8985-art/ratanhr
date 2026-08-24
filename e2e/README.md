# E2E Staging Fixtures

## Files

| File | Purpose |
|------|---------|
| `e2e_seed.sql` | Idempotent MySQL seed — inserts Company A (9001), Company B (9002), and 6 staging-only E2E user accounts with BCrypt-12 hashed passwords. Safe to re-run (`ON DUPLICATE KEY UPDATE`). |
| `.env.e2e.example` | Template for `.env.e2e` — copy to `.env.e2e`, add to `.gitignore`, never commit the real file. |

## Quick start

```bash
# 1. Seed the database
mysql -u <user> -p hrms < e2e/e2e_seed.sql

# 2. Create local credential file (gitignored)
cp e2e/.env.e2e.example .env.e2e

# 3. Run Playwright auth setup in isolation
npx playwright test --project=setup

# 4. Full suite
npx playwright test --project=chromium --project=firefox --project="Mobile Chrome"
```

## Accounts provisioned

| # | Role | Email | CompanyId |
|---|------|-------|-----------|
| 1 | superadmin | e2e.superadmin@ratan-staging.local | — |
| 2 | admin | e2e.adminA@ratan-staging.local | 9001 |
| 3 | employee | e2e.employeeA@ratan-staging.local | 9001 |
| 4 | admin | e2e.adminB@ratan-staging.local | 9002 |
| 5 | employee | e2e.employeeB@ratan-staging.local | 9002 |
| 6 | hr (auditor) | e2e.auditor@ratan-staging.local | — |

> Passwords are in `.env.e2e` (gitignored). Never use production credentials.
