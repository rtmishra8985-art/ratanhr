// Regression test for the silent access-token refresh-and-retry fix in
// src/api-client/http.ts.
//
// Root cause this guards against: the backend issues a 30-minute access-token
// cookie and a 7-day refresh-token cookie, and fully implements
// POST /api/auth/refresh, but no frontend code ever called it — every session
// was force-logged-out every 30 minutes instead of silently renewing. See the
// "FIX: silent access-token refresh on 401" block in http.ts.

import { describe, it, expect, vi, beforeEach } from 'vitest';

// Mock csrfFetch — the only transport apiRequest uses — so we control the
// exact sequence of responses without touching the network.
const mockCsrfFetch = vi.fn();
vi.mock('@/utils/csrfFetch', () => ({
  csrfFetch: (...args: unknown[]) => mockCsrfFetch(...args),
}));

// Import AFTER the mock so the module under test resolves the mocked csrfFetch.
import { apiRequest, ApiError } from '../api-client/http';

function jsonResponse(status: number, body: unknown): Response {
  return {
    ok: status >= 200 && status < 300,
    status,
    text: () => Promise.resolve(JSON.stringify(body)),
  } as unknown as Response;
}

describe('apiRequest — silent refresh-and-retry on 401', () => {
  beforeEach(() => {
    mockCsrfFetch.mockReset();
  });

  it('retries the original request once after a successful refresh', async () => {
    mockCsrfFetch
      // 1st call: the original request, expired access token → 401
      .mockResolvedValueOnce(jsonResponse(401, { message: 'Unauthorized' }))
      // 2nd call: POST /api/auth/refresh succeeds
      .mockResolvedValueOnce(jsonResponse(200, { success: true }))
      // 3rd call: the retried original request now succeeds
      .mockResolvedValueOnce(jsonResponse(200, { success: true, data: { id: 1 } }));

    // apiRequest returns the raw parsed body; unwrap() is a separate helper
    // callers apply on top (see http.ts unwrap doc comment) — not exercised here.
    const result = await apiRequest<{ success: boolean; data: { id: number } }>(
      '/api/employees/1',
    );

    expect(result).toEqual({ success: true, data: { id: 1 } });
    expect(mockCsrfFetch).toHaveBeenCalledTimes(3);
    // Middle call must be the refresh endpoint.
    const refreshCallUrl = String(mockCsrfFetch.mock.calls[1][0]);
    expect(refreshCallUrl).toContain('/api/auth/refresh');
  });

  it('surfaces the original 401 as ApiError when refresh itself fails', async () => {
    mockCsrfFetch
      .mockResolvedValueOnce(jsonResponse(401, { message: 'Unauthorized' }))
      // Refresh token expired/revoked/reused — refresh endpoint also 401s.
      .mockResolvedValueOnce(jsonResponse(401, { message: 'Invalid refresh token' }));

    await expect(apiRequest('/api/employees/1')).rejects.toBeInstanceOf(ApiError);

    // Exactly 2 calls: original + refresh attempt. No retry of the original
    // request is made because the refresh did not succeed — this is the
    // existing AuthGuard-logout path, unchanged by this fix.
    expect(mockCsrfFetch).toHaveBeenCalledTimes(2);
  });

  it('does not attempt to refresh when the 401 comes from the refresh endpoint itself', async () => {
    mockCsrfFetch.mockResolvedValueOnce(jsonResponse(401, { message: 'Invalid refresh token' }));

    await expect(apiRequest('/api/auth/refresh', { method: 'POST' })).rejects.toBeInstanceOf(
      ApiError,
    );

    // Must not recurse into another refresh attempt.
    expect(mockCsrfFetch).toHaveBeenCalledTimes(1);
  });

  it('does not attempt to refresh on a 401 from /api/auth/login (wrong credentials, not an expired session)', async () => {
    mockCsrfFetch.mockResolvedValueOnce(jsonResponse(401, { message: 'Invalid email or password' }));

    await expect(
      apiRequest('/api/auth/login', { method: 'POST', body: { email: 'a', password: 'b' } }),
    ).rejects.toBeInstanceOf(ApiError);

    expect(mockCsrfFetch).toHaveBeenCalledTimes(1);
  });

  it('coalesces concurrent 401s into a single refresh call (no refresh stampede)', async () => {
    mockCsrfFetch.mockImplementation((url: unknown) => {
      const u = String(url);
      if (u.includes('/api/auth/refresh')) {
        return Promise.resolve(jsonResponse(200, { success: true }));
      }
      // Every non-refresh call succeeds on the SECOND time it's made for a
      // given path; simplest way to model that here is to track call counts.
      return Promise.resolve(jsonResponse(401, { message: 'Unauthorized' }));
    });

    // Two requests in flight at once, both initially 401.
    const p1 = apiRequest('/api/employees/1').catch((e) => e);
    const p2 = apiRequest('/api/employees/2').catch((e) => e);
    await Promise.all([p1, p2]);

    const refreshCalls = mockCsrfFetch.mock.calls.filter((c) =>
      String(c[0]).includes('/api/auth/refresh'),
    );
    // Exactly one refresh call for two concurrent 401s.
    expect(refreshCalls.length).toBe(1);
  });

  it('does not retry a second time if the retried request also 401s', async () => {
    mockCsrfFetch
      .mockResolvedValueOnce(jsonResponse(401, { message: 'Unauthorized' })) // original
      .mockResolvedValueOnce(jsonResponse(200, { success: true })) // refresh succeeds
      .mockResolvedValueOnce(jsonResponse(401, { message: 'Unauthorized' })); // retry still 401s

    await expect(apiRequest('/api/employees/1')).rejects.toBeInstanceOf(ApiError);

    // original + refresh + one retry = 3 calls, never a second refresh/retry loop.
    expect(mockCsrfFetch).toHaveBeenCalledTimes(3);
  });
});
