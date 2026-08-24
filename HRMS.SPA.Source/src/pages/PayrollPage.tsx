import { useState } from 'react';
import { Plus, Play, Download, Loader2 } from 'lucide-react';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import * as z from 'zod';
import { toast } from 'sonner';
import { useListPayslips, useListSalaryStructures, useGetPayrollSummary } from '@workspace/api-client-react';
import { BonusDeductionContent } from './payroll/BonusDeductionPage';
import { csrfFetch } from '@/utils/csrfFetch';
import { useToast } from '@/hooks/use-toast';

import { PageHeader } from '@/components/layout/PageHeader';
import { Button } from '@/components/ui/button';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { SkeletonTable } from '@/components/shared/SkeletonTable';
import { Pagination } from '@/components/shared/Pagination';
import { StatusBadge } from '@/components/shared/StatusBadge';
import { EmptyState } from '@/components/shared/EmptyState';
import { Skeleton } from '@/components/ui/skeleton';
import { Input } from '@/components/ui/input';
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter, DialogDescription } from '@/components/ui/dialog';
import { Form, FormControl, FormField, FormItem, FormLabel, FormMessage } from '@/components/ui/form';
import {
  BarChart,
  Bar,
  XAxis,
  YAxis,
  CartesianGrid,
  Tooltip,
  ResponsiveContainer,
} from 'recharts';
import { usePaginationState } from '@/hooks/usePaginationState';
import { formatCurrency } from '@/utils/profileHelpers';
import { getErrorTitle, getErrorDescription } from '@/utils/apiError';

const BASE = import.meta.env.BASE_URL?.replace(/\/$/, '') ?? '';

// ─── Process Payroll (bulk-generate) ───────────────────────────────

const processPayrollSchema = z.object({
  month: z.coerce.number().int().min(1).max(12),
  year: z.coerce.number().int().min(2000).max(2100),
});
type ProcessPayrollForm = z.infer<typeof processPayrollSchema>;

const api = {
  bulkGenerate: (body: ProcessPayrollForm) =>
    csrfFetch(`${BASE}/api/payroll/bulk-generate`, {
      method: 'POST', credentials: 'include',
      headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body),
    }),
  upsertSalaryStructure: (employeeId: string, body: Record<string, unknown>) =>
    csrfFetch(`${BASE}/api/salary/${encodeURIComponent(employeeId)}`, {
      method: 'POST', credentials: 'include',
      headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body),
    }),
};

// ─── Add Structure ──────────────────────────────────────────────

const structureSchema = z.object({
  employeeId: z.string().min(1, 'Employee ID is required.'),
  ctc: z.coerce.number().min(0, 'CTC must be non-negative.'),
  basicPay: z.coerce.number().min(0),
  hra: z.coerce.number().min(0).optional().default(0),
  effectiveFrom: z.string().min(1, 'Effective date is required.'),
});
type StructureForm = z.infer<typeof structureSchema>;

/**
 * FIX: the payslip PDF "Download" button rendered on every row but had no
 * onClick handler at all — clicking it did nothing. The backend has always
 * fully supported this via a 3-step async flow (PayslipController):
 *   1. POST /api/payslip/{id}/pdf        -> 202 Accepted, { token, statusUrl }
 *   2. GET  /api/payslip/{id}/pdf/status/{token} -> poll until status === "ready"
 *   3. GET  /api/payslip/{id}/pdf/download/{token} -> binary PDF (single-use token)
 * This hook drives that flow and triggers a browser download of the blob.
 */
function usePayslipPdfDownload() {
  const { toast } = useToast();
  const [downloadingId, setDownloadingId] = useState<string | null>(null);

  const download = async (payslipId: string, employeeId: string | null | undefined, month: string, year: number) => {
    setDownloadingId(payslipId);
    try {
      const queueRes = await csrfFetch(`${BASE}/api/payslip/${payslipId}/pdf`, { method: 'POST' });
      const queueJson = await queueRes.json().catch(() => null);
      if (!queueRes.ok) throw new Error(queueJson?.message ?? 'Failed to queue PDF generation.');
      const token: string = queueJson.token;

      // Poll status every 800ms, up to ~20s (matches the token's short TTL headroom).
      let ready = false;
      for (let attempt = 0; attempt < 25; attempt++) {
        const statusRes = await csrfFetch(`${BASE}/api/payslip/${payslipId}/pdf/status/${token}`);
        if (statusRes.ok) {
          const statusJson = await statusRes.json();
          if (statusJson.status === 'ready') { ready = true; break; }
        }
        await new Promise((resolve) => setTimeout(resolve, 800));
      }
      if (!ready) throw new Error('PDF generation timed out. Please try again.');

      const downloadRes = await csrfFetch(`${BASE}/api/payslip/${payslipId}/pdf/download/${token}`);
      if (!downloadRes.ok) throw new Error('Failed to download the generated PDF.');
      const blob = await downloadRes.blob();
      const url = URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = `payslip-${employeeId ?? payslipId}-${year}-${month}.pdf`;
      document.body.appendChild(a);
      a.click();
      a.remove();
      URL.revokeObjectURL(url);
    } catch (error: unknown) {
      toast({
        title: 'Download failed',
        description: error instanceof Error ? error.message : 'Could not download payslip PDF.',
        variant: 'destructive',
      });
    } finally {
      setDownloadingId(null);
    }
  };

  return { download, downloadingId };
}

export default function PayrollPage() {
  const qc = useQueryClient();
  const { page, setPage, pageSize } = usePaginationState();
  const currentYear = new Date().getFullYear();
  const { download: downloadPayslipPdf, downloadingId } = usePayslipPdfDownload();

  const [processOpen, setProcessOpen] = useState(false);
  const [structureOpen, setStructureOpen] = useState(false);

  const { data: payslips, isLoading: loadingPayslips, isError: errorPayslips, error: payslipError, refetch: refetchPayslips } = useListPayslips({ page, pageSize });
  const { data: structures, isLoading: loadingStructures } = useListSalaryStructures();
  const { data: summary, isLoading: loadingSummary } = useGetPayrollSummary({ year: currentYear });

  const processForm = useForm<ProcessPayrollForm>({
    resolver: zodResolver(processPayrollSchema),
    defaultValues: { month: new Date().getMonth() + 1, year: currentYear },
  });
  const processMutation = useMutation({
    mutationFn: (values: ProcessPayrollForm) => api.bulkGenerate(values),
    onSuccess: async (res) => {
      const body = await res.json().catch(() => ({}));
      if (!res.ok) throw new Error(body.message ?? 'Failed to process payroll.');
      const result = body.data ?? body;
      toast.success(`Payroll processed: ${result.generated ?? 0} generated, ${result.skipped ?? 0} skipped, ${result.failed ?? 0} failed.`);
      qc.invalidateQueries({ queryKey: ['/api/payslip'] });
      qc.invalidateQueries({ queryKey: ['/api/analytics/payroll'] });
      setProcessOpen(false);
    },
    onError: (e: Error) => toast.error(e.message || 'Failed to process payroll.'),
  });

  const structureForm = useForm<StructureForm>({
    resolver: zodResolver(structureSchema),
    defaultValues: { employeeId: '', ctc: 0, basicPay: 0, hra: 0, effectiveFrom: '' },
  });
  const structureMutation = useMutation({
    mutationFn: (values: StructureForm) => api.upsertSalaryStructure(values.employeeId, {
      ctc: values.ctc,
      basicPay: values.basicPay,
      hra: values.hra,
      effectiveFrom: values.effectiveFrom,
    }),
    onSuccess: async (res) => {
      if (!res.ok) {
        const body = await res.json().catch(() => ({}));
        throw new Error(body.message ?? 'Failed to save salary structure.');
      }
      toast.success('Salary structure saved.');
      qc.invalidateQueries({ queryKey: ['/api/salary'] });
      setStructureOpen(false);
      structureForm.reset({ employeeId: '', ctc: 0, basicPay: 0, hra: 0, effectiveFrom: '' });
    },
    onError: (e: Error) => toast.error(e.message || 'Failed to save salary structure.'),
  });

  return (
    <div className="space-y-6">
      <PageHeader
        title="Payroll"
        description="Manage employee compensation, generate payslips, and view structures."
        actions={
          <Button onClick={() => setProcessOpen(true)}>
            <Play className="mr-2 h-4 w-4" />
            Process Payroll
          </Button>
        }
      />

      <Tabs defaultValue="payslips" className="w-full">
        <TabsList className="grid w-full sm:w-[560px] grid-cols-3">
          <TabsTrigger value="payslips">Payslips</TabsTrigger>
          <TabsTrigger value="structures">Salary Structures</TabsTrigger>
          <TabsTrigger value="bonuses-deductions">Bonuses & Deductions</TabsTrigger>
        </TabsList>

        <TabsContent value="payslips" className="space-y-6 mt-6">
          <Card>
            <CardHeader className="pb-2">
              <CardTitle>Annual Overview ({currentYear})</CardTitle>
            </CardHeader>
            <CardContent>
              <div className="h-[250px] w-full mt-4">
                {loadingSummary ? (
                  <Skeleton className="w-full h-full" />
                ) : summary?.months && summary.months.length > 0 ? (
                  <ResponsiveContainer width="100%" height="100%">
                    <BarChart data={summary.months} margin={{ top: 0, right: 0, bottom: 0, left: 0 }}>
                      <CartesianGrid strokeDasharray="3 3" vertical={false} stroke="hsl(var(--border))" />
                      <XAxis dataKey="month" stroke="hsl(var(--muted-foreground))" fontSize={12} tickLine={false} axisLine={false} />
                      <YAxis stroke="hsl(var(--muted-foreground))" fontSize={12} tickLine={false} axisLine={false} tickFormatter={(val) => `$${val / 1000}k`} />
                      <Tooltip cursor={{ fill: 'hsl(var(--muted)/0.5)' }} contentStyle={{ backgroundColor: 'hsl(var(--card))', borderColor: 'hsl(var(--border))', borderRadius: '8px' }} />
                      <Bar dataKey="totalGross" name="Gross" fill="hsl(var(--chart-1))" radius={[4, 4, 0, 0]} />
                      <Bar dataKey="totalNet" name="Net" fill="hsl(var(--chart-3))" radius={[4, 4, 0, 0]} />
                    </BarChart>
                  </ResponsiveContainer>
                ) : (
                  <div className="flex h-full items-center justify-center text-muted-foreground border border-dashed rounded-md">
                    No data available
                  </div>
                )}
              </div>
            </CardContent>
          </Card>

          <div className="bg-card border rounded-lg shadow-sm overflow-hidden">
            {loadingPayslips ? (
              <SkeletonTable columns={7} rows={10} />
            ) : errorPayslips ? (

              <EmptyState
                title={getErrorTitle(payslipError, 'Failed to load payslips')}
                description={getErrorDescription(payslipError)}
                onRetry={refetchPayslips}
              />
            ) : !payslips?.items.length ? (
              <EmptyState
                title="No payslips generated"
                description="Process payroll to generate payslips."
              />
            ) : (
              <>
                <div className="overflow-x-auto">
                  <Table>
                    <TableHeader>
                      <TableRow className="bg-muted/50">
                        <TableHead>Employee</TableHead>
                        <TableHead>Period</TableHead>
                        <TableHead className="text-right">Gross</TableHead>
                        <TableHead className="text-right">Deductions</TableHead>
                        <TableHead className="text-right font-bold">Net Salary</TableHead>
                        <TableHead>Status</TableHead>
                        <TableHead className="text-right">Actions</TableHead>
                      </TableRow>
                    </TableHeader>
                    <TableBody>
                      {payslips.items.map((slip) => (
                        <TableRow key={slip.id}>
                          <TableCell className="font-medium">
                            {slip.employeeName}
                            <div className="text-xs text-muted-foreground font-normal">{slip.employeeId}</div>
                          </TableCell>
                          <TableCell>{slip.month} {slip.year}</TableCell>
                          {/* Fix (previous): formatCurrency handles null/undefined safely */}
                          <TableCell className="text-right">{formatCurrency(slip.grossSalary)}</TableCell>
                          <TableCell className="text-right text-destructive">
                            {/* BUGFIX: this cell hardcoded '-$' + plain toLocaleString() (USD symbol,
                                US digit grouping) while every other amount on this same row/page uses
                                formatCurrency() (₹, en-IN grouping) since that helper was fixed — this
                                was a leftover call site that was missed during that migration. */}
                            -{formatCurrency(slip.deductions)}
                          </TableCell>
                          <TableCell className="text-right font-bold text-green-600 dark:text-green-500">
                            {formatCurrency(slip.netSalary)}
                          </TableCell>
                          <TableCell>
                            <StatusBadge status={slip.status} />
                          </TableCell>
                          <TableCell className="text-right">
                            <Button
                              size="sm"
                              variant="ghost"
                              disabled={downloadingId === slip.id}
                              onClick={() => downloadPayslipPdf(slip.id, slip.employeeId, slip.month, slip.year)}
                            >
                              {downloadingId === slip.id ? (
                                <Loader2 className="h-4 w-4 mr-2 animate-spin" />
                              ) : (
                                <Download className="h-4 w-4 mr-2" />
                              )}
                              PDF
                            </Button>
                          </TableCell>
                        </TableRow>
                      ))}
                    </TableBody>
                  </Table>
                </div>
                <Pagination
                  page={payslips.page}
                  pageSize={payslips.pageSize}
                  totalCount={payslips.totalCount}
                  totalPages={payslips.totalPages}
                  onPageChange={setPage}
                />
              </>
            )}
          </div>
        </TabsContent>

        <TabsContent value="structures" className="mt-6">
          <div className="flex justify-end mb-4">
            <Button size="sm" onClick={() => setStructureOpen(true)}>
              <Plus className="mr-2 h-4 w-4" /> Add Structure
            </Button>
          </div>
          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
            {loadingStructures
              ? Array.from({ length: 3 }).map((_, i) => (
                  <Card key={i}>
                    <CardContent className="p-6">
                      <Skeleton className="h-40 w-full" />
                    </CardContent>
                  </Card>
                ))
              : structures?.map((structure) => (
                  <Card key={structure.id}>
                    <CardHeader>
                      <CardTitle className="text-lg">{structure.name}</CardTitle>
                    </CardHeader>
                    <CardContent>
                      <div className="space-y-2 text-sm">
                        <div className="flex justify-between">
                          <span className="text-muted-foreground">Basic Salary</span>
                          <span className="font-medium">{formatCurrency(structure.basicSalary)}</span>
                        </div>
                        {structure.hra && (
                          <div className="flex justify-between">
                            <span className="text-muted-foreground">HRA</span>
                            <span className="font-medium">{formatCurrency(structure.hra)}</span>
                          </div>
                        )}
                        {structure.allowances && (
                          <div className="flex justify-between">
                            <span className="text-muted-foreground">Allowances</span>
                            <span className="font-medium">{formatCurrency(structure.allowances)}</span>
                          </div>
                        )}
                        <div className="pt-2 border-t flex justify-between font-bold text-base">
                          <span>Total Gross</span>
                          <span>{formatCurrency((structure.basicSalary ?? 0) + (structure.hra ?? 0) + (structure.allowances ?? 0))}</span>
                        </div>
                      </div>
                    </CardContent>
                  </Card>
                ))}
          </div>
        </TabsContent>

        {/* Fix M-05: Bonuses & Deductions tab */}
        <TabsContent value="bonuses-deductions" className="mt-6">
          <BonusDeductionContent />
        </TabsContent>
      </Tabs>

      {/* Process Payroll dialog */}
      <Dialog open={processOpen} onOpenChange={setProcessOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Process Payroll</DialogTitle>
            <DialogDescription>Generate payslips for all eligible employees for the selected period.</DialogDescription>
          </DialogHeader>
          <Form {...processForm}>
            <form onSubmit={processForm.handleSubmit((v) => processMutation.mutate(v))} className="space-y-4">
              <div className="grid grid-cols-2 gap-4">
                <FormField control={processForm.control} name="month" render={({ field }) => (
                  <FormItem><FormLabel>Month</FormLabel><FormControl><Input type="number" min={1} max={12} {...field} /></FormControl><FormMessage /></FormItem>
                )} />
                <FormField control={processForm.control} name="year" render={({ field }) => (
                  <FormItem><FormLabel>Year</FormLabel><FormControl><Input type="number" min={2000} max={2100} {...field} /></FormControl><FormMessage /></FormItem>
                )} />
              </div>
              <DialogFooter>
                <Button variant="outline" type="button" onClick={() => setProcessOpen(false)}>Cancel</Button>
                <Button type="submit" disabled={processMutation.isPending}>{processMutation.isPending ? 'Processing…' : 'Process Payroll'}</Button>
              </DialogFooter>
            </form>
          </Form>
        </DialogContent>
      </Dialog>

      {/* Add Structure dialog */}
      <Dialog open={structureOpen} onOpenChange={setStructureOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Add Salary Structure</DialogTitle>
            <DialogDescription>Define or update the salary structure for an employee.</DialogDescription>
          </DialogHeader>
          <Form {...structureForm}>
            <form onSubmit={structureForm.handleSubmit((v) => structureMutation.mutate(v))} className="space-y-4">
              <FormField control={structureForm.control} name="employeeId" render={({ field }) => (
                <FormItem><FormLabel>Employee ID</FormLabel><FormControl><Input {...field} placeholder="EMP-0001" /></FormControl><FormMessage /></FormItem>
              )} />
              <FormField control={structureForm.control} name="ctc" render={({ field }) => (
                <FormItem><FormLabel>CTC (Annual)</FormLabel><FormControl><Input type="number" step="0.01" {...field} /></FormControl><FormMessage /></FormItem>
              )} />
              <div className="grid grid-cols-2 gap-4">
                <FormField control={structureForm.control} name="basicPay" render={({ field }) => (
                  <FormItem><FormLabel>Basic Pay (Monthly)</FormLabel><FormControl><Input type="number" step="0.01" {...field} /></FormControl><FormMessage /></FormItem>
                )} />
                <FormField control={structureForm.control} name="hra" render={({ field }) => (
                  <FormItem><FormLabel>HRA (optional)</FormLabel><FormControl><Input type="number" step="0.01" {...field} /></FormControl><FormMessage /></FormItem>
                )} />
              </div>
              <FormField control={structureForm.control} name="effectiveFrom" render={({ field }) => (
                <FormItem><FormLabel>Effective From</FormLabel><FormControl><Input type="date" {...field} /></FormControl><FormMessage /></FormItem>
              )} />
              <DialogFooter>
                <Button variant="outline" type="button" onClick={() => setStructureOpen(false)}>Cancel</Button>
                <Button type="submit" disabled={structureMutation.isPending}>{structureMutation.isPending ? 'Saving…' : 'Save Structure'}</Button>
              </DialogFooter>
            </form>
          </Form>
        </DialogContent>
      </Dialog>
    </div>
  );
}
