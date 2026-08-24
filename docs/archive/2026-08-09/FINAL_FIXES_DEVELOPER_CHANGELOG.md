# RatanHR — Final Developer Fixes Changelog
**Date:** 2026-08-04  
**Base:** RatanHR_Source_Fixed_Phase9_RC + Production_Ready_v1.1 cherry-picks  
**Status:** ✅ All developer-blocking issues resolved

---

## Fixes Applied in This Build

### FIX E2E-GLOBALSETUP-001 — Root cause of every E2E 401 failure
**File:** `HRMS.SPA.Source/global-setup.ts`

**Problem:** `globalSetup` was using `E2E_BASE_URL` (the SPA/browser URL, e.g. `http://localhost:3000`) to POST `/api/auth/login`. The SPA does not expose `/api/auth/login` — only the .NET API does. Every role login returned HTTP 401/404, causing `playwright/.auth/*.json` to never be written. All 625 tests then failed at the setup project, leaving 0 tests run.

**Fix:** Now uses `E2E_API_URL` (defaults to `http://127.0.0.1:8082`) for API authentication requests. `E2E_BASE_URL` is reserved for browser page navigation (used by `playwright.config.ts`).

---

### FIX E2E-PLAYWRIGHTCONFIG-001 — Wrong default baseURL
**File:** `HRMS.SPA.Source/playwright.config.ts`

**Problem:** `use.baseURL` defaulted to `'http://127.0.0.1:8082'` (the API port). Browser tests navigating to `/` would hit the raw API JSON, not the SPA, causing every `page.goto('/')` to fail visually.

**Fix:** Default changed to `'http://127.0.0.1:3000'` (the SPA/Vite dev server port).

---

### FIX E2E-ENVTEMPLATE-001 — Missing SPA-side .env.e2e template
**File:** `HRMS.SPA.Source/.env.e2e.template` *(new file)*

**Problem:** `global-setup.ts` looks for `.env.e2e` inside `HRMS.SPA.Source/`. No template existed there, so developers had no way to know what to create. The root `.env.e2e.template` is for the Docker/API stack, not for Playwright.

**Fix:** Added `HRMS.SPA.Source/.env.e2e.template` with all credentials pre-filled (passwords match `e2e/e2e_seed.sql` BCrypt hashes). Developer only needs to `cp .env.e2e.template .env.e2e` — no values to change for local staging runs.

---

### FIX E2E-COMPOSE-001 — Missing SPA service in docker-compose.e2e.yml
**File:** `docker-compose.e2e.yml`

**Problem:** The E2E compose stack started MySQL + Redis + API but not the SPA. Playwright browser tests navigate to `E2E_BASE_URL` (the SPA), which was never running.

**Fix:** Added `spa` service: builds the React/Vite SPA with `bun run build:ci` and serves it via `vite preview` on port 3000. Depends on `api` being healthy before starting.

---

### FIX E2E-RUNSH-001 — run-e2e.sh missing SPA startup + wait
**File:** `e2e/run-e2e.sh`

**Problem:** Script started Docker services and ran Playwright but never waited for the SPA to be reachable. Playwright launched immediately after API health, before the SPA was built and served.

**Fix:** Added Step 5b — waits up to 120s for the SPA container to be reachable at `E2E_BASE_URL` before running Playwright.

---

### FIX CI-VITE-001 — SPA dev server start missing PORT and BASE_PATH
**Files:** `.github/workflows/e2e.yml`, `.github/workflows/ci.yml`

**Problem:** Both CI workflows started the SPA with `bun run dev --port 3000`. But `vite.config.ts` throws `Error: PORT environment variable is required` and `Error: BASE_PATH environment variable is required` when these are not set as env vars. The SPA never started in CI, blocking all E2E tests.

**Fix:** Changed to `PORT=3000 BASE_PATH=/ bun run dev`.

---

### FIX DOCKER-PIN-001 — SDK stage digest-pin CI enforcement added
**File:** `Dockerfile`

**Problem:** Runtime stage is already pinned (`aspnet:8.0.16@sha256:...`) but the SDK build stage is unpinned. No CI check enforced this.

**Fix:** Added CI enforcement snippet in Dockerfile comment:
```bash
grep -qE 'dotnet/sdk:8\.0\.416@sha256:' Dockerfile || \
  { echo "ERROR: SDK not digest-pinned. Run scripts/pin-docker-digests.sh" && exit 1; }
```
Run `scripts/pin-docker-digests.sh` locally and commit the result before deploying.

---

## Pre-existing Fixes (carried from Phase9_RC + v1.1)

| Fix | File | Description |
|-----|------|-------------|
| P2-OTEL | `HRMS.API.csproj` | OTel packages downgraded to stable GA (1.10.0), pre-release removed |
| BLOCKER-HANGFIRE | `ServiceExtensions.cs` | `Hangfire.MySql.Core` (incompatible) replaced with `Hangfire.Redis.StackExchange` |
| P3-01 | `SwaggerBasicAuthMiddleware.cs` | 403 Forbidden in non-dev when Swagger credentials not set |
| E2E-SEED-001/002/003 | `e2e/e2e_seed.sql` | Fixed UUID→INT IDs, removed non-existent columns |
| P2-04 | `PayrollService.cs` | Removed 500-employee hard cap — batch processing for unlimited employees |

---

## Remaining Client/DevOps Actions (not developer code)

| Action | Who | Priority |
|--------|-----|----------|
| Set `Swagger:Username` + `Swagger:Password` on server | DevOps | Before go-live |
| Run `scripts/pin-docker-digests.sh` and commit | DevOps/Dev | Before first build |
| Generate RSA key pair + set `Jwt__PrivateKeyPem` / `Jwt__PublicKeyPem` | DevOps | Before go-live |
| Run `bash e2e/run-e2e.sh` on staging to verify 625/625 E2E pass | QA | Go-live gate |
| Add GitHub Secrets for CI (`MYSQL_PASSWORD`, `JWT_*`, `ENCRYPTION_KEY`, etc.) | DevOps | Before CI runs |

---

## How to Run E2E Tests

```bash
# 1. Set up .env.e2e (no values to change for local staging)
cp HRMS.SPA.Source/.env.e2e.template HRMS.SPA.Source/.env.e2e

# 2. Fill in the 3 secrets that MUST be generated (see .env.e2e.template at root)
#    - MYSQL_ROOT_PASSWORD, JWT_PRIVATE_KEY_PEM, JWT_PUBLIC_KEY_PEM, ENCRYPTION_KEY

# 3. One-command full E2E run (starts all containers, seeds DB, runs all 625 tests)
bash e2e/run-e2e.sh
```
