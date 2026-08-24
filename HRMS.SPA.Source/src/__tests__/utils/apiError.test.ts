import { describe, it, expect } from 'vitest';
import {
  isApiError,
  getErrorStatus,
  getErrorMessage,
  getErrorTitle,
  getErrorDescription,
} from '@/utils/apiError';

// ─── isApiError ───────────────────────────────────────────────────────────────

describe('isApiError', () => {
  it('returns true for an object with a numeric status', () => {
    expect(isApiError({ status: 403, message: 'Forbidden' })).toBe(true);
  });

  it('returns false for a plain Error', () => {
    expect(isApiError(new Error('Something went wrong'))).toBe(false);
  });

  it('returns false for null', () => {
    expect(isApiError(null)).toBe(false);
  });

  it('returns false for undefined', () => {
    expect(isApiError(undefined)).toBe(false);
  });

  it('returns false when status is a string', () => {
    expect(isApiError({ status: '403' })).toBe(false);
  });

  it('returns false for a plain string', () => {
    expect(isApiError('error')).toBe(false);
  });
});

// ─── getErrorStatus ───────────────────────────────────────────────────────────

describe('getErrorStatus', () => {
  it('returns the numeric status from an ApiError', () => {
    expect(getErrorStatus({ status: 404, message: 'Not found' })).toBe(404);
  });

  it('returns 0 for a plain Error', () => {
    expect(getErrorStatus(new Error('fail'))).toBe(0);
  });

  it('returns 0 for null', () => {
    expect(getErrorStatus(null)).toBe(0);
  });
});

// ─── getErrorMessage ──────────────────────────────────────────────────────────

describe('getErrorMessage', () => {
  it('extracts message from an ApiError', () => {
    expect(getErrorMessage({ status: 500, message: 'Internal server error' })).toBe(
      'Internal server error',
    );
  });

  it('extracts message from a plain Error', () => {
    expect(getErrorMessage(new Error('Network timeout'))).toBe('Network timeout');
  });

  it('returns the fallback for null', () => {
    expect(getErrorMessage(null, 'Default fallback')).toBe('Default fallback');
  });

  it('uses the default fallback when none supplied', () => {
    expect(getErrorMessage(undefined)).toBe('An unexpected error occurred.');
  });
});

// ─── getErrorTitle ────────────────────────────────────────────────────────────

describe('getErrorTitle', () => {
  it('returns "Session expired" for 401', () => {
    expect(getErrorTitle({ status: 401, message: '' })).toBe('Session expired');
  });

  it('returns "Access denied" for 403', () => {
    expect(getErrorTitle({ status: 403, message: '' })).toBe('Access denied');
  });

  it('returns "Not found" for 404', () => {
    expect(getErrorTitle({ status: 404, message: '' })).toBe('Not found');
  });

  it('returns "Server error" for 500', () => {
    expect(getErrorTitle({ status: 500, message: '' })).toBe('Server error');
  });

  it('returns "Server error" for 503', () => {
    expect(getErrorTitle({ status: 503, message: '' })).toBe('Server error');
  });

  it('returns the custom fallback for unknown statuses', () => {
    expect(getErrorTitle({ status: 409, message: '' }, 'Conflict')).toBe('Conflict');
  });
});

// ─── getErrorDescription ─────────────────────────────────────────────────────

describe('getErrorDescription', () => {
  it('returns a session expired message for 401', () => {
    expect(getErrorDescription({ status: 401, message: '' })).toMatch(/session/i);
  });

  it('returns a permission message for 403', () => {
    expect(getErrorDescription({ status: 403, message: '' })).toMatch(/permission/i);
  });

  it('uses the ApiError message for unknown status when present', () => {
    expect(
      getErrorDescription({ status: 409, message: 'Conflict detected' }),
    ).toBe('Conflict detected');
  });
});
