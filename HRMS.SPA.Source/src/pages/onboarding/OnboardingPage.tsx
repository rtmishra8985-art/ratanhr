/**
 * OnboardingPage.tsx — Employee onboarding checklist + Admin template management.
 *
 * Fixes:
 *   - Admin tab added: template CRUD (create, edit, delete) + assign to employee.
 *   - Employee tab: unchanged functional checklist (mark steps done, progress bar).
 *
 * Backend: HRMS.API/Controllers/Onboarding/OnboardingController.cs
 * All API calls use credentials: 'include' (HttpOnly cookie auth).
 */
import { useState } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { useForm, useFieldArray } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import * as z from 'zod';
import {
  CheckCircle, Circle, ClipboardList, Plus, Pencil, Trash2,
  UserPlus, MoreHorizontal, RefreshCw, Settings,
} from 'lucide-react';
import { toast } from 'sonner';

import { PageHeader }    from '@/components/layout/PageHeader';
import { Button }        from '@/components/ui/button';
import { Badge }         from '@/components/ui/badge';
import { Input }         from '@/components/ui/input';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from '@/components/ui/card';
import { Progress }      from '@/components/ui/progress';
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
  DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu';
import {
  Form, FormControl, FormField, FormItem, FormLabel, FormMessage,
} from '@/components/ui/form';
import { SkeletonTable } from '@/components/shared/SkeletonTable';
import { EmptyState }    from '@/components/shared/EmptyState';
import { useToast }      from '@/hooks/use-toast';
import { useAuth }       from '@/hooks/useAuth';
import { usePermissions } from '@/hooks/usePermissions';
import { csrfFetch } from '@/utils/csrfFetch';

const BASE = import.meta.env.BASE_URL?.replace(/\/$/, '') ?? '';

// ─── Types ────────────────────────────────────────────────────────────────────

interface Step { title: string; description?: string; }

interface OnboardingRecord {
  id: number; templateName: string; steps: string;
  completedSteps: string; dueDate?: string; completedAt?: string;
}

interface OnboardingTemplateDto {
  id: number; name: string; steps: string; isActive: boolean; createdAt: string;
}

// ─── Helpers ─────────────────────────────────────────────────────────────────

async function apiFetch<T = unknown>(url: string, options?: RequestInit): Promise<T> {
  const res = await csrfFetch(`${BASE}${url}`, {
    credentials: 'include',
    ...options,
    headers: { 'Content-Type': 'application/json', ...options?.headers },
  });
  const json = await res.json().catch(() => ({})) as Record<string, unknown>;
  if (!res.ok) throw new Error((json.message as string) ?? `HTTP ${res.status}`);
  return (json.data ?? json) as T;
}

function parseSteps(raw: string): Step[] {
  try { return JSON.parse(raw) as Step[]; } catch { return []; }
}
function parseDone(raw: string): number[] {
  try { return JSON.parse(raw) as number[]; } catch { return []; }
}

// ─── Schemas ─────────────────────────────────────────────────────────────────

const stepSchema = z.object({
  title:       z.string().min(1, 'Step title is required').max(200),
  description: z.string().max(500).optional().default(''),
});

const templateSchema = z.object({
  name:  z.string().min(1, 'Template name is required').max(200),
  steps: z.array(stepSchema).min(1, 'Add at least one step'),
});
type TemplateFormValues = z.infer<typeof templateSchema>;

const assignSchema = z.object({
  employeeId: z.string().min(1, 'Employee ID is required'),
  templateId: z.coerce.number().min(1, 'Select a template'),
  dueDate:    z.string().optional(),
});
type AssignFormValues = z.infer<typeof assignSchema>;

// ─────────────────────────────────────────────────────────────────────────────
// EMPLOYEE TAB
// ─────────────────────────────────────────────────────────────────────────────

function EmployeeTab() {
  const { toast: toast2 } = useToast();
  const [record, setRecord]       = useState<OnboardingRecord | null | 'loading'>('loading');
  const [completing, setCompleting] = useState<number | null>(null);

  // Fetch on mount
  useState(() => {
    (async () => {
      try {
        const res = await csrfFetch(`${BASE}/api/onboarding/my`, { credentials: 'include' });
        if (res.status === 404) { setRecord(null); return; }
        const json = await res.json() as { data?: OnboardingRecord };
        setRecord(json.data ?? null);
      } catch { setRecord(null); }
    })();
  });

  const markStep = async (stepIndex: number) => {
    if (!record || record === 'loading') return;
    setCompleting(stepIndex);
    try {
      const res = await csrfFetch(`${BASE}/api/onboarding/records/${record.id}/complete-step`, {
        method: 'PATCH', credentials: 'include',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ stepIndex }),
      });
      const json = await res.json() as { message?: string };
      if (!res.ok) throw new Error(json.message ?? 'Request failed');
      const done = parseDone(record.completedSteps);
      if (!done.includes(stepIndex)) done.push(stepIndex);
      setRecord({ ...record, completedSteps: JSON.stringify(done) });
      toast2({ title: 'Step completed!' });
    } catch (e) {
      toast2({ title: 'Failed', description: String(e), variant: 'destructive' });
    } finally { setCompleting(null); }
  };

  if (record === 'loading') return <SkeletonTable columns={1} rows={5} />;

  if (!record) return (
    <EmptyState
      icon={ClipboardList}
      title="No active onboarding"
      description="Your admin will assign an onboarding checklist when you join."
    />
  );

  const steps = parseSteps(record.steps);
  const done  = parseDone(record.completedSteps);
  const pct   = steps.length > 0 ? Math.round((done.length / steps.length) * 100) : 0;

  return (
    <div className="max-w-2xl mx-auto space-y-6">
      <Card>
        <CardHeader>
          <CardTitle className="flex items-center justify-between">
            <span>{record.templateName}</span>
            <span className="text-sm font-normal text-muted-foreground">{done.length}/{steps.length} done</span>
          </CardTitle>
          {record.dueDate && (
            <CardDescription>Due: {new Date(record.dueDate).toLocaleDateString()}</CardDescription>
          )}
          <Progress value={pct} className="h-2 mt-2" />
        </CardHeader>
        <CardContent className="divide-y">
          {steps.map((step, idx) => {
            const isDone = done.includes(idx);
            return (
              <div key={idx} className={`flex items-center gap-4 py-4 ${isDone ? 'opacity-60' : ''}`}>
                <div className="shrink-0">
                  {isDone
                    ? <CheckCircle className="h-6 w-6 text-green-500" />
                    : <Circle className="h-6 w-6 text-muted-foreground" />}
                </div>
                <div className="flex-1 min-w-0">
                  <p className={`font-medium ${isDone ? 'line-through text-muted-foreground' : ''}`}>{step.title}</p>
                  {step.description && <p className="text-sm text-muted-foreground mt-0.5">{step.description}</p>}
                </div>
                {!isDone && (
                  <Button size="sm" variant="outline" disabled={completing === idx} onClick={() => markStep(idx)}>
                    {completing === idx ? 'Saving…' : 'Mark Done'}
                  </Button>
                )}
              </div>
            );
          })}
        </CardContent>
      </Card>

      {record.completedAt && (
        <Card className="border-green-200 bg-green-50 dark:bg-green-950/20">
          <CardContent className="p-4 flex items-center gap-3">
            <CheckCircle className="h-6 w-6 text-green-500 shrink-0" />
            <p className="text-green-700 dark:text-green-300 font-medium">
              Onboarding completed on {new Date(record.completedAt).toLocaleDateString()}.
            </p>
          </CardContent>
        </Card>
      )}
    </div>
  );
}

// ─────────────────────────────────────────────────────────────────────────────
// ADMIN — TEMPLATES TAB
// ─────────────────────────────────────────────────────────────────────────────

function AdminTemplatesTab() {
  const qc = useQueryClient();
  const [showCreate, setShowCreate]       = useState(false);
  const [editTemplate, setEditTemplate]   = useState<OnboardingTemplateDto | null>(null);
  const [deleteTemplate, setDeleteTemplate] = useState<OnboardingTemplateDto | null>(null);
  const [assignTemplate, setAssignTemplate] = useState<OnboardingTemplateDto | null>(null);

  const { data: templates = [], isLoading, refetch } = useQuery<OnboardingTemplateDto[]>({
    queryKey: ['onboarding-templates'],
    queryFn: () => apiFetch<OnboardingTemplateDto[]>('/api/onboarding/templates'),
  });

  const deleteMut = useMutation({
    mutationFn: (id: number) => apiFetch(`/api/onboarding/templates/${id}`, { method: 'DELETE' }),
    onSuccess: () => {
      toast.success('Template deleted');
      qc.invalidateQueries({ queryKey: ['onboarding-templates'] });
      setDeleteTemplate(null);
    },
    onError: (e: Error) => toast.error(e.message),
  });

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <Button variant="ghost" size="icon" onClick={() => refetch()} title="Refresh"><RefreshCw className="h-4 w-4" /></Button>
        <Button onClick={() => setShowCreate(true)}><Plus className="mr-2 h-4 w-4" /> New Template</Button>
      </div>

      <div className="border rounded-lg overflow-hidden">
        {isLoading ? <SkeletonTable columns={4} rows={4} /> : templates.length === 0 ? (
          <EmptyState
            icon={ClipboardList}
            title="No templates yet"
            description="Create a checklist template to assign to new employees."
          />
        ) : (
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Template Name</TableHead>
                <TableHead>Steps</TableHead>
                <TableHead>Status</TableHead>
                <TableHead>Created</TableHead>
                <TableHead className="w-[60px]" />
              </TableRow>
            </TableHeader>
            <TableBody>
              {templates.map((t) => {
                const steps = parseSteps(t.steps);
                return (
                  <TableRow key={t.id}>
                    <TableCell className="font-medium">{t.name}</TableCell>
                    <TableCell>{steps.length} step{steps.length !== 1 ? 's' : ''}</TableCell>
                    <TableCell>
                      <Badge variant={t.isActive ? 'default' : 'secondary'}>{t.isActive ? 'Active' : 'Inactive'}</Badge>
                    </TableCell>
                    <TableCell>{new Date(t.createdAt).toLocaleDateString()}</TableCell>
                    <TableCell>
                      <DropdownMenu>
                        <DropdownMenuTrigger asChild>
                          <Button variant="ghost" size="icon" className="h-8 w-8"><MoreHorizontal className="h-4 w-4" /></Button>
                        </DropdownMenuTrigger>
                        <DropdownMenuContent align="end">
                          <DropdownMenuItem onClick={() => setEditTemplate(t)}><Pencil className="mr-2 h-4 w-4" /> Edit</DropdownMenuItem>
                          <DropdownMenuItem onClick={() => setAssignTemplate(t)}><UserPlus className="mr-2 h-4 w-4" /> Assign to Employee</DropdownMenuItem>
                          <DropdownMenuItem className="text-destructive" onClick={() => setDeleteTemplate(t)}>
                            <Trash2 className="mr-2 h-4 w-4" /> Delete
                          </DropdownMenuItem>
                        </DropdownMenuContent>
                      </DropdownMenu>
                    </TableCell>
                  </TableRow>
                );
              })}
            </TableBody>
          </Table>
        )}
      </div>

      {(showCreate || editTemplate) && (
        <TemplateFormDialog
          template={editTemplate}
          onClose={() => { setShowCreate(false); setEditTemplate(null); }}
          onSaved={() => { setShowCreate(false); setEditTemplate(null); qc.invalidateQueries({ queryKey: ['onboarding-templates'] }); }}
        />
      )}

      {deleteTemplate && (
        <AlertDialog open onOpenChange={() => setDeleteTemplate(null)}>
          <AlertDialogContent>
            <AlertDialogHeader>
              <AlertDialogTitle>Delete Template</AlertDialogTitle>
              <AlertDialogDescription>
                Delete <strong>{deleteTemplate.name}</strong>? This cannot be undone.
              </AlertDialogDescription>
            </AlertDialogHeader>
            <AlertDialogFooter>
              <AlertDialogCancel>Cancel</AlertDialogCancel>
              <AlertDialogAction
                className="bg-destructive text-destructive-foreground hover:bg-destructive/90"
                onClick={() => deleteMut.mutate(deleteTemplate.id)}
                disabled={deleteMut.isPending}
              >
                {deleteMut.isPending ? 'Deleting…' : 'Delete'}
              </AlertDialogAction>
            </AlertDialogFooter>
          </AlertDialogContent>
        </AlertDialog>
      )}

      {assignTemplate && (
        <AssignDialog
          template={assignTemplate}
          onClose={() => setAssignTemplate(null)}
          onAssigned={() => setAssignTemplate(null)}
        />
      )}
    </div>
  );
}

// ─── Template Form Dialog ─────────────────────────────────────────────────────

function TemplateFormDialog({
  template, onClose, onSaved,
}: { template: OnboardingTemplateDto | null; onClose: () => void; onSaved: () => void }) {
  const existingSteps = template ? parseSteps(template.steps) : [];

  const form = useForm<TemplateFormValues>({
    resolver: zodResolver(templateSchema),
    defaultValues: {
      name:  template?.name ?? '',
      steps: existingSteps.length > 0 ? existingSteps : [{ title: '', description: '' }],
    },
  });

  const { fields, append, remove } = useFieldArray({ control: form.control, name: 'steps' });

  const mut = useMutation({
    mutationFn: (values: TemplateFormValues) => {
      const payload = { name: values.name, steps: JSON.stringify(values.steps) };
      const url    = template ? `/api/onboarding/templates/${template.id}` : '/api/onboarding/templates';
      const method = template ? 'PUT' : 'POST';
      return apiFetch(url, { method, body: JSON.stringify(payload) });
    },
    onSuccess: () => { toast.success(template ? 'Template updated' : 'Template created'); onSaved(); },
    onError: (e: Error) => toast.error(e.message),
  });

  return (
    <Dialog open onOpenChange={onClose}>
      <DialogContent className="max-w-2xl max-h-[90vh] overflow-y-auto">
        <DialogHeader><DialogTitle>{template ? 'Edit Template' : 'New Template'}</DialogTitle></DialogHeader>
        <Form {...form}>
          <form onSubmit={form.handleSubmit((v) => mut.mutate(v))} className="space-y-5">
            <FormField control={form.control} name="name" render={({ field }) => (
              <FormItem>
                <FormLabel>Template Name *</FormLabel>
                <FormControl><Input placeholder="e.g. IT Onboarding Checklist" {...field} /></FormControl>
                <FormMessage />
              </FormItem>
            )} />

            <div className="space-y-3">
              <div className="flex items-center justify-between">
                <FormLabel className="text-sm font-medium">Steps *</FormLabel>
                <Button type="button" size="sm" variant="outline" onClick={() => append({ title: '', description: '' })}>
                  <Plus className="mr-1 h-3 w-3" /> Add Step
                </Button>
              </div>

              {fields.map((field, idx) => (
                <Card key={field.id} className="p-4">
                  <div className="flex items-start gap-3">
                    <span className="text-sm font-medium text-muted-foreground pt-2 w-6 shrink-0">{idx + 1}.</span>
                    <div className="flex-1 space-y-2">
                      <FormField control={form.control} name={`steps.${idx}.title`} render={({ field: f }) => (
                        <FormItem>
                          <FormControl><Input placeholder="Step title *" {...f} /></FormControl>
                          <FormMessage />
                        </FormItem>
                      )} />
                      <FormField control={form.control} name={`steps.${idx}.description`} render={({ field: f }) => (
                        <FormItem>
                          <FormControl><Input placeholder="Description (optional)" {...f} /></FormControl>
                          <FormMessage />
                        </FormItem>
                      )} />
                    </div>
                    {fields.length > 1 && (
                      <Button type="button" variant="ghost" size="icon" className="h-8 w-8 mt-1 text-destructive hover:text-destructive" onClick={() => remove(idx)}>
                        <Trash2 className="h-4 w-4" />
                      </Button>
                    )}
                  </div>
                </Card>
              ))}
              {form.formState.errors.steps?.root && (
                <p className="text-sm text-destructive">{form.formState.errors.steps.root.message}</p>
              )}
            </div>

            <DialogFooter>
              <Button type="button" variant="outline" onClick={onClose}>Cancel</Button>
              <Button type="submit" disabled={mut.isPending}>
                {mut.isPending ? 'Saving…' : template ? 'Save Changes' : 'Create Template'}
              </Button>
            </DialogFooter>
          </form>
        </Form>
      </DialogContent>
    </Dialog>
  );
}

// ─── Assign Dialog ────────────────────────────────────────────────────────────

function AssignDialog({
  template, onClose, onAssigned,
}: { template: OnboardingTemplateDto; onClose: () => void; onAssigned: () => void }) {
  const form = useForm<AssignFormValues>({
    resolver: zodResolver(assignSchema),
    defaultValues: { templateId: template.id, employeeId: '', dueDate: '' },
  });

  const mut = useMutation({
    mutationFn: (values: AssignFormValues) =>
      apiFetch('/api/onboarding/assign', { method: 'POST', body: JSON.stringify(values) }),
    onSuccess: () => { toast.success('Onboarding assigned to employee'); onAssigned(); },
    onError: (e: Error) => toast.error(e.message),
  });

  return (
    <Dialog open onOpenChange={onClose}>
      <DialogContent className="max-w-sm">
        <DialogHeader>
          <DialogTitle>Assign: {template.name}</DialogTitle>
        </DialogHeader>
        <Form {...form}>
          <form onSubmit={form.handleSubmit((v) => mut.mutate(v))} className="space-y-4">
            <FormField control={form.control} name="employeeId" render={({ field }) => (
              <FormItem>
                <FormLabel>Employee ID *</FormLabel>
                <FormControl><Input placeholder="EMP-001" {...field} /></FormControl>
                <FormMessage />
              </FormItem>
            )} />
            <FormField control={form.control} name="dueDate" render={({ field }) => (
              <FormItem>
                <FormLabel>Due Date (optional)</FormLabel>
                <FormControl><Input type="date" {...field} /></FormControl>
                <FormMessage />
              </FormItem>
            )} />
            <DialogFooter>
              <Button type="button" variant="outline" onClick={onClose}>Cancel</Button>
              <Button type="submit" disabled={mut.isPending}>
                {mut.isPending ? 'Assigning…' : 'Assign'}
              </Button>
            </DialogFooter>
          </form>
        </Form>
      </DialogContent>
    </Dialog>
  );
}

// ─────────────────────────────────────────────────────────────────────────────
// PAGE
// ─────────────────────────────────────────────────────────────────────────────

export default function OnboardingPage() {
  const { isAuthenticated } = useAuth();
  // BUGFIX: previously compared profile.role against capitalized 'Admin'/'SuperAdmin',
  // but the backend returns lowercase role strings ("admin", "superadmin" - see
  // AppRoles.cs), so this check never matched and admins never saw the Templates tab.
  // usePermissions() already normalizes the role (lowercase, trimmed) correctly.
  const { isAdmin, isLoading: permissionsLoading } = usePermissions();
  void isAuthenticated;

  return (
    <div className="space-y-6">
      <PageHeader
        title="Onboarding"
        description={isAdmin ? 'Manage onboarding templates and assign them to employees.' : 'Complete your onboarding checklist.'}
        actions={isAdmin ? <Settings className="h-6 w-6 text-muted-foreground" /> : <ClipboardList className="h-6 w-6 text-muted-foreground" />}
      />

      {permissionsLoading ? null : isAdmin ? (
        <Tabs defaultValue="templates">
          <TabsList>
            <TabsTrigger value="templates">Templates</TabsTrigger>
            <TabsTrigger value="my">My Onboarding</TabsTrigger>
          </TabsList>
          <TabsContent value="templates" className="mt-4"><AdminTemplatesTab /></TabsContent>
          <TabsContent value="my"        className="mt-4"><EmployeeTab /></TabsContent>
        </Tabs>
      ) : (
        <EmployeeTab />
      )}
    </div>
  );
}
