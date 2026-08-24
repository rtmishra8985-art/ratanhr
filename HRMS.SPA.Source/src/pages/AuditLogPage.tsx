/**
 * AuditLogPage — Paginated, filterable audit trail viewer.
 *
 * Fix L-01: Backed by the existing AuditController at GET api/audit.
 *
 * Response shape: ApiResponse<List<AuditLog>>
 * Unwrapped from: { success, message, data: [...] }
 * Note: The endpoint returns a List (not a PagedResult), so total count is unknown.
 *       Page navigation uses hasMore detection: if the server returns fewer items
 *       than pageSize, we are on the last page.
 *
 * AuditLog fields: id, action, entityType, entityId, performedBy, performedByName,
 *                  ipAddress, details, success, occurredAt
 *
 * Query params: action (string filter), userId (int filter), page, pageSize
 *
 * Auth: superadmin only (enforced by server; page shows a clear 403 message if denied).
 */

import { useState } from 'react';
import { Search, ChevronDown, ChevronRight, Shield, ChevronLeft } from 'lucide-react';
import { useQuery } from '@tanstack/react-query';

import { PageHeader } from '@/components/layout/PageHeader';
import { Input } from '@/components/ui/input';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { SkeletonTable } from '@/components/shared/SkeletonTable';
import { EmptyState } from '@/components/shared/EmptyState';
import { useDebounce } from '@/hooks/useDebounce';
import { csrfFetch } from '@/utils/csrfFetch';

// ─── Types ────────────────────────────────────────────────────────────────────

interface ApiResponse<T> {
  success: boolean;
  message: string;
  data: T;
  errors: string[];
}

/** Shape of AuditLog as serialised by the API (camelCase). */
interface AuditLog {
  id: number;
  action: string;          // e.g. "LOGIN_SUCCESS", "EMPLOYEE_CREATE"
  entityType: string;      // e.g. "Employee", "Payslip"
  entityId: string | null;
  performedBy: number | null;
  performedByName: string | null;
  ipAddress: string | null;
  details: string | null;  // JSON string or free text
  success: boolean;
  occurredAt: string;      // ISO-8601
}

// ─── API fetch ────────────────────────────────────────────────────────────────

async function fetchAuditLogs(
  page: number,
  pageSize: number,
  action: string,
  userId: string,
): Promise<AuditLog[]> {
  const params = new URLSearchParams({
    page: String(page),
    pageSize: String(pageSize),
  });
  if (action.trim()) params.set('action', action.trim());
  if (userId.trim()) params.set('userId', userId.trim());

  const res = await csrfFetch(`/api/audit?${params}`, { credentials: 'include' });

  // Handle 403 separately — superadmin-only endpoint
  if (res.status === 403) throw new Error('Access denied. Audit Log is visible to superadmin only.');
  if (res.status === 401) throw new Error('You must be logged in to view the audit log.');

  const json: ApiResponse<AuditLog[]> = await res.json();
  if (!res.ok || !json.success) throw new Error(json.message || `HTTP ${res.status}`);
  return json.data;
}

// ─── Outcome badge ────────────────────────────────────────────────────────────

function OutcomeBadge({ success }: { success: boolean }) {
  return (
    <Badge variant={success ? 'default' : 'destructive'} className="text-xs">
      {success ? 'Success' : 'Failed'}
    </Badge>
  );
}

// ─── Action badge ─────────────────────────────────────────────────────────────

function ActionBadge({ action }: { action: string }) {
  const base = action.split('_')[0]; // LOGIN, EMPLOYEE, PAYSLIP …
  const variantMap: Record<string, 'default' | 'secondary' | 'destructive' | 'outline'> = {
    LOGIN: 'outline',
    LOGOUT: 'outline',
    CREATE: 'default',
    UPDATE: 'secondary',
    DELETE: 'destructive',
  };
  const variant = variantMap[base] ?? 'outline';
  return (
    <Badge variant={variant} className="font-mono text-xs whitespace-nowrap">
      {action}
    </Badge>
  );
}

// ─── Expandable detail row ────────────────────────────────────────────────────

function AuditRow({ entry }: { entry: AuditLog }) {
  const [expanded, setExpanded] = useState(false);

  // Try to pretty-print if details looks like JSON, otherwise show as plain text
  const renderDetails = () => {
    if (!entry.details) return null;
    try {
      const parsed = JSON.parse(entry.details);
      return (
        <pre className="bg-muted/40 border rounded p-3 text-xs font-mono overflow-auto max-h-48 whitespace-pre-wrap break-all">
          {JSON.stringify(parsed, null, 2)}
        </pre>
      );
    } catch {
      return (
        <p className="bg-muted/40 border rounded p-3 text-xs font-mono whitespace-pre-wrap break-all">
          {entry.details}
        </p>
      );
    }
  };

  const hasDetails = Boolean(entry.details);

  return (
    <>
      <TableRow className="hover:bg-muted/40">
        <TableCell className="text-xs text-muted-foreground whitespace-nowrap">
          {new Date(entry.occurredAt).toLocaleString()}
        </TableCell>
        <TableCell>
          <span className="text-sm font-medium">{entry.performedByName ?? '—'}</span>
          {entry.performedBy && (
            <div className="text-xs text-muted-foreground font-mono">#{entry.performedBy}</div>
          )}
        </TableCell>
        <TableCell>
          <ActionBadge action={entry.action} />
        </TableCell>
        <TableCell className="text-sm">
          <span className="font-medium">{entry.entityType}</span>
          {entry.entityId && (
            <span className="text-muted-foreground ml-1 font-mono text-xs">#{entry.entityId}</span>
          )}
        </TableCell>
        <TableCell className="text-xs text-muted-foreground font-mono">
          {entry.ipAddress ?? '—'}
        </TableCell>
        <TableCell>
          <OutcomeBadge success={entry.success} />
        </TableCell>
        <TableCell className="text-right">
          {hasDetails ? (
            <Button
              variant="ghost"
              size="sm"
              onClick={() => setExpanded((v) => !v)}
              aria-label={expanded ? 'Collapse details' : 'Expand details'}
            >
              {expanded ? (
                <ChevronDown className="h-4 w-4" />
              ) : (
                <ChevronRight className="h-4 w-4" />
              )}
            </Button>
          ) : (
            <span className="text-muted-foreground text-xs pr-2">—</span>
          )}
        </TableCell>
      </TableRow>

      {expanded && hasDetails && (
        <TableRow className="bg-muted/20">
          <TableCell colSpan={7} className="p-4">
            <p className="text-xs font-medium text-muted-foreground uppercase tracking-wide mb-2">Details</p>
            {renderDetails()}
          </TableCell>
        </TableRow>
      )}
    </>
  );
}

// ─── Page ─────────────────────────────────────────────────────────────────────

const PAGE_SIZE = 50;

export default function AuditLogPage() {
  const [actionFilter, setActionFilter] = useState('');
  const [userIdFilter, setUserIdFilter] = useState('');
  const [page, setPage] = useState(1);

  const debouncedAction = useDebounce(actionFilter, 400);
  const debouncedUserId = useDebounce(userIdFilter, 400);

  const { data, isLoading, isError, error, refetch } = useQuery<AuditLog[]>({
    queryKey: ['audit-log', page, PAGE_SIZE, debouncedAction, debouncedUserId],
    queryFn: () => fetchAuditLogs(page, PAGE_SIZE, debouncedAction, debouncedUserId),
  });

  // hasMore: if the server returned a full page, there may be more
  const hasMore = (data?.length ?? 0) >= PAGE_SIZE;
  const hasPrev = page > 1;

  return (
    <div className="space-y-6">
      <PageHeader
        title="Audit Log"
        description="Immutable record of all system actions. Superadmin only. Use for compliance, forensics, and change tracking."
      />

      {/* Filters */}
      <div className="flex flex-wrap gap-3">
        <div className="relative">
          <Search className="absolute left-3 top-1/2 -translate-y-1/2 h-4 w-4 text-muted-foreground pointer-events-none" />
          <Input
            className="pl-9 w-56"
            placeholder="Filter by action…"
            value={actionFilter}
            onChange={(e) => { setActionFilter(e.target.value); setPage(1); }}
          />
        </div>
        <Input
          className="w-40"
          placeholder="User ID…"
          type="number"
          min="1"
          value={userIdFilter}
          onChange={(e) => { setUserIdFilter(e.target.value); setPage(1); }}
        />
      </div>

      <div className="bg-card border rounded-lg shadow-sm overflow-hidden">
        {isLoading ? (
          <SkeletonTable columns={7} rows={10} />
        ) : isError ? (
          <EmptyState
            icon={Shield}
            title="Cannot load audit log"
            description={(error as Error)?.message ?? 'An unexpected error occurred.'}
            onRetry={refetch}
          />
        ) : !data?.length ? (
          <EmptyState
            icon={Shield}
            title={debouncedAction || debouncedUserId ? 'No matching entries' : 'No audit entries yet'}
            description={
              debouncedAction || debouncedUserId
                ? 'Try different filter values.'
                : 'Audit entries appear here as users interact with the system.'
            }
          />
        ) : (
          <>
            <div className="overflow-x-auto">
              <Table>
                <TableHeader>
                  <TableRow className="bg-muted/50">
                    <TableHead className="whitespace-nowrap">Timestamp</TableHead>
                    <TableHead>Actor</TableHead>
                    <TableHead>Action</TableHead>
                    <TableHead>Entity</TableHead>
                    <TableHead>IP Address</TableHead>
                    <TableHead>Outcome</TableHead>
                    <TableHead className="text-right">Details</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {data.map((entry) => (
                    <AuditRow key={entry.id} entry={entry} />
                  ))}
                </TableBody>
              </Table>
            </div>

            {/* Prev / Next navigation (no total count from server) */}
            <div className="flex items-center justify-between px-4 py-3 border-t text-sm text-muted-foreground">
              <span>
                Page {page}
                {data.length > 0 && ` · ${data.length} entries`}
              </span>
              <div className="flex gap-2">
                <Button
                  variant="outline"
                  size="sm"
                  onClick={() => setPage((p) => Math.max(1, p - 1))}
                  disabled={!hasPrev}
                >
                  <ChevronLeft className="h-4 w-4 mr-1" /> Prev
                </Button>
                <Button
                  variant="outline"
                  size="sm"
                  onClick={() => setPage((p) => p + 1)}
                  disabled={!hasMore}
                >
                  Next <ChevronRight className="h-4 w-4 ml-1" />
                </Button>
              </div>
            </div>
          </>
        )}
      </div>
    </div>
  );
}
