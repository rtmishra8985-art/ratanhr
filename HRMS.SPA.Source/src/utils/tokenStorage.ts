/**
 * tokenStorage.ts — Centralised, safe JWT token access.
 *
 * ──────────────────────────────────────────────────────────────────────────
 * SECURITY NOTE
 * The backend (AuthController) sets the access token and refresh token in
 * HttpOnly cookies — readable only by the browser, NOT by JavaScript.
 * This module is intentionally a no-op stub; the browser sends cookies
 * automatically on every credentialed fetch().
 *
 * Cookie mode is active. localStorage is NOT used for tokens.
 * ──────────────────────────────────────────────────────────────────────────
 */

/** Sentinel value stored in React state when operating in cookie mode. */
export const COOKIE_MODE_SENTINEL = '__cookie__' as const;

// ─── Cookie mode implementation ───────────────────────────────────────────────
// The browser automatically sends the HttpOnly cookie on every fetch with
// `credentials: 'include'`. No JavaScript reads or writes the token.

export const tokenStorage = {
  /** In cookie mode, return the sentinel so AuthContext knows auth is active. */
  get(): string | null { return COOKIE_MODE_SENTINEL; },

  /** Cookie is set by the server — no-op on the client. */
  set(_token: string): void { /* server-managed HttpOnly cookie */ },

  /** Cookie is cleared by the server on logout — no-op on the client. */
  remove(): void { /* server clears cookie via Set-Cookie: Max-Age=0 */ },
};
