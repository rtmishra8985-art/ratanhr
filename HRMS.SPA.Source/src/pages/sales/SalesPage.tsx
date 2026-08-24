/**
 * SalesPage.tsx — Full Sales/CRM module.
 *
 * Covers:
 *   - Dashboard: KPI cards (leads by status, pipeline value, customer count)
 *   - Leads:     List, search, filter by status, create, update status, delete
 *   - Customers: List, search, view details
 *   - Quotations: List, create, view
 *
 * All API calls use credentials: 'include' (HttpOnly cookie auth).
 * Backend: HRMS.API/Controllers/Sales/SalesController.cs
 */
import { useState } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import * as z from 'zod';
import {
  TrendingUp, Users, Building2, Plus, Search, RefreshCw,
  MoreHorizontal, Eye, Pencil, Trash2, CheckCircle, DollarSign,
  ShoppingBag, FileText, Phone, Mail,
} from 'lucide-react';
import { toast } from 'sonner';

import { PageHeader }    from '@/components/layout/PageHeader';
import { Button }        from '@/components/ui/button';
import { Badge }         from '@/components/ui/badge';
import { Input }         from '@/components/ui/input';
import { Textarea }      from '@/components/ui/textarea';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import {
  Table, TableBody, TableCell, TableHead, TableHeader, TableRow,
} from '@/components/ui/table';
import {
  Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter,
} from '@/components/ui/dialog';
import {
  AlertDialog, AlertDialogAction, AlertDialogCancel,
  AlertDialogContent, AlertDialogDescription, AlertDialogFooter,
  AlertDialogHeader, AlertDialogTitle,
} from '@/components/ui/alert-dialog';
import {
  DropdownMenu, DropdownMenuContent, DropdownMenuItem,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu';
import {
  Select, SelectContent, SelectItem, SelectTrigger, SelectValue,
} from '@/components/ui/select';
import {
  Form, FormControl, FormField, FormItem, FormLabel, FormMessage,
} from '@/components/ui/form';
import { Skeleton }       from '@/components/ui/skeleton';
import { SkeletonTable }  from '@/components/shared/SkeletonTable';
import { EmptyState }     from '@/components/shared/EmptyState';
import { Pagination }     from '@/components/shared/Pagination';
import { usePaginationState } from '@/hooks/usePaginationState';
import { csrfFetch } from '@/utils/csrfFetch';

const BASE = import.meta.env.BASE_URL?.replace(/\/$/, '') ?? '';

// ─── Types ────────────────────────────────────────────────────────────────────

interface LeadListDto {
  id: number; leadNo: string; companyName: string; contactPerson: string;
  mobile: string; email: string; city: string; leadSource: string;
  industry: string; ownerName?: string; priority: string; status: string;
  expectedValue?: number; nextFollowUpDate?: string; createdAt: string;
}

interface CustomerListDto {
  id: number; customerCode: string; companyName: string; contactPerson: string;
  contactPhone: string; contactEmail: string; salesPersonName?: string;
  isActive: boolean; createdAt: string;
}

interface SalesDashboard {
  totalLeads: number; openLeads: number; convertedLeads: number;
  totalCustomers: number; totalPipelineValue: number; leadsThisMonth: number;
  conversionRate: number;
}

interface QuotationListDto {
  id: number; quotationNo: string; customerName: string; grandTotal: number;
  status: string; validTill?: string; createdAt: string;
}

// ─── Helpers ─────────────────────────────────────────────────────────────────

async function apiFetch<T>(url: string, options?: RequestInit): Promise<T> {
  const res = await csrfFetch(`${BASE}${url}`, {
    credentials: 'include',
    ...options,
    headers: { 'Content-Type': 'application/json', ...options?.headers },
  });
  const json = await res.json().catch(() => ({})) as Record<string, unknown>;
  if (!res.ok) throw new Error((json.message as string) ?? `HTTP ${res.status}`);
  return (json.data ?? json) as T;
}

const LEAD_STATUSES = ['New', 'Contacted', 'Qualified', 'Proposal', 'Negotiation', 'Won', 'Lost'];
const PRIORITIES    = ['Low', 'Medium', 'High'];
const LEAD_SOURCES  = ['Website', 'Referral', 'Cold Call', 'Social Media', 'Email Campaign', 'Exhibition', 'Other'];
const INDUSTRIES    = ['IT', 'Manufacturing', 'Retail', 'Healthcare', 'Education', 'Finance', 'Real Estate', 'Other'];

function statusColor(s: string): 'default' | 'secondary' | 'destructive' | 'outline' {
  if (s === 'Won' || s === 'Converted')   return 'default';
  if (s === 'Lost')                         return 'destructive';
  if (s === 'Qualified' || s === 'Proposal') return 'secondary';
  return 'outline';
}
function priorityColor(p: string): string {
  if (p === 'High')   return 'text-red-600';
  if (p === 'Medium') return 'text-yellow-600';
  return 'text-green-600';
}

function fmt(v?: number) {
  if (v == null) return '—';
  return `₹${v.toLocaleString('en-IN')}`;
}
function fmtDate(d?: string) {
  if (!d) return '—';
  return new Date(d).toLocaleDateString('en-IN');
}

// ─── Schemas ─────────────────────────────────────────────────────────────────

const leadSchema = z.object({
  companyName:    z.string().min(1, 'Company name is required').max(200),
  contactPerson:  z.string().min(1, 'Contact person is required').max(100),
  mobile:         z.string().min(10, 'Valid mobile required').max(20),
  email:          z.string().email('Valid email required'),
  city:           z.string().min(1, 'City is required').max(100),
  state:          z.string().max(100).optional().default(''),
  country:        z.string().max(100).optional().default('India'),
  address:        z.string().max(500).optional().default(''),
  leadSource:     z.string().min(1, 'Lead source is required'),
  industry:       z.string().min(1, 'Industry is required'),
  priority:       z.string().min(1).default('Medium'),
  status:         z.string().min(1).default('New'),
  remarks:        z.string().max(1000).optional().default(''),
  expectedValue:  z.coerce.number().min(0).optional(),
  nextFollowUpDate: z.string().optional(),
});
type LeadFormValues = z.infer<typeof leadSchema>;

const customerSchema = z.object({
  companyName:      z.string().min(1, 'Company name required').max(200),
  contactPerson:    z.string().min(1, 'Contact person required').max(100),
  contactPhone:     z.string().min(10, 'Phone required').max(20),
  contactEmail:     z.string().email('Valid email required'),
  gst:              z.string().max(15).optional().default(''),
  pan:              z.string().max(10).optional().default(''),
  billingAddress:   z.string().max(500).optional().default(''),
  shippingAddress:  z.string().max(500).optional().default(''),
  isActive:         z.boolean().optional().default(true),
});
type CustomerFormValues = z.infer<typeof customerSchema>;

// ─── Dashboard Tab ────────────────────────────────────────────────────────────

function DashboardTab() {
  const { data, isLoading } = useQuery<SalesDashboard>({
    queryKey: ['sales-dashboard'],
    queryFn: () => apiFetch<SalesDashboard>('/api/sales/dashboard'),
  });

  const stats = [
    { label: 'Total Leads',       value: data?.totalLeads ?? 0,         icon: TrendingUp,   sub: `${data?.leadsThisMonth ?? 0} this month` },
    { label: 'Open Leads',        value: data?.openLeads ?? 0,          icon: Search,       sub: 'Actively pursued' },
    { label: 'Pipeline Value',    value: fmt(data?.totalPipelineValue),  icon: DollarSign,   sub: 'Expected revenue' },
    { label: 'Customers',         value: data?.totalCustomers ?? 0,      icon: Building2,    sub: `${((data?.conversionRate ?? 0)).toFixed(1)}% conversion` },
  ];

  return (
    <div className="space-y-6">
      <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
        {stats.map((s) => (
          <Card key={s.label}>
            <CardHeader className="flex flex-row items-center justify-between pb-2 space-y-0">
              <CardTitle className="text-sm font-medium text-muted-foreground">{s.label}</CardTitle>
              <s.icon className="h-4 w-4 text-muted-foreground" />
            </CardHeader>
            <CardContent>
              {isLoading
                ? <Skeleton className="h-8 w-20" />
                : <div className="text-2xl font-bold">{s.value}</div>}
              <p className="text-xs text-muted-foreground mt-1">{s.sub}</p>
            </CardContent>
          </Card>
        ))}
      </div>

      {/* Lead status breakdown */}
      <Card>
        <CardHeader><CardTitle className="text-base">Lead Pipeline Status</CardTitle></CardHeader>
        <CardContent>
          {isLoading
            ? <Skeleton className="h-8 w-full" />
            : (
              <div className="flex flex-wrap gap-3">
                {LEAD_STATUSES.map((s) => (
                  <Badge key={s} variant={statusColor(s)} className="text-sm px-3 py-1">{s}</Badge>
                ))}
              </div>
            )}
        </CardContent>
      </Card>
    </div>
  );
}

// ─── Leads Tab ────────────────────────────────────────────────────────────────

function LeadsTab() {
  const qc = useQueryClient();
  const { page, setPage, pageSize } = usePaginationState();
  const [search, setSearch]     = useState('');
  const [statusFilter, setStatusFilter] = useState('all');
  const [showCreate, setShowCreate]     = useState(false);
  const [editLead, setEditLead]         = useState<LeadListDto | null>(null);
  const [deleteLead, setDeleteLead]     = useState<LeadListDto | null>(null);
  const [statusLead, setStatusLead]     = useState<LeadListDto | null>(null);
  const [newStatus, setNewStatus]       = useState('');

  const params = new URLSearchParams({
    page: String(page), pageSize: String(pageSize),
    ...(search       ? { search }                  : {}),
    ...(statusFilter !== 'all' ? { status: statusFilter } : {}),
  });

  const { data, isLoading, refetch } = useQuery<{ data: LeadListDto[]; total: number }>({
    queryKey: ['sales-leads', page, pageSize, search, statusFilter],
    queryFn: () => apiFetch<{ data: LeadListDto[]; total: number }>(`/api/sales/leads?${params}`),
    placeholderData: (prev) => prev,
  });

  const leads = data?.data ?? [];
  const total = data?.total ?? 0;

  const deleteMut = useMutation({
    mutationFn: (id: number) => apiFetch(`/api/sales/leads/${id}`, { method: 'DELETE' }),
    onSuccess: () => { toast.success('Lead deleted'); qc.invalidateQueries({ queryKey: ['sales-leads'] }); setDeleteLead(null); },
    onError: (e: Error) => toast.error(e.message),
  });

  const statusMut = useMutation({
    mutationFn: ({ id, status }: { id: number; status: string }) =>
      apiFetch(`/api/sales/leads/${id}/status`, {
        method: 'PATCH', body: JSON.stringify({ status }),
      }),
    onSuccess: () => { toast.success('Status updated'); qc.invalidateQueries({ queryKey: ['sales-leads'] }); setStatusLead(null); },
    onError: (e: Error) => toast.error(e.message),
  });

  return (
    <div className="space-y-4">
      <div className="flex flex-col sm:flex-row gap-3 items-start sm:items-center justify-between">
        <div className="flex gap-2 flex-wrap">
          <div className="relative">
            <Search className="absolute left-2.5 top-2.5 h-4 w-4 text-muted-foreground" />
            <Input
              placeholder="Search leads..."
              value={search}
              onChange={(e) => { setSearch(e.target.value); setPage(1); }}
              className="pl-8 w-[220px]"
            />
          </div>
          <Select value={statusFilter} onValueChange={(v) => { setStatusFilter(v); setPage(1); }}>
            <SelectTrigger className="w-[150px]"><SelectValue placeholder="All Statuses" /></SelectTrigger>
            <SelectContent>
              <SelectItem value="all">All Statuses</SelectItem>
              {LEAD_STATUSES.map((s) => <SelectItem key={s} value={s}>{s}</SelectItem>)}
            </SelectContent>
          </Select>
          <Button variant="ghost" size="icon" onClick={() => refetch()} title="Refresh"><RefreshCw className="h-4 w-4" /></Button>
        </div>
        <Button onClick={() => setShowCreate(true)}><Plus className="mr-2 h-4 w-4" /> New Lead</Button>
      </div>

      <div className="border rounded-lg overflow-hidden">
        {isLoading ? <SkeletonTable columns={7} rows={8} /> : leads.length === 0 ? (
          <EmptyState icon={TrendingUp} title="No leads found" description="Add your first lead to get started." />
        ) : (
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Lead No</TableHead>
                <TableHead>Company</TableHead>
                <TableHead>Contact</TableHead>
                <TableHead>Status</TableHead>
                <TableHead>Priority</TableHead>
                <TableHead>Expected Value</TableHead>
                <TableHead>Next Follow-up</TableHead>
                <TableHead className="w-[60px]" />
              </TableRow>
            </TableHeader>
            <TableBody>
              {leads.map((lead) => (
                <TableRow key={lead.id}>
                  <TableCell className="font-mono text-xs">{lead.leadNo}</TableCell>
                  <TableCell>
                    <div className="font-medium">{lead.companyName}</div>
                    <div className="text-xs text-muted-foreground">{lead.industry}</div>
                  </TableCell>
                  <TableCell>
                    <div>{lead.contactPerson}</div>
                    <div className="text-xs text-muted-foreground">{lead.mobile}</div>
                  </TableCell>
                  <TableCell><Badge variant={statusColor(lead.status)}>{lead.status}</Badge></TableCell>
                  <TableCell><span className={`font-medium ${priorityColor(lead.priority)}`}>{lead.priority}</span></TableCell>
                  <TableCell>{fmt(lead.expectedValue)}</TableCell>
                  <TableCell>{fmtDate(lead.nextFollowUpDate)}</TableCell>
                  <TableCell>
                    <DropdownMenu>
                      <DropdownMenuTrigger asChild>
                        <Button variant="ghost" size="icon" className="h-8 w-8"><MoreHorizontal className="h-4 w-4" /></Button>
                      </DropdownMenuTrigger>
                      <DropdownMenuContent align="end">
                        <DropdownMenuItem onClick={() => setEditLead(lead)}><Pencil className="mr-2 h-4 w-4" /> Edit</DropdownMenuItem>
                        <DropdownMenuItem onClick={() => { setStatusLead(lead); setNewStatus(lead.status); }}>
                          <CheckCircle className="mr-2 h-4 w-4" /> Change Status
                        </DropdownMenuItem>
                        <DropdownMenuItem className="text-destructive" onClick={() => setDeleteLead(lead)}>
                          <Trash2 className="mr-2 h-4 w-4" /> Delete
                        </DropdownMenuItem>
                      </DropdownMenuContent>
                    </DropdownMenu>
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        )}
      </div>

      {total > pageSize && (
        <Pagination page={page} pageSize={pageSize} totalCount={total} totalPages={Math.max(1, Math.ceil(total / pageSize))} onPageChange={setPage} />
      )}

      {/* Create / Edit Dialog */}
      {(showCreate || editLead) && (
        <LeadFormDialog
          lead={editLead}
          onClose={() => { setShowCreate(false); setEditLead(null); }}
          onSaved={() => { setShowCreate(false); setEditLead(null); qc.invalidateQueries({ queryKey: ['sales-leads'] }); }}
        />
      )}

      {/* Status Dialog */}
      {statusLead && (
        <Dialog open onOpenChange={() => setStatusLead(null)}>
          <DialogContent className="max-w-sm">
            <DialogHeader><DialogTitle>Change Lead Status</DialogTitle></DialogHeader>
            <Select value={newStatus} onValueChange={setNewStatus}>
              <SelectTrigger><SelectValue /></SelectTrigger>
              <SelectContent>{LEAD_STATUSES.map((s) => <SelectItem key={s} value={s}>{s}</SelectItem>)}</SelectContent>
            </Select>
            <DialogFooter>
              <Button variant="outline" onClick={() => setStatusLead(null)}>Cancel</Button>
              <Button
                disabled={statusMut.isPending}
                onClick={() => statusMut.mutate({ id: statusLead.id, status: newStatus })}
              >
                {statusMut.isPending ? 'Saving…' : 'Update'}
              </Button>
            </DialogFooter>
          </DialogContent>
        </Dialog>
      )}

      {/* Delete Confirm */}
      {deleteLead && (
        <AlertDialog open onOpenChange={() => setDeleteLead(null)}>
          <AlertDialogContent>
            <AlertDialogHeader>
              <AlertDialogTitle>Delete Lead</AlertDialogTitle>
              <AlertDialogDescription>Delete <strong>{deleteLead.companyName}</strong>? This cannot be undone.</AlertDialogDescription>
            </AlertDialogHeader>
            <AlertDialogFooter>
              <AlertDialogCancel>Cancel</AlertDialogCancel>
              <AlertDialogAction
                className="bg-destructive text-destructive-foreground hover:bg-destructive/90"
                onClick={() => deleteMut.mutate(deleteLead.id)}
                disabled={deleteMut.isPending}
              >
                {deleteMut.isPending ? 'Deleting…' : 'Delete'}
              </AlertDialogAction>
            </AlertDialogFooter>
          </AlertDialogContent>
        </AlertDialog>
      )}
    </div>
  );
}

// ─── Lead Form Dialog ─────────────────────────────────────────────────────────

function LeadFormDialog({
  lead, onClose, onSaved,
}: { lead: LeadListDto | null; onClose: () => void; onSaved: () => void }) {
  const form = useForm<LeadFormValues>({
    resolver: zodResolver(leadSchema),
    defaultValues: lead
      ? {
          companyName: lead.companyName, contactPerson: lead.contactPerson,
          mobile: lead.mobile, email: lead.email, city: lead.city,
          leadSource: lead.leadSource, industry: lead.industry,
          priority: lead.priority, status: lead.status,
          expectedValue: lead.expectedValue ?? undefined,
          nextFollowUpDate: lead.nextFollowUpDate ? lead.nextFollowUpDate.slice(0, 10) : '',
          state: '', country: 'India', address: '', remarks: '',
        }
      : { priority: 'Medium', status: 'New', country: 'India' },
  });

  const mut = useMutation({
    mutationFn: (values: LeadFormValues) => {
      const url  = lead ? `/api/sales/leads/${lead.id}` : '/api/sales/leads';
      const method = lead ? 'PUT' : 'POST';
      return apiFetch(url, { method, body: JSON.stringify(values) });
    },
    onSuccess: () => { toast.success(lead ? 'Lead updated' : 'Lead created'); onSaved(); },
    onError: (e: Error) => toast.error(e.message),
  });

  return (
    <Dialog open onOpenChange={onClose}>
      <DialogContent className="max-w-2xl max-h-[90vh] overflow-y-auto">
        <DialogHeader><DialogTitle>{lead ? 'Edit Lead' : 'New Lead'}</DialogTitle></DialogHeader>
        <Form {...form}>
          <form onSubmit={form.handleSubmit((v) => mut.mutate(v))} className="space-y-4">
            <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
              <FormField control={form.control} name="companyName" render={({ field }) => (
                <FormItem><FormLabel>Company Name *</FormLabel><FormControl><Input {...field} /></FormControl><FormMessage /></FormItem>
              )} />
              <FormField control={form.control} name="contactPerson" render={({ field }) => (
                <FormItem><FormLabel>Contact Person *</FormLabel><FormControl><Input {...field} /></FormControl><FormMessage /></FormItem>
              )} />
              <FormField control={form.control} name="mobile" render={({ field }) => (
                <FormItem><FormLabel>Mobile *</FormLabel><FormControl><Input {...field} /></FormControl><FormMessage /></FormItem>
              )} />
              <FormField control={form.control} name="email" render={({ field }) => (
                <FormItem><FormLabel>Email *</FormLabel><FormControl><Input type="email" {...field} /></FormControl><FormMessage /></FormItem>
              )} />
              <FormField control={form.control} name="city" render={({ field }) => (
                <FormItem><FormLabel>City *</FormLabel><FormControl><Input {...field} /></FormControl><FormMessage /></FormItem>
              )} />
              <FormField control={form.control} name="state" render={({ field }) => (
                <FormItem><FormLabel>State</FormLabel><FormControl><Input {...field} /></FormControl><FormMessage /></FormItem>
              )} />
              <FormField control={form.control} name="leadSource" render={({ field }) => (
                <FormItem><FormLabel>Lead Source *</FormLabel>
                  <Select onValueChange={field.onChange} value={field.value}>
                    <FormControl><SelectTrigger><SelectValue placeholder="Select source" /></SelectTrigger></FormControl>
                    <SelectContent>{LEAD_SOURCES.map((s) => <SelectItem key={s} value={s}>{s}</SelectItem>)}</SelectContent>
                  </Select><FormMessage />
                </FormItem>
              )} />
              <FormField control={form.control} name="industry" render={({ field }) => (
                <FormItem><FormLabel>Industry *</FormLabel>
                  <Select onValueChange={field.onChange} value={field.value}>
                    <FormControl><SelectTrigger><SelectValue placeholder="Select industry" /></SelectTrigger></FormControl>
                    <SelectContent>{INDUSTRIES.map((i) => <SelectItem key={i} value={i}>{i}</SelectItem>)}</SelectContent>
                  </Select><FormMessage />
                </FormItem>
              )} />
              <FormField control={form.control} name="priority" render={({ field }) => (
                <FormItem><FormLabel>Priority</FormLabel>
                  <Select onValueChange={field.onChange} value={field.value}>
                    <FormControl><SelectTrigger><SelectValue /></SelectTrigger></FormControl>
                    <SelectContent>{PRIORITIES.map((p) => <SelectItem key={p} value={p}>{p}</SelectItem>)}</SelectContent>
                  </Select><FormMessage />
                </FormItem>
              )} />
              <FormField control={form.control} name="status" render={({ field }) => (
                <FormItem><FormLabel>Status</FormLabel>
                  <Select onValueChange={field.onChange} value={field.value}>
                    <FormControl><SelectTrigger><SelectValue /></SelectTrigger></FormControl>
                    <SelectContent>{LEAD_STATUSES.map((s) => <SelectItem key={s} value={s}>{s}</SelectItem>)}</SelectContent>
                  </Select><FormMessage />
                </FormItem>
              )} />
              <FormField control={form.control} name="expectedValue" render={({ field }) => (
                <FormItem><FormLabel>Expected Value (₹)</FormLabel>
                  <FormControl><Input type="number" min={0} step={0.01} {...field} /></FormControl><FormMessage />
                </FormItem>
              )} />
              <FormField control={form.control} name="nextFollowUpDate" render={({ field }) => (
                <FormItem><FormLabel>Next Follow-up Date</FormLabel>
                  <FormControl><Input type="date" {...field} /></FormControl><FormMessage />
                </FormItem>
              )} />
            </div>
            <FormField control={form.control} name="remarks" render={({ field }) => (
              <FormItem><FormLabel>Remarks</FormLabel>
                <FormControl><Textarea rows={3} {...field} /></FormControl><FormMessage />
              </FormItem>
            )} />
            <DialogFooter>
              <Button type="button" variant="outline" onClick={onClose}>Cancel</Button>
              <Button type="submit" disabled={mut.isPending}>
                {mut.isPending ? 'Saving…' : lead ? 'Save Changes' : 'Create Lead'}
              </Button>
            </DialogFooter>
          </form>
        </Form>
      </DialogContent>
    </Dialog>
  );
}

// ─── Customers Tab ────────────────────────────────────────────────────────────

function CustomersTab() {
  const qc = useQueryClient();
  const { page, setPage, pageSize } = usePaginationState();
  const [search, setSearch]       = useState('');
  const [showCreate, setShowCreate] = useState(false);
  const [viewCustomer, setViewCustomer] = useState<CustomerListDto | null>(null);
  const [deleteCustomer, setDeleteCustomer] = useState<CustomerListDto | null>(null);

  const params = new URLSearchParams({
    page: String(page), pageSize: String(pageSize),
    ...(search ? { search } : {}),
  });

  const { data, isLoading, refetch } = useQuery<{ data: CustomerListDto[]; total: number }>({
    queryKey: ['sales-customers', page, pageSize, search],
    queryFn: () => apiFetch<{ data: CustomerListDto[]; total: number }>(`/api/sales/customers?${params}`),
    placeholderData: (prev) => prev,
  });

  const customers = data?.data ?? [];
  const total     = data?.total ?? 0;

  const deleteMut = useMutation({
    mutationFn: (id: number) => apiFetch(`/api/sales/customers/${id}`, { method: 'DELETE' }),
    onSuccess: () => { toast.success('Customer deleted'); qc.invalidateQueries({ queryKey: ['sales-customers'] }); setDeleteCustomer(null); },
    onError: (e: Error) => toast.error(e.message),
  });

  return (
    <div className="space-y-4">
      <div className="flex gap-3 items-center justify-between">
        <div className="flex gap-2">
          <div className="relative">
            <Search className="absolute left-2.5 top-2.5 h-4 w-4 text-muted-foreground" />
            <Input
              placeholder="Search customers..."
              value={search}
              onChange={(e) => { setSearch(e.target.value); setPage(1); }}
              className="pl-8 w-[220px]"
            />
          </div>
          <Button variant="ghost" size="icon" onClick={() => refetch()} title="Refresh"><RefreshCw className="h-4 w-4" /></Button>
        </div>
        <Button onClick={() => setShowCreate(true)}><Plus className="mr-2 h-4 w-4" /> New Customer</Button>
      </div>

      <div className="border rounded-lg overflow-hidden">
        {isLoading ? <SkeletonTable columns={6} rows={8} /> : customers.length === 0 ? (
          <EmptyState icon={Building2} title="No customers found" description="Convert a lead or add a customer directly." />
        ) : (
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Code</TableHead>
                <TableHead>Company</TableHead>
                <TableHead>Contact</TableHead>
                <TableHead>Salesperson</TableHead>
                <TableHead>Status</TableHead>
                <TableHead>Created</TableHead>
                <TableHead className="w-[60px]" />
              </TableRow>
            </TableHeader>
            <TableBody>
              {customers.map((c) => (
                <TableRow key={c.id}>
                  <TableCell className="font-mono text-xs">{c.customerCode}</TableCell>
                  <TableCell className="font-medium">{c.companyName}</TableCell>
                  <TableCell>
                    <div className="flex items-center gap-1"><Phone className="h-3 w-3 text-muted-foreground" /> {c.contactPhone}</div>
                    <div className="flex items-center gap-1 text-xs text-muted-foreground"><Mail className="h-3 w-3" /> {c.contactEmail}</div>
                  </TableCell>
                  <TableCell>{c.salesPersonName ?? '—'}</TableCell>
                  <TableCell>
                    <Badge variant={c.isActive ? 'default' : 'secondary'}>{c.isActive ? 'Active' : 'Inactive'}</Badge>
                  </TableCell>
                  <TableCell>{fmtDate(c.createdAt)}</TableCell>
                  <TableCell>
                    <DropdownMenu>
                      <DropdownMenuTrigger asChild>
                        <Button variant="ghost" size="icon" className="h-8 w-8"><MoreHorizontal className="h-4 w-4" /></Button>
                      </DropdownMenuTrigger>
                      <DropdownMenuContent align="end">
                        <DropdownMenuItem onClick={() => setViewCustomer(c)}><Eye className="mr-2 h-4 w-4" /> View</DropdownMenuItem>
                        <DropdownMenuItem className="text-destructive" onClick={() => setDeleteCustomer(c)}>
                          <Trash2 className="mr-2 h-4 w-4" /> Delete
                        </DropdownMenuItem>
                      </DropdownMenuContent>
                    </DropdownMenu>
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        )}
      </div>

      {total > pageSize && (
        <Pagination page={page} pageSize={pageSize} totalCount={total} totalPages={Math.max(1, Math.ceil(total / pageSize))} onPageChange={setPage} />
      )}

      {showCreate && (
        <CustomerFormDialog
          onClose={() => setShowCreate(false)}
          onSaved={() => { setShowCreate(false); qc.invalidateQueries({ queryKey: ['sales-customers'] }); }}
        />
      )}

      {viewCustomer && (
        <Dialog open onOpenChange={() => setViewCustomer(null)}>
          <DialogContent className="max-w-md">
            <DialogHeader><DialogTitle>{viewCustomer.companyName}</DialogTitle></DialogHeader>
            <div className="space-y-3 text-sm">
              <div className="flex items-center gap-2"><Building2 className="h-4 w-4 text-muted-foreground" /> <span className="font-mono">{viewCustomer.customerCode}</span></div>
              <div className="flex items-center gap-2"><Users className="h-4 w-4 text-muted-foreground" /> {viewCustomer.contactPerson}</div>
              <div className="flex items-center gap-2"><Phone className="h-4 w-4 text-muted-foreground" /> {viewCustomer.contactPhone}</div>
              <div className="flex items-center gap-2"><Mail className="h-4 w-4 text-muted-foreground" /> {viewCustomer.contactEmail}</div>
              <div className="flex items-center gap-2"><TrendingUp className="h-4 w-4 text-muted-foreground" /> {viewCustomer.salesPersonName ?? 'Unassigned'}</div>
            </div>
            <DialogFooter>
              <Button variant="outline" onClick={() => setViewCustomer(null)}>Close</Button>
            </DialogFooter>
          </DialogContent>
        </Dialog>
      )}

      {deleteCustomer && (
        <AlertDialog open onOpenChange={() => setDeleteCustomer(null)}>
          <AlertDialogContent>
            <AlertDialogHeader>
              <AlertDialogTitle>Delete Customer</AlertDialogTitle>
              <AlertDialogDescription>Delete <strong>{deleteCustomer.companyName}</strong>? This cannot be undone.</AlertDialogDescription>
            </AlertDialogHeader>
            <AlertDialogFooter>
              <AlertDialogCancel>Cancel</AlertDialogCancel>
              <AlertDialogAction
                className="bg-destructive text-destructive-foreground hover:bg-destructive/90"
                onClick={() => deleteMut.mutate(deleteCustomer.id)}
                disabled={deleteMut.isPending}
              >
                {deleteMut.isPending ? 'Deleting…' : 'Delete'}
              </AlertDialogAction>
            </AlertDialogFooter>
          </AlertDialogContent>
        </AlertDialog>
      )}
    </div>
  );
}

// ─── Customer Form Dialog ─────────────────────────────────────────────────────

function CustomerFormDialog({ onClose, onSaved }: { onClose: () => void; onSaved: () => void }) {
  const form = useForm<CustomerFormValues>({
    resolver: zodResolver(customerSchema),
    defaultValues: { isActive: true, country: 'India' } as unknown as CustomerFormValues,
  });

  const mut = useMutation({
    mutationFn: (values: CustomerFormValues) =>
      apiFetch('/api/sales/customers', { method: 'POST', body: JSON.stringify(values) }),
    onSuccess: () => { toast.success('Customer created'); onSaved(); },
    onError: (e: Error) => toast.error(e.message),
  });

  return (
    <Dialog open onOpenChange={onClose}>
      <DialogContent className="max-w-2xl max-h-[90vh] overflow-y-auto">
        <DialogHeader><DialogTitle>New Customer</DialogTitle></DialogHeader>
        <Form {...form}>
          <form onSubmit={form.handleSubmit((v) => mut.mutate(v))} className="space-y-4">
            <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
              <FormField control={form.control} name="companyName" render={({ field }) => (
                <FormItem><FormLabel>Company Name *</FormLabel><FormControl><Input {...field} /></FormControl><FormMessage /></FormItem>
              )} />
              <FormField control={form.control} name="contactPerson" render={({ field }) => (
                <FormItem><FormLabel>Contact Person *</FormLabel><FormControl><Input {...field} /></FormControl><FormMessage /></FormItem>
              )} />
              <FormField control={form.control} name="contactPhone" render={({ field }) => (
                <FormItem><FormLabel>Phone *</FormLabel><FormControl><Input {...field} /></FormControl><FormMessage /></FormItem>
              )} />
              <FormField control={form.control} name="contactEmail" render={({ field }) => (
                <FormItem><FormLabel>Email *</FormLabel><FormControl><Input type="email" {...field} /></FormControl><FormMessage /></FormItem>
              )} />
              <FormField control={form.control} name="gst" render={({ field }) => (
                <FormItem><FormLabel>GST Number</FormLabel><FormControl><Input placeholder="27AAAAA0000A1Z5" {...field} /></FormControl><FormMessage /></FormItem>
              )} />
              <FormField control={form.control} name="pan" render={({ field }) => (
                <FormItem><FormLabel>PAN</FormLabel><FormControl><Input placeholder="AAAAA0000A" {...field} /></FormControl><FormMessage /></FormItem>
              )} />
            </div>
            <FormField control={form.control} name="billingAddress" render={({ field }) => (
              <FormItem><FormLabel>Billing Address</FormLabel><FormControl><Textarea rows={2} {...field} /></FormControl><FormMessage /></FormItem>
            )} />
            <FormField control={form.control} name="shippingAddress" render={({ field }) => (
              <FormItem><FormLabel>Shipping Address</FormLabel><FormControl><Textarea rows={2} {...field} /></FormControl><FormMessage /></FormItem>
            )} />
            <DialogFooter>
              <Button type="button" variant="outline" onClick={onClose}>Cancel</Button>
              <Button type="submit" disabled={mut.isPending}>{mut.isPending ? 'Saving…' : 'Create Customer'}</Button>
            </DialogFooter>
          </form>
        </Form>
      </DialogContent>
    </Dialog>
  );
}

// ─── Quotations Tab ───────────────────────────────────────────────────────────

function QuotationsTab() {
  const { page, setPage, pageSize } = usePaginationState();

  const { data, isLoading } = useQuery<{ data: QuotationListDto[]; total: number }>({
    queryKey: ['sales-quotations', page, pageSize],
    queryFn: () => apiFetch<{ data: QuotationListDto[]; total: number }>(
      `/api/sales/quotations?page=${page}&pageSize=${pageSize}`
    ),
    placeholderData: (prev) => prev,
  });

  const quotations = data?.data ?? [];
  const total      = data?.total ?? 0;

  const qStatusColor = (s: string): 'default' | 'secondary' | 'outline' | 'destructive' => {
    if (s === 'Accepted')  return 'default';
    if (s === 'Rejected')  return 'destructive';
    if (s === 'Sent')      return 'secondary';
    return 'outline';
  };

  return (
    <div className="space-y-4">
      <div className="border rounded-lg overflow-hidden">
        {isLoading ? <SkeletonTable columns={5} rows={6} /> : quotations.length === 0 ? (
          <EmptyState icon={FileText} title="No quotations yet" description="Quotations are created from customer records." />
        ) : (
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Quotation No</TableHead>
                <TableHead>Customer</TableHead>
                <TableHead>Total (₹)</TableHead>
                <TableHead>Status</TableHead>
                <TableHead>Valid Till</TableHead>
                <TableHead>Created</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {quotations.map((q) => (
                <TableRow key={q.id}>
                  <TableCell className="font-mono text-xs">{q.quotationNo}</TableCell>
                  <TableCell>{q.customerName}</TableCell>
                  <TableCell className="font-medium">{fmt(q.grandTotal)}</TableCell>
                  <TableCell><Badge variant={qStatusColor(q.status)}>{q.status}</Badge></TableCell>
                  <TableCell>{fmtDate(q.validTill)}</TableCell>
                  <TableCell>{fmtDate(q.createdAt)}</TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        )}
      </div>
      {total > pageSize && (
        <Pagination page={page} pageSize={pageSize} totalCount={total} totalPages={Math.max(1, Math.ceil(total / pageSize))} onPageChange={setPage} />
      )}
    </div>
  );
}

// ─── Page ─────────────────────────────────────────────────────────────────────

export default function SalesPage() {
  return (
    <div className="space-y-6">
      <PageHeader
        title="Sales / CRM"
        description="Manage leads, customers, quotations, and sales pipeline."
        actions={<ShoppingBag className="h-6 w-6 text-muted-foreground" />}
      />

      <Tabs defaultValue="dashboard">
        <TabsList className="grid w-full sm:w-auto grid-cols-4 sm:inline-flex">
          <TabsTrigger value="dashboard">Dashboard</TabsTrigger>
          <TabsTrigger value="leads">Leads</TabsTrigger>
          <TabsTrigger value="customers">Customers</TabsTrigger>
          <TabsTrigger value="quotations">Quotations</TabsTrigger>
        </TabsList>

        <TabsContent value="dashboard" className="mt-6"><DashboardTab /></TabsContent>
        <TabsContent value="leads"     className="mt-6"><LeadsTab /></TabsContent>
        <TabsContent value="customers" className="mt-6"><CustomersTab /></TabsContent>
        <TabsContent value="quotations" className="mt-6"><QuotationsTab /></TabsContent>
      </Tabs>
    </div>
  );
}
