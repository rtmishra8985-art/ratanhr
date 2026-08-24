/**
 * apiError.ts — Fix #3 & #7: Typed API error utilities.
 *
 * Replaces all `error as any` casts with proper type guards so TypeScript
 * can verify error handling at compile time.
 */

export interface ApiError {
  status: number;
  message: string;
  detail?: string;
}

/** Type guard — true when `error` looks like an ApiError. */
export function isApiError(error: unknown): error is ApiError {
  return (
    typeof error === 'object' &&
    error !== null &&
    'status' in error &&
    typeof (error as Record<string, unknown>).status === 'number'
  );
}

/**
 * Returns the HTTP status code from an error, or 0 if unavailable.
 * Use to branch on 401 / 403 / 404 / 500 etc.
 */
export function getErrorStatus(error: unknown): number {
  if (isApiError(error)) return error.status;
  return 0;
}

/**
 * Returns a human-readable error message from any thrown value.
 * Falls back to `fallback` when no message can be extracted.
 */
export function getErrorMessage(
  error: unknown,
  fallback = 'An unexpected error occurred.',
): string {
  if (isApiError(error) && error.message) return error.message;
  if (error instanceof Error && error.message) return error.message;
  return fallback;
}

/**
 * Returns a user-facing title for an HTTP error status code.
 *
 * 401 → "Session expired"
 * 403 → "Access denied"
 * 404 → "Not found"
 * 5xx → "Server error"
 * else → "Failed to load"
 */
export function getErrorTitle(error: unknown, fallback = 'Failed to load'): string {
  const status = getErrorStatus(error);
  if (status === 401) return 'Session expired';
  if (status === 403) return 'Access denied';
  if (status === 404) return 'Not found';
  if (status >= 500) return 'Server error';
  return fallback;
}

/**
 * Returns a user-facing description for an HTTP error status code.
 */
export function getErrorDescription(error: unknown): string {
  const status = getErrorStatus(error);
  if (status === 401) return 'Your session has expired. Please log in again.';
  if (status === 403) return 'You do not have permission to view this content.';
  if (status === 404) return 'The requested resource could not be found.';
  if (status >= 500) return 'A server error occurred. Please try again later.';
  return getErrorMessage(error, 'There was an error communicating with the server. Please try again.');
}
