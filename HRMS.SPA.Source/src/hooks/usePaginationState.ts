/**
 * usePaginationState.ts — Fix #5: Shared pagination hook.
 *
 * Eliminates the copy-pasted `const [page, setPage] = useState(1)` and
 * `const pageSize = 10` pattern that appeared in every list page.
 *
 * Usage:
 *   const { page, setPage, pageSize, resetPage } = usePaginationState();
 *   // or with a custom page size:
 *   const { page, setPage, pageSize, resetPage } = usePaginationState(20);
 */

import { useState } from 'react';

export interface PaginationState {
  page: number;
  setPage: (page: number) => void;
  pageSize: number;
  /** Resets page back to 1. Call this whenever a filter changes. */
  resetPage: () => void;
}

export function usePaginationState(pageSize = 10): PaginationState {
  const [page, setPage] = useState(1);
  const resetPage = () => setPage(1);
  return { page, setPage, pageSize, resetPage };
}
