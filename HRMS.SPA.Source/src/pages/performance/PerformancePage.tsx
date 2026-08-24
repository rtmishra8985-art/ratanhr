import { useState } from 'react';
import { Star } from 'lucide-react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import * as z from 'zod';
import { toast } from 'sonner';
import { useListGoals, useListReviews } from '@workspace/api-client-react';

import { PageHeader } from '@/components/layout/PageHeader';
import { Button } from '@/components/ui/button';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { SkeletonTable } from '@/components/shared/SkeletonTable';
import { Pagination } from '@/components/shared/Pagination';
import { StatusBadge } from '@/components/shared/StatusBadge';
import { Progress } from '@/components/ui/progress';
import { Input } from '@/components/ui/input';
import { Skeleton } from '@/components/ui/skeleton';
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter, DialogDescription } from '@/components/ui/dialog';
import { Form, FormControl, FormField, FormItem, FormLabel, FormMessage } from '@/components/ui/form';
import { usePaginationState } from '@/hooks/usePaginationState';
import { csrfFetch } from '@/utils/csrfFetch';

const BASE = import.meta.env.BASE_URL?.replace(/\/$/, '') ?? '';

interface ReviewDetail {
  id: number;
  employeeId: string;
  cycleName?: string | null;
  reviewType: string;
  status: string;
  selfRating?: number | null;
  managerRating?: number | null;
  finalRating?: number | null;
  selfComments: string;
  managerComments: string;
  hrComments: string;
  overallComments: string;
}

const api = {
  updateGoalProgress: (id: string, achievedValue: number) =>
    csrfFetch(`${BASE}/api/performance/goals/${id}/progress`, {
      method: 'PATCH', credentials: 'include',
      headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ achievedValue }),
    }),
  getReview: async (id: string): Promise<ReviewDetail> => {
    const r = await csrfFetch(`${BASE}/api/performance/reviews/${id}`, { credentials: 'include' });
    if (!r.ok) throw new Error('Failed to load review details.');
    const body = await r.json();
    return body.data ?? body;
  },
};

const progressSchema = z.object({
  achievedValue: z.coerce.number().min(0, 'Value must be non-negative.'),
});
type ProgressForm = z.infer<typeof progressSchema>;

export default function PerformancePage() {
  const qc = useQueryClient();
  const { pageSize } = usePaginationState();
  const { page: goalPage, setPage: setGoalPage } = usePaginationState();

  const { data: goals, isLoading: loadingGoals } = useListGoals({ page: goalPage, pageSize });
  const { data: reviews, isLoading: loadingReviews } = useListReviews({ page: 1, pageSize: 5 });

  const [updateGoalId, setUpdateGoalId] = useState<string | null>(null);
  const [viewReviewId, setViewReviewId] = useState<string | null>(null);

  const progressForm = useForm<ProgressForm>({
    resolver: zodResolver(progressSchema),
    defaultValues: { achievedValue: 0 },
  });
  const progressMutation = useMutation({
    mutationFn: (values: ProgressForm) => {
      if (!updateGoalId) throw new Error('No goal selected.');
      return api.updateGoalProgress(updateGoalId, values.achievedValue);
    },
    onSuccess: async (res) => {
      if (!res.ok) {
        const body = await res.json().catch(() => ({}));
        throw new Error(body.message ?? 'Failed to update progress.');
      }
      toast.success('Progress updated.');
      qc.invalidateQueries({ queryKey: ['/api/performance/goals'] });
      setUpdateGoalId(null);
      progressForm.reset({ achievedValue: 0 });
    },
    onError: (e: Error) => toast.error(e.message || 'Failed to update progress.'),
  });

  const { data: reviewDetail, isLoading: loadingReviewDetail } = useQuery({
    queryKey: ['/api/performance/reviews', viewReviewId],
    queryFn: () => api.getReview(viewReviewId!),
    enabled: viewReviewId !== null,
  });

  return (
    <div className="space-y-6">
      <PageHeader
        title="Performance"
        description="Track OKRs, reviews, and continuous feedback."
      />

      <Tabs defaultValue="goals" className="w-full">
        <TabsList className="grid w-full sm:w-[400px] grid-cols-3">
          <TabsTrigger value="goals">Goals</TabsTrigger>
          <TabsTrigger value="reviews">Reviews</TabsTrigger>
          <TabsTrigger value="cycles">Cycles</TabsTrigger>
        </TabsList>

        <TabsContent value="goals" className="space-y-6 mt-6">
          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
            {loadingGoals
              ? Array.from({ length: 6 }).map((_, i) => (
                  <Card key={i} className="h-48 animate-pulse bg-muted" />
                ))
              : goals?.items.map((goal) => (
                  <Card key={goal.id} className="flex flex-col">
                    <CardHeader className="pb-2">
                      <div className="flex justify-between items-start">
                        <CardTitle className="text-base leading-tight">{goal.title}</CardTitle>
                        <StatusBadge status={goal.status} className="ml-2 shrink-0" />
                      </div>
                      <div className="text-sm text-muted-foreground">
                        Assigned to:{' '}
                        <span className="font-medium text-foreground">{goal.employeeName}</span>
                      </div>
                    </CardHeader>
                    <CardContent className="mt-auto pt-4">
                      <div className="flex justify-between text-sm mb-2">
                        <span className="text-muted-foreground">Progress</span>
                        <span className="font-medium">{goal.progress ?? 0}%</span>
                      </div>
                      <Progress value={goal.progress ?? 0} className="h-2" />
                      <div className="flex justify-between text-xs text-muted-foreground mt-4 pt-4 border-t">
                        <span>
                          Due: {goal.dueDate ? new Date(goal.dueDate).toLocaleDateString() : 'N/A'}
                        </span>
                        <Button variant="link" className="h-auto p-0 text-xs" onClick={() => { setUpdateGoalId(goal.id); progressForm.reset({ achievedValue: 0 }); }}>
                          Update
                        </Button>
                      </div>
                    </CardContent>
                  </Card>
                ))}
          </div>
          {goals && (
            <Pagination
              page={goals.page}
              pageSize={goals.pageSize}
              totalCount={goals.totalCount}
              totalPages={goals.totalPages}
              onPageChange={setGoalPage}
            />
          )}
        </TabsContent>

        <TabsContent value="reviews" className="space-y-6 mt-6">
          <div className="space-y-4">
            {loadingReviews ? (
              <SkeletonTable columns={5} rows={5} />
            ) : reviews?.items.map((review) => (
              <Card key={review.id}>
                <CardContent className="p-6 flex flex-col md:flex-row gap-6 md:items-center justify-between">
                  <div className="flex items-center gap-4">
                    <div className="h-12 w-12 rounded-full bg-primary/10 flex items-center justify-center text-primary">
                      <Star className="h-6 w-6" />
                    </div>
                    <div>
                      <h4 className="font-semibold text-lg">{review.employeeName}</h4>
                      <p className="text-sm text-muted-foreground">{review.cycleName}</p>
                    </div>
                  </div>

                  <div className="grid grid-cols-2 md:grid-cols-4 gap-6 flex-1 max-w-2xl">
                    <div className="flex flex-col">
                      <span className="text-xs text-muted-foreground">Self Rating</span>
                      <span className="font-semibold text-lg">{review.selfRating ?? '-'} / 5</span>
                    </div>
                    <div className="flex flex-col">
                      <span className="text-xs text-muted-foreground">Manager Rating</span>
                      <span className="font-semibold text-lg">{review.managerRating ?? '-'} / 5</span>
                    </div>
                    <div className="flex flex-col">
                      <span className="text-xs text-muted-foreground">Final Rating</span>
                      <span className="font-semibold text-lg text-primary">
                        {review.finalRating ?? '-'} / 5
                      </span>
                    </div>
                    <div className="flex flex-col justify-center">
                      <StatusBadge status={review.status} />
                    </div>
                  </div>

                  <Button variant="outline" onClick={() => setViewReviewId(review.id)}>View Detail</Button>
                </CardContent>
              </Card>
            ))}
          </div>
        </TabsContent>

        <TabsContent value="cycles" className="mt-6">
          <div className="bg-card border p-8 rounded-lg text-center text-muted-foreground">
            Cycle management features available in next release.
          </div>
        </TabsContent>
      </Tabs>

      {/* Update goal progress dialog */}
      <Dialog open={updateGoalId !== null} onOpenChange={(open) => { if (!open) setUpdateGoalId(null); }}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Update Progress</DialogTitle>
            <DialogDescription>Enter the achieved value for this goal.</DialogDescription>
          </DialogHeader>
          <Form {...progressForm}>
            <form onSubmit={progressForm.handleSubmit((v) => progressMutation.mutate(v))} className="space-y-4">
              <FormField control={progressForm.control} name="achievedValue" render={({ field }) => (
                <FormItem><FormLabel>Achieved Value</FormLabel><FormControl><Input type="number" step="0.01" {...field} /></FormControl><FormMessage /></FormItem>
              )} />
              <DialogFooter>
                <Button variant="outline" type="button" onClick={() => setUpdateGoalId(null)}>Cancel</Button>
                <Button type="submit" disabled={progressMutation.isPending}>{progressMutation.isPending ? 'Updating…' : 'Update'}</Button>
              </DialogFooter>
            </form>
          </Form>
        </DialogContent>
      </Dialog>

      {/* View review detail dialog */}
      <Dialog open={viewReviewId !== null} onOpenChange={(open) => { if (!open) setViewReviewId(null); }}>
        <DialogContent className="max-w-lg">
          <DialogHeader>
            <DialogTitle>Review Detail</DialogTitle>
          </DialogHeader>
          {loadingReviewDetail ? (
            <Skeleton className="h-32 w-full" />
          ) : reviewDetail ? (
            <div className="space-y-3 text-sm">
              <div className="grid grid-cols-2 gap-2">
                <div><span className="text-muted-foreground">Type:</span> {reviewDetail.reviewType}</div>
                <div><span className="text-muted-foreground">Status:</span> <StatusBadge status={reviewDetail.status} /></div>
                <div><span className="text-muted-foreground">Self Rating:</span> {reviewDetail.selfRating ?? '—'} / 5</div>
                <div><span className="text-muted-foreground">Manager Rating:</span> {reviewDetail.managerRating ?? '—'} / 5</div>
                <div><span className="text-muted-foreground">Final Rating:</span> {reviewDetail.finalRating ?? '—'} / 5</div>
              </div>
              {reviewDetail.selfComments && (
                <div><h4 className="font-medium">Self Comments</h4><p className="text-muted-foreground">{reviewDetail.selfComments}</p></div>
              )}
              {reviewDetail.managerComments && (
                <div><h4 className="font-medium">Manager Comments</h4><p className="text-muted-foreground">{reviewDetail.managerComments}</p></div>
              )}
              {reviewDetail.hrComments && (
                <div><h4 className="font-medium">HR Comments</h4><p className="text-muted-foreground">{reviewDetail.hrComments}</p></div>
              )}
            </div>
          ) : (
            <p className="text-muted-foreground text-sm">Unable to load review details.</p>
          )}
          <DialogFooter>
            <Button variant="outline" onClick={() => setViewReviewId(null)}>Close</Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
