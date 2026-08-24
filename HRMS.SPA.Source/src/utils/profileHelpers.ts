/**
 * profileHelpers.ts
 * Defensive utility functions for safely accessing profile and user data.
 * Every function handles null, undefined, empty string, and empty objects
 * without throwing. No function ever returns undefined — callers always
 * receive a safe, renderable string.
 *
 * Fallback contract (kept consistent with unit tests):
 *   getUserInitials  → 'U'          (single-word name → first char only; empty/null → 'U')
 *   getDisplayName   → 'Unknown User'
 *   getDesignation   → 'Employee'
 *   getDepartment    → 'Not Assigned'
 *   getEmail         → 'No Email'
 *   getPhone         → 'Not Available'
 *   getRole          → 'Employee'   (not 'Role')
 *   getCompany       → 'Unknown Company'
 *   getBranch        → 'Unknown Branch'
 */

export interface ProfileLike {
  fullName?: string | null;
  firstName?: string | null;
  lastName?: string | null;
  designation?: string | null;
  departmentName?: string | null;
  email?: string | null;
  phone?: string | null;
  role?: string | null;
  /** Legacy — prefer companyName (matches UserProfile from domain.ts). */
  company?: string | null;
  /** Current API shape — matches UserProfile.companyName in domain.ts. */
  companyName?: string | null;
  /** Legacy — prefer branchName (matches UserProfile from domain.ts). */
  branch?: string | null;
  /** Current API shape — matches UserProfile.branchName in domain.ts. */
  branchName?: string | null;
  image?: string | null;
  avatarUrl?: string | null;
}

// ─── Helpers ──────────────────────────────────────────────────────────────────

function trimStr(v: unknown): string {
  return typeof v === 'string' ? v.trim() : '';
}

// ─── Exported functions ───────────────────────────────────────────────────────

/**
 * Returns 1–2 uppercase initials for an avatar.
 *
 *   getUserInitials({ firstName: 'John', lastName: 'Smith' }) → 'JS'
 *   getUserInitials({ fullName: 'John' })                     → 'J'   (single word → first char only)
 *   getUserInitials({ fullName: '' })                         → 'U'
 *   getUserInitials(null)                                     → 'U'
 *   getUserInitials(undefined)                                → 'U'
 */
export function getUserInitials(profile?: ProfileLike | null): string {
  if (!profile) return 'U';

  const rawName =
    trimStr(profile.fullName) ||
    [trimStr(profile.firstName), trimStr(profile.lastName)].filter(Boolean).join(' ');

  if (!rawName) return 'U';

  const parts = rawName.split(/\s+/).filter(Boolean);
  if (parts.length === 0 || !parts[0]) return 'U';

  const first  = parts[0].charAt(0).toUpperCase();
  // Two-word name: first initial + last initial (e.g. "John Smith" → "JS")
  // Single-word name: first initial only (e.g. "John" → "J")
  const second = parts.length > 1
    ? parts[parts.length - 1].charAt(0).toUpperCase()
    : '';

  return (first + second) || 'U';
}

/**
 * Returns the full display name, falling back to "Unknown User".
 */
export function getDisplayName(profile?: ProfileLike | null): string {
  if (!profile) return 'Unknown User';

  const full =
    trimStr(profile.fullName) ||
    [trimStr(profile.firstName), trimStr(profile.lastName)].filter(Boolean).join(' ');

  return full || 'Unknown User';
}

/**
 * Returns the designation / job title, falling back to "Employee".
 */
export function getDesignation(profile?: ProfileLike | null): string {
  if (!profile) return 'Employee';
  return trimStr(profile.designation) || 'Employee';
}

/**
 * Returns the department name, falling back to "Not Assigned".
 */
export function getDepartment(profile?: ProfileLike | null): string {
  if (!profile) return 'Not Assigned';
  return trimStr(profile.departmentName) || 'Not Assigned';
}

/**
 * Returns the email address, falling back to "No Email".
 */
export function getEmail(profile?: ProfileLike | null): string {
  if (!profile) return 'No Email';
  return trimStr(profile.email) || 'No Email';
}

/**
 * Returns the phone number, falling back to "Not Available".
 */
export function getPhone(profile?: ProfileLike | null): string {
  if (!profile) return 'Not Available';
  return trimStr(profile.phone) || 'Not Available';
}

/**
 * Returns the role string, falling back to "Employee".
 */
export function getRole(profile?: ProfileLike | null): string {
  if (!profile) return 'Employee';
  return trimStr(profile.role) || 'Employee';
}

/**
 * Returns the company name, falling back to "Unknown Company".
 */
export function getCompany(profile?: ProfileLike | null): string {
  if (!profile) return 'Unknown Company';
  return trimStr(profile.companyName) || trimStr(profile.company) || 'Unknown Company';
}

/**
 * Returns the branch name, falling back to "Unknown Branch".
 */
export function getBranch(profile?: ProfileLike | null): string {
  if (!profile) return 'Unknown Branch';
  return trimStr(profile.branchName) || trimStr(profile.branch) || 'Unknown Branch';
}

/**
 * Returns the avatar/image URL if it is a non-empty string, otherwise null.
 * Callers should render initials when this returns null.
 */
export function getAvatarUrl(profile?: ProfileLike | null): string | null {
  if (!profile) return null;
  return trimStr(profile.avatarUrl) || trimStr(profile.image) || null;
}

/**
 * Safely formats a number as currency.
 * BUGFIX: previously hardcoded the '$' (USD) symbol, but this is an Indian
 * payroll system (PAN/Aadhaar/GST fields, default state "Maharashtra", and
 * every other page — PayrollPage, BonusDeductionPage, ReportsPage, SalesPage,
 * ExpensesPage, TravelPage — renders amounts with the ₹ symbol). Every
 * payslip/salary/bonus/deduction amount rendered through this helper was
 * showing the wrong currency symbol. Now uses ₹ with Indian digit grouping
 * (en-IN locale, e.g. ₹1,00,000 not ₹100,000) to match the rest of the app.
 * Returns "₹0" for null, undefined, NaN, or 0.
 *
 *   formatCurrency(50000)  → "₹50,000"
 *   formatCurrency(null)   → "₹0"
 *   formatCurrency(0)      → "₹0"
 */
export function formatCurrency(value?: number | null): string {
  if (value == null || !isFinite(value) || value === 0) return '₹0';
  return `₹${value.toLocaleString('en-IN')}`;
}
