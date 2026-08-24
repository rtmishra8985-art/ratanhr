// Wired to existing backend: GET/POST/PUT/DELETE /api/organisation/designations
// (DepartmentController already exposes these endpoints — no new controller needed.)
// SEC-DEPT-01: credentials: 'include' on every API call.
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
import { Textarea }    from '@/components/ui/textarea';
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter } from '@/components/ui/dialog';
import { AlertDialog, AlertDialogAction, AlertDialogCancel, AlertDialogContent,
  AlertDialogDescription, AlertDialogFooter, AlertDialogHeader, AlertDialogTitle } from '@/components/ui/alert-dialog';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Form, FormControl, FormField, FormItem, FormLabel, FormMessage } from '@/components/ui/form';
import { Skeleton } from '@/components/ui/skeleton';
import { csrfFetch } from '@/utils/csrfFetch';

const BASE = import.meta.env.BASE_URL?.replace(/\/$/, '') ?? '';

// ─── Types ────────────────────────────────────────────────────────────────────

interface Designation {
  id: number;
  name: string;
  code?: string;
  description?: string;
  grade?: string;
  isActive: boolean;
}

interface PagedDesignations { items: Designation[]; totalCount: number; page: number; pageSize: number; }

// ─── Schema ───────────────────────────────────────────────────────────────────

const designationSchema = z.object({
  name:        z.string().min(1, 'Designation name is required').max(150),
  code:        z.string().max(20).optional(),
  grade:       z.string().max(50).optional(),
  description: z.string().max(500).optional(),
});
type DesignationForm = z.infer<typeof designationSchema>;

// ─── API helpers ──────────────────────────────────────────────────────────────

const api = {
  list:   (page = 1, size = 25) =>
    csrfFetch(`${BASE}/api/organisation/designations?page=${page}&pageSize=${size}`, { credentials: 'include' }),
  create: (body: DesignationForm) =>
    csrfFetch(`${BASE}/api/organisation/designations`, { method: 'POST', credentials: 'include',
      headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body) }),
  update: (id: number, body: DesignationForm) =>
    csrfFetch(`${BASE}/api/organisation/designations/${id}`, { method: 'PUT', credentials: 'include',
      headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body) }),
  delete: (id: number) =>
    csrfFetch(`${BASE}/api/organisation/designations/${id}`, { method: 'DELETE', credentials: 'include' }),
};

// ─── Component ────────────────────────────────────────────────────────────────

export default function DesignationPage() {
  const qc = useQueryClient();
  const [page,       setPage]     = useState(1);
  const [dialogOpen, setDialog]   = useState(false);
  const [deleteId,   setDeleteId] = useState<number | null>(null);
  const [editing,    setEditing]  = useState<Designation | null>(null);

  const { data, isLoading } = useQuery<PagedDesignations>({
    queryKey: ['designations', page],
    queryFn: async () => {
      const r = await api.list(page, 25);
      if (!r.ok) throw new Error('Failed to load designations');
      return r.json();
    },
  });

  const form = useForm<DesignationForm>({
    resolver: zodResolver(designationSchema),
    defaultValues: { name: '', code: '', grade: '', description: '' },
  });

  const closeDialog = () => {
    setDialog(false);
    setEditing(null);
    form.reset({ name: '', code: '', grade: '', description: '' });
  };

  const openCreate = () => {
    setEditing(null);
    form.reset({ name: '', code: '', grade: '', description: '' });
    setDialog(true);
  };

  const openEdit = (d: Designation) => {
    setEditing(d);
    form.reset({ name: d.name, code: d.code ?? '', grade: d.grade ?? '', description: d.description ?? '' });
    setDialog(true);
  };

  const saveMut = useMutation({
    mutationFn: async (values: DesignationForm) => {
      const res = editing ? await api.update(editing.id, values) : await api.create(values);
      if (!res.ok) {
        const d = await res.json().catch(() => ({}));
        throw new Error(d?.message ?? 'Save failed');
      }
      return res.json();
    },
    onSuccess: () => {
      toast.success(editing ? 'Designation updated.' : 'Designation created.');
      qc.invalidateQueries({ queryKey: ['designations'] });
      closeDialog();
    },
    onError: (e: Error) => toast.error(e.message),
  });

  const deleteMut = useMutation({
    mutationFn: async (id: number) => {
      const res = await api.delete(id);
      if (!res.ok) throw new Error('Delete failed');
    },
    onSuccess: () => {
      toast.success('Designation removed.');
      qc.invalidateQueries({ queryKey: ['designations'] });
      setDeleteId(null);
    },
    onError: (e: Error) => toast.error(e.message),
  });

  const designations = data?.items ?? [];
  const total        = data?.totalCount ?? 0;
  const totalPages   = Math.ceil(total / 25);

  return (
    <div className="space-y-6">
      <PageHeader
        title="Designations"
        description="Manage job designations across the organization."
        actions={
          <Button onClick={openCreate}>
            <Plus className="h-4 w-4 mr-2" />New Designation
          </Button>
        }
      />

      <div className="rounded-md border">
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>Designation</TableHead>
              <TableHead>Code</TableHead>
              <TableHead>Grade</TableHead>
              <TableHead>Description</TableHead>
              <TableHead>Status</TableHead>
              <TableHead className="text-right">Actions</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {isLoading
              ? Array.from({ length: 5 }).map((_, i) => (
                  <TableRow key={i}>
                    {Array.from({ length: 6 }).map((__, j) => (
                      <TableCell key={j}><Skeleton className="h-4 w-full" /></TableCell>
                    ))}
                  </TableRow>
                ))
              : designations.length === 0
                ? <TableRow><TableCell colSpan={6} className="text-center text-muted-foreground py-8">No designations found. Add one to get started.</TableCell></TableRow>
                : designations.map(d => (
                    <TableRow key={d.id}>
                      <TableCell className="font-medium">{d.name}</TableCell>
                      <TableCell className="font-mono text-sm">{d.code ?? '—'}</TableCell>
                      <TableCell>{d.grade ?? '—'}</TableCell>
                      <TableCell className="max-w-[240px] truncate text-sm text-muted-foreground" title={d.description}>
                        {d.description || '—'}
                      </TableCell>
                      <TableCell>
                        <Badge variant={d.isActive ? 'default' : 'secondary'}>
                          {d.isActive ? 'Active' : 'Inactive'}
                        </Badge>
                      </TableCell>
                      <TableCell className="text-right space-x-1">
                        <Button size="icon" variant="ghost" className="h-8 w-8" onClick={() => openEdit(d)}>
                          <Pencil className="h-4 w-4" />
                        </Button>
                        <Button
                          size="icon" variant="ghost"
                          className="h-8 w-8 text-destructive hover:text-destructive"
                          onClick={() => setDeleteId(d.id)}
                        >
                          <Trash2 className="h-4 w-4" />
                        </Button>
                      </TableCell>
                    </TableRow>
                  ))
            }
          </TableBody>
        </Table>
      </div>

      {totalPages > 1 && (
        <div className="flex items-center justify-between text-sm text-muted-foreground">
          <span>{total} designation{total !== 1 ? 's' : ''}</span>
          <div className="flex gap-2">
            <Button variant="outline" size="sm" disabled={page <= 1} onClick={() => setPage(p => p - 1)}>Previous</Button>
            <span className="self-center">Page {page} of {totalPages}</span>
            <Button variant="outline" size="sm" disabled={page >= totalPages} onClick={() => setPage(p => p + 1)}>Next</Button>
          </div>
        </div>
      )}

      {/* Create / Edit Dialog */}
      <Dialog open={dialogOpen} onOpenChange={open => { if (!open) closeDialog(); else setDialog(true); }}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>{editing ? 'Edit Designation' : 'New Designation'}</DialogTitle>
          </DialogHeader>
          <Form {...form}>
            <form onSubmit={form.handleSubmit(v => saveMut.mutate(v))} className="space-y-4">
              <FormField control={form.control} name="name" render={({ field }) => (
                <FormItem>
                  <FormLabel>Designation Name</FormLabel>
                  <FormControl><Input {...field} placeholder="Software Engineer" /></FormControl>
                  <FormMessage />
                </FormItem>
              )} />
              <div className="grid grid-cols-2 gap-4">
                <FormField control={form.control} name="code" render={({ field }) => (
                  <FormItem>
                    <FormLabel>Code <span className="text-muted-foreground text-xs">(optional)</span></FormLabel>
                    <FormControl><Input {...field} placeholder="SWE" /></FormControl>
                    <FormMessage />
                  </FormItem>
                )} />
                <FormField control={form.control} name="grade" render={({ field }) => (
                  <FormItem>
                    <FormLabel>Grade <span className="text-muted-foreground text-xs">(optional)</span></FormLabel>
                    <FormControl><Input {...field} placeholder="L3" /></FormControl>
                    <FormMessage />
                  </FormItem>
                )} />
              </div>
              <FormField control={form.control} name="description" render={({ field }) => (
                <FormItem>
                  <FormLabel>Description <span className="text-muted-foreground text-xs">(optional)</span></FormLabel>
                  <FormControl><Textarea {...field} rows={3} placeholder="Brief description…" /></FormControl>
                  <FormMessage />
                </FormItem>
              )} />
              <DialogFooter>
                <Button variant="outline" type="button" onClick={closeDialog}>Cancel</Button>
                <Button type="submit" disabled={saveMut.isPending}>
                  {saveMut.isPending ? 'Saving…' : 'Save'}
                </Button>
              </DialogFooter>
            </form>
          </Form>
        </DialogContent>
      </Dialog>

      {/* Delete confirmation */}
      <AlertDialog open={deleteId !== null} onOpenChange={open => { if (!open) setDeleteId(null); }}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Remove Designation?</AlertDialogTitle>
            <AlertDialogDescription>
              Employees assigned to this designation may need to be updated. Historical records are preserved.
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>Cancel</AlertDialogCancel>
            <AlertDialogAction
              className="bg-destructive text-destructive-foreground hover:bg-destructive/90"
              onClick={() => deleteId !== null && deleteMut.mutate(deleteId)}
            >
              Remove
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </div>
  );
}
