import { describe, it, expect } from 'vitest';
import {
  getUserInitials,
  getDisplayName,
  getDesignation,
  getDepartment,
  getEmail,
  getRole,
  formatCurrency,
} from '@/utils/profileHelpers';

// ─── getUserInitials ──────────────────────────────────────────────────────────

describe('getUserInitials', () => {
  it('returns initials from firstName and lastName', () => {
    expect(getUserInitials({ firstName: 'Ratan', lastName: 'Sharma' })).toBe('RS');
  });

  it('uppercases the initials', () => {
    expect(getUserInitials({ firstName: 'john', lastName: 'doe' })).toBe('JD');
  });

  it('falls back to fullName when first/last are absent', () => {
    expect(getUserInitials({ fullName: 'Priya Nair' })).toBe('PN');
  });

  // Spec: single-word name returns first character only (e.g. "John" → "J")
  it('returns first letter only for a single-word fullName', () => {
    expect(getUserInitials({ fullName: 'Admin' })).toBe('A');
  });

  it('returns first letter only for a single firstName with no lastName', () => {
    expect(getUserInitials({ firstName: 'John' })).toBe('J');
  });

  // Spec: empty / null / undefined → 'U'
  it('returns "U" when profile is null', () => {
    expect(getUserInitials(null)).toBe('U');
  });

  it('returns "U" when profile is undefined', () => {
    expect(getUserInitials(undefined)).toBe('U');
  });

  it('returns "U" when all name fields are empty strings', () => {
    expect(getUserInitials({ firstName: '', lastName: '', fullName: '' })).toBe('U');
  });

  it('returns "U" when fullName is null', () => {
    expect(getUserInitials({ fullName: null })).toBe('U');
  });

  it('returns "U" for an empty object {}', () => {
    expect(getUserInitials({})).toBe('U');
  });

  // Phase 10 test cases from spec
  it('Phase10 Case1: { fullName: "John Smith" } → "JS"', () => {
    expect(getUserInitials({ fullName: 'John Smith' })).toBe('JS');
  });

  it('Phase10 Case2: { fullName: "John" } → "J"', () => {
    expect(getUserInitials({ fullName: 'John' })).toBe('J');
  });

  it('Phase10 Case3: { fullName: "" } → "U"', () => {
    expect(getUserInitials({ fullName: '' })).toBe('U');
  });

  it('Phase10 Case4: { fullName: null } → "U"', () => {
    expect(getUserInitials({ fullName: null })).toBe('U');
  });

  it('Phase10 Case5: {} → "U"', () => {
    expect(getUserInitials({})).toBe('U');
  });

  it('Phase10 Case6: null → "U"', () => {
    expect(getUserInitials(null)).toBe('U');
  });
});

// ─── getDisplayName ───────────────────────────────────────────────────────────

describe('getDisplayName', () => {
  it('combines firstName and lastName', () => {
    expect(getDisplayName({ firstName: 'Ratan', lastName: 'Sharma' })).toBe('Ratan Sharma');
  });

  it('trims extra whitespace when lastName is absent', () => {
    expect(getDisplayName({ firstName: 'Ratan' })).toBe('Ratan');
  });

  it('falls back to fullName', () => {
    expect(getDisplayName({ fullName: 'Priya Nair' })).toBe('Priya Nair');
  });

  it('returns fallback string when profile is null', () => {
    expect(getDisplayName(null)).toBe('Unknown User');
  });

  it('returns fallback string for empty object {}', () => {
    expect(getDisplayName({})).toBe('Unknown User');
  });

  it('returns fallback string when fullName is empty string', () => {
    expect(getDisplayName({ fullName: '' })).toBe('Unknown User');
  });
});

// ─── getDesignation ───────────────────────────────────────────────────────────

describe('getDesignation', () => {
  it('returns designation when present', () => {
    expect(getDesignation({ designation: 'Software Engineer' })).toBe('Software Engineer');
  });

  it('returns fallback when designation is null', () => {
    expect(getDesignation({ designation: null })).toBe('Employee');
  });

  it('returns fallback when profile is undefined', () => {
    expect(getDesignation(undefined)).toBe('Employee');
  });

  it('returns fallback when designation is empty string', () => {
    expect(getDesignation({ designation: '' })).toBe('Employee');
  });
});

// ─── getDepartment ────────────────────────────────────────────────────────────

describe('getDepartment', () => {
  it('returns departmentName when present', () => {
    expect(getDepartment({ departmentName: 'Engineering' })).toBe('Engineering');
  });

  it('returns fallback when absent', () => {
    expect(getDepartment(null)).toBe('Not Assigned');
  });

  it('returns fallback for empty object', () => {
    expect(getDepartment({})).toBe('Not Assigned');
  });
});

// ─── getEmail ─────────────────────────────────────────────────────────────────

describe('getEmail', () => {
  it('returns email when present', () => {
    expect(getEmail({ email: 'ratan@example.com' })).toBe('ratan@example.com');
  });

  // Spec: fallback is "No Email"
  it('returns "No Email" when email is absent', () => {
    expect(getEmail({})).toBe('No Email');
  });

  it('returns "No Email" when email is null', () => {
    expect(getEmail({ email: null })).toBe('No Email');
  });

  it('returns "No Email" when email is empty string', () => {
    expect(getEmail({ email: '' })).toBe('No Email');
  });

  it('returns "No Email" when profile is null', () => {
    expect(getEmail(null)).toBe('No Email');
  });

  it('returns "No Email" when profile is undefined', () => {
    expect(getEmail(undefined)).toBe('No Email');
  });
});

// ─── getRole ─────────────────────────────────────────────────────────────────

describe('getRole', () => {
  it('returns role when present', () => {
    expect(getRole({ role: 'Admin' })).toBe('Admin');
  });

  it('returns fallback when role is empty string', () => {
    expect(getRole({ role: '' })).toBe('Employee');
  });

  it('returns fallback when profile is null', () => {
    expect(getRole(null)).toBe('Employee');
  });
});

// ─── formatCurrency ───────────────────────────────────────────────────────────

describe('formatCurrency', () => {
  it('formats a positive number', () => {
    const result = formatCurrency(50000);
    expect(result).toContain('50,000');
  });

  it('returns "₹0" for null', () => {
    expect(formatCurrency(null)).toBe('₹0');
  });

  it('returns "₹0" for undefined', () => {
    expect(formatCurrency(undefined)).toBe('₹0');
  });

  it('returns "₹0" for zero', () => {
    expect(formatCurrency(0)).toBe('₹0');
  });

  it('formats a decimal number', () => {
    const result = formatCurrency(1234.56);
    expect(result).toContain('1,234');
  });
});
