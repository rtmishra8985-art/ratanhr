/**
 * usePermissions.ts — Fix #2: UI-level Role-Based Access Control.
 *
 * Reads the current user's role from the profile API and exposes
 * boolean flags that components use to show/hide privileged actions.
 *
 * Usage:
 *   const { isAdmin, isManager } = usePermissions();
 *   {isAdmin && <Button>Delete</Button>}
 */

import { useGetProfile, getGetProfileQueryKey } from '@workspace/api-client-react';

export interface Permissions {
  /** Full access — can create, edit, delete, and process payroll. */
  isAdmin: boolean;
  /** True only for the superadmin role. Some endpoints (e.g. GET /api/audit)
   * are superadmin-only even though isAdmin is also true for a plain admin. */
  isSuperAdmin: boolean;
  /** Can approve leaves, assign assets, and view all employee records. */
  isManager: boolean;
  /** Base role — every authenticated user is an employee. */
  isEmployee: boolean;
  /** Raw role string from the API (lowercased), e.g. "admin", "manager", "employee". */
  role: string;
  /**
   * True while the profile (and therefore role) has not resolved yet.
   * FIX: components that branch rendering on isAdmin (e.g. DashboardPage) must
   * wait for this to be false before deciding which view to show — otherwise
   * every session (including Admin/SuperAdmin) briefly renders as isAdmin=false
   * on first paint, firing employee-only API calls that 403 for those roles.
   */
  isLoading: boolean;
}

const ADMIN_ROLES = new Set(['admin', 'superadmin', 'administrator', 'hr admin', 'hradmin']);
const MANAGER_ROLES = new Set(['manager', 'hr manager', 'hrmanager', 'supervisor', 'team lead', 'teamlead']);

export function usePermissions(): Permissions {
  const { data: profile, isLoading } = useGetProfile({
    query: {
      queryKey: getGetProfileQueryKey(),
    },
  });

  const role = (typeof profile?.role === 'string' ? profile.role.trim().toLowerCase() : '') as string;

  const isAdmin = ADMIN_ROLES.has(role);
  const isSuperAdmin = role === 'superadmin';
  const isManager = isAdmin || MANAGER_ROLES.has(role);

  return {
    isAdmin,
    isSuperAdmin,
    isManager,
    isEmployee: true,
    role,
    isLoading,
  };
}
