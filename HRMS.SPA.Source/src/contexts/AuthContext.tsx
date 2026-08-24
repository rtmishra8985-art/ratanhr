// The access token lives in an HttpOnly cookie set by the backend.
// tokenStorage returns COOKIE_MODE_SENTINEL; no localStorage read/write for tokens.
import { useEffect, useState } from 'react';
import { useLocation } from 'wouter';
import { useQueryClient } from '@tanstack/react-query';
import { setAuthTokenGetter, getGetProfileQueryKey } from '@workspace/api-client-react';
import { COOKIE_MODE_SENTINEL } from '@/utils/tokenStorage';
import { csrfFetch, setCsrfRequestToken } from '@/utils/csrfFetch';
import { AuthContext } from './auth-context';

/**
 * Calls GET /api/auth/csrf and caches the real double-submit RequestToken
 * (response body) in memory via setCsrfRequestToken — NOT the XSRF-TOKEN
 * cookie value, which is a deliberately different, non-interchangeable secret
 * (see csrfFetch.ts's top-of-file comment for the full explanation of why the
 * two must never be conflated).
 */
async function seedCsrfToken(): Promise<void> {
  const base = import.meta.env.BASE_URL?.replace(/\/$/, '') ?? '';
  try {
    const res = await fetch(`${base}/api/auth/csrf`, { credentials: 'include' });
    if (res.ok) {
      const body = (await res.json()) as { requestToken?: string };
      setCsrfRequestToken(body.requestToken ?? null);
    }
  } catch {
    // Best-effort — SameSite=Strict cookies already provide the primary
    // CSRF defence; this is defence-in-depth only.
  }
}

export function AuthProvider({ children }: { children: React.ReactNode }) {
  // In cookie mode the sentinel marks "authenticated" — the browser sends
  // the HttpOnly cookie automatically on every credentialed fetch.
  const [token, setTokenState] = useState<string | null>(COOKIE_MODE_SENTINEL);
  const [, setLocation] = useLocation();
  const queryClient = useQueryClient();

  useEffect(() => {
    // Do NOT forward a token as an Authorization header — the browser sends
    // the HttpOnly cookie automatically via credentials: 'include'.
    setAuthTokenGetter(() => null);
  }, []);

  const setToken = (newToken: string | null) => {
    // Only update React state; cookie lifecycle is managed by the server.
    setTokenState(newToken);
    // BUGFIX: ASP.NET Core's antiforgery token is bound to the identity that
    // requested it (GetAndStoreTokens ties the cookie token to the current
    // ClaimsPrincipal). A token fetched while anonymous (the mount-time seed
    // below) becomes invalid the instant the user authenticates — the very
    // next mutating request (e.g. Logout) fails CSRF validation even though
    // the mount-time seed "worked". Re-seed immediately after every
    // successful login/MFA-verify (both call setToken(COOKIE_MODE_SENTINEL))
    // so the cached RequestToken (and the XSRF-TOKEN cookie backing it) are
    // bound to the now-authenticated principal before the user can trigger
    // any subsequent mutation.
    if (newToken === COOKIE_MODE_SENTINEL) {
      void seedCsrfToken();
      // BUGFIX (GuestGuard/AuthGuard stale-cache interaction): GuestGuard
      // (login/forgot-password/reset-password pages) probes GET /api/profile
      // to detect an already-authenticated user, using the SAME react-query
      // cache key AuthGuard uses for protected pages. invalidateQueries only
      // marks the cached entry stale and starts a background refetch — it does
      // NOT clear the previously-resolved data, so a consumer reading the query
      // in the same tick still sees the OLD result until the refetch resolves.
      // removeQueries evicts the entry outright, so every consumer starts from
      // a clean isPending/no-data state and only redirects once the fresh
      // fetch actually resolves — no stale snapshot to act on.
      void queryClient.removeQueries({ queryKey: getGetProfileQueryKey() });
    }
  };

  // FIX SEC-05: isAuthenticated must be computed BEFORE the useEffect that reads
  // it, so this declaration is placed here rather than after the effect.
  // Block-scoped variable 'isAuthenticated' used before its declaration (TS2448)
  // was caused by the previous placement at line 56.
  const isAuthenticated = token === COOKIE_MODE_SENTINEL || Boolean(token);

  // BUGFIX (login/logout CSRF failure): GET /api/auth/csrf previously required
  // authentication, so this effect 401'd silently (see the .catch below) and the
  // XSRF-TOKEN cookie was NEVER set — not even after login, since this effect only
  // re-ran on isAuthenticated transitions and the failure was swallowed. Every
  // subsequent mutating request (logout, then any later login once a stale
  // hrms_access_token cookie triggered the CSRF filter) failed with
  // "CSRF token missing or invalid", and logout's failure was itself silently
  // swallowed, leaving the stale access-token cookie in place and permanently
  // blocking the next login. The endpoint is now [AllowAnonymous] (Program.cs) and
  // this effect runs once unconditionally on mount — not gated on isAuthenticated —
  // so the XSRF-TOKEN cookie exists before the very first login attempt, exactly
  // like a real browser session needs it to.
  useEffect(() => {
    void seedCsrfToken();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const logout = async () => {
    try {
      const res = await csrfFetch('/api/auth/logout', {
        method: 'POST',
        credentials: 'include',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ refreshToken: '' }),
      });
      // BUGFIX: previously only network-level exceptions were caught here, so an
      // HTTP error response (e.g. the CSRF 401 caused by the bug above) was
      // treated as a successful logout — the server-side cookie-clearing code
      // never ran, leaving a stale hrms_access_token cookie that then blocked
      // every subsequent login attempt. Retry once via a fresh CSRF token if the
      // first attempt was itself rejected for a CSRF reason, so a logout click
      // is self-healing even if the seed effect above raced with this call.
      if (!res.ok) {
        await seedCsrfToken();
        await csrfFetch('/api/auth/logout', {
          method: 'POST',
          credentials: 'include',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ refreshToken: '' }),
        }).catch(() => {});
      }
    } catch {
      // Best-effort — proceed to redirect even if the request fails
    }
    setTokenState(null);
    // BUGFIX (redirect ping-pong / React error #185 "Maximum update depth
    // exceeded"): this MUST evict the cached profile (removeQueries), not just
    // mark it stale (invalidateQueries). invalidateQueries leaves the last
    // resolved (successful, pre-logout) data in place while a background
    // refetch runs, so the instant setLocation('/login') below mounts
    // GuestGuard, it can still read that stale "successful" profile and
    // immediately redirect to /dashboard — which mounts AuthGuard, which reads
    // the now-null token (isAuthenticated=false) and immediately redirects
    // back to /login — an infinite synchronous ping-pong between the two
    // guards that trips React's re-render safeguard. removeQueries evicts the
    // cached entry outright, so GuestGuard starts from a clean
    // no-data/isPending state and has nothing stale to act on.
    queryClient.removeQueries({ queryKey: getGetProfileQueryKey() });
    setLocation('/login');
  };

  return (
    <AuthContext.Provider value={{ token, setToken, isAuthenticated, logout }}>
      {children}
    </AuthContext.Provider>
  );
}
