import { useState } from 'react';
import { Plus, Users } from 'lucide-react';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import * as z from 'zod';
import { toast } from 'sonner';
import { useListRequisitions, useListCandidates, useGetRecruitmentPipeline } from '@workspace/api-client-react';

import { PageHeader } from '@/components/layout/PageHeader';
import { Button } from '@/components/ui/button';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
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
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter, DialogDescription } from '@/components/ui/dialog';
import { Form, FormControl, FormField, FormItem, FormLabel, FormMessage } from '@/components/ui/form';
import { usePaginationState } from '@/hooks/usePaginationState';
import { csrfFetch } from '@/utils/csrfFetch';

const BASE = import.meta.env.BASE_URL?.replace(/\/$/, '') ?? '';

// ─── API helpers (backend field names per RequisitionDto.cs / CandidateDto.cs) ─

const api = {
  createRequisition: (body: {
    title: string; departmentName: string; description: string; openingsCount: number;
    experienceRequired: string; skillsRequired: string; jobType: string; location: string;
  }) =>
    csrfFetch(`${BASE}/api/recruitment/requisitions`, {
      method: 'POST', credentials: 'include',
      headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body),
    }),
  createCandidate: (body: {
    firstName: string; lastName: string; email: string; phone: string;
    currentDesignation: string; currentCompany: string; totalExperience: number; skills: string;
  }) => {
    const form = new FormData();
    Object.entries(body).forEach(([k, v]) => form.append(k, String(v)));
    return csrfFetch(`${BASE}/api/recruitment/candidates`, {
      method: 'POST', credentials: 'include', body: form,
    });
  },
};

const requisitionSchema = z.object({
  title: z.string().min(1, 'Job title is required.').max(200),
  departmentName: z.string().min(1, 'Department is required.').max(150),
  description: z.string().max(2000).optional().default(''),
  openingsCount: z.coerce.number().int().min(1, 'At least 1 opening is required.'),
  experienceRequired: z.string().max(100).optional().default(''),
  skillsRequired: z.string().max(500).optional().default(''),
  jobType: z.enum(['Full-time', 'Part-time', 'Contract', 'Internship']),
  location: z.string().max(200).optional().default(''),
});
type RequisitionForm = z.infer<typeof requisitionSchema>;

const candidateSchema = z.object({
  firstName: z.string().min(1, 'First name is required.').max(100),
  lastName: z.string().min(1, 'Last name is required.').max(100),
  email: z.string().email('Valid email is required.'),
  phone: z.string().min(1, 'Phone number is required.').max(20),
  currentDesignation: z.string().max(150).optional().default(''),
  currentCompany: z.string().max(150).optional().default(''),
  totalExperience: z.coerce.number().min(0).optional().default(0),
  skills: z.string().max(500).optional().default(''),
});
type CandidateForm = z.infer<typeof candidateSchema>;

export default function RecruitmentPage() {
  const qc = useQueryClient();
  const [reqDialogOpen, setReqDialogOpen] = useState(false);
  const [canDialogOpen, setCanDialogOpen] = useState(false);

  const { page: reqPage, setPage: setReqPage, pageSize } = usePaginationState();
  const { page: canPage, setPage: setCanPage } = usePaginationState();

  const { data: pipeline, isLoading: loadingPipeline } = useGetRecruitmentPipeline();
  const { data: requisitions, isLoading: loadingReq } = useListRequisitions({ page: reqPage, pageSize });
  const { data: candidates, isLoading: loadingCan } = useListCandidates({ page: canPage, pageSize });

  const requisitionForm = useForm<RequisitionForm>({
    resolver: zodResolver(requisitionSchema),
    defaultValues: {
      title: '', departmentName: '', description: '', openingsCount: 1,
      experienceRequired: '', skillsRequired: '', jobType: 'Full-time', location: '',
    },
  });
  const requisitionMutation = useMutation({
    mutationFn: (values: RequisitionForm) => api.createRequisition(values),
    onSuccess: async (res) => {
      if (!res.ok) {
        const body = await res.json().catch(() => ({}));
        throw new Error(body.message ?? 'Failed to create job posting.');
      }
      toast.success('Job posting created.');
      qc.invalidateQueries({ queryKey: ['/api/recruitment/requisitions'] });
      qc.invalidateQueries({ queryKey: ['/api/recruitment/dashboard'] });
      setReqDialogOpen(false);
      requisitionForm.reset();
    },
    onError: (e: Error) => toast.error(e.message || 'Failed to create job posting.'),
  });

  const candidateForm = useForm<CandidateForm>({
    resolver: zodResolver(candidateSchema),
    defaultValues: {
      firstName: '', lastName: '', email: '', phone: '',
      currentDesignation: '', currentCompany: '', totalExperience: 0, skills: '',
    },
  });
  const candidateMutation = useMutation({
    mutationFn: (values: CandidateForm) => api.createCandidate(values),
    onSuccess: async (res) => {
      if (!res.ok) {
        const body = await res.json().catch(() => ({}));
        throw new Error(body.message ?? 'Failed to add candidate.');
      }
      toast.success('Candidate added.');
      qc.invalidateQueries({ queryKey: ['/api/recruitment/candidates'] });
      qc.invalidateQueries({ queryKey: ['/api/recruitment/dashboard'] });
      setCanDialogOpen(false);
      candidateForm.reset();
    },
    onError: (e: Error) => toast.error(e.message || 'Failed to add candidate.'),
  });

  return (
    <div className="space-y-6">
      <PageHeader
        title="Recruitment"
        description="Manage job postings and applicant tracking."
      />

      {loadingPipeline ? (
        <Skeleton className="h-[120px] w-full rounded-xl" />
      ) : pipeline && pipeline.stages?.length > 0 ? (
        <Card className="bg-primary/5 border-primary/20">
          <CardContent className="p-6">
            <div className="flex flex-col md:flex-row justify-between items-center gap-6">
              <div className="flex flex-col text-center md:text-left">
                <span className="text-sm font-medium text-muted-foreground uppercase tracking-wider mb-1">
                  Total Pipeline
                </span>
                <div className="text-4xl font-bold text-primary">{pipeline.totalCandidates}</div>
                <span className="text-xs text-muted-foreground mt-1">
                  across {pipeline.totalOpenPositions} open positions
                </span>
              </div>

              <div className="flex-1 flex w-full justify-between items-end gap-2 overflow-x-auto pb-2">
                {pipeline.stages.map((stage, idx) => (
                  <div key={idx} className="flex flex-col items-center flex-1 min-w-[80px]">
                    <div className="text-2xl font-bold mb-2">{stage.count}</div>
                    <div className="w-full h-2 bg-primary/20 rounded-full mb-2 relative overflow-hidden">
                      <div
                        className="absolute top-0 left-0 h-full bg-primary"
                        style={{
                          width: `${Math.max(5, (stage.count / (pipeline.totalCandidates || 1)) * 100)}%`,
                        }}
                      />
                    </div>
                    <span className="text-xs font-medium text-muted-foreground text-center truncate w-full">
                      {stage.stage}
                    </span>
                  </div>
                ))}
              </div>
            </div>
          </CardContent>
        </Card>
      ) : null}

      <Tabs defaultValue="requisitions" className="w-full">
        <TabsList className="grid w-full sm:w-[400px] grid-cols-2">
          <TabsTrigger value="requisitions">Job Requisitions</TabsTrigger>
          <TabsTrigger value="candidates">Candidates</TabsTrigger>
        </TabsList>

        <TabsContent value="requisitions" className="space-y-4 mt-6">
          <div className="flex justify-end">
            <Button onClick={() => setReqDialogOpen(true)}>
              <Plus className="mr-2 h-4 w-4" /> New Job Posting
            </Button>
          </div>

          <div className="bg-card border rounded-lg shadow-sm overflow-hidden">
            {loadingReq ? (
              <SkeletonTable columns={6} rows={5} />
            ) : !requisitions?.items.length ? (
              <EmptyState title="No active job postings" />
            ) : (
              <>
                <Table>
                  <TableHeader>
                    <TableRow className="bg-muted/50">
                      <TableHead>Job Title</TableHead>
                      <TableHead>Department</TableHead>
                      <TableHead>Openings</TableHead>
                      <TableHead>Candidates</TableHead>
                      <TableHead>Status</TableHead>
                      <TableHead className="text-right">Actions</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {requisitions.items.map((req) => (
                      <TableRow key={req.id}>
                        <TableCell className="font-medium">{req.jobTitle}</TableCell>
                        <TableCell>{req.departmentName}</TableCell>
                        <TableCell>{req.openings}</TableCell>
                        <TableCell>
                          <div className="flex items-center text-muted-foreground">
                            <Users className="mr-2 h-4 w-4" />
                            {req.candidateCount ?? 0}
                          </div>
                        </TableCell>
                        <TableCell>
                          <StatusBadge status={req.status} />
                        </TableCell>
                        <TableCell className="text-right">
                          <Button size="sm" variant="outline">View</Button>
                        </TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
                <Pagination
                  page={requisitions.page}
                  pageSize={requisitions.pageSize}
                  totalCount={requisitions.totalCount}
                  totalPages={requisitions.totalPages}
                  onPageChange={setReqPage}
                />
              </>
            )}
          </div>
        </TabsContent>

        <TabsContent value="candidates" className="space-y-4 mt-6">
          <div className="flex justify-end">
            <Button onClick={() => setCanDialogOpen(true)}>
              <Plus className="mr-2 h-4 w-4" /> Add Candidate
            </Button>
          </div>

          <div className="bg-card border rounded-lg shadow-sm overflow-hidden">
            {loadingCan ? (
              <SkeletonTable columns={6} rows={10} />
            ) : !candidates?.items.length ? (
              <EmptyState title="No candidates found" />
            ) : (
              <>
                <Table>
                  <TableHeader>
                    <TableRow className="bg-muted/50">
                      <TableHead>Candidate</TableHead>
                      <TableHead>Applied For</TableHead>
                      <TableHead>Applied Date</TableHead>
                      <TableHead>Rating</TableHead>
                      <TableHead>Status</TableHead>
                      <TableHead className="text-right">Actions</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {candidates.items.map((can) => (
                      <TableRow key={can.id}>
                        <TableCell>
                          <div className="font-medium">{can.fullName}</div>
                          <div className="text-xs text-muted-foreground">{can.email}</div>
                        </TableCell>
                        <TableCell>{can.position || 'General'}</TableCell>
                        <TableCell>
                          {can.appliedAt ? new Date(can.appliedAt).toLocaleDateString() : '-'}
                        </TableCell>
                        <TableCell>
                          <div className="flex text-amber-500">
                            {Array.from({ length: 5 }).map((_, i) => (
                              <span
                                key={i}
                                className={i < (can.rating ?? 0) ? 'opacity-100' : 'opacity-30'}
                              >
                                ★
                              </span>
                            ))}
                          </div>
                        </TableCell>
                        <TableCell>
                          <StatusBadge status={can.status} />
                        </TableCell>
                        <TableCell className="text-right">
                          <Button size="sm" variant="outline">Details</Button>
                        </TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
                <Pagination
                  page={candidates.page}
                  pageSize={candidates.pageSize}
                  totalCount={candidates.totalCount}
                  totalPages={candidates.totalPages}
                  onPageChange={setCanPage}
                />
              </>
            )}
          </div>
        </TabsContent>
      </Tabs>

      {/* New Job Posting dialog */}
      <Dialog open={reqDialogOpen} onOpenChange={setReqDialogOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>New Job Posting</DialogTitle>
            <DialogDescription>Create a new job requisition.</DialogDescription>
          </DialogHeader>
          <Form {...requisitionForm}>
            <form onSubmit={requisitionForm.handleSubmit((v) => requisitionMutation.mutate(v))} className="space-y-4">
              <FormField control={requisitionForm.control} name="title" render={({ field }) => (
                <FormItem><FormLabel>Job Title</FormLabel><FormControl><Input {...field} placeholder="Senior Software Engineer" /></FormControl><FormMessage /></FormItem>
              )} />
              <FormField control={requisitionForm.control} name="departmentName" render={({ field }) => (
                <FormItem><FormLabel>Department</FormLabel><FormControl><Input {...field} placeholder="Engineering" /></FormControl><FormMessage /></FormItem>
              )} />
              <FormField control={requisitionForm.control} name="openingsCount" render={({ field }) => (
                <FormItem><FormLabel>Number of Openings</FormLabel><FormControl><Input type="number" min={1} {...field} /></FormControl><FormMessage /></FormItem>
              )} />
              <FormField control={requisitionForm.control} name="location" render={({ field }) => (
                <FormItem><FormLabel>Location (optional)</FormLabel><FormControl><Input {...field} placeholder="Mumbai, Remote" /></FormControl><FormMessage /></FormItem>
              )} />
              <FormField control={requisitionForm.control} name="skillsRequired" render={({ field }) => (
                <FormItem><FormLabel>Skills Required (optional)</FormLabel><FormControl><Input {...field} placeholder="React, TypeScript, .NET" /></FormControl><FormMessage /></FormItem>
              )} />
              <FormField control={requisitionForm.control} name="description" render={({ field }) => (
                <FormItem><FormLabel>Description (optional)</FormLabel><FormControl><Textarea {...field} rows={3} /></FormControl><FormMessage /></FormItem>
              )} />
              <DialogFooter>
                <Button variant="outline" type="button" onClick={() => setReqDialogOpen(false)}>Cancel</Button>
                <Button type="submit" disabled={requisitionMutation.isPending}>{requisitionMutation.isPending ? 'Creating…' : 'Create Posting'}</Button>
              </DialogFooter>
            </form>
          </Form>
        </DialogContent>
      </Dialog>

      {/* Add Candidate dialog */}
      <Dialog open={canDialogOpen} onOpenChange={setCanDialogOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Add Candidate</DialogTitle>
            <DialogDescription>Add a new candidate to the pipeline.</DialogDescription>
          </DialogHeader>
          <Form {...candidateForm}>
            <form onSubmit={candidateForm.handleSubmit((v) => candidateMutation.mutate(v))} className="space-y-4">
              <div className="grid grid-cols-2 gap-4">
                <FormField control={candidateForm.control} name="firstName" render={({ field }) => (
                  <FormItem><FormLabel>First Name</FormLabel><FormControl><Input {...field} /></FormControl><FormMessage /></FormItem>
                )} />
                <FormField control={candidateForm.control} name="lastName" render={({ field }) => (
                  <FormItem><FormLabel>Last Name</FormLabel><FormControl><Input {...field} /></FormControl><FormMessage /></FormItem>
                )} />
              </div>
              <FormField control={candidateForm.control} name="email" render={({ field }) => (
                <FormItem><FormLabel>Email</FormLabel><FormControl><Input type="email" {...field} /></FormControl><FormMessage /></FormItem>
              )} />
              <FormField control={candidateForm.control} name="phone" render={({ field }) => (
                <FormItem><FormLabel>Phone</FormLabel><FormControl><Input {...field} /></FormControl><FormMessage /></FormItem>
              )} />
              <FormField control={candidateForm.control} name="currentDesignation" render={({ field }) => (
                <FormItem><FormLabel>Current Designation (optional)</FormLabel><FormControl><Input {...field} /></FormControl><FormMessage /></FormItem>
              )} />
              <FormField control={candidateForm.control} name="skills" render={({ field }) => (
                <FormItem><FormLabel>Skills (optional)</FormLabel><FormControl><Input {...field} placeholder="React, Node.js" /></FormControl><FormMessage /></FormItem>
              )} />
              <DialogFooter>
                <Button variant="outline" type="button" onClick={() => setCanDialogOpen(false)}>Cancel</Button>
                <Button type="submit" disabled={candidateMutation.isPending}>{candidateMutation.isPending ? 'Adding…' : 'Add Candidate'}</Button>
              </DialogFooter>
            </form>
          </Form>
        </DialogContent>
      </Dialog>
    </div>
  );
}
