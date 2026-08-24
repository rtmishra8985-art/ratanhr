/**
 * BonusDeductionPage — Manage employee bonuses and deductions.
 *
 * Fix M-05: Bonus & Deduction management UI backed by the existing controllers:
 *   GET/POST/PUT/DELETE  api/bonuses     → BonusController.cs
 *   GET/POST/PUT/DELETE  api/deductions  → DeductionController.cs
 *
 * Response shape: ApiResponse<PagedResult<BonusDto>> / ApiResponse<PagedResult<DeductionDto>>
 * Unwrapped from: { success, message, data: { items, totalCount, page, pageSize, totalPages } }
 *
 * BonusDto:     id, employeeId, bonusType, amount, month, year, remarks, isTaxable, createdAt
 * DeductionDto: id, employeeId, deductionType, amount, month, year, remarks, createdAt
 *
 * Auth: admin or superadmin only.
 */

import { useState } from 'react';
import { Plus, Trash2 } from 'lucide-react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';

import { PageHeader } from '@/components/layout/PageHeader';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogFooter,
} from '@/components/ui/dialog';
import { Badge } from '@/components/ui/badge';
import { SkeletonTable } from '@/components/shared/SkeletonTable';
import { EmptyState } from '@/components/shared/EmptyState';
import { Pagination } from '@/components/shared/Pagination';
import { usePaginationState } from '@/hooks/usePaginationState';
import { formatCurrency } from '@/utils/profileHelpers';
import { getErrorTitle, getErrorDescription } from '@/utils/apiError';
import { csrfFetch } from '@/utils/csrfFetch';

// ─── Constants ────────────────────────────────────────────────────────────────

const MONTH_NAMES = [
  '', 'Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun',
  'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec',
];

const CURRENT_YEAR = new Date().getFullYear();
const YEARS = Array.from({ length: 6 }, (_, i) => CURRENT_YEAR - 2 + i);
const MONTHS = Array.from({ length: 12 }, (_, i) => i + 1);

const BONUS_TYPES = ['Performance', 'Festival', 'Joining', 'Retention', 'Annual', 'Other'];
const DEDUCTION_TYPES = ['Advance Recovery', 'Loan EMI', 'Disciplinary', 'Loss of Pay', 'Other'];

// ─── API types ────────────────────────────────────────────────────────────────

interface ApiResponse<T> {
  success: boolean;
  message: string;
  data: T;
  errors: string[];
}

interface PagedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

interface BonusDto {
  id: number;
  employeeId: string;
  bonusType: string;
  amount: number;
  month: number;
  year: number;
  remarks: string | null;
  isTaxable: boolean;
  createdAt: string;
}

interface DeductionDto {
  id: number;
  employeeId: string;
  deductionType: string;
  amount: number;
  month: number;
  year: number;
  remarks: string | null;
  createdAt: string;
}

// ─── API helpers ──────────────────────────────────────────────────────────────

async function apiFetch<T>(url: string, init?: RequestInit): Promise<T> {
  const res = await csrfFetch(url, { credentials: 'include', ...init });
  const json: ApiResponse<T> = await res.json();
  if (!res.ok || !json.success) {
    throw new Error(json.message || `HTTP ${res.status}`);
  }
  return json.data;
}

// ─── Create Bonus Dialog ──────────────────────────────────────────────────────

interface CreateBonusForm {
  employeeId: string;
  bonusType: string;
  amount: string;
  month: number;
  year: number;
  remarks: string;
  isTaxable: boolean;
}

function AddBonusDialog({ open, onClose }: { open: boolean; onClose: () => void }) {
  const qc = useQueryClient();
  const [form, setForm] = useState<CreateBonusForm>({
    employeeId: '',
    bonusType: BONUS_TYPES[0],
    amount: '',
    month: new Date().getMonth() + 1,
    year: CURRENT_YEAR,
    remarks: '',
    isTaxable: true,
  });
  const [error, setError] = useState('');

  const mutation = useMutation({
    mutationFn: () =>
      apiFetch('/api/bonuses', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          employeeId: form.employeeId,
          bonusType: form.bonusType,
          amount: parseFloat(form.amount),
          month: form.month,
          year: form.year,
          remarks: form.remarks || null,
          isTaxable: form.isTaxable,
        }),
      }),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['bonuses'] });
      onClose();
    },
    onError: (e: Error) => setError(e.message),
  });

  const set = (k: keyof CreateBonusForm) => (e: React.ChangeEvent<HTMLInputElement | HTMLSelectElement | HTMLTextAreaElement>) =>
    setForm((f) => ({ ...f, [k]: e.target.value }));

  return (
    <Dialog open={open} onOpenChange={(o) => !o && onClose()}>
      <DialogContent className="sm:max-w-md">
        <DialogHeader>
          <DialogTitle>Add Bonus</DialogTitle>
        </DialogHeader>
        <div className="space-y-3 py-2">
          {error && <p className="text-sm text-destructive">{error}</p>}
          <div className="space-y-1">
            <label className="text-sm font-medium">Employee ID</label>
            <Input placeholder="e.g. EMP001" value={form.employeeId} onChange={set('employeeId')} />
          </div>
          <div className="space-y-1">
            <label className="text-sm font-medium">Bonus Type</label>
            <select className="w-full border rounded-md px-3 py-2 text-sm bg-background" value={form.bonusType} onChange={set('bonusType')}>
              {BONUS_TYPES.map((t) => <option key={t}>{t}</option>)}
            </select>
          </div>
          <div className="grid grid-cols-2 gap-3">
            <div className="space-y-1">
              <label className="text-sm font-medium">Month</label>
              <select className="w-full border rounded-md px-3 py-2 text-sm bg-background" value={form.month} onChange={(e) => setForm((f) => ({ ...f, month: Number(e.target.value) }))}>
                {MONTHS.map((m) => <option key={m} value={m}>{MONTH_NAMES[m]}</option>)}
              </select>
            </div>
            <div className="space-y-1">
              <label className="text-sm font-medium">Year</label>
              <select className="w-full border rounded-md px-3 py-2 text-sm bg-background" value={form.year} onChange={(e) => setForm((f) => ({ ...f, year: Number(e.target.value) }))}>
                {YEARS.map((y) => <option key={y}>{y}</option>)}
              </select>
            </div>
          </div>
          <div className="space-y-1">
            <label className="text-sm font-medium">Amount (₹)</label>
            <Input type="number" min="0" step="0.01" placeholder="0.00" value={form.amount} onChange={set('amount')} />
          </div>
          <div className="flex items-center gap-2">
            <input type="checkbox" id="taxable" checked={form.isTaxable} onChange={(e) => setForm((f) => ({ ...f, isTaxable: e.target.checked }))} className="h-4 w-4" />
            <label htmlFor="taxable" className="text-sm">Taxable bonus</label>
          </div>
          <div className="space-y-1">
            <label className="text-sm font-medium">Remarks <span className="text-muted-foreground">(optional)</span></label>
            <Input placeholder="Optional note…" value={form.remarks} onChange={set('remarks')} />
          </div>
        </div>
        <DialogFooter>
          <Button variant="outline" onClick={onClose}>Cancel</Button>
          <Button
            onClick={() => mutation.mutate()}
            disabled={mutation.isPending || !form.employeeId || !form.amount}
          >
            {mutation.isPending ? 'Saving…' : 'Add Bonus'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

// ─── Create Deduction Dialog ──────────────────────────────────────────────────

interface CreateDeductionForm {
  employeeId: string;
  deductionType: string;
  amount: string;
  month: number;
  year: number;
  remarks: string;
}

function AddDeductionDialog({ open, onClose }: { open: boolean; onClose: () => void }) {
  const qc = useQueryClient();
  const [form, setForm] = useState<CreateDeductionForm>({
    employeeId: '',
    deductionType: DEDUCTION_TYPES[0],
    amount: '',
    month: new Date().getMonth() + 1,
    year: CURRENT_YEAR,
    remarks: '',
  });
  const [error, setError] = useState('');

  const mutation = useMutation({
    mutationFn: () =>
      apiFetch('/api/deductions', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          employeeId: form.employeeId,
          deductionType: form.deductionType,
          amount: parseFloat(form.amount),
          month: form.month,
          year: form.year,
          remarks: form.remarks || null,
        }),
      }),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['deductions'] });
      onClose();
    },
    onError: (e: Error) => setError(e.message),
  });

  const set = (k: keyof CreateDeductionForm) => (e: React.ChangeEvent<HTMLInputElement | HTMLSelectElement>) =>
    setForm((f) => ({ ...f, [k]: e.target.value }));

  return (
    <Dialog open={open} onOpenChange={(o) => !o && onClose()}>
      <DialogContent className="sm:max-w-md">
        <DialogHeader>
          <DialogTitle>Add Deduction</DialogTitle>
        </DialogHeader>
        <div className="space-y-3 py-2">
          {error && <p className="text-sm text-destructive">{error}</p>}
          <div className="space-y-1">
            <label className="text-sm font-medium">Employee ID</label>
            <Input placeholder="e.g. EMP001" value={form.employeeId} onChange={set('employeeId')} />
          </div>
          <div className="space-y-1">
            <label className="text-sm font-medium">Deduction Type</label>
            <select className="w-full border rounded-md px-3 py-2 text-sm bg-background" value={form.deductionType} onChange={set('deductionType')}>
              {DEDUCTION_TYPES.map((t) => <option key={t}>{t}</option>)}
            </select>
          </div>
          <div className="grid grid-cols-2 gap-3">
            <div className="space-y-1">
              <label className="text-sm font-medium">Month</label>
              <select className="w-full border rounded-md px-3 py-2 text-sm bg-background" value={form.month} onChange={(e) => setForm((f) => ({ ...f, month: Number(e.target.value) }))}>
                {MONTHS.map((m) => <option key={m} value={m}>{MONTH_NAMES[m]}</option>)}
              </select>
            </div>
            <div className="space-y-1">
              <label className="text-sm font-medium">Year</label>
              <select className="w-full border rounded-md px-3 py-2 text-sm bg-background" value={form.year} onChange={(e) => setForm((f) => ({ ...f, year: Number(e.target.value) }))}>
                {YEARS.map((y) => <option key={y}>{y}</option>)}
              </select>
            </div>
          </div>
          <div className="space-y-1">
            <label className="text-sm font-medium">Amount (₹)</label>
            <Input type="number" min="0" step="0.01" placeholder="0.00" value={form.amount} onChange={set('amount')} />
          </div>
          <div className="space-y-1">
            <label className="text-sm font-medium">Remarks <span className="text-muted-foreground">(optional)</span></label>
            <Input placeholder="Optional note…" value={form.remarks} onChange={set('remarks')} />
          </div>
        </div>
        <DialogFooter>
          <Button variant="outline" onClick={onClose}>Cancel</Button>
          <Button
            onClick={() => mutation.mutate()}
            disabled={mutation.isPending || !form.employeeId || !form.amount}
          >
            {mutation.isPending ? 'Saving…' : 'Add Deduction'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

// ─── Bonuses Panel ────────────────────────────────────────────────────────────

function BonusesPanel() {
  const { page, setPage, pageSize } = usePaginationState();
  const [showAdd, setShowAdd] = useState(false);
  const qc = useQueryClient();

  const { data, isLoading, isError, error, refetch } = useQuery<PagedResult<BonusDto>>({
    queryKey: ['bonuses', page, pageSize],
    queryFn: () =>
      apiFetch<PagedResult<BonusDto>>(
        `/api/bonuses?page=${page}&pageSize=${pageSize}`
      ),
  });

  const deleteMut = useMutation({
    mutationFn: (id: number) =>
      apiFetch(`/api/bonuses/${id}`, { method: 'DELETE' }),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['bonuses'] }),
  });

  return (
    <div className="space-y-4">
      <div className="flex justify-end">
        <Button size="sm" onClick={() => setShowAdd(true)}>
          <Plus className="mr-2 h-4 w-4" /> Add Bonus
        </Button>
      </div>
      <AddBonusDialog open={showAdd} onClose={() => setShowAdd(false)} />

      <div className="bg-card border rounded-lg shadow-sm overflow-hidden">
        {isLoading ? (
          <SkeletonTable columns={7} rows={8} />
        ) : isError ? (
          <EmptyState
            title={getErrorTitle(error, 'Failed to load bonuses')}
            description={getErrorDescription(error)}
            onRetry={refetch}
          />
        ) : !data?.items.length ? (
          <EmptyState title="No bonuses recorded" description="Add a bonus entry for an employee to get started." />
        ) : (
          <>
            <div className="overflow-x-auto">
              <Table>
                <TableHeader>
                  <TableRow className="bg-muted/50">
                    <TableHead>Employee ID</TableHead>
                    <TableHead>Bonus Type</TableHead>
                    <TableHead>Period</TableHead>
                    <TableHead className="text-right">Amount</TableHead>
                    <TableHead>Taxable</TableHead>
                    <TableHead>Remarks</TableHead>
                    <TableHead className="text-right">Actions</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {data.items.map((item) => (
                    <TableRow key={item.id}>
                      <TableCell className="font-mono text-sm">{item.employeeId}</TableCell>
                      <TableCell>
                        <Badge variant="secondary">{item.bonusType}</Badge>
                      </TableCell>
                      <TableCell className="text-sm">
                        {MONTH_NAMES[item.month]} {item.year}
                      </TableCell>
                      <TableCell className="text-right font-medium text-green-600 dark:text-green-500">
                        +{formatCurrency(item.amount)}
                      </TableCell>
                      <TableCell>
                        <Badge variant={item.isTaxable ? 'outline' : 'secondary'}>
                          {item.isTaxable ? 'Taxable' : 'Non-taxable'}
                        </Badge>
                      </TableCell>
                      <TableCell className="text-muted-foreground text-sm max-w-[160px] truncate">
                        {item.remarks ?? '—'}
                      </TableCell>
                      <TableCell className="text-right">
                        <Button
                          size="sm"
                          variant="ghost"
                          className="text-destructive hover:text-destructive hover:bg-destructive/10"
                          onClick={() => deleteMut.mutate(item.id)}
                          disabled={deleteMut.isPending}
                        >
                          <Trash2 className="h-4 w-4" />
                        </Button>
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </div>
            <Pagination
              page={data.page}
              pageSize={data.pageSize}
              totalCount={data.totalCount}
              totalPages={data.totalPages}
              onPageChange={setPage}
            />
          </>
        )}
      </div>
    </div>
  );
}

// ─── Deductions Panel ─────────────────────────────────────────────────────────

function DeductionsPanel() {
  const { page, setPage, pageSize } = usePaginationState();
  const [showAdd, setShowAdd] = useState(false);
  const qc = useQueryClient();

  const { data, isLoading, isError, error, refetch } = useQuery<PagedResult<DeductionDto>>({
    queryKey: ['deductions', page, pageSize],
    queryFn: () =>
      apiFetch<PagedResult<DeductionDto>>(
        `/api/deductions?page=${page}&pageSize=${pageSize}`
      ),
  });

  const deleteMut = useMutation({
    mutationFn: (id: number) =>
      apiFetch(`/api/deductions/${id}`, { method: 'DELETE' }),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['deductions'] }),
  });

  return (
    <div className="space-y-4">
      <div className="flex justify-end">
        <Button size="sm" onClick={() => setShowAdd(true)}>
          <Plus className="mr-2 h-4 w-4" /> Add Deduction
        </Button>
      </div>
      <AddDeductionDialog open={showAdd} onClose={() => setShowAdd(false)} />

      <div className="bg-card border rounded-lg shadow-sm overflow-hidden">
        {isLoading ? (
          <SkeletonTable columns={6} rows={8} />
        ) : isError ? (
          <EmptyState
            title={getErrorTitle(error, 'Failed to load deductions')}
            description={getErrorDescription(error)}
            onRetry={refetch}
          />
        ) : !data?.items.length ? (
          <EmptyState title="No deductions recorded" description="Add a deduction entry for an employee to get started." />
        ) : (
          <>
            <div className="overflow-x-auto">
              <Table>
                <TableHeader>
                  <TableRow className="bg-muted/50">
                    <TableHead>Employee ID</TableHead>
                    <TableHead>Deduction Type</TableHead>
                    <TableHead>Period</TableHead>
                    <TableHead className="text-right">Amount</TableHead>
                    <TableHead>Remarks</TableHead>
                    <TableHead className="text-right">Actions</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {data.items.map((item) => (
                    <TableRow key={item.id}>
                      <TableCell className="font-mono text-sm">{item.employeeId}</TableCell>
                      <TableCell>
                        <Badge variant="outline">{item.deductionType}</Badge>
                      </TableCell>
                      <TableCell className="text-sm">
                        {MONTH_NAMES[item.month]} {item.year}
                      </TableCell>
                      <TableCell className="text-right font-medium text-destructive">
                        -{formatCurrency(item.amount)}
                      </TableCell>
                      <TableCell className="text-muted-foreground text-sm max-w-[160px] truncate">
                        {item.remarks ?? '—'}
                      </TableCell>
                      <TableCell className="text-right">
                        <Button
                          size="sm"
                          variant="ghost"
                          className="text-destructive hover:text-destructive hover:bg-destructive/10"
                          onClick={() => deleteMut.mutate(item.id)}
                          disabled={deleteMut.isPending}
                        >
                          <Trash2 className="h-4 w-4" />
                        </Button>
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </div>
            <Pagination
              page={data.page}
              pageSize={data.pageSize}
              totalCount={data.totalCount}
              totalPages={data.totalPages}
              onPageChange={setPage}
            />
          </>
        )}
      </div>
    </div>
  );
}

// ─── Shared content (used as tab content inside PayrollPage) ──────────────────

export function BonusDeductionContent() {
  return (
    <Tabs defaultValue="bonuses" className="w-full">
      <TabsList className="grid w-full sm:w-[320px] grid-cols-2">
        <TabsTrigger value="bonuses">Bonuses</TabsTrigger>
        <TabsTrigger value="deductions">Deductions</TabsTrigger>
      </TabsList>
      <TabsContent value="bonuses" className="mt-6">
        <BonusesPanel />
      </TabsContent>
      <TabsContent value="deductions" className="mt-6">
        <DeductionsPanel />
      </TabsContent>
    </Tabs>
  );
}

// ─── Standalone page at /payroll/bonuses-deductions ───────────────────────────

export default function BonusDeductionPage() {
  return (
    <div className="space-y-6">
      <PageHeader
        title="Bonuses & Deductions"
        description="Manage one-off bonuses and custom deductions applied at payroll processing time."
      />
      <BonusDeductionContent />
    </div>
  );
}
