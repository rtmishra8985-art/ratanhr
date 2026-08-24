/**
 * setupTests.ts — Global test setup for Vitest + Testing Library.
 *
 * This file runs before every test suite. It:
 *  - Extends Vitest's expect with jest-dom matchers (toBeInTheDocument, etc.)
 *  - Stubs localStorage so tokenStorage tests are fully isolated.
 *  - Clears all mocks between tests to prevent state bleed.
 */
import '@testing-library/jest-dom';
import { afterEach, vi } from 'vitest';
import { cleanup } from '@testing-library/react';

// Automatically unmount React trees after every test.
afterEach(() => {
  cleanup();
  vi.clearAllMocks();
  localStorage.clear();
});
