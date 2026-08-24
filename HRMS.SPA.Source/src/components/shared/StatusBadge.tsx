import { Badge } from '@/components/ui/badge';
import { cn } from '@/lib/utils';

interface StatusBadgeProps {
  /** Accepts null or undefined safely — renders a neutral "Unknown" badge instead of crashing. */
  status?: string | null;
  className?: string;
}

export function StatusBadge({ status, className }: StatusBadgeProps) {
  // Defensive: guard against null / undefined / non-string values
  const safeStatus = typeof status === 'string' ? status : '';
  const normalized = safeStatus.toLowerCase().replace(/_/g, '').replace(/\s+/g, '');
  
  let variant: "default" | "secondary" | "destructive" | "outline" = "default";
  let classes = "";
  
  switch (normalized) {
    case 'active':
    case 'approved':
    case 'present':
    case 'resolved':
    case 'closed':
    case 'completed':
    case 'paid':
      classes = "bg-green-100 text-green-800 dark:bg-green-900/30 dark:text-green-400 border-green-200 dark:border-green-800";
      variant = "outline";
      break;
    case 'inactive':
    case 'rejected':
    case 'absent':
    case 'failed':
      variant = "destructive";
      break;
    case 'pending':
    case 'inprogress':
    case 'late':
    case 'open':
    case 'under_review':
      classes = "bg-yellow-100 text-yellow-800 dark:bg-yellow-900/30 dark:text-yellow-400 border-yellow-200 dark:border-yellow-800";
      variant = "outline";
      break;
    default:
      variant = "secondary";
  }

  return (
    <Badge variant={variant} className={cn("font-medium capitalize", classes, className)}>
      {safeStatus ? safeStatus.replace(/_/g, ' ') : 'Unknown'}
    </Badge>
  );
}

export function PriorityBadge({ priority, className }: { priority?: string | null; className?: string }) {
  // Defensive: guard against null / undefined / non-string values
  const safePriority = typeof priority === 'string' ? priority : '';
  const normalized = safePriority.toLowerCase();
  
  let classes = "";
  
  switch (normalized) {
    case 'critical':
      classes = "bg-red-100 text-red-800 dark:bg-red-900/30 dark:text-red-400 border-red-200 dark:border-red-800";
      break;
    case 'high':
      classes = "bg-orange-100 text-orange-800 dark:bg-orange-900/30 dark:text-orange-400 border-orange-200 dark:border-orange-800";
      break;
    case 'medium':
      classes = "bg-yellow-100 text-yellow-800 dark:bg-yellow-900/30 dark:text-yellow-400 border-yellow-200 dark:border-yellow-800";
      break;
    case 'low':
      classes = "bg-green-100 text-green-800 dark:bg-green-900/30 dark:text-green-400 border-green-200 dark:border-green-800";
      break;
    default:
      classes = "bg-gray-100 text-gray-800 dark:bg-gray-800 dark:text-gray-300 border-gray-200 dark:border-gray-700";
  }

  return (
    <Badge variant="outline" className={cn("font-medium capitalize", classes, className)}>
      {safePriority || 'Unknown'}
    </Badge>
  );
}
