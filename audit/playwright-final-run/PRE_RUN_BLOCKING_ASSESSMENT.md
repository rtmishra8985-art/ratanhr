# RatanHR — Playwright Pre-Go-Live E2E Run Assessment

**Attempted:** 2026-08-04  
**Run environment:** Replit (pnpm monorepo workspace, Node 24, NixOS)  
**Requested command:**
```
npx playwright test --project=chromium --project=firefox --project="Mobile Chrome"
```

**Result: ❌ CANNOT EXECUTE — Infrastructure prerequisites absent**

---

## Prerequisite Check

| Prerequisite | Required | Available | Status |
|---|---|---|---|
| .NET 8 SDK (`dotnet run`) | ✅ Required — starts HRMS API | ❌ Not installed | **BLOCKER** |
| MySQL / `mysql` CLI | ✅ Required — `hrms_staging` DB + seed | ❌ Not installed | **BLOCKER** |
| Redis (`redis-cli`) | ✅ Required — API startup health check | ❌ Not installed | **BLOCKER** |
| Node.js / bun | ✅ Required — runs SPA + Playwright | ✅ Node 24, bun 1.3.6 | OK |
| Playwright browsers | Required | ❌ Not installed | Blocked by above |
| `.env.e2e` with credentials | Required | ❌ Only `.env.e2e.example` present | Blocked by above |
| E2E seed accounts in DB | Required | ❌ No DB available to seed | **BLOCKER** |

All three blockers are hard infrastructure requirements. The Playwright suite cannot proceed without
them. No tests were executed.

---

## What the Playwright suite requires (from `e2e/README.md`)

```
1. mysql -u <user> -p hrms < e2e/e2e_seed.sql          ← seeds 6 E2E role accounts
2. cp e2e/.env.e2e.example .env.e2e                     ← fill real staging creds
3. dotnet run --project HRMS.API                        ← starts API on :9090
4. PORT=3000 BASE_PATH=/ bun run dev  (SPA)            ← starts SPA on :3000
5. E2E_BASE_URL=http://localhost:3000 bunx playwright test \
     --project=chromium --project=firefox --project="Mobile Chrome"
```

None of steps 1–4 are executable in this environment.

---

## Prior run history

Phase 7 Frontend & UX Audit (2026-08-03) reached step 5 on an isolated local environment but failed
at the Playwright `globalSetup` authentication step:

- Playwright enumerated **625 tests** (208 chromium + 208 firefox + 208 mobile-chrome + 1 setup)
- `global.setup.ts` attempted API login for 6 role accounts
- **Employee A login returned HTTP 401** (`Invalid credentials`)
- Root cause: isolated local MySQL did not contain the 6 E2E role accounts from `e2e_seed.sql`
- Result: **1 failed (setup), 624 not run, 0 passed**

That prior run was documented as **BLOCKED**, not PASS.

---

## E2E test inventory (from `playwright.config.ts --list`)

| Project | Tests | Spec files |
|---|---|---|
| chromium | 208 | 24 |
| firefox | 208 | 24 |
| Mobile Chrome | 208 | 24 |
| setup | 1 | 1 |
| **Total** | **625** | **25 specs + 1 setup** |

Spec files: `auth`, `session`, `rbac`, `smoke`, `employees`, `employees-crud`, `attendance`,
`leave`, `payroll`, `payslips`, `recruitment`, `performance`, `assets`, `sales`, `helpdesk`,
`reports`, `expenses`, `org-chart`, `training`, `settings-mfa`, `settings-password`,
`uploads-downloads`, `modal-forms`, `loading-empty-error-states`, `pagination-search-sort`

---

## What is needed to close this as PASS

1. **Provision a staging environment** with:
   - MySQL 8.x with `hrms_staging` database
   - Redis 7.x on port 6380
   - .NET 8 SDK (8.0.416 confirmed working in prior run)
   - MailHog (for email-dependent tests)

2. **Seed the E2E database**:
   ```bash
   mysql -u <user> -p hrms_staging < HRMS.SPA.Source/e2e/e2e_seed.sql
   ```
   This inserts 2 companies (ID 9001, 9002) and 6 BCrypt-12 hashed role accounts.

3. **Start the API**:
   ```bash
   cd HRMS.API
   E2E_BASE_URL=http://localhost:3000 dotnet run --project HRMS.API.csproj
   ```

4. **Start the SPA**:
   ```bash
   cd HRMS.SPA.Source
   PORT=3000 BASE_PATH=/ bun run dev
   ```

5. **Run the full suite**:
   ```bash
   cd HRMS.SPA.Source
   cp e2e/.env.e2e.example .env.e2e   # fill real staging passwords
   E2E_BASE_URL=http://localhost:3000 \
   npx playwright test \
     --project=chromium \
     --project=firefox \
     --project="Mobile Chrome"
   ```

6. **Save the HTML report**:
   ```bash
   cp -r HRMS.SPA.Source/playwright-report/* <repo>/audit/playwright-final-run/
   ```

7. Only once all 625 tests pass: update `PHASE7_FRONTEND_UX_AUDIT.md` verdict to PASS and fill
   `GO_LIVE_READINESS.md` with the actual browser versions, pass counts, and timestamp.

---

## Decision

**This run does not satisfy the pre-go-live requirement.**  
**PHASE 7 STATUS: BLOCKED (unchanged from 2026-08-03)**  
**GO-LIVE DECISION: ❌ NO-GO — P0 evidence not yet available**

Per the task specification: *"Exit non-zero if any test fails — do not proceed to deployment config
until this is clean."*

The infrastructure to run this suite is not present in the current environment. Deployment
configuration must not proceed until the suite is executed on a properly provisioned staging
environment and returns 625/625.
