/**
 * EmptyState.tsx — unified placeholder for empty lists, zero results, and errors.
 *
 * Added `onRetry` prop (Fix #3 follow-up): when provided, a "Try again" button
 * is rendered below the description. This turns error states into actionable
 * states rather than dead-ends that require a full page refresh.
 */

import { ReactNode } from 'react';
import { LucideIcon, FileX2, RefreshCcw } from 'lucide-react';
import { cn } from '@/lib/utils';
import { Button } from '@/components/ui/button';

interface EmptyStateProps {
  icon?: LucideIcon;
  title: string;
  description?: string;
  action?: ReactNode;
  /** When provided, renders a "Try again" button that calls this callback. */
  onRetry?: () => void;
  className?: string;
}

export function EmptyState({
  icon: Icon = FileX2,
  title,
  description,
  action,
  onRetry,
  className,
}: EmptyStateProps) {
  return (
    <div
      className={cn(
        'flex flex-col items-center justify-center p-8 text-center min-h-[400px] border border-dashed rounded-lg bg-muted/20',
        className,
      )}
    >
      <div className="flex h-20 w-20 items-center justify-center rounded-full bg-muted/50 mb-4">
        <Icon className="h-10 w-10 text-muted-foreground" aria-hidden="true" />
      </div>
      <h3 className="text-xl font-semibold tracking-tight">{title}</h3>
      {description && (
        <p className="text-sm text-muted-foreground max-w-sm mt-2 mb-6">{description}</p>
      )}
      {(action || onRetry) && (
        <div className="flex flex-col sm:flex-row items-center gap-3 mt-6">
          {onRetry && (
            <Button variant="outline" onClick={onRetry}>
              <RefreshCcw className="mr-2 h-4 w-4" aria-hidden="true" />
              Try again
            </Button>
          )}
          {action}
        </div>
      )}
    </div>
  );
}
