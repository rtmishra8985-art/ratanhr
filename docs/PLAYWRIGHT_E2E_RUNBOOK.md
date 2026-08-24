# Playwright E2E runbook

## Two suites

| Suite | Config | Needs backend? | Purpose |
|-------|--------|----------------|---------|
| Staging suite (631 tests, 26 files) | `playwright.config.ts` | **Yes** — `docker-compose.e2e.yml` | Full functional/RBAC coverage against the real API |
| Offline SPA smoke (4 tests) | `playwright.offline.config.ts` | No | Toolchain + app-shell health gate that runs anywhere, including CI sandboxes |

## Setup

```bash
cd HRMS.SPA.Source
bun install                 # or: npm ci
npx playwright install --with-deps chromium firefox
```

The earlier blocker (`Cannot find package '@playwright/test'`) was purely a
missing `node_modules` — `@playwright/test` has always been declared in
`devDependencies`. Installing dependencies resolves it.

If your CI image already ships a Chromium build, point Playwright at it instead
of downloading a second copy:

```bash
E2E_CHROMIUM_PATH=/path/to/chrome npx playwright test --config=playwright.offline.config.ts
```

## Offline smoke (no backend)

```bash
cd HRMS.SPA.Source
npx playwright test --config=playwright.offline.config.ts
```

It builds nothing itself — it starts `vite preview` via `webServer` (injecting
the required `PORT` / `BASE_PATH` / `NODE_ENV` env vars, which the Vite config
mandates), mocks every `/api/**` call, and asserts the shell mounts with no
uncaught page errors.

> This suite is what caught **SRI-001**: `src/vite-plugins/sri-plugin.ts` hashed
> `chunk.code` in `generateBundle`, before Vite finished post-processing the
> output, so every production build shipped a stale `integrity=` attribute and
> the browser refused to execute the main bundle — a blank page in **every**
> production deployment. The plugin now hashes the bytes actually written to
> disk in `writeBundle`.

## Full staging suite

```bash
./scripts/e2e-up.sh                       # from the repo root
cd HRMS.SPA.Source
cp .env.e2e.example .env.e2e              # fill in the 6 role credentials
E2E_API_URL=http://127.0.0.1:8082 E2E_BASE_URL=http://127.0.0.1:3000 npx playwright test
npx playwright show-report
```
