// Wired to: GET/POST /api/employees/{employeeId}/promotions
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
import { Input }    from '@/components/ui/input';
import { Textarea } from '@/components/ui/textarea';
import { Skeleton } from '@/components/ui/skeleton';
import { csrfFetch } from '@/utils/csrfFetch';

const BASE = import.meta.env.BASE_URL?.replace(/\/$/, '') ?? '';

interface PromotionRecord {
  id: number;
  fromDesignation: string;
  toDesignation: string;
  fromSalary?: number;
  toSalary?: number;
  effectiveDate: string;
  remarks?: string;
  createdAt: string;
}

const promotionSchema = z.object({
  toDesignation: z.string().min(1, 'New designation is required'),
  toSalary:      z.coerce.number().min(0).optional(),
  effectiveDate: z.string().min(1, 'Effective date is required'),
  remarks:       z.string().max(500).optional(),
});
type PromotionForm = z.infer<typeof promotionSchema>;

export default function EmployeePromotionPage() {
  const params = useParams();
  const employeeId = params.id as string;
  const qc = useQueryClient();
  const [dialogOpen, setDialog] = useState(false);

  const { data, isLoading } = useQuery<{ items: PromotionRecord[]; totalCount: number }>({
    queryKey: ['employee-promotions', employeeId],
    queryFn: async () => {
      const r = await csrfFetch(`${BASE}/api/employees/${employeeId}/promotions`, { credentials: 'include' });
      if (!r.ok) throw new Error('Failed to load promotions');
        return r.json().then((d: unknown) => {
          const payload = (d as { data?: unknown })?.data ?? d;
          return payload as { items: PromotionRecord[]; totalCount: number };
        });
    },
    enabled: Boolean(employeeId),
  });

  const form = useForm<PromotionForm>({
    resolver: zodResolver(promotionSchema),
    defaultValues: { toDesignation: '', toSalary: undefined, effectiveDate: '', remarks: '' },
  });

  const saveMut = useMutation({
    mutationFn: async (values: PromotionForm) => {
      const res = await csrfFetch(`${BASE}/api/employees/${employeeId}/promotions`, {
        method: 'POST', credentials: 'include',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(values),
      });
      if (!res.ok) {
        const d = await res.json().catch(() => ({}));
        throw new Error((d as { message?: string })?.message ?? 'Promotion failed');
      }
      return res.json();
    },
    onSuccess: () => {
      toast.success('Promotion recorded.');
      qc.invalidateQueries({ queryKey: ['employee-promotions', employeeId] });
      setDialog(false);
      form.reset();
    },
    onError: (e: Error) => toast.error(e.message),
  });

  const promotions = data?.items ?? [];

  return (
    <div className="space-y-6">
      <PageHeader
        title="Employee Promotions"
        breadcrumbs={
          <div className="flex items-center text-sm text-muted-foreground">
            <Link href={`/employees/${employeeId}`} className="hover:text-foreground flex items-center transition-colors">
              <ArrowLeft className="mr-1 h-3 w-3" />Employee Detail
            </Link>
            <span className="mx-2">/</span>
            <span className="text-foreground font-medium">Promotions</span>
          </div>
        }
        actions={
          <Button onClick={() => setDialog(true)}>
            <Plus className="h-4 w-4 mr-2" />Record Promotion
          </Button>
        }
      />

      <div className="rounded-md border">
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>From Designation</TableHead>
              <TableHead>To Designation</TableHead>
              <TableHead>From Salary</TableHead>
              <TableHead>To Salary</TableHead>
              <TableHead>Effective Date</TableHead>
              <TableHead>Remarks</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {isLoading
              ? Array.from({ length: 4 }).map((_, i) => (
                  <TableRow key={i}>{Array.from({ length: 6 }).map((__, j) => <TableCell key={j}><Skeleton className="h-4 w-full" /></TableCell>)}</TableRow>
                ))
              : promotions.length === 0
                ? <TableRow><TableCell colSpan={6} className="text-center text-muted-foreground py-8">No promotion records found.</TableCell></TableRow>
                : promotions.map(p => (
                    <TableRow key={p.id}>
                      <TableCell className="text-muted-foreground">{p.fromDesignation}</TableCell>
                      <TableCell className="font-medium">{p.toDesignation}</TableCell>
                      <TableCell className="text-muted-foreground">
                        {p.fromSalary !== undefined ? `₹${p.fromSalary.toLocaleString('en-IN')}` : '—'}
                      </TableCell>
                      <TableCell>
                        {p.toSalary !== undefined ? `₹${p.toSalary.toLocaleString('en-IN')}` : '—'}
                      </TableCell>
                      <TableCell>{new Date(p.effectiveDate).toLocaleDateString('en-IN')}</TableCell>
                      <TableCell className="max-w-[200px] truncate text-sm text-muted-foreground">{p.remarks ?? '—'}</TableCell>
                    </TableRow>
                  ))
            }
          </TableBody>
        </Table>
      </div>

      {/* Record Promotion Dialog */}
      <Dialog open={dialogOpen} onOpenChange={open => { if (!open) { setDialog(false); form.reset(); } }}>
        <DialogContent>
          <DialogHeader><DialogTitle>Record Promotion</DialogTitle></DialogHeader>
          <Form {...form}>
            <form onSubmit={form.handleSubmit(v => saveMut.mutate(v))} className="space-y-4">
              <FormField control={form.control} name="toDesignation" render={({ field }) => (
                <FormItem><FormLabel>New Designation</FormLabel><FormControl><Input {...field} placeholder="Senior Engineer" /></FormControl><FormMessage /></FormItem>
              )} />
              <FormField control={form.control} name="toSalary" render={({ field }) => (
                <FormItem>
                  <FormLabel>New Salary <span className="text-muted-foreground text-xs">(optional)</span></FormLabel>
                  <FormControl><Input {...field} type="number" placeholder="80000" /></FormControl>
                  <FormMessage />
                </FormItem>
              )} />
              <FormField control={form.control} name="effectiveDate" render={({ field }) => (
                <FormItem><FormLabel>Effective Date</FormLabel><FormControl><Input {...field} type="date" /></FormControl><FormMessage /></FormItem>
              )} />
              <FormField control={form.control} name="remarks" render={({ field }) => (
                <FormItem>
                  <FormLabel>Remarks <span className="text-muted-foreground text-xs">(optional)</span></FormLabel>
                  <FormControl><Textarea {...field} rows={3} placeholder="Reason for promotion…" /></FormControl>
                  <FormMessage />
                </FormItem>
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
