/**
 * badgeVariants.ts — Centralised status → Badge variant mapping.
 * Eliminates copy-paste of identical switch/map patterns across pages.
 * 
 * Usage:
 *   import { statusVariant, leaveStatusVariant } from '@/utils/badgeVariants';
 *   <Badge variant={statusVariant(item.status)}>{item.status}</Badge>
 */


type BadgeVariant = 'default' | 'secondary' | 'destructive' | 'outline';

// ── Generic status (Approved / Pending / Rejected / Active / Inactive) ────────

const STATUS_MAP: Record<string, BadgeVariant> = {
  approved:    'default',
  active:      'default',
  completed:   'default',
  open:        'default',
  accepted:    'default',
  won:         'default',
  paid:        'default',
  present:     'default',

  pending:           'secondary',
  'pending approval': 'secondary',
  submitted:         'secondary',
  draft:             'outline',
  under_review:      'secondary',

  rejected:    'destructive',
  terminated:  'destructive',
  inactive:    'destructive',
  closed:      'destructive',
  lost:        'destructive',
  overdue:     'destructive',
  absent:      'destructive',
  failed:      'destructive',
  cancelled:   'destructive',

  late:        'outline',
  'on leave':  'outline',
  half_day:    'outline',
};

/**
 * Returns the Badge variant for a given status string.
 * Case-insensitive. Falls back to 'outline' for unknown values.
 */
export function statusVariant(status?: string | null): BadgeVariant {
  if (!status) return 'outline';
  return STATUS_MAP[status.toLowerCase()] ?? 'outline';
}

// ── Leave-specific ────────────────────────────────────────────────────────────

export function leaveStatusVariant(status?: string | null): BadgeVariant {
  if (!status) return 'outline';
  switch (status.toLowerCase()) {
    case 'approved':  return 'default';
    case 'pending':   return 'secondary';
    case 'rejected':  return 'destructive';
    default:          return 'outline';
  }
}

// ── Priority badge (Helpdesk / Recruitment) ───────────────────────────────────

const PRIORITY_MAP: Record<string, BadgeVariant> = {
  critical:  'destructive',
  high:      'destructive',
  medium:    'secondary',
  low:       'outline',
  normal:    'outline',
};

export function priorityVariant(priority?: string | null): BadgeVariant {
  if (!priority) return 'outline';
  return PRIORITY_MAP[priority.toLowerCase()] ?? 'outline';
}
