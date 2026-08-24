// Wired to: GET/POST /api/employees/{employeeId}/exit
// Accessed from employee detail. Requires admin/superadmin role.
// SEC: All API calls use credentials: 'include'.
import { useState } from 'react';
import { useParams, Link } from 'wouter';
import { ArrowLeft, LogOut } from 'lucide-react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import * as z from 'zod';
import { toast } from 'sonner';

import { PageHeader }  from '@/components/layout/PageHeader';
import { Button }      from '@/components/ui/button';
import { Badge }       from '@/components/ui/badge';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter } from '@/components/ui/dialog';
import { Form, FormControl, FormField, FormItem, FormLabel, FormMessage } from '@/components/ui/form';
import { Input }    from '@/components/ui/input';
import { Textarea } from '@/components/ui/textarea';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Skeleton } from '@/components/ui/skeleton';
import { csrfFetch } from '@/utils/csrfFetch';

const BASE = import.meta.env.BASE_URL?.replace(/\/$/, '') ?? '';

interface ExitRecord {
  id: number;
  exitType: string;
  exitDate: string;
  reason?: string;
  noticePeriodDays?: number;
  lastWorkingDate?: string;
  status: string;
  createdAt: string;
}

const EXIT_TYPES = ['Resignation', 'Termination', 'Retirement', 'Abandonment', 'Contract End', 'Death'];

const exitSchema = z.object({
  exitType:          z.string().min(1, 'Exit type is required'),
  exitDate:          z.string().min(1, 'Exit date is required'),
  lastWorkingDate:   z.string().optional(),
  noticePeriodDays:  z.coerce.number().int().min(0).optional(),
  reason:            z.string().max(500).optional(),
});
type ExitForm = z.infer<typeof exitSchema>;

export default function EmployeeExitPage() {
  const params = useParams();
  const employeeId = params.id as string;
  const qc = useQueryClient();
  const [dialogOpen, setDialog] = useState(false);

  const { data: exitRecord, isLoading } = useQuery<ExitRecord | null>({
    queryKey: ['employee-exit', employeeId],
    queryFn: async () => {
      const r = await csrfFetch(`${BASE}/api/employees/${employeeId}/exit`, { credentials: 'include' });
      if (r.status === 404) return null;
      if (!r.ok) throw new Error('Failed to load exit record');
       return r.json().then((d: unknown) => {
         const payload = (d as { data?: unknown })?.data ?? d;
         return payload as ExitRecord | null;
       });
    },
    enabled: Boolean(employeeId),
  });

  const form = useForm<ExitForm>({
    resolver: zodResolver(exitSchema),
    defaultValues: { exitType: '', exitDate: '', lastWorkingDate: '', noticePeriodDays: undefined, reason: '' },
  });

  const initiateMut = useMutation({
    mutationFn: async (values: ExitForm) => {
      const res = await csrfFetch(`${BASE}/api/employees/${employeeId}/exit`, {
        method: 'POST', credentials: 'include',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(values),
      });
      if (!res.ok) {
        const d = await res.json().catch(() => ({}));
        throw new Error((d as { message?: string })?.message ?? 'Exit initiation failed');
      }
      return res.json();
    },
    onSuccess: () => {
      toast.success('Exit process initiated.');
      qc.invalidateQueries({ queryKey: ['employee-exit', employeeId] });
      setDialog(false);
      form.reset();
    },
    onError: (e: Error) => toast.error(e.message),
  });

  return (
    <div className="space-y-6">
      <PageHeader
        title="Employee Exit"
        breadcrumbs={
          <div className="flex items-center text-sm text-muted-foreground">
            <Link href={`/employees/${employeeId}`} className="hover:text-foreground flex items-center transition-colors">
              <ArrowLeft className="mr-1 h-3 w-3" />Employee Detail
            </Link>
            <span className="mx-2">/</span>
            <span className="text-foreground font-medium">Exit</span>
          </div>
        }
        actions={
          !exitRecord && (
            <Button variant="destructive" onClick={() => setDialog(true)}>
              <LogOut className="h-4 w-4 mr-2" />Initiate Exit
            </Button>
          )
        }
      />

      {isLoading ? (
        <Card><CardContent className="p-6"><Skeleton className="h-32 w-full" /></CardContent></Card>
      ) : !exitRecord ? (
        <Card>
          <CardContent className="flex flex-col items-center justify-center py-12 text-center gap-3">
            <LogOut className="h-10 w-10 text-muted-foreground" />
            <p className="text-muted-foreground">No exit record found for this employee.</p>
            <Button variant="destructive" onClick={() => setDialog(true)}>
              <LogOut className="h-4 w-4 mr-2" />Initiate Exit Process
            </Button>
          </CardContent>
        </Card>
      ) : (
        <Card>
          <CardHeader>
            <div className="flex items-center justify-between">
              <CardTitle>Exit Record</CardTitle>
              <Badge variant={exitRecord.status === 'Completed' ? 'default' : 'secondary'}>
                {exitRecord.status}
              </Badge>
            </div>
          </CardHeader>
          <CardContent>
            <dl className="grid grid-cols-2 gap-4 text-sm">
              <div>
                <dt className="text-muted-foreground">Exit Type</dt>
                <dd className="font-medium mt-1">{exitRecord.exitType}</dd>
              </div>
              <div>
                <dt className="text-muted-foreground">Exit Date</dt>
                <dd className="font-medium mt-1">{new Date(exitRecord.exitDate).toLocaleDateString('en-IN')}</dd>
              </div>
              {exitRecord.lastWorkingDate && (
                <div>
                  <dt className="text-muted-foreground">Last Working Date</dt>
                  <dd className="font-medium mt-1">{new Date(exitRecord.lastWorkingDate).toLocaleDateString('en-IN')}</dd>
                </div>
              )}
              {exitRecord.noticePeriodDays !== undefined && (
                <div>
                  <dt className="text-muted-foreground">Notice Period</dt>
                  <dd className="font-medium mt-1">{exitRecord.noticePeriodDays} days</dd>
                </div>
              )}
              {exitRecord.reason && (
                <div className="col-span-2">
                  <dt className="text-muted-foreground">Reason</dt>
                  <dd className="mt-1">{exitRecord.reason}</dd>
                </div>
              )}
              <div>
                <dt className="text-muted-foreground">Initiated On</dt>
                <dd className="font-medium mt-1">{new Date(exitRecord.createdAt).toLocaleDateString('en-IN')}</dd>
              </div>
            </dl>
          </CardContent>
        </Card>
      )}

      {/* Initiate Exit Dialog */}
      <Dialog open={dialogOpen} onOpenChange={open => { if (!open) { setDialog(false); form.reset(); } }}>
        <DialogContent>
          <DialogHeader><DialogTitle>Initiate Exit Process</DialogTitle></DialogHeader>
          <Form {...form}>
            <form onSubmit={form.handleSubmit(v => initiateMut.mutate(v))} className="space-y-4">
              <FormField control={form.control} name="exitType" render={({ field }) => (
                <FormItem>
                  <FormLabel>Exit Type</FormLabel>
                  <Select onValueChange={field.onChange} value={field.value}>
                    <FormControl><SelectTrigger><SelectValue placeholder="Select type…" /></SelectTrigger></FormControl>
                    <SelectContent>
                      {EXIT_TYPES.map(t => <SelectItem key={t} value={t}>{t}</SelectItem>)}
                    </SelectContent>
                  </Select>
                  <FormMessage />
                </FormItem>
              )} />
              <div className="grid grid-cols-2 gap-4">
                <FormField control={form.control} name="exitDate" render={({ field }) => (
                  <FormItem><FormLabel>Exit Date</FormLabel><FormControl><Input {...field} type="date" /></FormControl><FormMessage /></FormItem>
                )} />
                <FormField control={form.control} name="lastWorkingDate" render={({ field }) => (
                  <FormItem>
                    <FormLabel>Last Working Date <span className="text-muted-foreground text-xs">(opt)</span></FormLabel>
                    <FormControl><Input {...field} type="date" /></FormControl>
                    <FormMessage />
                  </FormItem>
                )} />
              </div>
              <FormField control={form.control} name="noticePeriodDays" render={({ field }) => (
                <FormItem>
                  <FormLabel>Notice Period (days) <span className="text-muted-foreground text-xs">(optional)</span></FormLabel>
                  <FormControl><Input {...field} type="number" placeholder="30" /></FormControl>
                  <FormMessage />
                </FormItem>
              )} />
              <FormField control={form.control} name="reason" render={({ field }) => (
                <FormItem>
                  <FormLabel>Reason <span className="text-muted-foreground text-xs">(optional)</span></FormLabel>
                  <FormControl><Textarea {...field} rows={3} placeholder="Reason for exit…" /></FormControl>
                  <FormMessage />
                </FormItem>
              )} />
              <DialogFooter>
                <Button variant="outline" type="button" onClick={() => { setDialog(false); form.reset(); }}>Cancel</Button>
                <Button type="submit" variant="destructive" disabled={initiateMut.isPending}>
                  {initiateMut.isPending ? 'Initiating…' : 'Initiate Exit'}
                </Button>
              </DialogFooter>
            </form>
          </Form>
        </DialogContent>
      </Dialog>
    </div>
  );
}
