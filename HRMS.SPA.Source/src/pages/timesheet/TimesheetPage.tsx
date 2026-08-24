// FIX 10 — Timesheet page: employee entry + admin approval workflow.
// Backend: HRMS.API/Controllers/Timesheet/TimesheetController.cs (fully implemented).
// All fetch calls use credentials: 'include' (cookie-based auth, Fix 1).
import { useState } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import * as z from 'zod';
import { Clock, Plus, Check, X } from 'lucide-react';
import { toast } from 'sonner';

import { PageHeader }   from '@/components/layout/PageHeader';
import { Button }       from '@/components/ui/button';
import { Badge }        from '@/components/ui/badge';
import { Input }        from '@/components/ui/input';
import { Textarea }     from '@/components/ui/textarea';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import {
  Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter,
} from '@/components/ui/dialog';
import {
  AlertDialog, AlertDialogAction, AlertDialogCancel,
  AlertDialogContent, AlertDialogDescription, AlertDialogFooter,
  AlertDialogHeader, AlertDialogTitle,
} from '@/components/ui/alert-dialog';
import {
  Table, TableBody, TableCell, TableHead, TableHeader, TableRow,
} from '@/components/ui/table';
import {
  Form, FormControl, FormField, FormItem, FormLabel, FormMessage,
} from '@/components/ui/form';
import { Card, CardContent } from '@/components/ui/card';
import { Skeleton }     from '@/components/ui/skeleton';
import { useAuth }                              from '@/hooks/useAuth';
import { usePermissions }                       from '@/hooks/usePermissions';
import { csrfFetch } from '@/utils/csrfFetch';

const BASE = import.meta.env.BASE_URL?.replace(/\/$/, '') ?? '';

// ─── Types ────────────────────────────────────────────────────────────────────

interface TimesheetEntry {
  id: number;
  companyId: number;
  employeeId: string;
  workDate: string;
  projectCode: string;
  taskDescription: string;
  hoursWorked: number;
  status: 'Draft' | 'Submitted' | 'Approved' | 'Rejected';
  managerRemarks?: string;
  approvedByUserId?: number;
  approvedAt?: string;
  createdAt: string;
  updatedAt?: string;
}

interface PagedItems { items: TimesheetEntry[]; totalCount: number; page: number; pageSize: number; }

// ─── Schema ───────────────────────────────────────────────────────────────────

const entrySchema = z.object({
  workDate:        z.string().min(1, 'Date is required').refine(
    (d) => new Date(d) <= new Date(), { message: 'Work date cannot be in the future' }
  ),
  projectCode:     z.string().min(1, 'Project code is required').max(50),
  taskDescription: z.string().min(1, 'Task description is required').max(500),
  hoursWorked:     z.coerce.number().min(0.5, 'Minimum 0.5 hours').max(24, 'Maximum 24 hours'),
});
type EntryFormValues = z.infer<typeof entrySchema>;

const rejectSchema = z.object({ remarks: z.string().min(1, 'Remarks are required') });
type RejectFormValues = z.infer<typeof rejectSchema>;

// ─── Helpers ─────────────────────────────────────────────────────────────────

function statusBadge(status: TimesheetEntry['status']) {
  const map: Record<string, 'secondary' | 'outline' | 'default' | 'destructive'> = {
    Draft: 'outline', Submitted: 'secondary', Approved: 'default', Rejected: 'destructive',
  };
  return <Badge variant={map[status] ?? 'outline'}>{status}</Badge>;
}

async function apiFetch(url: string, options?: RequestInit) {
  const res = await csrfFetch(`${BASE}${url}`, {
    credentials: 'include',
    ...options,
    headers: { 'Content-Type': 'application/json', ...options?.headers },
  });
  const json: Record<string, unknown> = await res.json().catch(() => ({} as Record<string, unknown>));
  if (!res.ok) throw new Error((json as { message?: string }).message ?? `HTTP ${res.status}`);
  return json as Record<string, unknown>;
}

// ─── Entry Form Dialog ────────────────────────────────────────────────────────

interface EntryDialogProps {
  open: boolean;
  onClose: () => void;
  existing?: TimesheetEntry;
  onSaved: () => void;
}

function EntryDialog({ open, onClose, existing, onSaved }: EntryDialogProps) {
  const form = useForm<EntryFormValues>({
    resolver: zodResolver(entrySchema),
    defaultValues: existing
      ? {
          workDate:        existing.workDate,
          projectCode:     existing.projectCode,
          taskDescription: existing.taskDescription,
          hoursWorked:     existing.hoursWorked,
        }
      : { workDate: new Date().toISOString().split('T')[0], hoursWorked: 8 },
  });

  const onSubmit = async (values: EntryFormValues) => {
    try {
      if (existing) {
        await apiFetch(`/api/timesheet/${existing.id}`, { method: 'PUT', body: JSON.stringify(values) });
        toast.success('Entry updated.');
      } else {
        await apiFetch('/api/timesheet', { method: 'POST', body: JSON.stringify(values) });
        toast.success('Timesheet entry created.');
      }
      onSaved();
      onClose();
    } catch (e) {
      toast.error(String(e));
    }
  };

  return (
    <Dialog open={open} onOpenChange={(o) => !o && onClose()}>
      <DialogContent className="sm:max-w-md">
        <DialogHeader>
          <DialogTitle>{existing ? 'Edit Entry' : 'New Timesheet Entry'}</DialogTitle>
        </DialogHeader>
        <Form {...form}>
          <form onSubmit={form.handleSubmit(onSubmit)} className="space-y-4">
            <FormField control={form.control} name="workDate" render={({ field }) => (
              <FormItem>
                <FormLabel>Work Date</FormLabel>
                <FormControl>
                  <Input type="date" max={new Date().toISOString().split('T')[0]} {...field} />
                </FormControl>
                <FormMessage />
              </FormItem>
            )} />
            <FormField control={form.control} name="projectCode" render={({ field }) => (
              <FormItem>
                <FormLabel>Project Code</FormLabel>
                <FormControl><Input placeholder="e.g. PROJ-101" maxLength={50} {...field} /></FormControl>
                <FormMessage />
              </FormItem>
            )} />
            <FormField control={form.control} name="taskDescription" render={({ field }) => (
              <FormItem>
                <FormLabel>Task Description</FormLabel>
                <FormControl>
                  <Textarea placeholder="Describe the work done…" rows={3} maxLength={500} {...field} />
                </FormControl>
                <FormMessage />
              </FormItem>
            )} />
            <FormField control={form.control} name="hoursWorked" render={({ field }) => (
              <FormItem>
                <FormLabel>Hours Worked</FormLabel>
                <FormControl>
                  <Input type="number" min="0.5" max="24" step="0.5" {...field} />
                </FormControl>
                <FormMessage />
              </FormItem>
            )} />
            <DialogFooter>
              <Button variant="outline" type="button" onClick={onClose}>Cancel</Button>
              <Button type="submit" disabled={form.formState.isSubmitting}>
                {form.formState.isSubmitting ? 'Saving…' : existing ? 'Update' : 'Create'}
              </Button>
            </DialogFooter>
          </form>
        </Form>
      </DialogContent>
    </Dialog>
  );
}

// ─── Reject Dialog ────────────────────────────────────────────────────────────

function RejectDialog({ entryId, onClose, onRejected }: {
  entryId: number | null; onClose: () => void; onRejected: () => void;
}) {
  const form = useForm<RejectFormValues>({ resolver: zodResolver(rejectSchema) });

  const onSubmit = async (values: RejectFormValues) => {
    try {
      await apiFetch(`/api/timesheet/${entryId}/reject`, {
        method: 'POST', body: JSON.stringify({ remarks: values.remarks }),
      });
      toast.success('Timesheet rejected.');
      onRejected();
      onClose();
    } catch (e) {
      toast.error(String(e));
    }
  };

  return (
    <Dialog open={entryId !== null} onOpenChange={(o) => !o && onClose()}>
      <DialogContent className="sm:max-w-sm">
        <DialogHeader><DialogTitle>Reject Timesheet</DialogTitle></DialogHeader>
        <Form {...form}>
          <form onSubmit={form.handleSubmit(onSubmit)} className="space-y-4">
            <FormField control={form.control} name="remarks" render={({ field }) => (
              <FormItem>
                <FormLabel>Reason for rejection</FormLabel>
                <FormControl>
                  <Textarea placeholder="Explain the issue…" rows={3} {...field} />
                </FormControl>
                <FormMessage />
              </FormItem>
            )} />
            <DialogFooter>
              <Button variant="outline" type="button" onClick={onClose}>Cancel</Button>
              <Button variant="destructive" type="submit" disabled={form.formState.isSubmitting}>
                {form.formState.isSubmitting ? 'Rejecting…' : 'Reject'}
              </Button>
            </DialogFooter>
          </form>
        </Form>
      </DialogContent>
    </Dialog>
  );
}

// ─── Loading skeleton ─────────────────────────────────────────────────────────

function TableSkeleton() {
  return (
    <div className="space-y-2">
      {[...Array(4)].map((_, i) => (
        <Skeleton key={i} className="h-12 w-full rounded-md" />
      ))}
    </div>
  );
}

// ─── Employee Entries Table ───────────────────────────────────────────────────

function MyEntriesTab() {
  const qc = useQueryClient();
  const [dialogOpen, setDialogOpen] = useState(false);
  const [editing, setEditing] = useState<TimesheetEntry | undefined>();
  const [deleteId, setDeleteId] = useState<number | null>(null);

  const { data, isLoading, refetch } = useQuery<PagedItems>({
    queryKey: ['timesheet', 'my'],
    queryFn: () => apiFetch('/api/timesheet/my?pageSize=50').then((r) => r.data as PagedItems),
  });

  const submitMutation = useMutation({
    mutationFn: (id: number) => apiFetch(`/api/timesheet/${id}/submit`, { method: 'POST' }),
    onSuccess: () => { toast.success('Submitted for approval.'); qc.invalidateQueries({ queryKey: ['timesheet', 'my'] }); },
    onError: (e) => toast.error(String(e)),
  });

  const deleteMutation = useMutation({
    mutationFn: (id: number) => apiFetch(`/api/timesheet/${id}`, { method: 'DELETE' }),
    onSuccess: () => { toast.success('Entry deleted.'); setDeleteId(null); qc.invalidateQueries({ queryKey: ['timesheet', 'my'] }); },
    onError: (e) => toast.error(String(e)),
  });

  const entries = data?.items ?? [];

  return (
    <div className="space-y-4">
      <div className="flex justify-end">
        <Button onClick={() => { setEditing(undefined); setDialogOpen(true); }}>
          <Plus className="h-4 w-4 mr-2" /> New Entry
        </Button>
      </div>

      {isLoading ? <TableSkeleton /> : entries.length === 0 ? (
        <Card><CardContent className="flex flex-col items-center py-12 gap-3">
          <Clock className="h-10 w-10 text-muted-foreground" />
          <p className="text-muted-foreground">No timesheet entries yet. Create your first one.</p>
        </CardContent></Card>
      ) : (
        <div className="rounded-md border">
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Date</TableHead>
                <TableHead>Project</TableHead>
                <TableHead>Task</TableHead>
                <TableHead className="text-right">Hours</TableHead>
                <TableHead>Status</TableHead>
                <TableHead>Actions</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {entries.map((e) => (
                <TableRow key={e.id}>
                  <TableCell className="whitespace-nowrap">{e.workDate}</TableCell>
                  <TableCell className="font-mono text-sm">{e.projectCode}</TableCell>
                  <TableCell className="max-w-xs truncate" title={e.taskDescription}>
                    {e.taskDescription}
                  </TableCell>
                  <TableCell className="text-right">{e.hoursWorked}h</TableCell>
                  <TableCell>{statusBadge(e.status)}</TableCell>
                  <TableCell>
                    {e.status === 'Draft' && (
                      <div className="flex items-center gap-2">
                        <Button size="sm" variant="outline"
                          onClick={() => { setEditing(e); setDialogOpen(true); }}>Edit</Button>
                        <Button size="sm" variant="outline"
                          onClick={() => submitMutation.mutate(e.id)}
                          disabled={submitMutation.isPending}>Submit</Button>
                        <Button size="sm" variant="ghost" className="text-destructive"
                          onClick={() => setDeleteId(e.id)}>Delete</Button>
                      </div>
                    )}
                    {e.status === 'Rejected' && e.managerRemarks && (
                      <span className="text-xs text-muted-foreground italic">{e.managerRemarks}</span>
                    )}
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </div>
      )}

      <EntryDialog
        open={dialogOpen}
        onClose={() => setDialogOpen(false)}
        existing={editing}
        onSaved={() => refetch()}
      />

      <AlertDialog open={deleteId !== null} onOpenChange={(o) => !o && setDeleteId(null)}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Delete entry?</AlertDialogTitle>
            <AlertDialogDescription>This action cannot be undone.</AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>Cancel</AlertDialogCancel>
            <AlertDialogAction
              className="bg-destructive text-destructive-foreground hover:bg-destructive/90"
              onClick={() => deleteId && deleteMutation.mutate(deleteId)}>
              Delete
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </div>
  );
}

// ─── Admin Pending Approvals Tab ──────────────────────────────────────────────

function PendingApprovalsTab() {
  const qc = useQueryClient();
  const [rejectId, setRejectId] = useState<number | null>(null);

  const { data, isLoading, refetch } = useQuery<PagedItems>({
    queryKey: ['timesheet', 'pending'],
    queryFn: () => apiFetch('/api/timesheet/pending?pageSize=100').then((r) => r.data as PagedItems),
  });

  const approveMutation = useMutation({
    mutationFn: (id: number) => apiFetch(`/api/timesheet/${id}/approve`, { method: 'POST' }),
    onSuccess: () => { toast.success('Timesheet approved.'); qc.invalidateQueries({ queryKey: ['timesheet'] }); },
    onError: (e) => toast.error(String(e)),
  });

  const entries = data?.items ?? [];

  return (
    <div className="space-y-4">
      {isLoading ? <TableSkeleton /> : entries.length === 0 ? (
        <Card><CardContent className="flex flex-col items-center py-12 gap-3">
          <Check className="h-10 w-10 text-muted-foreground" />
          <p className="text-muted-foreground">No pending timesheets. All caught up!</p>
        </CardContent></Card>
      ) : (
        <div className="rounded-md border">
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Employee</TableHead>
                <TableHead>Date</TableHead>
                <TableHead>Project</TableHead>
                <TableHead>Task</TableHead>
                <TableHead className="text-right">Hours</TableHead>
                <TableHead>Actions</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {entries.map((e) => (
                <TableRow key={e.id}>
                  <TableCell className="font-mono text-sm">{e.employeeId}</TableCell>
                  <TableCell className="whitespace-nowrap">{e.workDate}</TableCell>
                  <TableCell className="font-mono text-sm">{e.projectCode}</TableCell>
                  <TableCell className="max-w-xs truncate" title={e.taskDescription}>
                    {e.taskDescription}
                  </TableCell>
                  <TableCell className="text-right">{e.hoursWorked}h</TableCell>
                  <TableCell>
                    <div className="flex items-center gap-2">
                      <Button size="sm" variant="outline" className="text-green-600 border-green-200"
                        onClick={() => approveMutation.mutate(e.id)}
                        disabled={approveMutation.isPending}>
                        <Check className="h-3.5 w-3.5 mr-1" /> Approve
                      </Button>
                      <Button size="sm" variant="outline" className="text-destructive border-destructive/20"
                        onClick={() => setRejectId(e.id)}>
                        <X className="h-3.5 w-3.5 mr-1" /> Reject
                      </Button>
                    </div>
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </div>
      )}

      <RejectDialog
        entryId={rejectId}
        onClose={() => setRejectId(null)}
        onRejected={() => refetch()}
      />
    </div>
  );
}

// ─── Page ─────────────────────────────────────────────────────────────────────

export default function TimesheetPage() {
  const { isAuthenticated } = useAuth();
  void isAuthenticated;

  // BUGFIX SEC-TIMESHEET-01 cont'd: role sourced from the server via GET /api/auth/me
  // through usePermissions(), which correctly normalizes the raw lowercase role
  // ("admin"/"superadmin" per AppRoles.cs) instead of comparing against a
  // capitalized 'Admin' literal that could never match and hid the admin tab.
  const { isAdmin: showAdmin, isLoading: permissionsLoading } = usePermissions();

  return (
    <div className="space-y-6">
      <PageHeader
        title="Timesheet"
        description="Log your daily work hours, submit for approval, and track status."
        actions={<Clock className="h-6 w-6 text-muted-foreground" />}
      />

      {permissionsLoading ? null : showAdmin ? (
        <Tabs defaultValue="pending">
          <TabsList>
            <TabsTrigger value="pending">Pending Approvals</TabsTrigger>
            <TabsTrigger value="mine">My Entries</TabsTrigger>
          </TabsList>
          <TabsContent value="pending" className="mt-4"><PendingApprovalsTab /></TabsContent>
          <TabsContent value="mine" className="mt-4"><MyEntriesTab /></TabsContent>
        </Tabs>
      ) : (
        <MyEntriesTab />
      )}
    </div>
  );
}
