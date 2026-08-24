/**
 * playwright.offline.config.ts — backend-free SPA smoke configuration.
 *
 * The main playwright.config.ts targets a full staging stack (API + MySQL +
 * Redis) and therefore cannot run in CI sandboxes or on a developer laptop
 * without docker-compose.e2e.yml running.
 *
 * This config runs the SPA alone against the production build served by
 * `vite preview`, with every /api/** call fulfilled by Playwright route
 * mocks inside the specs. It proves the Playwright toolchain, browsers and
 * application shell are healthy without requiring the backend.
 *
 *   npx playwright test --config=playwright.offline.config.ts
 */
import { defineConfig, devices } from '@playwright/test';

const PORT = Number(process.env.E2E_OFFLINE_PORT ?? 4173);

export default defineConfig({
  testDir: './e2e-offline',
  testMatch: '**/*.spec.ts',
  fullyParallel: true,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 1 : 0,
  workers: 1,
  reporter: [['list']],
  use: {
    baseURL: `http://127.0.0.1:${PORT}`,
    trace: 'on-first-retry',
    screenshot: 'only-on-failure',
  },
  projects: [
    {
      name: 'chromium',
      use: {
        ...devices['Desktop Chrome'],
        // Allows CI images that already ship a Chromium build to reuse it
        // instead of downloading a second copy.
        launchOptions: process.env.E2E_CHROMIUM_PATH
          ? { executablePath: process.env.E2E_CHROMIUM_PATH }
          : {},
      },
    },
  ],
  webServer: {
    command: `npx vite preview --config vite.config.ts --host 127.0.0.1 --port ${PORT} --strictPort`,
    url: `http://127.0.0.1:${PORT}/`,
    env: { PORT: String(PORT), BASE_PATH: '/', NODE_ENV: 'production' },
    reuseExistingServer: !process.env.CI,
    timeout: 120_000,
  },
});
