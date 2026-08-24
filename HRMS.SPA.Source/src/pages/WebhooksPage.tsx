// Webhook Management — list, register, and delete webhook subscriptions.
//
// BUG FIX: the Sidebar's "Webhooks" nav item linked to a static /webhooks.html
// file that never existed anywhere in the codebase (no public/webhooks.html,
// no React route) despite WebhookController.cs fully implementing list/
// register/delete/event-discovery. This page provides the missing frontend
// for that backend.
import { useState } from 'react';
import { Plus, Trash2 } from 'lucide-react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import * as z from 'zod';
import { toast } from 'sonner';

import { PageHeader } from '@/components/layout/PageHeader';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import {
  Table, TableBody, TableCell, TableHead, TableHeader, TableRow,
} from '@/components/ui/table';
import { Input } from '@/components/ui/input';
import {
  Select, SelectContent, SelectItem, SelectTrigger, SelectValue,
} from '@/components/ui/select';
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter, DialogDescription } from '@/components/ui/dialog';
import { AlertDialog, AlertDialogAction, AlertDialogCancel, AlertDialogContent, AlertDialogDescription, AlertDialogFooter, AlertDialogHeader, AlertDialogTitle } from '@/components/ui/alert-dialog';
import { Form, FormControl, FormField, FormItem, FormLabel, FormMessage } from '@/components/ui/form';
import { SkeletonTable } from '@/components/shared/SkeletonTable';
import { EmptyState } from '@/components/shared/EmptyState';
import { csrfFetch } from '@/utils/csrfFetch';

const BASE = import.meta.env.BASE_URL?.replace(/\/$/, '') ?? '';

interface WebhookSubscription {
  id: number;
  companyId?: number | null;
  eventType: string;
  targetUrl: string;
  isActive: boolean;
  createdAt: string;
}

const api = {
  list: async (): Promise<WebhookSubscription[]> => {
    const r = await csrfFetch(`${BASE}/api/webhooks`, { credentials: 'include' });
    if (!r.ok) throw new Error('Failed to load webhooks.');
    const body = await r.json();
    return body.data ?? body;
  },
  eventTypes: async (): Promise<string[]> => {
    const r = await csrfFetch(`${BASE}/api/webhooks/events`, { credentials: 'include' });
    if (!r.ok) throw new Error('Failed to load event types.');
    const body = await r.json();
    return body.data ?? body;
  },
  register: (body: { eventType: string; targetUrl: string }) =>
    csrfFetch(`${BASE}/api/webhooks`, {
      method: 'POST', credentials: 'include',
      headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body),
    }),
  remove: (id: number) =>
    csrfFetch(`${BASE}/api/webhooks/${id}`, { method: 'DELETE', credentials: 'include' }),
};

const registerSchema = z.object({
  eventType: z.string().min(1, 'Event type is required.'),
  targetUrl: z.string().url('Must be a valid URL.'),
});
type RegisterForm = z.infer<typeof registerSchema>;

export default function WebhookManagementPage() {
  const qc = useQueryClient();
  const [createOpen, setCreateOpen] = useState(false);
  const [deleteId, setDeleteId] = useState<number | null>(null);

  const { data: webhooks, isLoading, isError } = useQuery({
    queryKey: ['/api/webhooks'],
    queryFn: api.list,
  });
  const { data: eventTypes } = useQuery({
    queryKey: ['/api/webhooks/events'],
    queryFn: api.eventTypes,
  });

  const registerForm = useForm<RegisterForm>({
    resolver: zodResolver(registerSchema),
    defaultValues: { eventType: '', targetUrl: '' },
  });
  const registerMutation = useMutation({
    mutationFn: (values: RegisterForm) => api.register(values),
    onSuccess: async (res) => {
      if (!res.ok) {
        const body = await res.json().catch(() => ({}));
        throw new Error(body.message ?? 'Failed to register webhook.');
      }
      toast.success('Webhook registered.');
      qc.invalidateQueries({ queryKey: ['/api/webhooks'] });
      setCreateOpen(false);
      registerForm.reset({ eventType: '', targetUrl: '' });
    },
    onError: (e: Error) => toast.error(e.message || 'Failed to register webhook.'),
  });

  const deleteMutation = useMutation({
    mutationFn: (id: number) => api.remove(id),
    onSuccess: (res) => {
      if (!res.ok) throw new Error('Failed to remove webhook.');
      toast.success('Webhook removed.');
      qc.invalidateQueries({ queryKey: ['/api/webhooks'] });
      setDeleteId(null);
    },
    onError: () => toast.error('Failed to remove webhook.'),
  });

  return (
    <div className="space-y-6">
      <PageHeader
        title="Webhooks"
        description="Subscribe external endpoints to HRMS event notifications."
        actions={
          <Button onClick={() => setCreateOpen(true)}>
            <Plus className="mr-2 h-4 w-4" /> Register Webhook
          </Button>
        }
      />

      <Card>
        <CardContent className="p-0">
          {isLoading ? (
            <SkeletonTable columns={4} rows={5} />
          ) : isError ? (
            <EmptyState title="Failed to load webhooks" description="Please try again later." />
          ) : !webhooks?.length ? (
            <EmptyState
              title="No webhooks registered"
              description="Register a webhook to receive real-time event notifications."
              action={<Button onClick={() => setCreateOpen(true)}><Plus className="mr-2 h-4 w-4" /> Register Webhook</Button>}
            />
          ) : (
            <Table>
              <TableHeader>
                <TableRow className="bg-muted/50">
                  <TableHead>Event Type</TableHead>
                  <TableHead>Target URL</TableHead>
                  <TableHead>Status</TableHead>
                  <TableHead className="text-right">Actions</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {webhooks.map((wh) => (
                  <TableRow key={wh.id}>
                    <TableCell><code className="text-xs bg-muted px-1.5 py-0.5 rounded">{wh.eventType}</code></TableCell>
                    <TableCell className="font-mono text-xs truncate max-w-xs">{wh.targetUrl}</TableCell>
                    <TableCell>
                      <Badge variant={wh.isActive ? 'default' : 'outline'}>{wh.isActive ? 'Active' : 'Inactive'}</Badge>
                    </TableCell>
                    <TableCell className="text-right">
                      <Button variant="ghost" size="icon" className="text-destructive" onClick={() => setDeleteId(wh.id)}>
                        <Trash2 className="h-4 w-4" />
                      </Button>
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          )}
        </CardContent>
      </Card>

      {/* Register dialog */}
      <Dialog open={createOpen} onOpenChange={setCreateOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Register Webhook</DialogTitle>
            <DialogDescription>Subscribe a URL to receive HRMS event notifications via HTTP POST.</DialogDescription>
          </DialogHeader>
          <Form {...registerForm}>
            <form onSubmit={registerForm.handleSubmit((v) => registerMutation.mutate(v))} className="space-y-4">
              <FormField control={registerForm.control} name="eventType" render={({ field }) => (
                <FormItem>
                  <FormLabel>Event Type</FormLabel>
                  <Select value={field.value} onValueChange={field.onChange}>
                    <FormControl>
                      <SelectTrigger><SelectValue placeholder="Select an event type" /></SelectTrigger>
                    </FormControl>
                    <SelectContent className="max-h-64">
                      {(eventTypes ?? []).map((et) => (
                        <SelectItem key={et} value={et}>{et}</SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                  <FormMessage />
                </FormItem>
              )} />
              <FormField control={registerForm.control} name="targetUrl" render={({ field }) => (
                <FormItem>
                  <FormLabel>Target URL</FormLabel>
                  <FormControl><Input {...field} placeholder="https://example.com/webhooks/hrms" /></FormControl>
                  <FormMessage />
                </FormItem>
              )} />
              <DialogFooter>
                <Button variant="outline" type="button" onClick={() => setCreateOpen(false)}>Cancel</Button>
                <Button type="submit" disabled={registerMutation.isPending}>{registerMutation.isPending ? 'Registering\u2026' : 'Register'}</Button>
              </DialogFooter>
            </form>
          </Form>
        </DialogContent>
      </Dialog>

      {/* Delete confirmation */}
      <AlertDialog open={deleteId !== null} onOpenChange={(open) => { if (!open) setDeleteId(null); }}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Remove webhook subscription?</AlertDialogTitle>
            <AlertDialogDescription>This endpoint will stop receiving event notifications. This action cannot be undone.</AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>Cancel</AlertDialogCancel>
            <AlertDialogAction
              className="bg-destructive text-destructive-foreground hover:bg-destructive/90"
              onClick={() => deleteId !== null && deleteMutation.mutate(deleteId)}
            >
              Remove
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </div>
  );
}
