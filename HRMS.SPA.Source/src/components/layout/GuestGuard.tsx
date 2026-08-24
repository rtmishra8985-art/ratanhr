/**
 * GuestGuard.tsx — Mirror image of AuthGuard.tsx, for guest-only routes
 * (/login, /forgot-password, /reset-password).
 *
 * BUGFIX: those routes had NO guard against an already-authenticated user
 * navigating to them directly (URL bar, bookmark, browser back/forward, or a
 * stale tab). Confirmed live: after a successful login, navigating back to
 * /login (or pressing the browser Back button) left the user sitting on the
 * login form instead of being sent to /dashboard.
 *
 * IMPORTANT: this CANNOT simply redirect whenever AuthContext's isAuthenticated
 * is true. AuthContext's `token` state defaults optimistically to
 * COOKIE_MODE_SENTINEL on every page load (see AuthContext.tsx) — the app has
 * no way to know whether a session cookie actually exists until a real request
 * is made, and /login renders outside <AuthGuard>/<Layout>, so no profile check
 * has happened yet. Naively trusting isAuthenticated here would redirect a
 * genuinely logged-out user away from /login to /dashboard, where AuthGuard's
 * own profile check would then 401 and bounce them right back — a redirect
 * loop / flash for the single most common case (a logged-out user opening the
 * app for the first time). Instead, this guard makes its OWN lightweight
 * GET /api/profile probe (via the same react-query hook AuthGuard uses, so the
 * result is cached and shared — no duplicate network cost once truly
 * authenticated) and only redirects once that call has actually resolved
 * successfully. While the probe is in flight, or once it has resolved with a
 * 401/403 (confirming there is no real session), the guest page renders
 * normally.
 */
import { useEffect } from 'react';
import { useLocation } from 'wouter';
import { useGetProfile, getGetProfileQueryKey } from '@workspace/api-client-react';

export function GuestGuard({ children }: { children: React.ReactNode }) {
  const [, setLocation] = useLocation();

  const { data: profile, isSuccess } = useGetProfile({
    query: {
      // Always enabled here (unlike AuthGuard, which gates on the optimistic
      // isAuthenticated) — this IS the real signal for whether a session
      // exists, since guest routes have no other authority to consult.
      enabled: true,
      retry: false,
      queryKey: getGetProfileQueryKey(),
    },
  });

  useEffect(() => {
    if (isSuccess && profile) {
      setLocation('/dashboard');
    }
  }, [isSuccess, profile, setLocation]);

  if (isSuccess && profile) {
    return null;
  }

  return <>{children}</>;
}
