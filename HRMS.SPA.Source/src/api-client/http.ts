/**
 * http.ts — low-level HTTP layer for the generated-style API client.
 *
 * The SPA previously imported `@workspace/api-client-react`, a workspace
 * package that was never shipped with the source archive, so every import of
 * it failed to resolve (TS2307) and the build could not complete. This module
 * plus `./index.ts` provide that package locally; the alias
 * `@workspace/api-client-react` resolves here (see tsconfig/vite/vitest).
 *
 * Auth model: the backend sets HttpOnly cookies, so requests are always sent
 * with `credentials: 'include'`. An optional bearer-token getter is supported
 * for deployments that use header auth instead.
 */

import { csrfFetch } from '@/utils/csrfFetch';

export type QueryParams = Record<
  string,
  string | number | boolean | null | undefined
>;

let authTokenGetter: (() => string | null) | null = null;

/** Register a callback returning the bearer token (or null in cookie mode). */
export function setAuthTokenGetter(getter: (() => string | null) | null): void {
  authTokenGetter = getter;
}

/** Base URL for the API. Empty string means "same origin" (proxied by nginx). */
export const API_BASE_URL: string =
  (import.meta.env?.VITE_API_BASE_URL as string | undefined)?.replace(/\/$/, '') ?? '';

// ── FIX: silent access-token refresh on 401 ─────────────────────────────────
// The backend issues a 30-minute access-token cookie plus a 7-day rotating
// refresh-token cookie and fully implements POST /api/auth/refresh (see
// AuthController.Refresh / AuthService.RefreshTokenAsync, including reuse
// detection and MFA-verified enforcement). Until this fix, no frontend code
// ever called that endpoint: AuthGuard only reacted to a 401/403 on the
// profile check by logging the user out immediately. In practice every
// session was force-logged-out every 30 minutes even though the backend was
// fully capable of renewing it silently, and the refresh-token cookie was
// never used for its intended purpose.
//
// apiRequest now retries exactly once on a 401: it calls /api/auth/refresh
// (which reads the refresh token from its own HttpOnly cookie — no token
// handling in JS) and, if that succeeds, re-issues the original request.
// Concurrent 401s share a single in-flight refresh call so a page that fires
// several requests at once does not trigger a refresh stampede.
const REFRESH_PATH = '/api/auth/refresh';
// Never attempt a refresh-and-retry for the auth endpoints themselves —
// doing so for /api/auth/refresh would be infinite recursion, and doing so
// for /api/auth/login or /api/auth/logout is never the correct behaviour
// (a 401 from login means "wrong credentials", not "expired session").
const NO_REFRESH_PATHS = ['/api/auth/refresh', '/api/auth/login', '/api/auth/logout'];

let refreshInFlight: Promise<boolean> | null = null;

async function tryRefreshAccessToken(): Promise<boolean> {
  if (!refreshInFlight) {
    refreshInFlight = csrfFetch(`${API_BASE_URL}${REFRESH_PATH}`, {
      method: 'POST',
      credentials: 'include',
      headers: { 'Content-Type': 'application/json' },
    })
      .then((res) => res.ok)
      .catch(() => false)
      .finally(() => {
        refreshInFlight = null;
      });
  }
  return refreshInFlight;
}

export class ApiError extends Error {
  readonly status: number;
  readonly payload: unknown;

  constructor(message: string, status: number, payload: unknown) {
    super(message);
    this.name = 'ApiError';
    this.status = status;
    this.payload = payload;
  }
}

export function buildUrl(path: string, params?: QueryParams): string {
  const search = new URLSearchParams();
  if (params) {
    for (const [key, value] of Object.entries(params)) {
      if (value === undefined || value === null || value === '') continue;
      search.append(key, String(value));
    }
  }
  const qs = search.toString();
  return `${API_BASE_URL}${path}${qs ? `?${qs}` : ''}`;
}

async function parseBody(res: Response): Promise<unknown> {
  const text = await res.text();
  if (!text) return null;
  try {
    return JSON.parse(text);
  } catch {
    return text;
  }
}

function extractMessage(payload: unknown, fallback: string): string {
  if (payload && typeof payload === 'object') {
    const rec = payload as Record<string, unknown>;
    for (const key of ['message', 'detail', 'title', 'error']) {
      const v = rec[key];
      if (typeof v === 'string' && v.trim()) return v;
    }
  }
  if (typeof payload === 'string' && payload.trim()) return payload;
  return fallback;
}

export interface RequestOptions {
  method?: 'GET' | 'POST' | 'PUT' | 'PATCH' | 'DELETE';
  params?: QueryParams;
  body?: unknown;
  signal?: AbortSignal;
}

/** Perform a request and return the parsed JSON body, typed as `T`. */
export async function apiRequest<T>(
  path: string,
  options: RequestOptions = {},
): Promise<T> {
  const { method = 'GET', params, body, signal } = options;

  const headers: Record<string, string> = { Accept: 'application/json' };

  const token = authTokenGetter?.() ?? null;
  if (token) headers['Authorization'] = `Bearer ${token}`;

  const isFormData = typeof FormData !== 'undefined' && body instanceof FormData;
  if (body !== undefined && !isFormData) {
    headers['Content-Type'] = 'application/json';
  }

  const doFetch = () =>
    csrfFetch(buildUrl(path, params), {
      method,
      headers,
      signal,
      ...(body === undefined
        ? {}
        : { body: isFormData ? (body as FormData) : JSON.stringify(body) }),
    });

  let res = await doFetch();

  // Silent refresh-and-retry: a 401 on a normal API call most often means the
  // 30-minute access-token cookie has expired, not that the user's session is
  // actually over (the 7-day refresh-token cookie is very likely still valid).
  // Retry exactly once after a successful refresh to avoid surprising the user
  // with a forced logout mid-session. AuthGuard remains the final backstop:
  // if refresh itself fails (expired/revoked/reused refresh token), the retried
  // request will still 401 and the caller's existing error handling — plus
  // AuthGuard's own profile-check — takes over exactly as before this fix.
  if (
    res.status === 401 &&
    !NO_REFRESH_PATHS.some((p) => path.startsWith(p)) &&
    (await tryRefreshAccessToken())
  ) {
    res = await doFetch();
  }

  const payload = await parseBody(res);

  if (!res.ok) {
    throw new ApiError(
      extractMessage(payload, `Request failed with status ${res.status}`),
      res.status,
      payload,
    );
  }

  return payload as T;
}

/**
 * Some endpoints wrap their payload in the backend's ApiResponse<T> envelope
 * (`{ success, message, data, errors }`) and some return the domain object
 * directly. This unwraps the envelope so hook consumers always receive the
 * domain object.
 *
 * FIX: the previous check `Object.keys(rec).length <= 2` never matched a real
 * ApiResponse payload, which always has 4 keys (success, message, data,
 * errors) — see HRMS.Application.Common.ApiResponse<T> (backend). As a result
 * EVERY hook using unwrap() (useGetProfile, useLogin, useGetDashboardSummary,
 * etc. — 14 call sites) received the raw envelope instead of the domain object,
 * so e.g. `profile.role` was always undefined and usePermissions().isAdmin was
 * always false regardless of the real role. The correct signal that a payload
 * is an ApiResponse envelope is the presence of the `success` key (a domain
 * object would never legitimately have this key), not a key count.
 */
export function unwrap<T>(payload: unknown): T {
  if (payload && typeof payload === 'object' && !Array.isArray(payload)) {
    const rec = payload as Record<string, unknown>;
    if ('success' in rec && 'data' in rec) {
      return rec['data'] as T;
    }
  }
  return payload as T;
}
