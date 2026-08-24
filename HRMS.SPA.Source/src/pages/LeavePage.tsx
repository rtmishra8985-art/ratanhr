//            Added validation, loading state, success/error toast, and list refresh.
import { useState } from 'react';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { Plus, Check, X } from 'lucide-react';
import { useListLeaveRequests, useListLeaveTypes } from '@workspace/api-client-react';
import { toast } from 'sonner';

import { PageHeader } from '@/components/layout/PageHeader';
import { Button } from '@/components/ui/button';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { SkeletonTable } from '@/components/shared/SkeletonTable';
import { Pagination } from '@/components/shared/Pagination';
import { StatusBadge } from '@/components/shared/StatusBadge';
import { EmptyState } from '@/components/shared/EmptyState';
import { Badge } from '@/components/ui/badge';
import { Skeleton } from '@/components/ui/skeleton';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter } from '@/components/ui/dialog';
import { Label } from '@/components/ui/label';
import { Input } from '@/components/ui/input';
import { Textarea } from '@/components/ui/textarea';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { usePaginationState } from '@/hooks/usePaginationState';
import { csrfFetch } from '@/utils/csrfFetch';

const BASE = import.meta.env.BASE_URL?.replace(/\/$/, '') ?? '';

// ─── Apply Leave form state ───────────────────────────────────────────────────

interface ApplyLeaveForm {
  leaveTypeId: string;
  startDate: string;
  endDate: string;
  reason: string;
}

const emptyForm: ApplyLeaveForm = { leaveTypeId: '', startDate: '', endDate: '', reason: '' };

// ─── Component ────────────────────────────────────────────────────────────────

export default function LeavePage() {

  const { page, setPage, pageSize, resetPage } = usePaginationState();
  const [statusFilter, setStatusFilter] = useState<string | null>(null);


  const [dialogOpen, setDialogOpen] = useState(false);
  const [form, setForm] = useState<ApplyLeaveForm>(emptyForm);
  const [formErrors, setFormErrors] = useState<Partial<ApplyLeaveForm>>({});

  const queryClient = useQueryClient();

  const { data: leaves, isLoading: loadingLeaves, isError: errorLeaves } = useListLeaveRequests({
    page,
    pageSize,
    status: statusFilter || undefined,
  });

  const { data: leaveTypes, isLoading: loadingTypes } = useListLeaveTypes();

  const handleStatusFilter = (status: string | null) => {
    setStatusFilter(status);
    resetPage();
  };


  const applyMutation = useMutation({
    mutationFn: async (payload: { leaveTypeId: number; startDate: string; endDate: string; reason?: string }) => {
      const res = await csrfFetch(`${BASE}/api/leave/apply`, {
        method: 'POST',
        credentials: 'include',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(payload),
      });
      if (!res.ok) {
        const data = await res.json().catch(() => ({}));
        throw new Error(data?.message ?? 'Failed to apply for leave');
      }
      return res.json();
    },
    onSuccess: () => {
      toast.success('Leave request submitted successfully.');
      setDialogOpen(false);
      setForm(emptyForm);
      setFormErrors({});
      // Refresh the leave list to reflect the new request
      queryClient.invalidateQueries({ queryKey: ['leave'] });
    },
    onError: (err: Error) => {
      toast.error(err.message ?? 'Failed to submit leave request.');
    },
  });

  // HOTFIX: Approve/Reject — wired to POST /api/leave/{id}/decision (LeaveController.Decide).
  // Buttons previously had no onClick handler at all (dead UI).
  const decisionMutation = useMutation({
    mutationFn: async ({ id, approve }: { id: string; approve: boolean }) => {
      const res = await csrfFetch(`${BASE}/api/leave/${id}/decision`, {
        method: 'POST',
        credentials: 'include',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ approve }),
      });
      const data = await res.json().catch(() => ({}));
      if (!res.ok) throw new Error(data?.message ?? 'Failed to record decision.');
      return data;
    },
    onSuccess: (_data, variables) => {
      toast.success(variables.approve ? 'Leave request approved.' : 'Leave request rejected.');
      queryClient.invalidateQueries({ queryKey: ['leave'] });
    },
    onError: (err: Error) => {
      toast.error(err.message ?? 'Failed to record decision.');
    },
  });


  const validate = (): boolean => {
    const errors: Partial<ApplyLeaveForm> = {};
    if (!form.leaveTypeId) errors.leaveTypeId = 'Please select a leave type.';
    if (!form.startDate)   errors.startDate   = 'Start date is required.';
    if (!form.endDate)     errors.endDate     = 'End date is required.';
    if (form.startDate && form.endDate && form.endDate < form.startDate) {
      errors.endDate = 'End date must be on or after start date.';
    }
    setFormErrors(errors);
    return Object.keys(errors).length === 0;
  };

  const handleApplySubmit = () => {
    if (!validate()) return;
    applyMutation.mutate({
      leaveTypeId: Number(form.leaveTypeId),
      startDate:   form.startDate,
      endDate:     form.endDate,
      reason:      form.reason || undefined,
    });
  };

  const handleDialogClose = (open: boolean) => {
    if (!open) {
      setForm(emptyForm);
      setFormErrors({});
    }
    setDialogOpen(open);
  };

  return (
    <div className="space-y-6">
      <PageHeader
        title="Leave Management"
        description="Review and process time-off requests."
        actions={

          <Button onClick={() => setDialogOpen(true)}>
            <Plus className="mr-2 h-4 w-4" />
            Apply Leave
          </Button>
        }
      />

      <Tabs defaultValue="requests" className="w-full">
        <TabsList className="grid w-full sm:w-[400px] grid-cols-2">
          <TabsTrigger value="requests">All Requests</TabsTrigger>
          <TabsTrigger value="types">Leave Types</TabsTrigger>
        </TabsList>

        <TabsContent value="requests" className="space-y-4 mt-6">
          <div className="flex gap-2">
            {[null, 'Pending', 'Approved', 'Rejected'].map((s) => (
              <Button
                key={String(s)}
                variant={statusFilter === s ? 'default' : 'outline'}
                size="sm"
                onClick={() => handleStatusFilter(s)}
              >
                {s ?? 'All'}
              </Button>
            ))}
          </div>

          <div className="bg-card border rounded-lg shadow-sm overflow-hidden">
            {loadingLeaves ? (
              <SkeletonTable columns={7} rows={10} />
            ) : errorLeaves ? (
              <EmptyState title="Error" description="Failed to load leave requests." />
            ) : !leaves?.items.length ? (
              <EmptyState
                title="No requests found"
                description="No leave requests match the current filters."
              />
            ) : (
              <>
                <div className="overflow-x-auto">
                  <Table>
                    <TableHeader>
                      <TableRow className="bg-muted/50">
                        <TableHead>Employee</TableHead>
                        <TableHead>Type</TableHead>
                        <TableHead>Duration</TableHead>
                        <TableHead>Days</TableHead>
                        <TableHead>Reason</TableHead>
                        <TableHead>Status</TableHead>
                        <TableHead className="text-right">Actions</TableHead>
                      </TableRow>
                    </TableHeader>
                    <TableBody>
                      {leaves.items.map((leave) => (
                        <TableRow key={leave.id}>
                          <TableCell className="font-medium">
                            {leave.employeeName}
                            <div className="text-xs text-muted-foreground font-normal">{leave.employeeId}</div>
                          </TableCell>
                          <TableCell>{leave.leaveTypeName}</TableCell>
                          <TableCell>
                            <div className="text-sm">
                              {new Date(leave.startDate).toLocaleDateString()} -{' '}
                              {new Date(leave.endDate).toLocaleDateString()}
                            </div>
                            <div className="text-xs text-muted-foreground">
                              Applied on{' '}
                              {leave.appliedAt ? new Date(leave.appliedAt).toLocaleDateString() : '-'}
                            </div>
                          </TableCell>
                          <TableCell>{leave.days}</TableCell>
                          <TableCell
                            className="max-w-[200px] truncate"
                            title={leave.reason || ''}
                          >
                            {leave.reason || '-'}
                          </TableCell>
                          <TableCell>
                            <StatusBadge status={leave.status} />
                          </TableCell>
                          <TableCell className="text-right space-x-2">
                            {leave.status === 'Pending' && (
                              <>
                                <Button
                                  size="icon"
                                  variant="outline"
                                  className="h-8 w-8 text-green-600 hover:text-green-700 hover:bg-green-50 border-green-200"
                                  aria-label="Approve leave"
                                  disabled={decisionMutation.isPending}
                                  onClick={() => decisionMutation.mutate({ id: leave.id, approve: true })}
                                >
                                  <Check className="h-4 w-4" />
                                </Button>
                                <Button
                                  size="icon"
                                  variant="outline"
                                  className="h-8 w-8 text-red-600 hover:text-red-700 hover:bg-red-50 border-red-200"
                                  aria-label="Reject leave"
                                  disabled={decisionMutation.isPending}
                                  onClick={() => decisionMutation.mutate({ id: leave.id, approve: false })}
                                >
                                  <X className="h-4 w-4" />
                                </Button>
                              </>
                            )}
                          </TableCell>
                        </TableRow>
                      ))}
                    </TableBody>
                  </Table>
                </div>
                <Pagination
                  page={leaves.page}
                  pageSize={leaves.pageSize}
                  totalCount={leaves.totalCount}
                  totalPages={leaves.totalPages}
                  onPageChange={setPage}
                />
              </>
            )}
          </div>
        </TabsContent>

        <TabsContent value="types" className="mt-6">
          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
            {loadingTypes
              ? Array.from({ length: 4 }).map((_, i) => (
                  <Card key={i}>
                    <CardContent className="p-6">
                      <Skeleton className="h-24 w-full" />
                    </CardContent>
                  </Card>
                ))
              : leaveTypes?.map((type) => (
                  <Card key={type.id}>
                    <CardHeader className="pb-2">
                      <div className="flex justify-between items-start">
                        <CardTitle className="text-lg">{type.name}</CardTitle>
                        <Badge variant={type.isPaid ? 'default' : 'secondary'}>
                          {type.isPaid ? 'Paid' : 'Unpaid'}
                        </Badge>
                      </div>
                    </CardHeader>
                    <CardContent>
                      <p className="text-sm text-muted-foreground mt-2">
                        {type.maxDays
                          ? `Maximum ${type.maxDays} days per year`
                          : 'No maximum limit set'}
                      </p>
                    </CardContent>
                  </Card>
                ))}
          </div>
        </TabsContent>
      </Tabs>

      {/* HOTFIX P0: Apply Leave Dialog — wired to POST /api/leave/apply */}
      <Dialog open={dialogOpen} onOpenChange={handleDialogClose}>
        <DialogContent className="sm:max-w-[480px]">
          <DialogHeader>
            <DialogTitle>Apply for Leave</DialogTitle>
          </DialogHeader>

          <div className="space-y-4 py-2">
            {/* Leave Type */}
            <div className="space-y-1">
              <Label htmlFor="leaveType">Leave Type <span className="text-destructive">*</span></Label>
              <Select
                value={form.leaveTypeId}
                onValueChange={(val) => setForm(f => ({ ...f, leaveTypeId: val }))}
              >
                <SelectTrigger id="leaveType">
                  <SelectValue placeholder="Select leave type…" />
                </SelectTrigger>
                <SelectContent>
                  {leaveTypes?.map((t) => (
                    <SelectItem key={t.id} value={String(t.id)}>
                      {t.name}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
              {formErrors.leaveTypeId && (
                <p className="text-xs text-destructive">{formErrors.leaveTypeId}</p>
              )}
            </div>

            {/* Start Date */}
            <div className="space-y-1">
              <Label htmlFor="startDate">Start Date <span className="text-destructive">*</span></Label>
              <Input
                id="startDate"
                type="date"
                value={form.startDate}
                onChange={(e) => setForm(f => ({ ...f, startDate: e.target.value }))}
              />
              {formErrors.startDate && (
                <p className="text-xs text-destructive">{formErrors.startDate}</p>
              )}
            </div>

            {/* End Date */}
            <div className="space-y-1">
              <Label htmlFor="endDate">End Date <span className="text-destructive">*</span></Label>
              <Input
                id="endDate"
                type="date"
                value={form.endDate}
                min={form.startDate}
                onChange={(e) => setForm(f => ({ ...f, endDate: e.target.value }))}
              />
              {formErrors.endDate && (
                <p className="text-xs text-destructive">{formErrors.endDate}</p>
              )}
            </div>

            {/* Reason */}
            <div className="space-y-1">
              <Label htmlFor="reason">Reason <span className="text-muted-foreground text-xs">(optional)</span></Label>
              <Textarea
                id="reason"
                rows={3}
                value={form.reason}
                onChange={(e) => setForm(f => ({ ...f, reason: e.target.value }))}
                placeholder="Brief reason for leave request…"
              />
            </div>
          </div>

          <DialogFooter>
            <Button
              variant="outline"
              onClick={() => handleDialogClose(false)}
              disabled={applyMutation.isPending}
            >
              Cancel
            </Button>
            <Button onClick={handleApplySubmit} disabled={applyMutation.isPending}>
              {applyMutation.isPending ? 'Submitting…' : 'Submit Request'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
