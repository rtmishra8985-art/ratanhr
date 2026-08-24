import path from 'path';
import { defineConfig } from 'vitest/config';
import react from '@vitejs/plugin-react';

/**
 * Vitest configuration for unit + component tests.
 *
 * Run:
 *   pnpm test              — run all tests once
 *   pnpm test:watch        — watch mode
 *   pnpm test:coverage     — with coverage report
 */
export default defineConfig({
  plugins: [react()],
  test: {
    globals: true,
    environment: 'jsdom',
    setupFiles: ['./src/setupTests.ts'],
    include: ['src/__tests__/**/*.{test,spec}.{ts,tsx}'],
    exclude: ['node_modules', 'dist'],
    coverage: {
      provider: 'v8',
      reporter: ['text', 'html', 'lcov'],
      include: ['src/utils/**', 'src/hooks/**', 'src/components/shared/**'],
      exclude: ['src/components/ui/**', 'src/__tests__/**'],
      thresholds: {
        lines: 70,
        functions: 70,
        branches: 60,
        statements: 70,
      },
    },
  },
  resolve: {
    alias: {
      '@': path.resolve(__dirname, 'src'),
      '@workspace/api-client-react': path.resolve(__dirname, 'src/api-client/index.ts'),
    },
  },
});
