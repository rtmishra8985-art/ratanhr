// Fixed: useState side-effect anti-pattern → useEffect with cancellation in ProgramsTab + MyEnrollmentsTab
import { useState, useEffect } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import * as z from 'zod';
import { Plus, BookOpen, Award } from 'lucide-react';
import { PageHeader }  from '@/components/layout/PageHeader';
import { Button }      from '@/components/ui/button';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from '@/components/ui/card';
import { Badge }       from '@/components/ui/badge';
import { Input }       from '@/components/ui/input';
import { Textarea }    from '@/components/ui/textarea';
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter, DialogDescription } from '@/components/ui/dialog';
import { Form, FormControl, FormField, FormItem, FormLabel, FormMessage } from '@/components/ui/form';
import { SkeletonTable } from '@/components/shared/SkeletonTable';
import { EmptyState }    from '@/components/shared/EmptyState';
import { usePermissions } from '@/hooks/usePermissions';
import { useToast }       from '@/hooks/use-toast';
import { csrfFetch } from '@/utils/csrfFetch';

const BASE = import.meta.env.BASE_URL.replace(/\/$/, '');

const programSchema = z.object({
  title: z.string().min(1, 'Title is required.').max(200),
  description: z.string().max(2000).optional(),
  startDate: z.string().min(1, 'Start date is required.'),
  endDate: z.string().min(1, 'End date is required.'),
  trainer: z.string().max(150).optional(),
  maxSeats: z.coerce.number().int().min(0),
});
type ProgramForm = z.infer<typeof programSchema>;

interface TrainingProgram {
  id: number;
  title: string;
  description?: string;
  startDate: string;
  endDate: string;
  trainer?: string;
  maxSeats: number;
  enrolledCount: number;
  isActive: boolean;
}

interface Enrollment {
  id: number;
  trainingTitle: string;
  status: string;
  completionDate?: string;
}

function ProgramCard({ program, isAdmin, onEnroll }: {
  program: TrainingProgram;
  isAdmin: boolean;
  onEnroll: (id: number) => void;
}) {
  const seatsLeft = program.maxSeats > 0 ? program.maxSeats - program.enrolledCount : null;
  return (
    <Card className="hover:shadow-md transition-shadow">
      <CardHeader className="pb-2">
        <div className="flex items-start justify-between gap-2">
          <CardTitle className="text-base">{program.title}</CardTitle>
          {seatsLeft !== null && (
            <Badge variant={seatsLeft > 0 ? 'secondary' : 'destructive'} className="shrink-0">
              {seatsLeft > 0 ? `${seatsLeft} seats left` : 'Full'}
            </Badge>
          )}
        </div>
        {program.trainer && (
          <CardDescription>Trainer: {program.trainer}</CardDescription>
        )}
      </CardHeader>
      <CardContent className="space-y-3">
        {program.description && (
          <p className="text-sm text-muted-foreground line-clamp-2">{program.description}</p>
        )}
        <div className="flex justify-between text-xs text-muted-foreground">
          <span>Start: {new Date(program.startDate).toLocaleDateString()}</span>
          <span>End: {new Date(program.endDate).toLocaleDateString()}</span>
        </div>
        {!isAdmin && (
          <Button
            size="sm"
            className="w-full"
            onClick={() => onEnroll(program.id)}
            disabled={seatsLeft !== null && seatsLeft <= 0}
          >
            <BookOpen className="mr-2 h-4 w-4" /> Enroll
          </Button>
        )}
      </CardContent>
    </Card>
  );
}

export default function TrainingPage() {
  const { isAdmin } = usePermissions();
  const { toast } = useToast();
  const [createOpen, setCreateOpen] = useState(false);
  const [refreshKey, setRefreshKey] = useState(0);

  const createForm = useForm<ProgramForm>({
    resolver: zodResolver(programSchema),
    defaultValues: { title: '', description: '', startDate: '', endDate: '', trainer: '', maxSeats: 0 },
  });

  const onCreateProgram = async (values: ProgramForm) => {
    try {
      const res = await csrfFetch(`${BASE}/api/training`, {
        method: 'POST',
        credentials: 'include',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(values),
      });
      const json = await res.json();
      if (!res.ok) throw new Error(json.message ?? 'Failed to create program.');
      toast({ title: 'Program created', description: json.message });
      setCreateOpen(false);
      createForm.reset({ title: '', description: '', startDate: '', endDate: '', trainer: '', maxSeats: 0 });
      setRefreshKey((k) => k + 1);
    } catch (e) {
      toast({ title: 'Failed to create program', description: String(e), variant: 'destructive' });
    }
  };

  const onEnroll = async (programId: number) => {
    try {
      // FIX [3]: The access token is stored in an HttpOnly cookie — document.cookie
      // cannot read it, so the previous JWT-parse block always silently yielded
      // empId = ''. Fetch the profile endpoint instead; the cookie is sent
      // automatically by the browser with credentials: 'include'.
      let empId = '';
      try {
        const profileRes = await csrfFetch(`${BASE}/api/profile`, { credentials: 'include' });
        if (profileRes.ok) {
          const profileJson = await profileRes.json() as { data?: { employeeId?: string } };
          empId = profileJson.data?.employeeId ?? '';
        }
      } catch {
        empId = '';
      }
      const res = await csrfFetch(`${BASE}/api/training/${programId}/enroll`, {
        method: 'POST',
        credentials: 'include',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ employeeId: empId }),
      });
      const json = await res.json();
      if (!res.ok) throw new Error(json.message ?? 'Enroll failed');
      toast({ title: 'Enrolled', description: json.message });
    } catch (e) {
      toast({ title: 'Enrollment failed', description: String(e), variant: 'destructive' });
    }
  };

  return (
    <div className="space-y-6">
      <PageHeader
        title="Training & LMS"
        description="Browse programs, enroll, and track your learning journey."
        actions={
          isAdmin ? (
            <Button size="sm" onClick={() => setCreateOpen(true)}><Plus className="mr-2 h-4 w-4" /> New Program</Button>
          ) : undefined
        }
      />

      {/* New Program dialog */}
      <Dialog open={createOpen} onOpenChange={setCreateOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>New Training Program</DialogTitle>
            <DialogDescription>Create a new training program for employees to enroll in.</DialogDescription>
          </DialogHeader>
          <Form {...createForm}>
            <form onSubmit={createForm.handleSubmit(onCreateProgram)} className="space-y-4">
              <FormField control={createForm.control} name="title" render={({ field }) => (
                <FormItem><FormLabel>Title</FormLabel><FormControl><Input {...field} placeholder="Advanced React Patterns" /></FormControl><FormMessage /></FormItem>
              )} />
              <div className="grid grid-cols-2 gap-4">
                <FormField control={createForm.control} name="startDate" render={({ field }) => (
                  <FormItem><FormLabel>Start Date</FormLabel><FormControl><Input type="date" {...field} /></FormControl><FormMessage /></FormItem>
                )} />
                <FormField control={createForm.control} name="endDate" render={({ field }) => (
                  <FormItem><FormLabel>End Date</FormLabel><FormControl><Input type="date" {...field} /></FormControl><FormMessage /></FormItem>
                )} />
              </div>
              <FormField control={createForm.control} name="trainer" render={({ field }) => (
                <FormItem><FormLabel>Trainer (optional)</FormLabel><FormControl><Input {...field} /></FormControl><FormMessage /></FormItem>
              )} />
              <FormField control={createForm.control} name="maxSeats" render={({ field }) => (
                <FormItem><FormLabel>Max Seats</FormLabel><FormControl><Input type="number" min={0} {...field} /></FormControl><FormMessage /></FormItem>
              )} />
              <FormField control={createForm.control} name="description" render={({ field }) => (
                <FormItem><FormLabel>Description (optional)</FormLabel><FormControl><Textarea {...field} rows={3} /></FormControl><FormMessage /></FormItem>
              )} />
              <DialogFooter>
                <Button variant="outline" type="button" onClick={() => setCreateOpen(false)}>Cancel</Button>
                <Button type="submit">Create Program</Button>
              </DialogFooter>
            </form>
          </Form>
        </DialogContent>
      </Dialog>

      <Tabs defaultValue="programs">
        <TabsList>
          <TabsTrigger value="programs">Programs</TabsTrigger>
          {!isAdmin && <TabsTrigger value="my">My Enrollments</TabsTrigger>}
        </TabsList>

        <TabsContent value="programs">
          <ProgramsTab isAdmin={isAdmin} onEnroll={onEnroll} refreshKey={refreshKey} />
        </TabsContent>

        {!isAdmin && (
          <TabsContent value="my">
            <MyEnrollmentsTab />
          </TabsContent>
        )}
      </Tabs>
    </div>
  );
}

function ProgramsTab({ isAdmin, onEnroll, refreshKey }: { isAdmin: boolean; onEnroll: (id: number) => void; refreshKey: number }) {
  const [programs, setPrograms] = useState<TrainingProgram[]>([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    let cancelled = false;
    setLoading(true);
    (async () => {
      try {
        const res = await csrfFetch(`${BASE}/api/training?pageSize=50`, {
          credentials: 'include',
        });
        const json = await res.json();
        if (!cancelled) setPrograms(json.data?.items ?? []);
      } catch { /* empty */ } finally {
        if (!cancelled) setLoading(false);
      }
    })();
    return () => { cancelled = true; };
  }, [refreshKey]);

  if (loading) return <SkeletonTable columns={3} rows={4} />;
  if (!programs.length) return <EmptyState title="No training programs" description="Check back later or ask your admin to create one." />;

  return (
    <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-4 mt-4">
      {programs.map((p) => (
        <ProgramCard key={p.id} program={p} isAdmin={isAdmin} onEnroll={onEnroll} />
      ))}
    </div>
  );
}

function MyEnrollmentsTab() {
  const [enrollments, setEnrollments] = useState<Enrollment[]>([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    let cancelled = false;
    (async () => {
      try {
        const res = await csrfFetch(`${BASE}/api/training/enrollments/my`, {
          credentials: 'include',
        });
        const json = await res.json();
        if (!cancelled) setEnrollments(json.data ?? []);
      } catch { /* empty */ } finally {
        if (!cancelled) setLoading(false);
      }
    })();
    return () => { cancelled = true; };
  }, []);

  if (loading) return <SkeletonTable columns={3} rows={3} />;
  if (!enrollments.length) return <EmptyState title="No enrollments yet" description="Enroll in a training program from the Programs tab." />;

  return (
    <div className="space-y-3 mt-4">
      {enrollments.map((e) => (
        <Card key={e.id}>
          <CardContent className="flex items-center justify-between p-4">
            <div className="flex items-center gap-3">
              <Award className="h-5 w-5 text-primary" />
              <span className="font-medium">{e.trainingTitle}</span>
            </div>
            <Badge variant={e.status === 'Completed' ? 'default' : 'secondary'}>{e.status}</Badge>
          </CardContent>
        </Card>
      ))}
    </div>
  );
}
