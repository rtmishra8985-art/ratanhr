// Enhanced: full multi-item Expense Claim page with 2-step approval workflow
import { useState, useEffect, useRef } from 'react';
import {
  Plus, Receipt, CheckCircle, XCircle, Trash2, Send,
  ChevronDown, ChevronUp, RotateCcw
} from 'lucide-react';
import { PageHeader }    from '@/components/layout/PageHeader';
import { Button }        from '@/components/ui/button';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { Card, CardContent } from '@/components/ui/card';
import { Badge }         from '@/components/ui/badge';
import {
  Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter,
} from '@/components/ui/dialog';
import {
  Select, SelectContent, SelectItem, SelectTrigger, SelectValue,
} from '@/components/ui/select';
import { Input }    from '@/components/ui/input';
import { Label }    from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { useForm }  from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import * as z from 'zod';
import {
  Form, FormControl, FormField, FormItem, FormLabel, FormMessage,
} from '@/components/ui/form';
import { SkeletonTable } from '@/components/shared/SkeletonTable';
import { EmptyState }    from '@/components/shared/EmptyState';
import { usePermissions } from '@/hooks/usePermissions';
import { useToast }       from '@/hooks/use-toast';
import { statusVariant }  from '@/utils/badgeVariants';
import { csrfFetch } from '@/utils/csrfFetch';

const BASE = import.meta.env.BASE_URL.replace(/\/$/, '');

const CATEGORIES = ['Hotel', 'Flight', 'Cab', 'Fuel', 'Food', 'Train', 'Bus', 'Miscellaneous'];

// ── Types ─────────────────────────────────────────────────────────────────────

interface ExpenseItem {
  id: number; category: string; description: string; amount: number;
  gstAmount: number; currency: string; expenseDate: string; receiptPath?: string;
}
interface ExpenseApproval {
  id: number; step: string; status: string;
  approverName?: string; comments?: string; actionAt?: string;
}
interface ExpenseHistory {
  id: number; action: string; previousStatus?: string; newStatus?: string;
  performedByName?: string; remarks?: string; createdAt: string;
}
interface ExpenseClaim {
  id: number; employeeId: string; title: string; currency: string;
  totalAmount: number; totalGst: number; status: string;
  submittedAt?: string; notes?: string; createdAt: string;
  items: ExpenseItem[]; approvals: ExpenseApproval[]; history: ExpenseHistory[];
}

// ── Validation ────────────────────────────────────────────────────────────────

const itemSchema = z.object({
  category:    z.string().min(1),
  description: z.string().min(1, 'Description required'),
  amount:      z.coerce.number().positive('Amount must be positive'),
  gstAmount:   z.coerce.number().min(0),
  currency:    z.string().min(1).max(10).default('INR'),
  expenseDate: z.string().min(1, 'Date required'),
});
type ItemForm = z.infer<typeof itemSchema>;

const claimSchema = z.object({
  title:    z.string().min(1, 'Title required').max(200),
  currency: z.string().min(1).max(10).default('INR'),
  notes:    z.string().optional(),
});
type ClaimForm = z.infer<typeof claimSchema>;

const decideSchema = z.object({
  step: z.string().min(1), approve: z.boolean(),
  sendBack: z.boolean().default(false), comments: z.string().optional(),
});
type DecideForm = z.infer<typeof decideSchema>;

// ── Expense claim card ────────────────────────────────────────────────────────

function ClaimCard({
  claim, isAdmin, onDecide, onSubmit, onDelete
}: {
  claim: ExpenseClaim; isAdmin: boolean;
  onDecide: (c: ExpenseClaim) => void;
  onSubmit: (id: number) => void;
  onDelete: (id: number) => void;
}) {
  const [expanded, setExpanded] = useState(false);

  return (
    <Card>
      <CardContent className="p-4">
        <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3">
          <div className="flex items-start gap-3">
            <Receipt className="h-5 w-5 text-muted-foreground shrink-0 mt-0.5" />
            <div>
              <p className="font-medium">{claim.title}</p>
              <p className="text-sm text-muted-foreground">
                {claim.currency} {claim.totalAmount.toLocaleString()}
                {claim.totalGst > 0 && ` · GST ${claim.totalGst.toLocaleString()}`}
                {' · '}{claim.items.length} item{claim.items.length !== 1 ? 's' : ''}
              </p>
            </div>
          </div>
          <div className="flex items-center gap-2 shrink-0 flex-wrap">
            <Badge variant={statusVariant(claim.status)}>{claim.status}</Badge>
            {claim.status === 'Draft' && (
              <>
                <Button size="sm" variant="outline" className="text-blue-600 border-blue-200"
                  onClick={() => onSubmit(claim.id)}>
                  <Send className="h-3 w-3 mr-1" /> Submit
                </Button>
                <Button size="sm" variant="outline" className="text-destructive border-destructive/20"
                  onClick={() => onDelete(claim.id)}>
                  <Trash2 className="h-3 w-3 mr-1" /> Delete
                </Button>
              </>
            )}
            {isAdmin && ['Submitted', 'ManagerApproved'].includes(claim.status) && (
              <Button size="sm" variant="outline" onClick={() => onDecide(claim)}>
                <CheckCircle className="h-3 w-3 mr-1" /> Review
              </Button>
            )}
            <Button size="sm" variant="ghost" onClick={() => setExpanded(!expanded)}>
              {expanded ? <ChevronUp className="h-4 w-4" /> : <ChevronDown className="h-4 w-4" />}
            </Button>
          </div>
        </div>

        {expanded && (
          <div className="mt-4 pt-4 border-t space-y-3">
            {claim.items.length > 0 && (
              <div>
                <p className="text-xs font-semibold text-muted-foreground uppercase tracking-wide mb-2">Line Items</p>
                <div className="space-y-1">
                  {claim.items.map(item => (
                    <div key={item.id} className="flex justify-between text-sm py-1 border-b last:border-0">
                      <div>
                        <span className="font-medium">{item.category}</span>
                        <span className="text-muted-foreground"> · {item.description}</span>
                        <span className="text-xs text-muted-foreground ml-1">({item.expenseDate})</span>
                      </div>
                      <div className="text-right shrink-0 ml-4">
                        <p>{item.currency} {item.amount.toLocaleString()}</p>
                        {item.gstAmount > 0 && <p className="text-xs text-muted-foreground">GST {item.gstAmount.toLocaleString()}</p>}
                      </div>
                    </div>
                  ))}
                </div>
              </div>
            )}
            {claim.approvals.length > 0 && (
              <div>
                <p className="text-xs font-semibold text-muted-foreground uppercase tracking-wide mb-1">Approvals</p>
                <div className="space-y-1">
                  {claim.approvals.map(a => (
                    <div key={a.id} className="flex items-center gap-2 text-sm">
                      <Badge variant={a.status === 'Approved' ? 'default' : a.status === 'Rejected' ? 'destructive' : 'secondary'} className="text-xs">
                        {a.step}
                      </Badge>
                      <span className="text-muted-foreground">{a.status}</span>
                      {a.approverName && <span>· {a.approverName}</span>}
                      {a.comments && <span className="italic text-muted-foreground">· "{a.comments}"</span>}
                    </div>
                  ))}
                </div>
              </div>
            )}
            {claim.history.length > 0 && (
              <div>
                <p className="text-xs font-semibold text-muted-foreground uppercase tracking-wide mb-1">History</p>
                <div className="space-y-1">
                  {claim.history.map(h => (
                    <div key={h.id} className="flex gap-2 text-sm">
                      <div className="w-2 h-2 mt-1.5 rounded-full bg-primary shrink-0" />
                      <div>
                        <span className="font-medium">{h.action}</span>
                        <p className="text-xs text-muted-foreground">{new Date(h.createdAt).toLocaleString()}</p>
                      </div>
                    </div>
                  ))}
                </div>
              </div>
            )}
          </div>
        )}
      </CardContent>
    </Card>
  );
}

// ── Main page ─────────────────────────────────────────────────────────────────

export default function ExpensesPage() {
  const { isAdmin } = usePermissions();
  const { toast }   = useToast();
  const [createOpen, setCreateOpen] = useState(false);
  const [decideOpen, setDecideOpen] = useState(false);
  const [selected,   setSelected]   = useState<ExpenseClaim | null>(null);
  const [myClaims,   setMyClaims]   = useState<ExpenseClaim[]>([]);
  const [adminClaims, setAdminClaims] = useState<ExpenseClaim[]>([]);
  const [loading,    setLoading]    = useState(true);

  // Line items state for the create dialog
  const [items, setItems] = useState<ItemForm[]>([]);
  const receiptRefs = useRef<(HTMLInputElement | null)[]>([]);

  const claimForm = useForm<ClaimForm>({
    resolver: zodResolver(claimSchema),
    defaultValues: { title: '', currency: 'INR', notes: '' },
  });

  const itemForm = useForm<ItemForm>({
    resolver: zodResolver(itemSchema),
    defaultValues: { category: 'Miscellaneous', currency: 'INR', amount: 0, gstAmount: 0 },
  });

  const decideForm = useForm<DecideForm>({
    resolver: zodResolver(decideSchema),
    defaultValues: { step: 'Manager', approve: true, sendBack: false },
  });

  const load = async () => {
    setLoading(true);
    try {
      const myRes = await csrfFetch(`${BASE}/api/expenses/my`, { credentials: 'include' });
      setMyClaims((await myRes.json()).data ?? []);
      if (isAdmin) {
        const adminRes = await csrfFetch(`${BASE}/api/expenses?pageSize=50`, { credentials: 'include' });
        setAdminClaims((await adminRes.json()).data?.items ?? []);
      }
    } catch { /* silent */ } finally { setLoading(false); }
  };

  useEffect(() => { load(); }, []); // eslint-disable-line react-hooks/exhaustive-deps

  const addItem = (values: ItemForm) => {
    setItems(prev => [...prev, values]);
    itemForm.reset({ category: 'Miscellaneous', currency: 'INR', amount: 0, gstAmount: 0 });
  };

  const removeItem = (index: number) => setItems(prev => prev.filter((_, i) => i !== index));

  const onCreateClaim = async (values: ClaimForm) => {
    if (items.length === 0) {
      toast({ title: 'Add at least one expense item', variant: 'destructive' }); return;
    }
    try {
      // Build FormData for multipart submit (supporting receipt files)
      const fd = new FormData();
      fd.append('title', values.title);
      fd.append('currency', values.currency);
      if (values.notes) fd.append('notes', values.notes);
      items.forEach((item, i) => {
        fd.append(`Items[${i}].Category`, item.category);
        fd.append(`Items[${i}].Description`, item.description);
        fd.append(`Items[${i}].Amount`, String(item.amount));
        fd.append(`Items[${i}].GstAmount`, String(item.gstAmount));
        fd.append(`Items[${i}].Currency`, item.currency);
        fd.append(`Items[${i}].ExpenseDate`, item.expenseDate);
        const file = receiptRefs.current[i]?.files?.[0];
        if (file) fd.append(`Items[${i}].Receipt`, file);
      });

      const res = await csrfFetch(`${BASE}/api/expenses`, {
        method: 'POST', credentials: 'include', body: fd,
      });
      if (!res.ok) throw new Error((await res.json()).message ?? 'Failed');
      toast({ title: 'Expense claim created as Draft' });
      setCreateOpen(false); claimForm.reset(); setItems([]); await load();
    } catch (e) {
      toast({ title: 'Error', description: String(e), variant: 'destructive' });
    }
  };

  const onSubmitClaim = async (id: number) => {
    const res = await csrfFetch(`${BASE}/api/expenses/${id}/submit`, { method: 'PATCH', credentials: 'include' });
    if (res.ok) { toast({ title: 'Submitted for approval' }); await load(); }
    else toast({ title: 'Failed', variant: 'destructive' });
  };

  const onDeleteClaim = async (id: number) => {
    const res = await csrfFetch(`${BASE}/api/expenses/${id}`, { method: 'DELETE', credentials: 'include' });
    if (res.ok) { toast({ title: 'Deleted' }); await load(); }
    else toast({ title: 'Failed', variant: 'destructive' });
  };

  const openDecide = (claim: ExpenseClaim) => {
    setSelected(claim);
    const nextStep = claim.status === 'Submitted' ? 'Manager' : 'Finance';
    decideForm.reset({ step: nextStep, approve: true, sendBack: false });
    setDecideOpen(true);
  };

  const onDecide = async (values: DecideForm) => {
    if (!selected) return;
    try {
      const res = await csrfFetch(`${BASE}/api/expenses/${selected.id}/decide`, {
        method: 'PATCH', credentials: 'include',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(values),
      });
      if (!res.ok) throw new Error((await res.json()).message ?? 'Failed');
      toast({ title: values.sendBack ? 'Sent back' : values.approve ? 'Approved' : 'Rejected' });
      setDecideOpen(false); setSelected(null); await load();
    } catch (e) {
      toast({ title: 'Error', description: String(e), variant: 'destructive' });
    }
  };

  const totalAmount = items.reduce((s, i) => s + (Number(i.amount) || 0), 0);

  return (
    <div className="space-y-6">
      <PageHeader
        title="Expense Claims"
        description="Submit and manage expense reimbursement claims."
        actions={
          <Button onClick={() => setCreateOpen(true)}>
            <Plus className="h-4 w-4 mr-2" /> New Claim
          </Button>
        }
      />

      <Tabs defaultValue="mine">
        <TabsList>
          <TabsTrigger value="mine">My Claims</TabsTrigger>
          {isAdmin && <TabsTrigger value="all">All Claims</TabsTrigger>}
        </TabsList>

        <TabsContent value="mine" className="space-y-3 mt-4">
          {loading ? <SkeletonTable rows={3} /> : myClaims.length === 0
            ? <EmptyState title="No expense claims" description="Submit a new claim to get started." icon={Receipt} />
            : myClaims.map(c => (
              <ClaimCard key={c.id} claim={c} isAdmin={isAdmin}
                onDecide={openDecide} onSubmit={onSubmitClaim} onDelete={onDeleteClaim} />
            ))}
        </TabsContent>

        {isAdmin && (
          <TabsContent value="all" className="space-y-3 mt-4">
            {loading ? <SkeletonTable rows={3} /> : adminClaims.length === 0
              ? <EmptyState title="No claims" description="No expense claims in the system." icon={Receipt} />
              : adminClaims.map(c => (
                <ClaimCard key={c.id} claim={c} isAdmin={isAdmin}
                  onDecide={openDecide} onSubmit={onSubmitClaim} onDelete={onDeleteClaim} />
              ))}
          </TabsContent>
        )}
      </Tabs>

      {/* ── Create dialog ── */}
      <Dialog open={createOpen} onOpenChange={setCreateOpen}>
        <DialogContent className="max-w-3xl max-h-[90vh] overflow-y-auto">
          <DialogHeader><DialogTitle>New Expense Claim</DialogTitle></DialogHeader>

          <Form {...claimForm}>
            <div className="space-y-4">
              <div className="grid grid-cols-2 gap-3">
                <FormField control={claimForm.control} name="title" render={({ field }) => (
                  <FormItem><FormLabel>Claim Title</FormLabel>
                    <FormControl><Input placeholder="Aug business trip expenses" {...field} /></FormControl>
                    <FormMessage />
                  </FormItem>
                )} />
                <FormField control={claimForm.control} name="currency" render={({ field }) => (
                  <FormItem><FormLabel>Currency</FormLabel>
                    <Select onValueChange={field.onChange} defaultValue={field.value}>
                      <FormControl><SelectTrigger><SelectValue /></SelectTrigger></FormControl>
                      <SelectContent>
                        {['INR', 'USD', 'EUR', 'GBP', 'AED', 'SGD'].map(c => (
                          <SelectItem key={c} value={c}>{c}</SelectItem>
                        ))}
                      </SelectContent>
                    </Select>
                  </FormItem>
                )} />
              </div>

              <FormField control={claimForm.control} name="notes" render={({ field }) => (
                <FormItem><FormLabel>Notes</FormLabel>
                  <FormControl><Textarea rows={1} placeholder="Any additional context…" {...field} /></FormControl>
                </FormItem>
              )} />

              {/* ── Line items ── */}
              <div className="space-y-2">
                <p className="text-sm font-semibold">Expense Items</p>

                {items.length > 0 && (
                  <div className="space-y-1">
                    {items.map((item, i) => (
                      <div key={i} className="flex items-center justify-between gap-2 p-2 rounded-md bg-muted/40 text-sm">
                        <div>
                          <span className="font-medium">{item.category}</span>
                          <span className="text-muted-foreground"> · {item.description}</span>
                          <span className="text-muted-foreground"> · {item.expenseDate}</span>
                        </div>
                        <div className="flex items-center gap-2 shrink-0">
                          <span className="font-medium">{item.currency} {Number(item.amount).toLocaleString()}</span>
                          <Button size="sm" variant="ghost" className="h-6 w-6 p-0 text-destructive"
                            onClick={() => removeItem(i)}>
                            <Trash2 className="h-3 w-3" />
                          </Button>
                        </div>
                      </div>
                    ))}
                    <div className="text-right text-sm font-semibold pr-2">
                      Total: ₹{totalAmount.toLocaleString()}
                    </div>
                  </div>
                )}

                {/* Add item form */}
                <div className="border rounded-md p-3 space-y-3">
                  <p className="text-xs font-medium text-muted-foreground uppercase tracking-wide">Add Item</p>
                  <Form {...itemForm}>
                    <div className="grid grid-cols-2 gap-2">
                      <FormField control={itemForm.control} name="category" render={({ field }) => (
                        <FormItem><FormLabel className="text-xs">Category</FormLabel>
                          <Select onValueChange={field.onChange} defaultValue={field.value}>
                            <FormControl><SelectTrigger className="h-8"><SelectValue /></SelectTrigger></FormControl>
                            <SelectContent>
                              {CATEGORIES.map(c => <SelectItem key={c} value={c}>{c}</SelectItem>)}
                            </SelectContent>
                          </Select>
                        </FormItem>
                      )} />
                      <FormField control={itemForm.control} name="currency" render={({ field }) => (
                        <FormItem><FormLabel className="text-xs">Currency</FormLabel>
                          <Select onValueChange={field.onChange} defaultValue={field.value}>
                            <FormControl><SelectTrigger className="h-8"><SelectValue /></SelectTrigger></FormControl>
                            <SelectContent>
                              {['INR', 'USD', 'EUR', 'GBP', 'AED', 'SGD'].map(c => (
                                <SelectItem key={c} value={c}>{c}</SelectItem>
                              ))}
                            </SelectContent>
                          </Select>
                        </FormItem>
                      )} />
                    </div>
                    <FormField control={itemForm.control} name="description" render={({ field }) => (
                      <FormItem><FormLabel className="text-xs">Description</FormLabel>
                        <FormControl><Input className="h-8" placeholder="Hotel Night 1, Uber to airport…" {...field} /></FormControl>
                        <FormMessage />
                      </FormItem>
                    )} />
                    <div className="grid grid-cols-3 gap-2">
                      <FormField control={itemForm.control} name="amount" render={({ field }) => (
                        <FormItem><FormLabel className="text-xs">Amount</FormLabel>
                          <FormControl><Input className="h-8" type="number" step="0.01" {...field} /></FormControl>
                          <FormMessage />
                        </FormItem>
                      )} />
                      <FormField control={itemForm.control} name="gstAmount" render={({ field }) => (
                        <FormItem><FormLabel className="text-xs">GST</FormLabel>
                          <FormControl><Input className="h-8" type="number" step="0.01" {...field} /></FormControl>
                        </FormItem>
                      )} />
                      <FormField control={itemForm.control} name="expenseDate" render={({ field }) => (
                        <FormItem><FormLabel className="text-xs">Date</FormLabel>
                          <FormControl><Input className="h-8" type="date" {...field} /></FormControl>
                          <FormMessage />
                        </FormItem>
                      )} />
                    </div>
                    <div>
                      <Label className="text-xs">Bill / Receipt</Label>
                      <Input
                        type="file" accept="image/*,.pdf"
                        className="h-8 mt-1"
                        ref={(el) => { receiptRefs.current[items.length] = el; }}
                      />
                    </div>
                    <Button type="button" size="sm" variant="outline"
                      onClick={itemForm.handleSubmit(addItem)}>
                      <Plus className="h-3 w-3 mr-1" /> Add Item
                    </Button>
                  </Form>
                </div>
              </div>

              <DialogFooter>
                <Button variant="outline" type="button" onClick={() => { setCreateOpen(false); setItems([]); claimForm.reset(); }}>Cancel</Button>
                <Button onClick={claimForm.handleSubmit(onCreateClaim)} disabled={claimForm.formState.isSubmitting}>
                  Create Draft
                </Button>
              </DialogFooter>
            </div>
          </Form>
        </DialogContent>
      </Dialog>

      {/* ── Decide dialog ── */}
      <Dialog open={decideOpen} onOpenChange={setDecideOpen}>
        <DialogContent className="max-w-md">
          <DialogHeader><DialogTitle>Review Expense Claim</DialogTitle></DialogHeader>
          {selected && (
            <div className="text-sm space-y-1 mb-4 p-3 bg-muted/40 rounded-md">
              <p><span className="font-medium">Title:</span> {selected.title}</p>
              <p><span className="font-medium">Total:</span> {selected.currency} {selected.totalAmount.toLocaleString()}</p>
              <p><span className="font-medium">Status:</span> {selected.status}</p>
            </div>
          )}
          <Form {...decideForm}>
            <form onSubmit={decideForm.handleSubmit(onDecide)} className="space-y-4">
              <FormField control={decideForm.control} name="step" render={({ field }) => (
                <FormItem><FormLabel>Approval Step</FormLabel>
                  <Select onValueChange={field.onChange} defaultValue={field.value}>
                    <FormControl><SelectTrigger><SelectValue /></SelectTrigger></FormControl>
                    <SelectContent>
                      {['Manager', 'Finance'].map(s => (
                        <SelectItem key={s} value={s}>{s}</SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </FormItem>
              )} />
              <div className="flex gap-3">
                <Button type="submit" onClick={() => { decideForm.setValue('approve', true); decideForm.setValue('sendBack', false); }}
                  className="flex-1 bg-green-600 hover:bg-green-700">
                  <CheckCircle className="h-4 w-4 mr-1" /> Approve
                </Button>
                <Button type="submit" variant="outline" onClick={() => { decideForm.setValue('approve', false); decideForm.setValue('sendBack', true); }}
                  className="flex-1 border-orange-300 text-orange-600 hover:bg-orange-50">
                  <RotateCcw className="h-4 w-4 mr-1" /> Send Back
                </Button>
                <Button type="submit" variant="outline" onClick={() => { decideForm.setValue('approve', false); decideForm.setValue('sendBack', false); }}
                  className="flex-1 border-destructive/30 text-destructive hover:bg-destructive/5">
                  <XCircle className="h-4 w-4 mr-1" /> Reject
                </Button>
              </div>
              <FormField control={decideForm.control} name="comments" render={({ field }) => (
                <FormItem><FormLabel>Comments (optional)</FormLabel>
                  <FormControl><Textarea rows={2} placeholder="Reason…" {...field} /></FormControl>
                </FormItem>
              )} />
            </form>
          </Form>
        </DialogContent>
      </Dialog>
    </div>
  );
}
