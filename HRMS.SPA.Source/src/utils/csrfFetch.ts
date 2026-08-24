/**
 * csrfFetch.ts — CSRF-safe drop-in replacement for the global fetch().
 *
 * SECURITY FIX: Direct fetch() calls bypass the X-XSRF-TOKEN header check
 * required by the server's CsrfValidationFilter for all authenticated
 * state-changing requests (POST, PUT, PATCH, DELETE).
 *
 * BUGFIX (real double-submit correctness): ASP.NET Core's antiforgery system
 * issues TWO DIFFERENT, deliberately non-equal secrets from a single call to
 * GetAndStoreTokens: a CookieToken (written to the XSRF-TOKEN cookie) and a
 * RequestToken (returned only in the JSON response body of GET /api/auth/csrf
 * — see Program.cs's mapping of that route). The correct double-submit pattern
 * requires the client to echo the RequestToken (body value) back as the
 * X-XSRF-TOKEN header; the server then validates that header value against the
 * CookieToken the browser sends automatically. This module previously read the
 * XSRF-TOKEN *cookie* value and sent that as the header — sending a secret back
 * as its own proof, which the server never expects and always rejects once an
 * access-token cookie is also present (ValidateTokens throws "the cookie token
 * and the request token were swapped"). This was NOT caught by casual testing
 * because CsrfValidationFilter only activates on requests carrying an existing
 * hrms_access_token cookie — so a bare first login (no prior session) skipped
 * CSRF validation entirely and appeared to work, while every subsequent
 * mutation (logout, and any login/mutation once a session cookie existed)
 * always failed. Fixed by caching the real RequestToken in memory (see
 * setCsrfRequestToken/getCsrfRequestToken below) whenever GET /api/auth/csrf
 * resolves, and sending THAT value as the header instead of the cookie.
 *
 * This wrapper:
 *   1. Reads the in-memory RequestToken cached from the last successful
 *      GET /api/auth/csrf call (see AuthContext.tsx, which calls that endpoint
 *      on mount and again immediately after every login/MFA verification).
 *   2. Injects the X-XSRF-TOKEN header on every mutating request.
 *   3. Always sets credentials: 'include' so the HttpOnly auth cookie and the
 *      XSRF-TOKEN cookie (read directly by the server, never by this code) are
 *      both sent automatically by the browser.
 *
 * Usage: replace fetch(url, init) with csrfFetch(url, init) throughout
 *        the SPA. No other changes needed at the call site.
 */

const CSRF_HEADER  = 'X-XSRF-TOKEN';
const SAFE_METHODS = new Set(['GET', 'HEAD', 'OPTIONS', 'TRACE']);

// In-memory cache of the RequestToken from the last successful
// GET /api/auth/csrf response body. Never persisted (no localStorage/
// sessionStorage) — a fresh call is required after every full page reload,
// exactly like the XSRF-TOKEN cookie itself, and both are refreshed together
// by the same AuthContext.tsx call sites.
let cachedRequestToken: string | null = null;

/** Called by AuthContext.tsx after every successful GET /api/auth/csrf. */
export function setCsrfRequestToken(token: string | null): void {
  cachedRequestToken = token;
}

/** Exposed for tests / diagnostics; csrfFetch uses this internally. */
export function getCsrfRequestToken(): string | null {
  return cachedRequestToken;
}

/**
 * CSRF-aware fetch wrapper. Identical API to the global fetch(), but:
 * - Always sends cookies (credentials: 'include').
 * - Injects X-XSRF-TOKEN on POST / PUT / PATCH / DELETE, using the cached
 *   RequestToken (body value), never the XSRF-TOKEN cookie value.
 */
export async function csrfFetch(
  input: RequestInfo | URL,
  init: RequestInit = {},
): Promise<Response> {
  const method = (init.method ?? 'GET').toUpperCase();

  const headers = new Headers(init.headers);

  // Always credential-include for HttpOnly auth cookie.
  const credentials: RequestCredentials = 'include';

  if (!SAFE_METHODS.has(method) && cachedRequestToken) {
    headers.set(CSRF_HEADER, cachedRequestToken);
  }

  return fetch(input, { ...init, credentials, headers });
}
