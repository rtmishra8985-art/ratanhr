// Assets Management — Add/Assign/Return/View wired to the backend AssetsController.
// Previously "Add Asset"/"Assign"/"Return"/"View" were dead buttons with no
// onClick handler despite AssetsController fully implementing create/assign/
// return/history. Follows the same self-contained csrfFetch + react-query
// pattern used by DepartmentPage.tsx (no changes to the generated api-client
// needed — this page owns its own small API helper + types, matching the
// backend AssetDtos.cs field names).
import { useState } from 'react';
import { Laptop, AlertTriangle, CheckCircle, PackageSearch, Search, Plus } from 'lucide-react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import * as z from 'zod';
import { toast } from 'sonner';
import { useListAssets, useGetAssetSummary } from '@workspace/api-client-react';

import { PageHeader } from '@/components/layout/PageHeader';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
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
import { Skeleton } from '@/components/ui/skeleton';
import { Input } from '@/components/ui/input';
import { Textarea } from '@/components/ui/textarea';
import {
  Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter, DialogDescription,
} from '@/components/ui/dialog';
import { Form, FormControl, FormField, FormItem, FormLabel, FormMessage } from '@/components/ui/form';
import { usePaginationState } from '@/hooks/usePaginationState';
import { getErrorTitle, getErrorDescription } from '@/utils/apiError';
import { csrfFetch } from '@/utils/csrfFetch';

const BASE = import.meta.env.BASE_URL?.replace(/\/$/, '') ?? '';

// ─── Backend detail/history shapes (AssetDtos.cs) ──────────────────────────
// Distinct from the summary Asset type in domain.ts (used for the list),
// since the detail/history endpoints return more fields than the list view.

interface AssetHistoryEntry {
  id: number;
  action: string;
  employeeName?: string | null;
  notes?: string | null;
  timestamp: string;
}

interface AssetDetail {
  id: number;
  assetCode: string;
  name: string;
  description?: string | null;
  categoryName?: string | null;
  serialNumber?: string | null;
  purchaseDate?: string | null;
  purchasePrice?: number | null;
  currentValue?: number | null;
  status: string;
  location?: string | null;
  assignedToEmployeeId?: string | null;
  assignedToName?: string | null;
  assignedAt?: string | null;
}

const api = {
  create: (body: { name: string; assetCode: string; description?: string; serialNumber?: string; location?: string }) =>
    csrfFetch(`${BASE}/api/assets`, {
      method: 'POST', credentials: 'include',
      headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body),
    }),
  assign: (id: string | number, employeeId: string, notes?: string) =>
    csrfFetch(`${BASE}/api/assets/${id}/assign`, {
      method: 'POST', credentials: 'include',
      headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ employeeId, notes }),
    }),
  return: (id: string | number, condition?: string, notes?: string) =>
    csrfFetch(`${BASE}/api/assets/${id}/return`, {
      method: 'POST', credentials: 'include',
      headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ condition, notes }),
    }),
  get: async (id: string | number): Promise<AssetDetail> => {
    const r = await csrfFetch(`${BASE}/api/assets/${id}`, { credentials: 'include' });
    if (!r.ok) throw new Error('Failed to load asset details.');
    return r.json();
  },
  history: async (id: string | number): Promise<AssetHistoryEntry[]> => {
    const r = await csrfFetch(`${BASE}/api/assets/${id}/history`, { credentials: 'include' });
    if (!r.ok) throw new Error('Failed to load asset history.');
    return r.json();
  },
};

// ─── Schemas ────────────────────────────────────────────────────────────────

const createAssetSchema = z.object({
  name: z.string().min(1, 'Asset name is required.').max(200),
  assetCode: z.string().min(1, 'Asset code is required.').max(50),
  description: z.string().max(1000).optional(),
  serialNumber: z.string().max(100).optional(),
  location: z.string().max(200).optional(),
});
type CreateAssetForm = z.infer<typeof createAssetSchema>;

const assignSchema = z.object({
  employeeId: z.string().min(1, 'Employee ID is required.'),
  notes: z.string().max(500).optional(),
});
type AssignForm = z.infer<typeof assignSchema>;

const returnSchema = z.object({
  condition: z.string().max(50).optional(),
  notes: z.string().max(500).optional(),
});
type ReturnForm = z.infer<typeof returnSchema>;

export default function AssetsPage() {
  const qc = useQueryClient();
  const [search, setSearch] = useState('');

  const { page, setPage, pageSize, resetPage } = usePaginationState();

  const { data: summary, isLoading: loadingSummary } = useGetAssetSummary();
  const { data: assets, isLoading: loadingAssets, isError, error, refetch } = useListAssets({
    page,
    pageSize,
    search: search || undefined,
  });

  // Dialog state
  const [createOpen, setCreateOpen] = useState(false);
  const [assignTarget, setAssignTarget] = useState<{ id: string; name: string } | null>(null);
  const [returnTarget, setReturnTarget] = useState<{ id: string; name: string } | null>(null);
  const [viewId, setViewId] = useState<string | null>(null);

  const invalidateAssets = () => {
    qc.invalidateQueries({ queryKey: ['/api/assets'] });
    qc.invalidateQueries({ queryKey: ['/api/assets/summary'] });
  };

  const createForm = useForm<CreateAssetForm>({
    resolver: zodResolver(createAssetSchema),
    defaultValues: { name: '', assetCode: '', description: '', serialNumber: '', location: '' },
  });
  const createMutation = useMutation({
    mutationFn: (values: CreateAssetForm) => api.create(values),
    onSuccess: async (res) => {
      if (!res.ok) {
        const body = await res.json().catch(() => ({}));
        throw new Error(body.message ?? 'Failed to create asset.');
      }
      toast.success('Asset created.');
      invalidateAssets();
      setCreateOpen(false);
      createForm.reset();
    },
    onError: (e: Error) => toast.error(e.message || 'Failed to create asset.'),
  });

  const assignForm = useForm<AssignForm>({
    resolver: zodResolver(assignSchema),
    defaultValues: { employeeId: '', notes: '' },
  });
  const assignMutation = useMutation({
    mutationFn: (values: AssignForm) => {
      if (!assignTarget) throw new Error('No asset selected.');
      return api.assign(assignTarget.id, values.employeeId, values.notes);
    },
    onSuccess: async (res) => {
      if (!res.ok) {
        const body = await res.json().catch(() => ({}));
        throw new Error(body.message ?? 'Failed to assign asset.');
      }
      toast.success('Asset assigned.');
      invalidateAssets();
      setAssignTarget(null);
      assignForm.reset();
    },
    onError: (e: Error) => toast.error(e.message || 'Failed to assign asset.'),
  });

  const returnForm = useForm<ReturnForm>({
    resolver: zodResolver(returnSchema),
    defaultValues: { condition: 'Good', notes: '' },
  });
  const returnMutation = useMutation({
    mutationFn: (values: ReturnForm) => {
      if (!returnTarget) throw new Error('No asset selected.');
      return api.return(returnTarget.id, values.condition, values.notes);
    },
    onSuccess: async (res) => {
      if (!res.ok) {
        const body = await res.json().catch(() => ({}));
        throw new Error(body.message ?? 'Failed to return asset.');
      }
      toast.success('Asset returned.');
      invalidateAssets();
      setReturnTarget(null);
      returnForm.reset();
    },
    onError: (e: Error) => toast.error(e.message || 'Failed to return asset.'),
  });

  const { data: viewDetail, isLoading: loadingDetail } = useQuery({
    queryKey: ['/api/assets', viewId, 'detail'],
    queryFn: () => api.get(viewId!),
    enabled: viewId !== null,
  });
  const { data: viewHistory, isLoading: loadingHistory } = useQuery({
    queryKey: ['/api/assets', viewId, 'history'],
    queryFn: () => api.history(viewId!),
    enabled: viewId !== null,
  });

  return (
    <div className="space-y-6">
      <PageHeader
        title="Asset Management"
        description="Track company hardware, software, and physical assets."
        actions={
          <Button onClick={() => setCreateOpen(true)}>
            <Plus className="mr-2 h-4 w-4" /> Add Asset
          </Button>
        }
      />

      <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
        {loadingSummary ? (
          Array.from({ length: 4 }).map((_, i) => (
            <Card key={i}>
              <CardContent className="p-6">
                <Skeleton className="h-12 w-full" />
              </CardContent>
            </Card>
          ))
        ) : summary ? (
          <>
            <Card>
              <CardContent className="p-6 flex items-center gap-4">
                <div className="p-3 bg-blue-100 dark:bg-blue-900/30 text-blue-700 dark:text-blue-400 rounded-lg">
                  <PackageSearch className="h-6 w-6" />
                </div>
                <div>
                  <div className="text-2xl font-bold">{summary.total}</div>
                  <div className="text-xs text-muted-foreground uppercase font-medium">Total Assets</div>
                </div>
              </CardContent>
            </Card>
            <Card>
              <CardContent className="p-6 flex items-center gap-4">
                <div className="p-3 bg-green-100 dark:bg-green-900/30 text-green-700 dark:text-green-400 rounded-lg">
                  <CheckCircle className="h-6 w-6" />
                </div>
                <div>
                  <div className="text-2xl font-bold">{summary.assigned}</div>
                  <div className="text-xs text-muted-foreground uppercase font-medium">Assigned</div>
                </div>
              </CardContent>
            </Card>
            <Card>
              <CardContent className="p-6 flex items-center gap-4">
                <div className="p-3 bg-purple-100 dark:bg-purple-900/30 text-purple-700 dark:text-purple-400 rounded-lg">
                  <Laptop className="h-6 w-6" />
                </div>
                <div>
                  <div className="text-2xl font-bold">{summary.available}</div>
                  <div className="text-xs text-muted-foreground uppercase font-medium">Available</div>
                </div>
              </CardContent>
            </Card>
            <Card>
              <CardContent className="p-6 flex items-center gap-4">
                <div className="p-3 bg-orange-100 dark:bg-orange-900/30 text-orange-700 dark:text-orange-400 rounded-lg">
                  <AlertTriangle className="h-6 w-6" />
                </div>
                <div>
                  <div className="text-2xl font-bold">{summary.underMaintenance}</div>
                  <div className="text-xs text-muted-foreground uppercase font-medium">Maintenance</div>
                </div>
              </CardContent>
            </Card>
          </>
        ) : null}
      </div>

      <div className="flex items-center bg-card p-2 border rounded-lg shadow-sm w-full max-w-sm">
        <Search className="ml-2 h-4 w-4 text-muted-foreground" />
        <Input
          className="border-0 focus-visible:ring-0 shadow-none h-8"
          placeholder="Search by code, name or user..."
          value={search}
          onChange={(e) => {
            setSearch(e.target.value);
            resetPage();
          }}
        />
      </div>

      <div className="bg-card border rounded-lg shadow-sm overflow-hidden">
        {loadingAssets ? (
          <SkeletonTable columns={6} rows={10} />
        ) : isError ? (

          <EmptyState
            title={getErrorTitle(error, 'Failed to load assets')}
            description={getErrorDescription(error)}
            onRetry={refetch}
          />
        ) : !assets?.items?.length ? (

          <EmptyState
            title={search ? 'No assets found' : 'No assets yet'}
            description={
              search
                ? `No assets match "${search}". Try a different search term.`
                : 'Get started by adding your first asset.'
            }
            action={
              !search ? (
                <Button onClick={() => setCreateOpen(true)}>
                  <Plus className="mr-2 h-4 w-4" /> Add Asset
                </Button>
              ) : undefined
            }
          />
        ) : (
          <>
            <Table>
              <TableHeader>
                <TableRow className="bg-muted/50">
                  <TableHead>Asset Code</TableHead>
                  <TableHead>Name</TableHead>
                  <TableHead>Category</TableHead>
                  <TableHead>Assigned To</TableHead>
                  <TableHead>Status</TableHead>
                  <TableHead className="text-right">Actions</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {assets.items.map((asset) => (
                  <TableRow key={asset.id}>
                    <TableCell className="font-mono text-sm">{asset.assetCode}</TableCell>
                    <TableCell className="font-medium">{asset.name}</TableCell>
                    <TableCell>{asset.categoryName || '-'}</TableCell>
                    <TableCell>
                      {asset.assignedToName || (
                        <span className="text-muted-foreground italic">Unassigned</span>
                      )}
                    </TableCell>
                    <TableCell>
                      <StatusBadge status={asset.status} />
                    </TableCell>
                    <TableCell className="text-right space-x-2">
                      {/* Previous fix: optional chaining on status.toLowerCase() */}
                      {(asset.status?.toLowerCase() ?? '') === 'available' ? (
                        <Button
                          size="sm"
                          variant="outline"
                          className="text-primary"
                          onClick={() => { setAssignTarget({ id: asset.id, name: asset.name }); assignForm.reset({ employeeId: '', notes: '' }); }}
                        >
                          Assign
                        </Button>
                      ) : (asset.status?.toLowerCase() ?? '') === 'assigned' ? (
                        <Button
                          size="sm"
                          variant="outline"
                          onClick={() => { setReturnTarget({ id: asset.id, name: asset.name }); returnForm.reset({ condition: 'Good', notes: '' }); }}
                        >
                          Return
                        </Button>
                      ) : null}
                      <Button size="sm" variant="ghost" onClick={() => setViewId(asset.id)}>
                        View
                      </Button>
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
            <Pagination
              page={assets.page}
              pageSize={assets.pageSize}
              totalCount={assets.totalCount}
              totalPages={assets.totalPages}
              onPageChange={setPage}
            />
          </>
        )}
      </div>

      {/* Add Asset dialog */}
      <Dialog open={createOpen} onOpenChange={setCreateOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Add Asset</DialogTitle>
            <DialogDescription>Register a new piece of company hardware, software, or equipment.</DialogDescription>
          </DialogHeader>
          <Form {...createForm}>
            <form onSubmit={createForm.handleSubmit((v) => createMutation.mutate(v))} className="space-y-4">
              <FormField control={createForm.control} name="name" render={({ field }) => (
                <FormItem><FormLabel>Asset Name</FormLabel><FormControl><Input {...field} placeholder="MacBook Pro 16&quot;" /></FormControl><FormMessage /></FormItem>
              )} />
              <FormField control={createForm.control} name="assetCode" render={({ field }) => (
                <FormItem><FormLabel>Asset Code</FormLabel><FormControl><Input {...field} placeholder="AST-0001" /></FormControl><FormMessage /></FormItem>
              )} />
              <FormField control={createForm.control} name="serialNumber" render={({ field }) => (
                <FormItem><FormLabel>Serial Number (optional)</FormLabel><FormControl><Input {...field} /></FormControl><FormMessage /></FormItem>
              )} />
              <FormField control={createForm.control} name="location" render={({ field }) => (
                <FormItem><FormLabel>Location (optional)</FormLabel><FormControl><Input {...field} placeholder="Mumbai HQ" /></FormControl><FormMessage /></FormItem>
              )} />
              <FormField control={createForm.control} name="description" render={({ field }) => (
                <FormItem><FormLabel>Description (optional)</FormLabel><FormControl><Textarea {...field} rows={3} /></FormControl><FormMessage /></FormItem>
              )} />
              <DialogFooter>
                <Button variant="outline" type="button" onClick={() => setCreateOpen(false)}>Cancel</Button>
                <Button type="submit" disabled={createMutation.isPending}>{createMutation.isPending ? 'Creating…' : 'Create Asset'}</Button>
              </DialogFooter>
            </form>
          </Form>
        </DialogContent>
      </Dialog>

      {/* Assign dialog */}
      <Dialog open={assignTarget !== null} onOpenChange={(open) => { if (!open) setAssignTarget(null); }}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Assign Asset</DialogTitle>
            <DialogDescription>Assign &quot;{assignTarget?.name}&quot; to an employee.</DialogDescription>
          </DialogHeader>
          <Form {...assignForm}>
            <form onSubmit={assignForm.handleSubmit((v) => assignMutation.mutate(v))} className="space-y-4">
              <FormField control={assignForm.control} name="employeeId" render={({ field }) => (
                <FormItem><FormLabel>Employee ID</FormLabel><FormControl><Input {...field} placeholder="EMP-0001" /></FormControl><FormMessage /></FormItem>
              )} />
              <FormField control={assignForm.control} name="notes" render={({ field }) => (
                <FormItem><FormLabel>Notes (optional)</FormLabel><FormControl><Textarea {...field} rows={2} /></FormControl><FormMessage /></FormItem>
              )} />
              <DialogFooter>
                <Button variant="outline" type="button" onClick={() => setAssignTarget(null)}>Cancel</Button>
                <Button type="submit" disabled={assignMutation.isPending}>{assignMutation.isPending ? 'Assigning…' : 'Assign'}</Button>
              </DialogFooter>
            </form>
          </Form>
        </DialogContent>
      </Dialog>

      {/* Return dialog */}
      <Dialog open={returnTarget !== null} onOpenChange={(open) => { if (!open) setReturnTarget(null); }}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Return Asset</DialogTitle>
            <DialogDescription>Return &quot;{returnTarget?.name}&quot; to inventory.</DialogDescription>
          </DialogHeader>
          <Form {...returnForm}>
            <form onSubmit={returnForm.handleSubmit((v) => returnMutation.mutate(v))} className="space-y-4">
              <FormField control={returnForm.control} name="condition" render={({ field }) => (
                <FormItem><FormLabel>Condition</FormLabel><FormControl><Input {...field} placeholder="Good / Damaged / Lost" /></FormControl><FormMessage /></FormItem>
              )} />
              <FormField control={returnForm.control} name="notes" render={({ field }) => (
                <FormItem><FormLabel>Notes (optional)</FormLabel><FormControl><Textarea {...field} rows={2} /></FormControl><FormMessage /></FormItem>
              )} />
              <DialogFooter>
                <Button variant="outline" type="button" onClick={() => setReturnTarget(null)}>Cancel</Button>
                <Button type="submit" disabled={returnMutation.isPending}>{returnMutation.isPending ? 'Returning…' : 'Return'}</Button>
              </DialogFooter>
            </form>
          </Form>
        </DialogContent>
      </Dialog>

      {/* View detail + history dialog */}
      <Dialog open={viewId !== null} onOpenChange={(open) => { if (!open) setViewId(null); }}>
        <DialogContent className="max-w-lg">
          <DialogHeader>
            <DialogTitle>Asset Details</DialogTitle>
          </DialogHeader>
          {loadingDetail ? (
            <Skeleton className="h-32 w-full" />
          ) : viewDetail ? (
            <div className="space-y-4 text-sm">
              <div className="grid grid-cols-2 gap-2">
                <div><span className="text-muted-foreground">Code:</span> {viewDetail.assetCode}</div>
                <div><span className="text-muted-foreground">Status:</span> <StatusBadge status={viewDetail.status} /></div>
                <div><span className="text-muted-foreground">Category:</span> {viewDetail.categoryName ?? '—'}</div>
                <div><span className="text-muted-foreground">Serial #:</span> {viewDetail.serialNumber ?? '—'}</div>
                <div><span className="text-muted-foreground">Location:</span> {viewDetail.location ?? '—'}</div>
                <div><span className="text-muted-foreground">Assigned To:</span> {viewDetail.assignedToName ?? 'Unassigned'}</div>
              </div>
              {viewDetail.description && (
                <p className="text-muted-foreground">{viewDetail.description}</p>
              )}
              <div>
                <h4 className="font-medium mb-2">History</h4>
                {loadingHistory ? (
                  <Skeleton className="h-16 w-full" />
                ) : !viewHistory?.length ? (
                  <p className="text-muted-foreground text-xs">No history recorded yet.</p>
                ) : (
                  <ul className="space-y-2 max-h-48 overflow-auto">
                    {viewHistory.map((h) => (
                      <li key={h.id} className="border-l-2 border-primary/40 pl-2">
                        <span className="font-medium">{h.action}</span>
                        {h.employeeName && <span className="text-muted-foreground"> — {h.employeeName}</span>}
                        <div className="text-xs text-muted-foreground">{new Date(h.timestamp).toLocaleString()}</div>
                        {h.notes && <div className="text-xs">{h.notes}</div>}
                      </li>
                    ))}
                  </ul>
                )}
              </div>
            </div>
          ) : (
            <p className="text-muted-foreground text-sm">Unable to load asset details.</p>
          )}
          <DialogFooter>
            <Button variant="outline" onClick={() => setViewId(null)}>Close</Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
