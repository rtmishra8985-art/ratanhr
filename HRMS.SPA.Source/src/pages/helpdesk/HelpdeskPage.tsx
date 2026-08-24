// Helpdesk — New Ticket + ticket detail (comments) wired to HelpdeskController.
// Previously "New Ticket" and the row-level "Open" button were dead (no onClick)
// despite the backend fully supporting create/comment/assign. Follows the same
// self-contained csrfFetch + react-query pattern used by DepartmentPage.tsx /
// AssetsPage.tsx.
import { useState } from 'react';
import { Clock, CheckCircle2, AlertCircle, Plus, Send } from 'lucide-react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import * as z from 'zod';
import { toast } from 'sonner';
import { useListTickets, useGetHelpdeskSummary } from '@workspace/api-client-react';

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
import { StatusBadge, PriorityBadge } from '@/components/shared/StatusBadge';
import { EmptyState } from '@/components/shared/EmptyState';
import { Skeleton } from '@/components/ui/skeleton';
import { Input } from '@/components/ui/input';
import { Textarea } from '@/components/ui/textarea';
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter, DialogDescription } from '@/components/ui/dialog';
import { Form, FormControl, FormField, FormItem, FormLabel, FormMessage } from '@/components/ui/form';
import { usePaginationState } from '@/hooks/usePaginationState';
import { getErrorTitle, getErrorDescription } from '@/utils/apiError';
import { csrfFetch } from '@/utils/csrfFetch';

const BASE = import.meta.env.BASE_URL?.replace(/\/$/, '') ?? '';

interface TicketComment {
  id: number;
  authorName?: string | null;
  message: string;
  isInternal: boolean;
  createdAt: string;
}

const api = {
  create: (body: { title: string; description?: string; priority: string; categoryId?: number }) =>
    csrfFetch(`${BASE}/api/helpdesk/tickets`, {
      method: 'POST', credentials: 'include',
      headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body),
    }),
  comments: async (id: number): Promise<TicketComment[]> => {
    const r = await csrfFetch(`${BASE}/api/helpdesk/tickets/${id}/comments`, { credentials: 'include' });
    if (!r.ok) throw new Error('Failed to load comments.');
    return r.json();
  },
  addComment: (id: number, message: string) =>
    csrfFetch(`${BASE}/api/helpdesk/tickets/${id}/comments`, {
      method: 'POST', credentials: 'include',
      headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ message, isInternal: false }),
    }),
};

const createTicketSchema = z.object({
  title: z.string().min(1, 'Title is required.').max(300),
  description: z.string().max(5000).optional(),
  priority: z.enum(['Low', 'Medium', 'High', 'Critical']),
});
type CreateTicketForm = z.infer<typeof createTicketSchema>;

const commentSchema = z.object({
  message: z.string().min(1, 'Comment cannot be empty.').max(5000),
});
type CommentForm = z.infer<typeof commentSchema>;

export default function HelpdeskPage() {
  const qc = useQueryClient();
  const { page, setPage, pageSize } = usePaginationState();

  const { data: summary, isLoading: loadingSummary } = useGetHelpdeskSummary();
  const { data: tickets, isLoading: loadingTickets, isError, error, refetch } = useListTickets({ page, pageSize });

  const [createOpen, setCreateOpen] = useState(false);
  const [openTicketId, setOpenTicketId] = useState<number | null>(null);

  const createForm = useForm<CreateTicketForm>({
    resolver: zodResolver(createTicketSchema),
    defaultValues: { title: '', description: '', priority: 'Medium' },
  });
  const createMutation = useMutation({
    mutationFn: (values: CreateTicketForm) => api.create(values),
    onSuccess: async (res) => {
      if (!res.ok) {
        const body = await res.json().catch(() => ({}));
        throw new Error(body.message ?? 'Failed to create ticket.');
      }
      toast.success('Ticket created.');
      qc.invalidateQueries({ queryKey: ['/api/helpdesk/tickets'] });
      qc.invalidateQueries({ queryKey: ['/api/helpdesk/summary'] });
      setCreateOpen(false);
      createForm.reset({ title: '', description: '', priority: 'Medium' });
    },
    onError: (e: Error) => toast.error(e.message || 'Failed to create ticket.'),
  });

  const openTicket = tickets?.items.find((t) => t.id === openTicketId) ?? null;

  const { data: comments, isLoading: loadingComments } = useQuery({
    queryKey: ['/api/helpdesk/tickets', openTicketId, 'comments'],
    queryFn: () => api.comments(openTicketId!),
    enabled: openTicketId !== null,
  });

  const commentForm = useForm<CommentForm>({
    resolver: zodResolver(commentSchema),
    defaultValues: { message: '' },
  });
  const commentMutation = useMutation({
    mutationFn: (values: CommentForm) => {
      if (openTicketId === null) throw new Error('No ticket selected.');
      return api.addComment(openTicketId, values.message);
    },
    onSuccess: async (res) => {
      if (!res.ok) {
        const body = await res.json().catch(() => ({}));
        throw new Error(body.message ?? 'Failed to add comment.');
      }
      qc.invalidateQueries({ queryKey: ['/api/helpdesk/tickets', openTicketId, 'comments'] });
      commentForm.reset({ message: '' });
    },
    onError: (e: Error) => toast.error(e.message || 'Failed to add comment.'),
  });

  return (
    <div className="space-y-6">
      <PageHeader
        title="Helpdesk"
        description="Support tickets and IT service requests."
        actions={
          <Button onClick={() => setCreateOpen(true)}>
            <Plus className="mr-2 h-4 w-4" /> New Ticket
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
              <CardContent className="p-6 flex items-center justify-between">
                <div>
                  <div className="text-3xl font-bold text-yellow-600 dark:text-yellow-500">{summary.open}</div>
                  <div className="text-sm font-medium text-muted-foreground mt-1">Open Tickets</div>
                </div>
                <AlertCircle className="h-8 w-8 text-yellow-500/20" />
              </CardContent>
            </Card>
            <Card>
              <CardContent className="p-6 flex items-center justify-between">
                <div>
                  <div className="text-3xl font-bold text-blue-600 dark:text-blue-500">{summary.inProgress}</div>
                  <div className="text-sm font-medium text-muted-foreground mt-1">In Progress</div>
                </div>
                <Clock className="h-8 w-8 text-blue-500/20" />
              </CardContent>
            </Card>
            <Card>
              <CardContent className="p-6 flex items-center justify-between">
                <div>
                  <div className="text-3xl font-bold text-green-600 dark:text-green-500">{summary.resolved}</div>
                  <div className="text-sm font-medium text-muted-foreground mt-1">Resolved</div>
                </div>
                <CheckCircle2 className="h-8 w-8 text-green-500/20" />
              </CardContent>
            </Card>
            <Card>
              <CardContent className="p-6 flex items-center justify-between">
                <div>
                  <div className="text-3xl font-bold text-red-600 dark:text-red-500">{summary.critical}</div>
                  <div className="text-sm font-medium text-muted-foreground mt-1">Critical Priority</div>
                </div>
                <AlertCircle className="h-8 w-8 text-red-500/20" />
              </CardContent>
            </Card>
          </>
        ) : null}
      </div>

      <div className="bg-card border rounded-lg shadow-sm overflow-hidden">
        {loadingTickets ? (
          <SkeletonTable columns={7} rows={10} />
        ) : isError ? (

          <EmptyState
            title={getErrorTitle(error, 'Failed to load tickets')}
            description={getErrorDescription(error)}
            onRetry={refetch}
          />
        ) : !tickets?.items?.length ? (
          <EmptyState
            title="No tickets found"
            description="No support tickets have been raised yet."
            action={
              <Button onClick={() => setCreateOpen(true)}>
                <Plus className="mr-2 h-4 w-4" /> New Ticket
              </Button>
            }
          />
        ) : (
          <>
            <Table>
              <TableHeader>
                <TableRow className="bg-muted/50">
                  <TableHead className="w-[100px]">Ticket ID</TableHead>
                  <TableHead>Subject</TableHead>
                  <TableHead>Requester</TableHead>
                  <TableHead>Priority</TableHead>
                  <TableHead>Status</TableHead>
                  <TableHead>Created</TableHead>
                  <TableHead className="text-right">Actions</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {tickets.items.map((ticket) => (
                  <TableRow
                    key={ticket.id}
                    className="cursor-pointer hover:bg-muted/50"
                    onClick={() => setOpenTicketId(ticket.id)}
                  >
                    {/* Previous fix: safe null check on ticket.id */}
                    <TableCell className="font-mono text-xs text-muted-foreground">
                      #TKT-{ticket.id != null ? String(ticket.id).padStart(4, '0') : '0000'}
                    </TableCell>
                    <TableCell>
                      <div className="font-medium line-clamp-1">{ticket.title}</div>
                      <div className="text-xs text-muted-foreground">{ticket.categoryName}</div>
                    </TableCell>
                    <TableCell>{ticket.raisedByName || 'System'}</TableCell>
                    <TableCell>
                      <PriorityBadge priority={ticket.priority} />
                    </TableCell>
                    <TableCell>
                      <StatusBadge status={ticket.status} />
                    </TableCell>
                    <TableCell className="text-sm">
                      {ticket.createdAt ? new Date(ticket.createdAt).toLocaleDateString() : '-'}
                    </TableCell>
                    <TableCell className="text-right">
                      <Button size="sm" variant="ghost" onClick={(e) => { e.stopPropagation(); setOpenTicketId(ticket.id); }}>
                        Open
                      </Button>
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
            <Pagination
              page={tickets.page}
              pageSize={tickets.pageSize}
              totalCount={tickets.totalCount}
              totalPages={tickets.totalPages}
              onPageChange={setPage}
            />
          </>
        )}
      </div>

      {/* New Ticket dialog */}
      <Dialog open={createOpen} onOpenChange={setCreateOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>New Ticket</DialogTitle>
            <DialogDescription>Raise a support or IT service request.</DialogDescription>
          </DialogHeader>
          <Form {...createForm}>
            <form onSubmit={createForm.handleSubmit((v) => createMutation.mutate(v))} className="space-y-4">
              <FormField control={createForm.control} name="title" render={({ field }) => (
                <FormItem><FormLabel>Subject</FormLabel><FormControl><Input {...field} placeholder="Laptop not booting" /></FormControl><FormMessage /></FormItem>
              )} />
              <FormField control={createForm.control} name="description" render={({ field }) => (
                <FormItem><FormLabel>Description (optional)</FormLabel><FormControl><Textarea {...field} rows={4} /></FormControl><FormMessage /></FormItem>
              )} />
              <DialogFooter>
                <Button variant="outline" type="button" onClick={() => setCreateOpen(false)}>Cancel</Button>
                <Button type="submit" disabled={createMutation.isPending}>{createMutation.isPending ? 'Submitting…' : 'Submit Ticket'}</Button>
              </DialogFooter>
            </form>
          </Form>
        </DialogContent>
      </Dialog>

      {/* Ticket detail / comments dialog */}
      <Dialog open={openTicketId !== null} onOpenChange={(open) => { if (!open) setOpenTicketId(null); }}>
        <DialogContent className="max-w-lg">
          <DialogHeader>
            <DialogTitle>{openTicket?.title ?? `Ticket #TKT-${String(openTicketId ?? 0).padStart(4, '0')}`}</DialogTitle>
            {openTicket && (
              <DialogDescription className="flex items-center gap-2">
                <StatusBadge status={openTicket.status} />
                <PriorityBadge priority={openTicket.priority} />
              </DialogDescription>
            )}
          </DialogHeader>

          <div className="space-y-3 max-h-64 overflow-auto">
            {loadingComments ? (
              <Skeleton className="h-16 w-full" />
            ) : !comments?.length ? (
              <p className="text-muted-foreground text-sm">No comments yet.</p>
            ) : (
              comments.map((c) => (
                <div key={c.id} className="border rounded-md p-2 text-sm">
                  <div className="flex items-center justify-between">
                    <span className="font-medium">{c.authorName ?? 'Unknown'}</span>
                    <span className="text-xs text-muted-foreground">{new Date(c.createdAt).toLocaleString()}</span>
                  </div>
                  <p className="mt-1">{c.message}</p>
                </div>
              ))
            )}
          </div>

          <Form {...commentForm}>
            <form onSubmit={commentForm.handleSubmit((v) => commentMutation.mutate(v))} className="flex items-end gap-2 pt-2 border-t">
              <FormField control={commentForm.control} name="message" render={({ field }) => (
                <FormItem className="flex-1"><FormControl><Textarea {...field} rows={2} placeholder="Add a comment…" /></FormControl><FormMessage /></FormItem>
              )} />
              <Button type="submit" size="icon" disabled={commentMutation.isPending}>
                <Send className="h-4 w-4" />
              </Button>
            </form>
          </Form>

          <DialogFooter>
            <Button variant="outline" onClick={() => setOpenTicketId(null)}>Close</Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
