// BLOCKER-13 — FRONTEND / API END-TO-END BEHAVIOR (Phase 2)
//
// Tests for AuthGuard 403 handling added in Phase 2 (Blocker 13).
// The original AuthGuard only handled 401; Phase 2 extends it to treat 403
// (account suspended, role revoked server-side) the same as 401 — log out and
// redirect so the user gets a clear prompt to re-authenticate.
//
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import React from 'react';

// ── Module mocks ──────────────────────────────────────────────────────────────

const mockLogout = vi.fn();
const mockSetLocation = vi.fn();

vi.mock('@/hooks/useAuth', () => ({
  useAuth: () => ({
    isAuthenticated: true,
    logout: mockLogout,
  }),
}));

vi.mock('wouter', () => ({
  useLocation: () => ['/dashboard', mockSetLocation],
}));

// Shared state so each test can configure the profile query outcome.
let profileQueryResult: {
  isLoading: boolean;
  isError: boolean;
  error: unknown;
  refetch: () => void;
} = { isLoading: false, isError: false, error: null, refetch: vi.fn() };

vi.mock('@workspace/api-client-react', () => ({
  useGetProfile: () => profileQueryResult,
  getGetProfileQueryKey: () => ['profile'],
}));

vi.mock('@/utils/apiError', () => ({
  isApiError: (e: unknown): e is { status: number } =>
    typeof e === 'object' && e !== null && 'status' in (e as object),
}));

vi.mock('@/components/ui/button', () => ({
  Button: ({ children, onClick }: { children: React.ReactNode; onClick: () => void }) => (
    <button onClick={onClick}>{children}</button>
  ),
}));

// ── Tests ──────────────────────────────────────────────────────────────────────

// Import AFTER mocks so the module resolves mocked dependencies.
import { AuthGuard } from '../components/layout/AuthGuard';

describe('AuthGuard — Phase 2 (Blocker 13)', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    profileQueryResult = { isLoading: false, isError: false, error: null, refetch: vi.fn() };
  });

  // ── Existing behaviour: 401 still triggers logout ──────────────────────────

  it('calls logout when profile query returns 401', async () => {
    profileQueryResult = {
      isLoading: false,
      isError: true,
      error: { status: 401, message: 'Unauthorized' },
      refetch: vi.fn(),
    };

    render(<AuthGuard><div>protected</div></AuthGuard>);

    await waitFor(() => {
      expect(mockLogout).toHaveBeenCalledTimes(1);
    });
  });

  // ── New behaviour: 403 also triggers logout ────────────────────────────────

  it('calls logout when profile query returns 403 (BLOCKER-13)', async () => {
    profileQueryResult = {
      isLoading: false,
      isError: true,
      error: { status: 403, message: 'Forbidden' },
      refetch: vi.fn(),
    };

    render(<AuthGuard><div>protected</div></AuthGuard>);

    await waitFor(() => {
      expect(mockLogout).toHaveBeenCalledTimes(1);
    });
  });

  it('renders null (not children) after 403 triggers logout', async () => {
    profileQueryResult = {
      isLoading: false,
      isError: true,
      error: { status: 403, message: 'Forbidden' },
      refetch: vi.fn(),
    };

    const { container } = render(
      <AuthGuard><div data-testid="protected-content">protected</div></AuthGuard>,
    );

    await waitFor(() => expect(mockLogout).toHaveBeenCalled());
    expect(screen.queryByTestId('protected-content')).toBeNull();
    expect(container.firstChild).toBeNull();
  });

  // ── Network errors (non-4xx) still show the retry UI ──────────────────────

  it('shows retry UI for non-auth errors (network failure)', async () => {
    profileQueryResult = {
      isLoading: false,
      isError: true,
      error: new Error('Network error'),   // no .status property
      refetch: vi.fn(),
    };

    render(<AuthGuard><div>protected</div></AuthGuard>);

    await waitFor(() => {
      expect(screen.getByText(/unable to verify/i)).toBeTruthy();
    });
    expect(mockLogout).not.toHaveBeenCalled();
  });

  // ── Loading spinner is shown while profile is pending ─────────────────────

  it('shows loading spinner while profile query is pending', () => {
    profileQueryResult = {
      isLoading: true,
      isError: false,
      error: null,
      refetch: vi.fn(),
    };

    render(<AuthGuard><div data-testid="protected-content">protected</div></AuthGuard>);

    expect(screen.queryByTestId('protected-content')).toBeNull();
    // Spinner container present
    const spinner = document.querySelector('[aria-label="Verifying session"]');
    expect(spinner).not.toBeNull();
  });

  // ── Happy path: profile loaded, children rendered ─────────────────────────

  it('renders children when profile loads successfully', async () => {
    profileQueryResult = {
      isLoading: false,
      isError: false,
      error: null,
      refetch: vi.fn(),
    };

    render(<AuthGuard><div data-testid="protected-content">protected</div></AuthGuard>);

    expect(screen.getByTestId('protected-content')).toBeTruthy();
    expect(mockLogout).not.toHaveBeenCalled();
  });
});
