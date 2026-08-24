/**
 * tokenStorage.test.ts — Tests for cookie-mode tokenStorage.
 *
 * In cookie mode the module is intentionally a no-op on the client;
 * all token lifecycle is handled by the server via HttpOnly cookies.
 */
import { describe, it, expect, beforeEach } from 'vitest';
import { tokenStorage, COOKIE_MODE_SENTINEL } from '@/utils/tokenStorage';

beforeEach(() => {
  localStorage.clear();
});

describe('tokenStorage (cookie mode)', () => {
  it('get() returns COOKIE_MODE_SENTINEL regardless of localStorage state', () => {
    // Even if something wrote to localStorage, cookie mode ignores it
    localStorage.setItem('hrms_token', 'some-stale-token');
    expect(tokenStorage.get()).toBe(COOKIE_MODE_SENTINEL);
  });

  it('set() is a no-op — does not write to localStorage', () => {
    tokenStorage.set('some-token');
    expect(localStorage.getItem('hrms_token')).toBeNull();
  });

  it('remove() is a no-op — does not modify localStorage', () => {
    localStorage.setItem('hrms_token', 'leftover');
    tokenStorage.remove();
    // localStorage should be untouched by remove() in cookie mode
    expect(localStorage.getItem('hrms_token')).toBe('leftover');
  });

  it('COOKIE_MODE_SENTINEL is the string "__cookie__"', () => {
    expect(COOKIE_MODE_SENTINEL).toBe('__cookie__');
  });
});
