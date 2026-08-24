// Restored: Department management SPA page.
// Full CRUD for company departments, wired to the Organisation API.
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

interface Department {
  id: number;
  name: string;
  code: string;
  description?: string;
  headName?: string;
  employeeCount: number;
  isActive: boolean;
}

interface PagedDepts { items: Department[]; totalCount: number; page: number; pageSize: number; }

// ─── Schema ───────────────────────────────────────────────────────────────────

const deptSchema = z.object({
  name:        z.string().min(1, 'Department name is required').max(150),
  code:        z.string().min(1, 'Code is required').max(20).regex(/^[A-Z0-9_-]+$/, 'Uppercase letters, digits, _ and - only'),
  description: z.string().max(500).optional(),
  headName:    z.string().max(200).optional(),
});
type DeptForm = z.infer<typeof deptSchema>;

// ─── API helpers ──────────────────────────────────────────────────────────────

const api = {
  list:   (page = 1, size = 25) =>
    csrfFetch(`${BASE}/api/organisation/departments?page=${page}&pageSize=${size}`, { credentials: 'include' }),
  create: (body: DeptForm) =>
    csrfFetch(`${BASE}/api/organisation/departments`, { method: 'POST', credentials: 'include',
      headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body) }),
  update: (id: number, body: DeptForm) =>
    csrfFetch(`${BASE}/api/organisation/departments/${id}`, { method: 'PUT', credentials: 'include',
      headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body) }),
  delete: (id: number) =>
    csrfFetch(`${BASE}/api/organisation/departments/${id}`, { method: 'DELETE', credentials: 'include' }),
};

// ─── Component ────────────────────────────────────────────────────────────────

export default function DepartmentPage() {
  const qc = useQueryClient();
  const [page,       setPage]      = useState(1);
  const [dialogOpen, setDialog]    = useState(false);
  const [deleteId,   setDeleteId]  = useState<number | null>(null);
  const [editing,    setEditing]   = useState<Department | null>(null);

  const { data, isLoading } = useQuery<PagedDepts>({
    queryKey: ['departments', page],
    queryFn: async () => { const r = await api.list(page, 25); if (!r.ok) throw new Error('Failed'); return r.json(); },
  });

  const form = useForm<DeptForm>({
    resolver: zodResolver(deptSchema),
    defaultValues: { name: '', code: '', description: '', headName: '' },
  });

  const saveMut = useMutation({
    mutationFn: async (values: DeptForm) => {
      const res = editing ? await api.update(editing.id, values) : await api.create(values);
      if (!res.ok) { const e = await res.json().catch(() => ({})); throw new Error(e.message ?? 'Save failed'); }
    },
    onSuccess: () => {
      toast.success(editing ? 'Department updated.' : 'Department created.');
      qc.invalidateQueries({ queryKey: ['departments'] });
      closeDialog();
    },
    onError: (e: Error) => toast.error(e.message),
  });

  const deleteMut = useMutation({
    mutationFn: async (id: number) => { const r = await api.delete(id); if (!r.ok) throw new Error('Delete failed'); },
    onSuccess: () => { toast.success('Department deleted.'); qc.invalidateQueries({ queryKey: ['departments'] }); setDeleteId(null); },
    onError: () => toast.error('Failed to delete. Ensure no employees are assigned.'),
  });

  const openCreate = () => { setEditing(null); form.reset({ name: '', code: '', description: '', headName: '' }); setDialog(true); };
  const openEdit   = (d: Department) => { setEditing(d); form.reset({ name: d.name, code: d.code, description: d.description ?? '', headName: d.headName ?? '' }); setDialog(true); };
  const closeDialog = () => { setDialog(false); setEditing(null); form.reset(); };

  const departments = data?.items ?? [];
  const totalPages  = Math.ceil((data?.totalCount ?? 0) / 25);

  return (
    <div className="space-y-6">
      <PageHeader
        title="Departments"
        description="Manage your company's organisational departments."
        actions={<Button onClick={openCreate}><Plus className="h-4 w-4 mr-2" />New Department</Button>}
      />

      <div className="rounded-md border">
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>Name</TableHead>
              <TableHead>Code</TableHead>
              <TableHead>Head</TableHead>
              <TableHead>Employees</TableHead>
              <TableHead>Status</TableHead>
              <TableHead className="text-right">Actions</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {isLoading
              ? Array.from({ length: 6 }).map((_, i) => (
                  <TableRow key={i}>{Array.from({ length: 6 }).map((__, j) => <TableCell key={j}><Skeleton className="h-4 w-full" /></TableCell>)}</TableRow>
                ))
              : departments.length === 0
                ? <TableRow><TableCell colSpan={6} className="text-center text-muted-foreground py-8">No departments yet. Create the first one.</TableCell></TableRow>
                : departments.map(d => (
                    <TableRow key={d.id}>
                      <TableCell className="font-medium">{d.name}</TableCell>
                      <TableCell><code className="bg-muted px-1.5 py-0.5 rounded text-xs">{d.code}</code></TableCell>
                      <TableCell>{d.headName ?? '—'}</TableCell>
                      <TableCell>{d.employeeCount}</TableCell>
                      <TableCell><Badge variant={d.isActive ? 'default' : 'outline'}>{d.isActive ? 'Active' : 'Inactive'}</Badge></TableCell>
                      <TableCell className="text-right">
                        <Button variant="ghost" size="icon" onClick={() => openEdit(d)}><Pencil className="h-4 w-4" /></Button>
                        <Button variant="ghost" size="icon" className="text-destructive" onClick={() => setDeleteId(d.id)}><Trash2 className="h-4 w-4" /></Button>
                      </TableCell>
                    </TableRow>
                  ))
            }
          </TableBody>
        </Table>
      </div>

      {totalPages > 1 && (
        <div className="flex items-center justify-end gap-2">
          <Button variant="outline" size="sm" disabled={page <= 1} onClick={() => setPage(p => p - 1)}>Previous</Button>
          <span className="text-sm text-muted-foreground">Page {page} of {totalPages}</span>
          <Button variant="outline" size="sm" disabled={page >= totalPages} onClick={() => setPage(p => p + 1)}>Next</Button>
        </div>
      )}

      {/* Create / Edit Dialog */}
      <Dialog open={dialogOpen} onOpenChange={setDialog}>
        <DialogContent>
          <DialogHeader><DialogTitle>{editing ? 'Edit Department' : 'New Department'}</DialogTitle></DialogHeader>
          <Form {...form}>
            <form onSubmit={form.handleSubmit(v => saveMut.mutate(v))} className="space-y-4">
              <FormField control={form.control} name="name" render={({ field }) => (
                <FormItem><FormLabel>Department Name</FormLabel><FormControl><Input {...field} placeholder="Engineering" /></FormControl><FormMessage /></FormItem>
              )} />
              <FormField control={form.control} name="code" render={({ field }) => (
                <FormItem><FormLabel>Code</FormLabel><FormControl><Input {...field} placeholder="ENG" className="uppercase" onChange={e => field.onChange(e.target.value.toUpperCase())} /></FormControl><FormMessage /></FormItem>
              )} />
              <FormField control={form.control} name="headName" render={({ field }) => (
                <FormItem><FormLabel>Department Head (optional)</FormLabel><FormControl><Input {...field} placeholder="Jane Doe" /></FormControl><FormMessage /></FormItem>
              )} />
              <FormField control={form.control} name="description" render={({ field }) => (
                <FormItem><FormLabel>Description (optional)</FormLabel><FormControl><Textarea {...field} rows={3} placeholder="Brief description…" /></FormControl><FormMessage /></FormItem>
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
            <AlertDialogTitle>Delete Department?</AlertDialogTitle>
            <AlertDialogDescription>Employees assigned to this department will need to be reassigned. This action cannot be undone.</AlertDialogDescription>
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
