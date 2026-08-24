// Wired to: GET/POST /api/employees/{employeeId}/transfers
// Accessed from employee detail. Requires admin/superadmin role.
// SEC: All API calls use credentials: 'include'.
import { useState } from 'react';
import { useParams, Link } from 'wouter';
import { ArrowLeft, Plus } from 'lucide-react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import * as z from 'zod';
import { toast } from 'sonner';

import { PageHeader }  from '@/components/layout/PageHeader';
import { Button }      from '@/components/ui/button';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter } from '@/components/ui/dialog';
import { Form, FormControl, FormField, FormItem, FormLabel, FormMessage } from '@/components/ui/form';
import { Input }   from '@/components/ui/input';
import { Textarea } from '@/components/ui/textarea';
import { Skeleton } from '@/components/ui/skeleton';
import { csrfFetch } from '@/utils/csrfFetch';

const BASE = import.meta.env.BASE_URL?.replace(/\/$/, '') ?? '';

interface TransferRecord {
  id: number;
  fromDepartment: string;
  toDepartment: string;
  fromLocation?: string;
  toLocation?: string;
  effectiveDate: string;
  reason?: string;
  createdAt: string;
}

const transferSchema = z.object({
  toDepartment:  z.string().min(1, 'Target department is required'),
  toLocation:    z.string().optional(),
  effectiveDate: z.string().min(1, 'Effective date is required'),
  reason:        z.string().max(500).optional(),
});
type TransferForm = z.infer<typeof transferSchema>;

export default function EmployeeTransferPage() {
  const params = useParams();
  const employeeId = params.id as string;
  const qc = useQueryClient();
  const [dialogOpen, setDialog] = useState(false);

  const { data, isLoading } = useQuery<{ items: TransferRecord[]; totalCount: number }>({
    queryKey: ['employee-transfers', employeeId],
    queryFn: async () => {
      const r = await csrfFetch(`${BASE}/api/employees/${employeeId}/transfers`, { credentials: 'include' });
      if (!r.ok) throw new Error('Failed to load transfers');
        return r.json().then((d: unknown) => {
          const payload = (d as { data?: unknown })?.data ?? d;
          return payload as { items: TransferRecord[]; totalCount: number };
        });
    },
    enabled: Boolean(employeeId),
  });

  const form = useForm<TransferForm>({
    resolver: zodResolver(transferSchema),
    defaultValues: { toDepartment: '', toLocation: '', effectiveDate: '', reason: '' },
  });

  const saveMut = useMutation({
    mutationFn: async (values: TransferForm) => {
      const res = await csrfFetch(`${BASE}/api/employees/${employeeId}/transfers`, {
        method: 'POST', credentials: 'include',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(values),
      });
      if (!res.ok) {
        const d = await res.json().catch(() => ({}));
        throw new Error((d as { message?: string })?.message ?? 'Transfer failed');
      }
      return res.json();
    },
    onSuccess: () => {
      toast.success('Transfer recorded.');
      qc.invalidateQueries({ queryKey: ['employee-transfers', employeeId] });
      setDialog(false);
      form.reset();
    },
    onError: (e: Error) => toast.error(e.message),
  });

  const transfers = data?.items ?? [];

  return (
    <div className="space-y-6">
      <PageHeader
        title="Employee Transfers"
        breadcrumbs={
          <div className="flex items-center text-sm text-muted-foreground">
            <Link href={`/employees/${employeeId}`} className="hover:text-foreground flex items-center transition-colors">
              <ArrowLeft className="mr-1 h-3 w-3" />Employee Detail
            </Link>
            <span className="mx-2">/</span>
            <span className="text-foreground font-medium">Transfers</span>
          </div>
        }
        actions={
          <Button onClick={() => setDialog(true)}>
            <Plus className="h-4 w-4 mr-2" />Record Transfer
          </Button>
        }
      />

      <div className="rounded-md border">
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>From Department</TableHead>
              <TableHead>To Department</TableHead>
              <TableHead>From Location</TableHead>
              <TableHead>To Location</TableHead>
              <TableHead>Effective Date</TableHead>
              <TableHead>Reason</TableHead>
              <TableHead>Created</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {isLoading
              ? Array.from({ length: 4 }).map((_, i) => (
                  <TableRow key={i}>{Array.from({ length: 7 }).map((__, j) => <TableCell key={j}><Skeleton className="h-4 w-full" /></TableCell>)}</TableRow>
                ))
              : transfers.length === 0
                ? <TableRow><TableCell colSpan={7} className="text-center text-muted-foreground py-8">No transfer records found.</TableCell></TableRow>
                : transfers.map(t => (
                    <TableRow key={t.id}>
                      <TableCell>{t.fromDepartment}</TableCell>
                      <TableCell className="font-medium">{t.toDepartment}</TableCell>
                      <TableCell className="text-muted-foreground">{t.fromLocation ?? '—'}</TableCell>
                      <TableCell>{t.toLocation ?? '—'}</TableCell>
                      <TableCell>{new Date(t.effectiveDate).toLocaleDateString('en-IN')}</TableCell>
                      <TableCell className="max-w-[180px] truncate text-sm text-muted-foreground">{t.reason ?? '—'}</TableCell>
                      <TableCell className="text-sm text-muted-foreground">{new Date(t.createdAt).toLocaleDateString('en-IN')}</TableCell>
                    </TableRow>
                  ))
            }
          </TableBody>
        </Table>
      </div>

      {/* Record Transfer Dialog */}
      <Dialog open={dialogOpen} onOpenChange={open => { if (!open) { setDialog(false); form.reset(); } }}>
        <DialogContent>
          <DialogHeader><DialogTitle>Record Transfer</DialogTitle></DialogHeader>
          <Form {...form}>
            <form onSubmit={form.handleSubmit(v => saveMut.mutate(v))} className="space-y-4">
              <FormField control={form.control} name="toDepartment" render={({ field }) => (
                <FormItem><FormLabel>Target Department</FormLabel><FormControl><Input {...field} placeholder="Engineering" /></FormControl><FormMessage /></FormItem>
              )} />
              <FormField control={form.control} name="toLocation" render={({ field }) => (
                <FormItem><FormLabel>Target Location <span className="text-muted-foreground text-xs">(optional)</span></FormLabel><FormControl><Input {...field} placeholder="Mumbai HQ" /></FormControl><FormMessage /></FormItem>
              )} />
              <FormField control={form.control} name="effectiveDate" render={({ field }) => (
                <FormItem><FormLabel>Effective Date</FormLabel><FormControl><Input {...field} type="date" /></FormControl><FormMessage /></FormItem>
              )} />
              <FormField control={form.control} name="reason" render={({ field }) => (
                <FormItem><FormLabel>Reason <span className="text-muted-foreground text-xs">(optional)</span></FormLabel><FormControl><Textarea {...field} rows={3} placeholder="Reason for transfer…" /></FormControl><FormMessage /></FormItem>
              )} />
              <DialogFooter>
                <Button variant="outline" type="button" onClick={() => { setDialog(false); form.reset(); }}>Cancel</Button>
                <Button type="submit" disabled={saveMut.isPending}>{saveMut.isPending ? 'Saving…' : 'Save'}</Button>
              </DialogFooter>
            </form>
          </Form>
        </DialogContent>
      </Dialog>
    </div>
  );
}
