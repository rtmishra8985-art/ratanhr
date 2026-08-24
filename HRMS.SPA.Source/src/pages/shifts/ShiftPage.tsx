// Restored: Shift management SPA page.
// SEC-SHIFT-01: All API calls use credentials: 'include' (cookie-based auth).
// Provides full CRUD for shift definitions scoped to the current company.
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
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter } from '@/components/ui/dialog';
import { AlertDialog, AlertDialogAction, AlertDialogCancel, AlertDialogContent,
  AlertDialogDescription, AlertDialogFooter, AlertDialogHeader, AlertDialogTitle } from '@/components/ui/alert-dialog';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Form, FormControl, FormField, FormItem, FormLabel, FormMessage } from '@/components/ui/form';
import { Skeleton } from '@/components/ui/skeleton';
import { Switch }   from '@/components/ui/switch';
import { csrfFetch } from '@/utils/csrfFetch';

const BASE = import.meta.env.BASE_URL?.replace(/\/$/, '') ?? '';

// ─── Types ────────────────────────────────────────────────────────────────────

interface Shift {
  id: number;
  name: string;
  startTime: string;   // "HH:mm"
  endTime: string;     // "HH:mm"
  graceMinutes: number;
  isNightShift: boolean;
  isActive: boolean;
}

interface PagedShifts { items: Shift[]; totalCount: number; page: number; pageSize: number; }

// ─── Schema ───────────────────────────────────────────────────────────────────

const shiftSchema = z.object({
  name:          z.string().min(1, 'Shift name is required').max(100),
  startTime:     z.string().regex(/^\d{2}:\d{2}$/, 'Use HH:mm format'),
  endTime:       z.string().regex(/^\d{2}:\d{2}$/, 'Use HH:mm format'),
  graceMinutes:  z.coerce.number().int().min(0).max(60),
  isNightShift:  z.boolean(),
  isActive:      z.boolean(),
});
type ShiftForm = z.infer<typeof shiftSchema>;

// ─── API helpers ──────────────────────────────────────────────────────────────

const api = {
  list:   (page = 1, size = 20) =>
    csrfFetch(`${BASE}/api/shifts?page=${page}&pageSize=${size}`, { credentials: 'include' }),
  create: (body: Omit<Shift, 'id'>) =>
    csrfFetch(`${BASE}/api/shifts`, { method: 'POST', credentials: 'include',
      headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body) }),
  update: (id: number, body: Omit<Shift, 'id'>) =>
    csrfFetch(`${BASE}/api/shifts/${id}`, { method: 'PUT', credentials: 'include',
      headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body) }),
  delete: (id: number) =>
    csrfFetch(`${BASE}/api/shifts/${id}`, { method: 'DELETE', credentials: 'include' }),
};

// ─── Component ────────────────────────────────────────────────────────────────

export default function ShiftPage() {
  const qc = useQueryClient();
  const [dialogOpen, setDialogOpen] = useState(false);
  const [deleteId,   setDeleteId]   = useState<number | null>(null);
  const [editing,    setEditing]    = useState<Shift | null>(null);

  const { data, isLoading } = useQuery<PagedShifts>({
    queryKey: ['shifts'],
    queryFn: async () => { const r = await api.list(); if (!r.ok) throw new Error('Failed'); return r.json(); },
  });

  const form = useForm<ShiftForm>({
    resolver: zodResolver(shiftSchema),
    defaultValues: { name: '', startTime: '09:00', endTime: '18:00', graceMinutes: 10, isNightShift: false, isActive: true },
  });

  const saveMut = useMutation({
    mutationFn: async (values: ShiftForm) => {
      const res = editing
        ? await api.update(editing.id, values)
        : await api.create(values);
      if (!res.ok) { const e = await res.json().catch(() => ({})); throw new Error(e.message ?? 'Save failed'); }
    },
    onSuccess: () => {
      toast.success(editing ? 'Shift updated.' : 'Shift created.');
      qc.invalidateQueries({ queryKey: ['shifts'] });
      closeDialog();
    },
    onError: (e: Error) => toast.error(e.message),
  });

  const deleteMut = useMutation({
    mutationFn: async (id: number) => {
      const res = await api.delete(id);
      if (!res.ok) throw new Error('Delete failed');
    },
    onSuccess: () => { toast.success('Shift deleted.'); qc.invalidateQueries({ queryKey: ['shifts'] }); setDeleteId(null); },
    onError: () => toast.error('Failed to delete shift.'),
  });

  const openCreate = () => { setEditing(null); form.reset({ name: '', startTime: '09:00', endTime: '18:00', graceMinutes: 10, isNightShift: false, isActive: true }); setDialogOpen(true); };
  const openEdit   = (s: Shift) => { setEditing(s); form.reset({ name: s.name, startTime: s.startTime, endTime: s.endTime, graceMinutes: s.graceMinutes, isNightShift: s.isNightShift, isActive: s.isActive }); setDialogOpen(true); };
  const closeDialog = () => { setDialogOpen(false); setEditing(null); form.reset(); };

  const shifts = data?.items ?? [];

  return (
    <div className="space-y-6">
      <PageHeader
        title="Shifts"
        description="Define and manage company shift schedules."
        actions={<Button onClick={openCreate}><Plus className="h-4 w-4 mr-2" />New Shift</Button>}
      />

      <div className="rounded-md border">
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>Name</TableHead>
              <TableHead>Start</TableHead>
              <TableHead>End</TableHead>
              <TableHead>Grace (min)</TableHead>
              <TableHead>Night</TableHead>
              <TableHead>Status</TableHead>
              <TableHead className="text-right">Actions</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {isLoading
              ? Array.from({ length: 5 }).map((_, i) => (
                  <TableRow key={i}>
                    {Array.from({ length: 7 }).map((__, j) => (
                      <TableCell key={j}><Skeleton className="h-4 w-full" /></TableCell>
                    ))}
                  </TableRow>
                ))
              : shifts.length === 0
                ? <TableRow><TableCell colSpan={7} className="text-center text-muted-foreground py-8">No shifts found. Create the first one.</TableCell></TableRow>
                : shifts.map(s => (
                    <TableRow key={s.id}>
                      <TableCell className="font-medium">{s.name}</TableCell>
                      <TableCell>{s.startTime}</TableCell>
                      <TableCell>{s.endTime}</TableCell>
                      <TableCell>{s.graceMinutes}</TableCell>
                      <TableCell>{s.isNightShift ? <Badge variant="secondary">Night</Badge> : '—'}</TableCell>
                      <TableCell><Badge variant={s.isActive ? 'default' : 'outline'}>{s.isActive ? 'Active' : 'Inactive'}</Badge></TableCell>
                      <TableCell className="text-right">
                        <Button variant="ghost" size="icon" onClick={() => openEdit(s)}><Pencil className="h-4 w-4" /></Button>
                        <Button variant="ghost" size="icon" className="text-destructive" onClick={() => setDeleteId(s.id)}><Trash2 className="h-4 w-4" /></Button>
                      </TableCell>
                    </TableRow>
                  ))
            }
          </TableBody>
        </Table>
      </div>

      {/* Create / Edit Dialog */}
      <Dialog open={dialogOpen} onOpenChange={setDialogOpen}>
        <DialogContent>
          <DialogHeader><DialogTitle>{editing ? 'Edit Shift' : 'New Shift'}</DialogTitle></DialogHeader>
          <Form {...form}>
            <form onSubmit={form.handleSubmit(v => saveMut.mutate(v))} className="space-y-4">
              <FormField control={form.control} name="name" render={({ field }) => (
                <FormItem><FormLabel>Shift Name</FormLabel><FormControl><Input {...field} placeholder="Morning" /></FormControl><FormMessage /></FormItem>
              )} />
              <div className="grid grid-cols-2 gap-4">
                <FormField control={form.control} name="startTime" render={({ field }) => (
                  <FormItem><FormLabel>Start Time</FormLabel><FormControl><Input type="time" {...field} /></FormControl><FormMessage /></FormItem>
                )} />
                <FormField control={form.control} name="endTime" render={({ field }) => (
                  <FormItem><FormLabel>End Time</FormLabel><FormControl><Input type="time" {...field} /></FormControl><FormMessage /></FormItem>
                )} />
              </div>
              <FormField control={form.control} name="graceMinutes" render={({ field }) => (
                <FormItem><FormLabel>Grace Period (minutes)</FormLabel><FormControl><Input type="number" min={0} max={60} {...field} /></FormControl><FormMessage /></FormItem>
              )} />
              <div className="flex gap-6">
                <FormField control={form.control} name="isNightShift" render={({ field }) => (
                  <FormItem className="flex items-center gap-2"><FormLabel>Night Shift</FormLabel><FormControl><Switch checked={field.value} onCheckedChange={field.onChange} /></FormControl></FormItem>
                )} />
                <FormField control={form.control} name="isActive" render={({ field }) => (
                  <FormItem className="flex items-center gap-2"><FormLabel>Active</FormLabel><FormControl><Switch checked={field.value} onCheckedChange={field.onChange} /></FormControl></FormItem>
                )} />
              </div>
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
            <AlertDialogTitle>Delete Shift?</AlertDialogTitle>
            <AlertDialogDescription>This action cannot be undone. Employees currently assigned to this shift may be affected.</AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>Cancel</AlertDialogCancel>
            <AlertDialogAction className="bg-destructive text-destructive-foreground hover:bg-destructive/90"
              onClick={() => deleteId !== null && deleteMut.mutate(deleteId)}>Delete</AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </div>
  );
}
