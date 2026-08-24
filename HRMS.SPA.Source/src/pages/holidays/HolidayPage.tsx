// Restored: Holiday calendar SPA page.
// Full CRUD for company public holidays, with year filter.
// SEC-HOLIDAY-01: credentials: 'include' on every API call.
import { useState } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import * as z from 'zod';
import { Plus, Pencil, Trash2 } from 'lucide-react';
import { toast } from 'sonner';

import { PageHeader }  from '@/components/layout/PageHeader';
import { Button }      from '@/components/ui/button';
import { Badge }       from '@/components/ui/badge';
import { Input }       from '@/components/ui/input';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter } from '@/components/ui/dialog';
import { AlertDialog, AlertDialogAction, AlertDialogCancel, AlertDialogContent,
  AlertDialogDescription, AlertDialogFooter, AlertDialogHeader, AlertDialogTitle } from '@/components/ui/alert-dialog';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Form, FormControl, FormField, FormItem, FormLabel, FormMessage } from '@/components/ui/form';
import { Skeleton } from '@/components/ui/skeleton';
import { csrfFetch } from '@/utils/csrfFetch';

const BASE = import.meta.env.BASE_URL?.replace(/\/$/, '') ?? '';

// ─── Types ────────────────────────────────────────────────────────────────────

type HolidayType = 'National' | 'Optional' | 'Regional';

interface Holiday {
  id: number;
  year: number;
  name: string;
  date: string;           // yyyy-MM-dd
  holidayType: HolidayType;
  description?: string;
  isRecurring: boolean;
}

// ─── Schema ───────────────────────────────────────────────────────────────────

const holidaySchema = z.object({
  name:        z.string().min(1, 'Holiday name is required').max(200),
  date:        z.string().regex(/^\d{4}-\d{2}-\d{2}$/, 'Use YYYY-MM-DD format'),
  holidayType: z.enum(['National', 'Optional', 'Regional']),
  description: z.string().max(500).optional(),
  isRecurring: z.boolean(),
});
type HolidayForm = z.infer<typeof holidaySchema>;

// ─── Helpers ──────────────────────────────────────────────────────────────────

const typeBadgeVariant = (t: HolidayType): 'default' | 'secondary' | 'outline' =>
  t === 'National' ? 'default' : t === 'Optional' ? 'secondary' : 'outline';

function fmtDate(d: string) {
  try { return new Date(d).toLocaleDateString('en-IN', { day: 'numeric', month: 'short', year: 'numeric' }); }
  catch { return d; }
}

// ─── API helpers ──────────────────────────────────────────────────────────────

const api = {
  list:   (year: number) =>
    csrfFetch(`${BASE}/api/holidays?year=${year}`, { credentials: 'include' }),
  create: (body: HolidayForm & { year: number }) =>
    csrfFetch(`${BASE}/api/holidays`, { method: 'POST', credentials: 'include',
      headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body) }),
  update: (id: number, body: HolidayForm & { year: number }) =>
    csrfFetch(`${BASE}/api/holidays/${id}`, { method: 'PUT', credentials: 'include',
      headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body) }),
  delete: (id: number) =>
    csrfFetch(`${BASE}/api/holidays/${id}`, { method: 'DELETE', credentials: 'include' }),
};

// ─── Component ────────────────────────────────────────────────────────────────

const currentYear = new Date().getFullYear();
const yearOptions = Array.from({ length: 5 }, (_, i) => currentYear - 1 + i);

export default function HolidayPage() {
  const qc = useQueryClient();
  const [year,       setYear]     = useState(currentYear);
  const [dialogOpen, setDialog]   = useState(false);
  const [deleteId,   setDeleteId] = useState<number | null>(null);
  const [editing,    setEditing]  = useState<Holiday | null>(null);

  const { data: holidays = [], isLoading } = useQuery<Holiday[]>({
    queryKey: ['holidays', year],
    queryFn: async () => { const r = await api.list(year); if (!r.ok) throw new Error('Failed'); return r.json(); },
  });

  const form = useForm<HolidayForm>({
    resolver: zodResolver(holidaySchema),
    defaultValues: { name: '', date: '', holidayType: 'National', description: '', isRecurring: false },
  });

  const saveMut = useMutation({
    mutationFn: async (values: HolidayForm) => {
      const body = { ...values, year };
      const res  = editing ? await api.update(editing.id, body) : await api.create(body);
      if (!res.ok) { const e = await res.json().catch(() => ({})); throw new Error(e.message ?? 'Save failed'); }
    },
    onSuccess: () => {
      toast.success(editing ? 'Holiday updated.' : 'Holiday added.');
      qc.invalidateQueries({ queryKey: ['holidays'] });
      closeDialog();
    },
    onError: (e: Error) => toast.error(e.message),
  });

  const deleteMut = useMutation({
    mutationFn: async (id: number) => { const r = await api.delete(id); if (!r.ok) throw new Error('Delete failed'); },
    onSuccess: () => { toast.success('Holiday removed.'); qc.invalidateQueries({ queryKey: ['holidays'] }); setDeleteId(null); },
    onError: () => toast.error('Failed to remove holiday.'),
  });

  const openCreate = () => {
    setEditing(null);
    form.reset({ name: '', date: `${year}-01-01`, holidayType: 'National', description: '', isRecurring: false });
    setDialog(true);
  };
  const openEdit = (h: Holiday) => {
    setEditing(h);
    form.reset({ name: h.name, date: h.date, holidayType: h.holidayType, description: h.description ?? '', isRecurring: h.isRecurring });
    setDialog(true);
  };
  const closeDialog = () => { setDialog(false); setEditing(null); form.reset(); };

  return (
    <div className="space-y-6">
      <PageHeader
        title="Holiday Calendar"
        description="Manage public and optional holidays for your company."
        actions={
          <div className="flex items-center gap-3">
            <Select value={String(year)} onValueChange={v => setYear(Number(v))}>
              <SelectTrigger className="w-28"><SelectValue /></SelectTrigger>
              <SelectContent>{yearOptions.map(y => <SelectItem key={y} value={String(y)}>{y}</SelectItem>)}</SelectContent>
            </Select>
            <Button onClick={openCreate}><Plus className="h-4 w-4 mr-2" />Add Holiday</Button>
          </div>
        }
      />

      <div className="rounded-md border">
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>Name</TableHead>
              <TableHead>Date</TableHead>
              <TableHead>Type</TableHead>
              <TableHead>Recurring</TableHead>
              <TableHead>Description</TableHead>
              <TableHead className="text-right">Actions</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {isLoading
              ? Array.from({ length: 8 }).map((_, i) => (
                  <TableRow key={i}>{Array.from({ length: 6 }).map((__, j) => <TableCell key={j}><Skeleton className="h-4 w-full" /></TableCell>)}</TableRow>
                ))
              : holidays.length === 0
                ? <TableRow><TableCell colSpan={6} className="text-center text-muted-foreground py-8">No holidays for {year}. Add the first one.</TableCell></TableRow>
                : holidays.map(h => (
                    <TableRow key={h.id}>
                      <TableCell className="font-medium">{h.name}</TableCell>
                      <TableCell>{fmtDate(h.date)}</TableCell>
                      <TableCell><Badge variant={typeBadgeVariant(h.holidayType)}>{h.holidayType}</Badge></TableCell>
                      <TableCell>{h.isRecurring ? <Badge variant="outline">Annual</Badge> : '—'}</TableCell>
                      <TableCell className="text-sm text-muted-foreground max-w-xs truncate">{h.description ?? '—'}</TableCell>
                      <TableCell className="text-right">
                        <Button variant="ghost" size="icon" onClick={() => openEdit(h)}><Pencil className="h-4 w-4" /></Button>
                        <Button variant="ghost" size="icon" className="text-destructive" onClick={() => setDeleteId(h.id)}><Trash2 className="h-4 w-4" /></Button>
                      </TableCell>
                    </TableRow>
                  ))
            }
          </TableBody>
        </Table>
      </div>

      {/* Create / Edit */}
      <Dialog open={dialogOpen} onOpenChange={setDialog}>
        <DialogContent>
          <DialogHeader><DialogTitle>{editing ? 'Edit Holiday' : `Add Holiday — ${year}`}</DialogTitle></DialogHeader>
          <Form {...form}>
            <form onSubmit={form.handleSubmit(v => saveMut.mutate(v))} className="space-y-4">
              <FormField control={form.control} name="name" render={({ field }) => (
                <FormItem><FormLabel>Holiday Name</FormLabel><FormControl><Input {...field} placeholder="Republic Day" /></FormControl><FormMessage /></FormItem>
              )} />
              <FormField control={form.control} name="date" render={({ field }) => (
                <FormItem><FormLabel>Date</FormLabel><FormControl><Input type="date" {...field} /></FormControl><FormMessage /></FormItem>
              )} />
              <FormField control={form.control} name="holidayType" render={({ field }) => (
                <FormItem><FormLabel>Type</FormLabel>
                  <Select value={field.value} onValueChange={field.onChange}>
                    <FormControl><SelectTrigger><SelectValue /></SelectTrigger></FormControl>
                    <SelectContent>
                      <SelectItem value="National">National</SelectItem>
                      <SelectItem value="Optional">Optional</SelectItem>
                      <SelectItem value="Regional">Regional</SelectItem>
                    </SelectContent>
                  </Select>
                  <FormMessage /></FormItem>
              )} />
              <FormField control={form.control} name="description" render={({ field }) => (
                <FormItem><FormLabel>Description (optional)</FormLabel><FormControl><Input {...field} placeholder="Brief note…" /></FormControl><FormMessage /></FormItem>
              )} />
              <FormField control={form.control} name="isRecurring" render={({ field }) => (
                <FormItem className="flex items-center gap-3">
                  <input type="checkbox" id="recurring" checked={field.value} onChange={e => field.onChange(e.target.checked)} className="h-4 w-4" />
                  <FormLabel htmlFor="recurring" className="cursor-pointer">Repeat every year</FormLabel>
                </FormItem>
              )} />
              <DialogFooter>
                <Button variant="outline" type="button" onClick={closeDialog}>Cancel</Button>
                <Button type="submit" disabled={saveMut.isPending}>{saveMut.isPending ? 'Saving…' : 'Save'}</Button>
              </DialogFooter>
            </form>
          </Form>
        </DialogContent>
      </Dialog>

      {/* Delete confirmation */}
      <AlertDialog open={deleteId !== null} onOpenChange={open => { if (!open) setDeleteId(null); }}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Remove Holiday?</AlertDialogTitle>
            <AlertDialogDescription>This will remove the holiday from the {year} calendar.</AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>Cancel</AlertDialogCancel>
            <AlertDialogAction className="bg-destructive text-destructive-foreground hover:bg-destructive/90"
              onClick={() => deleteId !== null && deleteMut.mutate(deleteId)}>Remove</AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </div>
  );
}
