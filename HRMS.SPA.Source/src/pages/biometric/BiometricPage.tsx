// Restored: Biometric log viewer SPA page.
// Read-only view of biometric device punch records, with date-range filter and CSV export.
// SEC-BIOMETRIC-01: All API calls use credentials: 'include' (cookie-based auth).
//            Fixed query param from employeeId → userId (matches backend BiometricLogFilterDto)
import { useState } from 'react';
import { keepPreviousData, useQuery } from '@tanstack/react-query';
import { Download } from 'lucide-react';

import { PageHeader }  from '@/components/layout/PageHeader';
import { Button }      from '@/components/ui/button';
import { Input }       from '@/components/ui/input';
import { Badge }       from '@/components/ui/badge';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Skeleton }    from '@/components/ui/skeleton';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { csrfFetch } from '@/utils/csrfFetch';

const BASE = import.meta.env.BASE_URL?.replace(/\/$/, '') ?? '';

// ─── Types ────────────────────────────────────────────────────────────────────

interface BiometricLog {
  id: number;
  employeeId: string;
  employeeName: string;
  deviceId: string;
  punchType: 'IN' | 'OUT';
  logTime: string;   // ISO
  location?: string;
  isProcessed: boolean;
}

interface PagedLogs { items: BiometricLog[]; totalCount: number; page: number; pageSize: number; }

// ─── Helpers ──────────────────────────────────────────────────────────────────

function fmtDateTime(iso: string) {
  try { return new Date(iso).toLocaleString('en-IN', { dateStyle: 'medium', timeStyle: 'short' }); }
  catch { return iso; }
}

// ─── Component ────────────────────────────────────────────────────────────────

export default function BiometricPage() {
  const today = new Date().toISOString().slice(0, 10);
  const [from,   setFrom]   = useState(today);
  const [to,     setTo]     = useState(today);
  const [empId,  setEmpId]  = useState('');
  const [page,   setPage]   = useState(1);
  const pageSize = 25;

  const qKey = ['biometric', from, to, empId, page];

  const { data, isLoading, isFetching } = useQuery<PagedLogs>({
    queryKey: qKey,
    queryFn: async () => {

      // HOTFIX P0: corrected param employeeId → userId (backend uses userId)
      const params = new URLSearchParams({
        from, to, page: String(page), pageSize: String(pageSize),
        ...(empId ? { userId: empId } : {}),
      });
      const res = await csrfFetch(`${BASE}/api/biometric/logs?${params}`, { credentials: 'include' });
      if (!res.ok) throw new Error('Failed to load biometric logs');
      return (await res.json()) as PagedLogs;
    },
    placeholderData: keepPreviousData,
  });

  const exportCsv = () => {

    const params = new URLSearchParams({ from, to, ...(empId ? { userId: empId } : {}), format: 'csv' });
    window.open(`${BASE}/api/biometric/logs/export?${params}`, '_blank');
  };

  const logs  = data?.items ?? [];
  const total = data?.totalCount ?? 0;
  const pages = Math.ceil(total / pageSize);

  return (
    <div className="space-y-6">
      <PageHeader
        title="Biometric Logs"
        description="View punch-in/out records captured from biometric devices."
        actions={
          <Button variant="outline" onClick={exportCsv}>
            <Download className="h-4 w-4 mr-2" />Export CSV
          </Button>
        }
      />

      {/* Filters */}
      <Card>
        <CardHeader><CardTitle className="text-sm font-medium">Filters</CardTitle></CardHeader>
        <CardContent>
          <div className="flex flex-wrap gap-4 items-end">
            <div className="flex flex-col gap-1">
              <label className="text-xs text-muted-foreground">From</label>
              <Input type="date" value={from} onChange={e => { setFrom(e.target.value); setPage(1); }} className="w-40" />
            </div>
            <div className="flex flex-col gap-1">
              <label className="text-xs text-muted-foreground">To</label>
              <Input type="date" value={to} onChange={e => { setTo(e.target.value); setPage(1); }} className="w-40" />
            </div>
            <div className="flex flex-col gap-1">
              <label className="text-xs text-muted-foreground">Employee ID</label>
              <Input value={empId} onChange={e => { setEmpId(e.target.value); setPage(1); }} placeholder="EMP-001" className="w-40" />
            </div>
          </div>
        </CardContent>
      </Card>

      {/* Log table */}
      <div className="rounded-md border">
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>Employee</TableHead>
              <TableHead>Device</TableHead>
              <TableHead>Punch</TableHead>
              <TableHead>Time</TableHead>
              <TableHead>Location</TableHead>
              <TableHead>Status</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {isLoading || isFetching
              ? Array.from({ length: 8 }).map((_, i) => (
                  <TableRow key={i}>
                    {Array.from({ length: 6 }).map((__, j) => (
                      <TableCell key={j}><Skeleton className="h-4 w-full" /></TableCell>
                    ))}
                  </TableRow>
                ))
              : logs.length === 0
                ? <TableRow><TableCell colSpan={6} className="text-center text-muted-foreground py-8">No biometric records found for the selected period.</TableCell></TableRow>
                : logs.map(log => (
                    <TableRow key={log.id}>
                      <TableCell>
                        <div className="font-medium">{log.employeeName}</div>
                        <div className="text-xs text-muted-foreground">{log.employeeId}</div>
                      </TableCell>
                      <TableCell className="font-mono text-sm">{log.deviceId}</TableCell>
                      <TableCell>
                        <Badge variant={log.punchType === 'IN' ? 'default' : 'secondary'}>
                          {log.punchType}
                        </Badge>
                      </TableCell>
                      <TableCell className="text-sm">{fmtDateTime(log.logTime)}</TableCell>
                      <TableCell className="text-sm">{log.location ?? '—'}</TableCell>
                      <TableCell>
                        <Badge variant={log.isProcessed ? 'outline' : 'secondary'}>
                          {log.isProcessed ? 'Processed' : 'Pending'}
                        </Badge>
                      </TableCell>
                    </TableRow>
                  ))
            }
          </TableBody>
        </Table>
      </div>

      {/* Pagination */}
      {pages > 1 && (
        <div className="flex items-center justify-between text-sm text-muted-foreground">
          <span>Showing {(page - 1) * pageSize + 1}–{Math.min(page * pageSize, total)} of {total}</span>
          <div className="flex gap-2">
            <Button variant="outline" size="sm" disabled={page <= 1} onClick={() => setPage(p => p - 1)}>Previous</Button>
            <Button variant="outline" size="sm" disabled={page >= pages} onClick={() => setPage(p => p + 1)}>Next</Button>
          </div>
        </div>
      )}
    </div>
  );
}
