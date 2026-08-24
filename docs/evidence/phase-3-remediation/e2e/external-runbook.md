# External Runbook — Playwright E2E

Run on a machine with Bun, Docker, and internet access to download browser binaries
(this sandbox has no bun, and `npx playwright install` failed here too — see
`e2e-attempt.txt`: apt sources and the Node package mirror were unreachable under
this sandbox's egress allowlist).

```bash
cd RatanHR-merged-release-candidate/HRMS.SPA.Source

cp .env.e2e.example .env.e2e
# Fill in real values for a disposable E2E database's seeded accounts.
# Never commit .env.e2e — it is already gitignored.

bun install --frozen-lockfile

bun run e2e:install     # playwright install --with-deps chromium firefox

# Bring up the E2E stack (see ../docker/external-runbook.md first)
cd ..
docker compose -f docker-compose.e2e.yml up -d

cd HRMS.SPA.Source
bun run e2e
```

Confirm and record in `docs/phase-3-readiness.md`:

- Browsers installed successfully
- `global-setup.ts` loaded `.env.e2e` and logged in all 6 roles (superAdmin, adminA,
  employeeA, adminB, employeeB, auditor) — if any fails to log in, the whole run
  aborts before specs execute, per the config's own design
- Specs actually executed (not skipped) for: auth/MFA/session, employee CRUD,
  attendance, leave, payroll, payslips, RBAC, cross-tenant isolation, upload/download,
  error/empty/loading states, core navigation
- No unexpected browser console errors or API/server errors
- Failure screenshots/traces retained (Playwright does this automatically on retry
  failures per `playwright.config.ts`)
- Final pass/fail counts recorded exactly

Save the HTML report and any trace files under
`docs/evidence/phase-3-remediation/e2e/` before marking this item VERIFIED.

## Note on package manager

`playwright.config.ts`'s header comment references `pnpm e2e`, but the repository's
actual lockfile is `bun.lock` and `package.json` scripts are plain `npm`-style
(`"e2e": "playwright test"`, run via `bun run e2e`). This is a harmless stale-comment
inconsistency, not a functional blocker — flagging so it can be fixed for clarity.
