// Enhanced: full Travel & Expense workflow — Travel Request page
import { useState, useEffect } from 'react';
import {
  Plus, Plane, CheckCircle, XCircle, Send, Trash2,
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
import { Textarea } from '@/components/ui/textarea';
import { Checkbox } from '@/components/ui/checkbox';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import * as z from 'zod';
import {
  Form, FormControl, FormField, FormItem, FormLabel, FormMessage,
} from '@/components/ui/form';
import { SkeletonTable } from '@/components/shared/SkeletonTable';
import { EmptyState }    from '@/components/shared/EmptyState';
import { usePermissions } from '@/hooks/usePermissions';
import { useToast }       from '@/hooks/use-toast';
import { csrfFetch } from '@/utils/csrfFetch';

const BASE = import.meta.env.BASE_URL.replace(/\/$/, '');

// ── Types ─────────────────────────────────────────────────────────────────────

interface TravelApproval {
  id: number; step: string; status: string;
  approverName?: string; comments?: string; actionAt?: string;
}
interface TravelHistory {
  id: number; action: string; previousStatus?: string; newStatus?: string;
  performedByName?: string; remarks?: string; createdAt: string;
}
interface TravelRequest {
  id: number; employeeId: string; travelType: string;
  purpose: string; fromCity: string; toCity: string;
  startDate: string; endDate: string; modeOfTravel: string;
  advanceRequired: boolean; advanceAmount: number; estimatedCost: number;
  status: string; notes?: string; createdAt: string;
  approvals: TravelApproval[]; history: TravelHistory[];
}

// ── Validation ────────────────────────────────────────────────────────────────

const schema = z.object({
  travelType:      z.string().min(1),
  purpose:         z.string().min(1, 'Purpose is required'),
  fromCity:        z.string().min(1, 'From city is required'),
  toCity:          z.string().min(1, 'To city is required'),
  startDate:       z.string().min(1),
  endDate:         z.string().min(1),
  modeOfTravel:    z.string().min(1),
  advanceRequired: z.boolean().default(false),
  advanceAmount:   z.coerce.number().min(0),
  estimatedCost:   z.coerce.number().min(0),
  notes:           z.string().optional(),
}).refine((d) => new Date(d.endDate) >= new Date(d.startDate), {
  message: 'Return date must be on or after start date',
  path: ['endDate'],
});
type FormValues = z.infer<typeof schema>;

const decideSchema = z.object({
  step:     z.string().min(1),
  approve:  z.boolean(),
  sendBack: z.boolean().default(false),
  comments: z.string().optional(),
});
type DecideValues = z.infer<typeof decideSchema>;

// ── Status badge helper ───────────────────────────────────────────────────────

function travelStatusVariant(status: string): 'default' | 'secondary' | 'destructive' | 'outline' {
  switch (status) {
    case 'Draft':            return 'secondary';
    case 'Submitted':        return 'default';
    case 'ManagerApproved':
    case 'HRApproved':
    case 'FinanceApproved':
    case 'Completed':        return 'default';
    case 'Rejected':         return 'destructive';
    case 'Cancelled':        return 'outline';
    default:                 return 'secondary';
  }
}

// ── Timeline component ────────────────────────────────────────────────────────

function StatusTimeline({ history }: { history: TravelHistory[] }) {
  return (
    <div className="space-y-2 mt-2">
      {history.map((h) => (
        <div key={h.id} className="flex gap-2 text-sm">
          <div className="w-2 h-2 mt-1.5 rounded-full bg-primary shrink-0" />
          <div>
            <span className="font-medium">{h.action}</span>
            {h.performedByName && <span className="text-muted-foreground"> · {h.performedByName}</span>}
            <p className="text-xs text-muted-foreground">{new Date(h.createdAt).toLocaleString()}</p>
            {h.remarks && <p className="text-xs text-muted-foreground italic">{h.remarks}</p>}
          </div>
        </div>
      ))}
    </div>
  );
}

// ── Travel request card ───────────────────────────────────────────────────────

function TravelCard({
  req, isAdmin, onDecide, onSubmit, onCancel, onDelete
}: {
  req: TravelRequest; isAdmin: boolean;
  onDecide: (r: TravelRequest) => void;
  onSubmit: (id: number) => void;
  onCancel: (id: number) => void;
  onDelete: (id: number) => void;
}) {
  const [expanded, setExpanded] = useState(false);

  return (
    <Card>
      <CardContent className="p-4">
        <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3">
          <div className="flex items-start gap-3">
            <Plane className="h-5 w-5 text-muted-foreground shrink-0 mt-0.5" />
            <div>
              <p className="font-medium">{req.purpose}</p>
              <p className="text-sm text-muted-foreground">
                {req.fromCity} → {req.toCity} · {req.travelType} · {req.modeOfTravel}
              </p>
              <p className="text-xs text-muted-foreground mt-0.5">
                {new Date(req.startDate).toLocaleDateString()} – {new Date(req.endDate).toLocaleDateString()}
                {' · '}Est: ₹{req.estimatedCost.toLocaleString()}
              </p>
            </div>
          </div>
          <div className="flex items-center gap-2 shrink-0 flex-wrap">
            <Badge variant={travelStatusVariant(req.status)}>{req.status}</Badge>
            {req.status === 'Draft' && (
              <>
                <Button size="sm" variant="outline" className="text-blue-600 border-blue-200"
                  onClick={() => onSubmit(req.id)}>
                  <Send className="h-3 w-3 mr-1" /> Submit
                </Button>
                <Button size="sm" variant="outline" className="text-destructive border-destructive/20"
                  onClick={() => onDelete(req.id)}>
                  <Trash2 className="h-3 w-3 mr-1" /> Delete
                </Button>
              </>
            )}
            {req.status !== 'Draft' && req.status !== 'Completed' && req.status !== 'Rejected' && (
              <Button size="sm" variant="outline" className="text-orange-600 border-orange-200"
                onClick={() => onCancel(req.id)}>
                <XCircle className="h-3 w-3 mr-1" /> Cancel
              </Button>
            )}
            {isAdmin && ['Submitted', 'ManagerApproved', 'HRApproved'].includes(req.status) && (
              <Button size="sm" variant="outline" onClick={() => onDecide(req)}>
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
            {req.notes && <p className="text-sm text-muted-foreground"><span className="font-medium">Notes:</span> {req.notes}</p>}
            {req.advanceRequired && (
              <p className="text-sm"><span className="font-medium">Advance Required:</span> ₹{req.advanceAmount.toLocaleString()}</p>
            )}
            {req.approvals.length > 0 && (
              <div>
                <p className="text-xs font-semibold text-muted-foreground uppercase tracking-wide mb-1">Approvals</p>
                <div className="space-y-1">
                  {req.approvals.map(a => (
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
            {req.history.length > 0 && (
              <div>
                <p className="text-xs font-semibold text-muted-foreground uppercase tracking-wide mb-1">History</p>
                <StatusTimeline history={req.history} />
              </div>
            )}
          </div>
        )}
      </CardContent>
    </Card>
  );
}

// ── Main page ─────────────────────────────────────────────────────────────────

export default function TravelPage() {
  const { isAdmin } = usePermissions();
  const { toast }   = useToast();
  const [createOpen, setCreateOpen] = useState(false);
  const [decideOpen, setDecideOpen] = useState(false);
  const [selected,   setSelected]   = useState<TravelRequest | null>(null);
  const [myList,     setMyList]     = useState<TravelRequest[]>([]);
  const [adminList,  setAdminList]  = useState<TravelRequest[]>([]);
  const [loading,    setLoading]    = useState(true);

  const form = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: {
      travelType: 'Domestic', modeOfTravel: 'Flight',
      advanceRequired: false, advanceAmount: 0, estimatedCost: 0
    }
  });

  const decideForm = useForm<DecideValues>({
    resolver: zodResolver(decideSchema),
    defaultValues: { step: 'Manager', approve: true, sendBack: false }
  });

  const load = async () => {
    setLoading(true);
    try {
      const myRes = await csrfFetch(`${BASE}/api/travel/my`, { credentials: 'include' });
      setMyList((await myRes.json()).data ?? []);
      if (isAdmin) {
        const adminRes = await csrfFetch(`${BASE}/api/travel?pageSize=50`, { credentials: 'include' });
        setAdminList((await adminRes.json()).data?.items ?? []);
      }
    } catch { /* silent */ } finally { setLoading(false); }
  };

  useEffect(() => { load(); }, []); // eslint-disable-line react-hooks/exhaustive-deps

  const onCreate = async (values: FormValues) => {
    try {
      const res = await csrfFetch(`${BASE}/api/travel`, {
        method: 'POST', credentials: 'include',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(values),
      });
      if (!res.ok) throw new Error((await res.json()).message ?? 'Failed');
      toast({ title: 'Travel request created' });
      setCreateOpen(false); form.reset();
      await load();
    } catch (e) {
      toast({ title: 'Error', description: String(e), variant: 'destructive' });
    }
  };

  const onSubmitRequest = async (id: number) => {
    const res = await csrfFetch(`${BASE}/api/travel/${id}/submit`, { method: 'PATCH', credentials: 'include' });
    if (res.ok) { toast({ title: 'Submitted for approval' }); await load(); }
    else toast({ title: 'Failed', variant: 'destructive' });
  };

  const onCancelRequest = async (id: number) => {
    const res = await csrfFetch(`${BASE}/api/travel/${id}/cancel`, { method: 'PATCH', credentials: 'include' });
    if (res.ok) { toast({ title: 'Request cancelled' }); await load(); }
    else toast({ title: 'Failed', variant: 'destructive' });
  };

  const onDeleteRequest = async (id: number) => {
    const res = await csrfFetch(`${BASE}/api/travel/${id}`, { method: 'DELETE', credentials: 'include' });
    if (res.ok) { toast({ title: 'Deleted' }); await load(); }
    else toast({ title: 'Failed', variant: 'destructive' });
  };

  const openDecide = (req: TravelRequest) => {
    setSelected(req);
    const nextStep = req.status === 'Submitted' ? 'Manager' : req.status === 'ManagerApproved' ? 'HR' : 'Finance';
    decideForm.reset({ step: nextStep, approve: true, sendBack: false });
    setDecideOpen(true);
  };

  const onDecide = async (values: DecideValues) => {
    if (!selected) return;
    try {
      const res = await csrfFetch(`${BASE}/api/travel/${selected.id}/decide`, {
        method: 'PATCH', credentials: 'include',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(values),
      });
      if (!res.ok) throw new Error((await res.json()).message ?? 'Failed');
      toast({ title: values.sendBack ? 'Sent back' : values.approve ? 'Approved' : 'Rejected' });
      setDecideOpen(false); setSelected(null);
      await load();
    } catch (e) {
      toast({ title: 'Error', description: String(e), variant: 'destructive' });
    }
  };

  return (
    <div className="space-y-6">
      <PageHeader
        title="Travel Requests"
        description="Submit and manage business travel requests through the approval workflow."
        actions={
          <Button onClick={() => setCreateOpen(true)}>
            <Plus className="h-4 w-4 mr-2" /> New Request
          </Button>
        }
      />

      <Tabs defaultValue="mine">
        <TabsList>
          <TabsTrigger value="mine">My Requests</TabsTrigger>
          {isAdmin && <TabsTrigger value="all">All Requests</TabsTrigger>}
        </TabsList>

        <TabsContent value="mine" className="space-y-3 mt-4">
          {loading ? <SkeletonTable rows={3} /> : myList.length === 0
            ? <EmptyState title="No travel requests" description="Submit a new request to get started." icon={Plane} />
            : myList.map(r => (
              <TravelCard key={r.id} req={r} isAdmin={isAdmin}
                onDecide={openDecide} onSubmit={onSubmitRequest}
                onCancel={onCancelRequest} onDelete={onDeleteRequest} />
            ))}
        </TabsContent>

        {isAdmin && (
          <TabsContent value="all" className="space-y-3 mt-4">
            {loading ? <SkeletonTable rows={3} /> : adminList.length === 0
              ? <EmptyState title="No requests" description="No travel requests in the system." icon={Plane} />
              : adminList.map(r => (
                <TravelCard key={r.id} req={r} isAdmin={isAdmin}
                  onDecide={openDecide} onSubmit={onSubmitRequest}
                  onCancel={onCancelRequest} onDelete={onDeleteRequest} />
              ))}
          </TabsContent>
        )}
      </Tabs>

      {/* ── Create dialog ── */}
      <Dialog open={createOpen} onOpenChange={setCreateOpen}>
        <DialogContent className="max-w-2xl max-h-[90vh] overflow-y-auto">
          <DialogHeader><DialogTitle>New Travel Request</DialogTitle></DialogHeader>
          <Form {...form}>
            <form onSubmit={form.handleSubmit(onCreate)} className="space-y-4">
              <div className="grid grid-cols-2 gap-3">
                <FormField control={form.control} name="travelType" render={({ field }) => (
                  <FormItem><FormLabel>Travel Type</FormLabel>
                    <Select onValueChange={field.onChange} defaultValue={field.value}>
                      <FormControl><SelectTrigger><SelectValue /></SelectTrigger></FormControl>
                      <SelectContent>
                        {['Local', 'Domestic', 'International'].map(t => (
                          <SelectItem key={t} value={t}>{t}</SelectItem>
                        ))}
                      </SelectContent>
                    </Select><FormMessage />
                  </FormItem>
                )} />
                <FormField control={form.control} name="modeOfTravel" render={({ field }) => (
                  <FormItem><FormLabel>Mode of Travel</FormLabel>
                    <Select onValueChange={field.onChange} defaultValue={field.value}>
                      <FormControl><SelectTrigger><SelectValue /></SelectTrigger></FormControl>
                      <SelectContent>
                        {['Flight', 'Train', 'Bus', 'Car', 'Cab', 'Ship', 'Other'].map(m => (
                          <SelectItem key={m} value={m}>{m}</SelectItem>
                        ))}
                      </SelectContent>
                    </Select><FormMessage />
                  </FormItem>
                )} />
              </div>

              <FormField control={form.control} name="purpose" render={({ field }) => (
                <FormItem><FormLabel>Purpose</FormLabel>
                  <FormControl><Input placeholder="Client meeting, conference…" {...field} /></FormControl>
                  <FormMessage />
                </FormItem>
              )} />

              <div className="grid grid-cols-2 gap-3">
                <FormField control={form.control} name="fromCity" render={({ field }) => (
                  <FormItem><FormLabel>From</FormLabel>
                    <FormControl><Input placeholder="City" {...field} /></FormControl>
                    <FormMessage />
                  </FormItem>
                )} />
                <FormField control={form.control} name="toCity" render={({ field }) => (
                  <FormItem><FormLabel>To</FormLabel>
                    <FormControl><Input placeholder="City / Country" {...field} /></FormControl>
                    <FormMessage />
                  </FormItem>
                )} />
              </div>

              <div className="grid grid-cols-2 gap-3">
                <FormField control={form.control} name="startDate" render={({ field }) => (
                  <FormItem><FormLabel>Start Date</FormLabel>
                    <FormControl><Input type="date" {...field} /></FormControl>
                    <FormMessage />
                  </FormItem>
                )} />
                <FormField control={form.control} name="endDate" render={({ field }) => (
                  <FormItem><FormLabel>End Date</FormLabel>
                    <FormControl><Input type="date" {...field} /></FormControl>
                    <FormMessage />
                  </FormItem>
                )} />
              </div>

              <FormField control={form.control} name="estimatedCost" render={({ field }) => (
                <FormItem><FormLabel>Estimated Cost (₹)</FormLabel>
                  <FormControl><Input type="number" step="0.01" {...field} /></FormControl>
                  <FormMessage />
                </FormItem>
              )} />

              <div className="flex items-center gap-3">
                <FormField control={form.control} name="advanceRequired" render={({ field }) => (
                  <FormItem className="flex items-center gap-2 space-y-0">
                    <FormControl>
                      <Checkbox checked={field.value} onCheckedChange={field.onChange} />
                    </FormControl>
                    <FormLabel className="font-normal cursor-pointer">Advance required</FormLabel>
                  </FormItem>
                )} />
                {form.watch('advanceRequired') && (
                  <FormField control={form.control} name="advanceAmount" render={({ field }) => (
                    <FormItem className="flex-1">
                      <FormControl><Input type="number" step="0.01" placeholder="Advance amount (₹)" {...field} /></FormControl>
                      <FormMessage />
                    </FormItem>
                  )} />
                )}
              </div>

              <FormField control={form.control} name="notes" render={({ field }) => (
                <FormItem><FormLabel>Notes</FormLabel>
                  <FormControl><Textarea rows={2} placeholder="Any additional details…" {...field} /></FormControl>
                </FormItem>
              )} />

              <DialogFooter>
                <Button variant="outline" type="button" onClick={() => setCreateOpen(false)}>Cancel</Button>
                <Button type="submit" disabled={form.formState.isSubmitting}>Create as Draft</Button>
              </DialogFooter>
            </form>
          </Form>
        </DialogContent>
      </Dialog>

      {/* ── Approve / Reject dialog ── */}
      <Dialog open={decideOpen} onOpenChange={setDecideOpen}>
        <DialogContent className="max-w-md">
          <DialogHeader>
            <DialogTitle>Review Travel Request</DialogTitle>
          </DialogHeader>
          {selected && (
            <div className="text-sm space-y-1 mb-4 p-3 bg-muted/40 rounded-md">
              <p><span className="font-medium">Purpose:</span> {selected.purpose}</p>
              <p><span className="font-medium">Route:</span> {selected.fromCity} → {selected.toCity}</p>
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
                      {['Manager', 'HR', 'Finance'].map(s => (
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
                  <FormControl><Textarea rows={2} placeholder="Reason for decision…" {...field} /></FormControl>
                </FormItem>
              )} />
            </form>
          </Form>
        </DialogContent>
      </Dialog>
    </div>
  );
}
