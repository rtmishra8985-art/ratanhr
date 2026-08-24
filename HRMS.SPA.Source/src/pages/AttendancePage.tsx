// Attendance — wired to the real backend route (GET /api/attendance/web),
// plus working Filter (status/date-range) and Export (Excel) buttons.
//
// BUG FIX: this page's data hooks previously called useListAttendance()
// (-> GET /api/attendance) and useGetTodayAttendanceSummary()
// (-> GET /api/attendance/dashboard) — neither route exists anywhere in
// AttendanceController.cs (verified: only /web, /web/my, /excel, and their
// mutation siblings exist; there is no bare `/api/attendance` and no
// `/dashboard` sub-route). Every load of this page 404'd silently (the
// generated hooks treat a failed request as "loading forever" / empty state,
// so this was not obviously broken in casual testing). Switched to the real
// GET /api/attendance/web endpoint (which returns AttendanceFilterDto-shaped
// paged WebAttendanceDto rows) and derive the summary counts client-side from
// the same page of data, avoiding the need for a new backend endpoint.
//
// Also wires the previously-dead "Filter" (status + date range) and "Export"
// (Excel download via GET /api/reports/attendance/export) buttons.
import { useMemo, useState } from 'react';
import { Calendar as CalendarIcon, Filter, Download } from 'lucide-react';
import { useQuery } from '@tanstack/react-query';
import { toast } from 'sonner';

import { PageHeader } from '@/components/layout/PageHeader';
import { Button } from '@/components/ui/button';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { Card, CardContent } from '@/components/ui/card';
import { SkeletonTable } from '@/components/shared/SkeletonTable';
import { Pagination } from '@/components/shared/Pagination';
import { StatusBadge } from '@/components/shared/StatusBadge';
import { EmptyState } from '@/components/shared/EmptyState';
import { Skeleton } from '@/components/ui/skeleton';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { usePaginationState } from '@/hooks/usePaginationState';
import { csrfFetch } from '@/utils/csrfFetch';

const BASE = import.meta.env.BASE_URL?.replace(/\/$/, '') ?? '';

interface WebAttendanceRow {
  id: number;
  employeeId: string;
  employeeName: string;
  attDate: string;
  checkIn?: string | null;
  checkOut?: string | null;
  status: string;
  hoursWorked?: number | null;
}

interface PagedWebAttendance {
  items: WebAttendanceRow[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

async function fetchAttendance(params: {
  page: number; pageSize: number; status?: string; startDate?: string; endDate?: string;
}): Promise<PagedWebAttendance> {
  const qs = new URLSearchParams();
  qs.set('page', String(params.page));
  qs.set('pageSize', String(params.pageSize));
  if (params.status) qs.set('status', params.status);
  if (params.startDate) qs.set('startDate', params.startDate);
  if (params.endDate) qs.set('endDate', params.endDate);

  const res = await csrfFetch(`${BASE}/api/attendance/web?${qs.toString()}`, { credentials: 'include' });
  if (!res.ok) throw new Error('Failed to load attendance records.');
  const body = await res.json();
  return body.data ?? body;
}

const STATUS_OPTIONS = ['Present', 'Absent', 'Half Day', 'Leave', 'Holiday', 'Weekend'];

export default function AttendancePage() {
  const { page, setPage, pageSize } = usePaginationState();

  const [filterOpen, setFilterOpen] = useState(false);
  const [status, setStatus] = useState<string | undefined>(undefined);
  const [startDate, setStartDate] = useState('');
  const [endDate, setEndDate] = useState('');
  const [exporting, setExporting] = useState(false);

  const { data, isLoading, isError, error, refetch } = useQuery({
    queryKey: ['/api/attendance/web', page, pageSize, status, startDate, endDate],
    queryFn: () => fetchAttendance({ page, pageSize, status, startDate, endDate }),
  });

  // Derived summary from the current page — the backend has no dedicated
  // admin attendance-dashboard-count endpoint (only /employee dashboard
  // stats exist), so this reflects the loaded page, not a company-wide total.
  const summary = useMemo(() => {
    const items = data?.items ?? [];
    return {
      total: data?.totalCount ?? 0,
      present: items.filter((i) => i.status === 'Present').length,
      late: items.filter((i) => i.status === 'Half Day').length,
      absent: items.filter((i) => i.status === 'Absent').length,
      onLeave: items.filter((i) => i.status === 'Leave').length,
    };
  }, [data]);

  const handleExport = async () => {
    const now = new Date();
    setExporting(true);
    try {
      const qs = new URLSearchParams({
        month: String(now.getMonth() + 1),
        year: String(now.getFullYear()),
      });
      const res = await csrfFetch(`${BASE}/api/reports/attendance/export?${qs.toString()}`, { credentials: 'include' });
      if (!res.ok) throw new Error(await res.text().catch(() => 'Export failed.'));
      const blob = await res.blob();
      const link = document.createElement('a');
      link.href = URL.createObjectURL(blob);
      link.download = `Attendance_${now.getFullYear()}_${String(now.getMonth() + 1).padStart(2, '0')}.xlsx`;
      link.click();
      URL.revokeObjectURL(link.href);
    } catch (e) {
      toast.error(e instanceof Error ? e.message : 'Export failed.');
    } finally {
      setExporting(false);
    }
  };

  return (
    <div className="space-y-6">
      <PageHeader
        title="Attendance"
        description="Monitor employee attendance and work hours."
      />

      <div className="grid grid-cols-2 md:grid-cols-5 gap-4">
        {isLoading ? (
          Array.from({ length: 5 }).map((_, i) => (
            <Card key={i}>
              <CardContent className="p-6">
                <Skeleton className="h-10 w-full" />
              </CardContent>
            </Card>
          ))
        ) : (
          <>
            <Card className="bg-blue-50/50 dark:bg-blue-900/10 border-blue-100 dark:border-blue-900">
              <CardContent className="p-6 flex flex-col justify-center items-center text-center">
                <span className="text-sm font-medium text-muted-foreground mb-1">Total</span>
                <span className="text-3xl font-bold">{summary.total}</span>
              </CardContent>
            </Card>
            <Card className="bg-green-50/50 dark:bg-green-900/10 border-green-100 dark:border-green-900">
              <CardContent className="p-6 flex flex-col justify-center items-center text-center">
                <span className="text-sm font-medium text-green-700 dark:text-green-400 mb-1">Present</span>
                <span className="text-3xl font-bold text-green-700 dark:text-green-400">{summary.present}</span>
              </CardContent>
            </Card>
            <Card className="bg-yellow-50/50 dark:bg-yellow-900/10 border-yellow-100 dark:border-yellow-900">
              <CardContent className="p-6 flex flex-col justify-center items-center text-center">
                <span className="text-sm font-medium text-yellow-700 dark:text-yellow-400 mb-1">Half Day</span>
                <span className="text-3xl font-bold text-yellow-700 dark:text-yellow-400">{summary.late}</span>
              </CardContent>
            </Card>
            <Card className="bg-red-50/50 dark:bg-red-900/10 border-red-100 dark:border-red-900">
              <CardContent className="p-6 flex flex-col justify-center items-center text-center">
                <span className="text-sm font-medium text-red-700 dark:text-red-400 mb-1">Absent</span>
                <span className="text-3xl font-bold text-red-700 dark:text-red-400">{summary.absent}</span>
              </CardContent>
            </Card>
            <Card className="bg-purple-50/50 dark:bg-purple-900/10 border-purple-100 dark:border-purple-900">
              <CardContent className="p-6 flex flex-col justify-center items-center text-center">
                <span className="text-sm font-medium text-purple-700 dark:text-purple-400 mb-1">On Leave</span>
                <span className="text-3xl font-bold text-purple-700 dark:text-purple-400">{summary.onLeave}</span>
              </CardContent>
            </Card>
          </>
        )}
      </div>

      <div className="flex flex-col sm:flex-row items-center justify-between gap-4 bg-card p-4 border rounded-lg shadow-sm">
        <div className="flex items-center gap-2 text-sm text-muted-foreground">
          <CalendarIcon className="h-4 w-4" />
          {startDate || endDate ? (
            <span>{startDate || '…'} to {endDate || '…'}</span>
          ) : (
            <span>All dates</span>
          )}
        </div>
        <div className="flex items-center gap-2 w-full sm:w-auto">
          <Popover open={filterOpen} onOpenChange={setFilterOpen}>
            <PopoverTrigger asChild>
              <Button variant="outline" size="sm" className="w-full sm:w-auto">
                <Filter className="mr-2 h-4 w-4" />
                Filter{status ? `: ${status}` : ''}
              </Button>
            </PopoverTrigger>
            <PopoverContent className="w-72 space-y-3">
              <div className="space-y-1">
                <Label>Status</Label>
                <Select value={status ?? 'all'} onValueChange={(v) => { setStatus(v === 'all' ? undefined : v); setPage(1); }}>
                  <SelectTrigger><SelectValue placeholder="All statuses" /></SelectTrigger>
                  <SelectContent>
                    <SelectItem value="all">All statuses</SelectItem>
                    {STATUS_OPTIONS.map((s) => <SelectItem key={s} value={s}>{s}</SelectItem>)}
                  </SelectContent>
                </Select>
              </div>
              <div className="grid grid-cols-2 gap-2">
                <div className="space-y-1">
                  <Label>From</Label>
                  <Input type="date" value={startDate} onChange={(e) => { setStartDate(e.target.value); setPage(1); }} />
                </div>
                <div className="space-y-1">
                  <Label>To</Label>
                  <Input type="date" value={endDate} onChange={(e) => { setEndDate(e.target.value); setPage(1); }} />
                </div>
              </div>
              <Button
                variant="ghost"
                size="sm"
                className="w-full"
                onClick={() => { setStatus(undefined); setStartDate(''); setEndDate(''); setPage(1); setFilterOpen(false); }}
              >
                Clear Filters
              </Button>
            </PopoverContent>
          </Popover>
          <Button variant="outline" size="sm" className="w-full sm:w-auto" onClick={handleExport} disabled={exporting}>
            <Download className="mr-2 h-4 w-4" />
            {exporting ? 'Exporting…' : 'Export'}
          </Button>
        </div>
      </div>

      <div className="bg-card border rounded-lg shadow-sm overflow-hidden">
        {isLoading ? (
          <SkeletonTable columns={6} rows={10} />
        ) : isError ? (

          <EmptyState
            title="Failed to load attendance records"
            description={error instanceof Error ? error.message : 'An unexpected error occurred.'}
            onRetry={refetch}
          />
        ) : !data?.items.length ? (
          <EmptyState
            title="No attendance records found"
            description="No attendance data for the selected period."
          />
        ) : (
          <>
            <div className="overflow-x-auto">
              <Table>
                <TableHeader>
                  <TableRow className="bg-muted/50 hover:bg-muted/50">
                    <TableHead>Employee</TableHead>
                    <TableHead>Date</TableHead>
                    <TableHead>Check In</TableHead>
                    <TableHead>Check Out</TableHead>
                    <TableHead>Work Hours</TableHead>
                    <TableHead>Status</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {data.items.map((record) => (
                    <TableRow key={record.id}>
                      <TableCell className="font-medium">
                        {record.employeeName}
                        <div className="text-xs text-muted-foreground font-normal">{record.employeeId}</div>
                      </TableCell>
                      <TableCell>{record.attDate ? new Date(record.attDate).toLocaleDateString() : '-'}</TableCell>
                      <TableCell>{record.checkIn || '--:--'}</TableCell>
                      <TableCell>{record.checkOut || '--:--'}</TableCell>
                      <TableCell>{record.hoursWorked ? `${record.hoursWorked}h` : '-'}</TableCell>
                      <TableCell>
                        <StatusBadge status={record.status} />
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </div>
            <Pagination
              page={data.page}
              pageSize={data.pageSize}
              totalCount={data.totalCount}
              totalPages={data.totalPages}
              onPageChange={setPage}
            />
          </>
        )}
      </div>
    </div>
  );
}
