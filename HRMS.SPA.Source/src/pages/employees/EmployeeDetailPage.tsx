// Employee Detail — Edit Profile, Reset Password, and real documents wired to
// backend endpoints.
//
// BUG FIXES:
//   1. "Edit Profile" had no onClick — now opens a dialog that PUTs to
//      /api/employees/{id} (multipart/form-data per EmployeeController.Update).
//   2. "Reset Password" had no onClick. There is no admin-triggered
//      "force reset this employee's password" endpoint on EmployeeController —
//      the only real capability is the self-service forgot-password flow
//      (POST /api/auth/forgot-password), which sends a reset link to the
//      employee's own email. Wired to that real endpoint rather than
//      fabricating a nonexistent admin-reset API.
//   3. "Recent Documents" previously hardcoded two fake filenames
//      (Offer_Letter.pdf, NDA_Signed.pdf) with non-functional Download
//      buttons, despite EmployeeDocumentController fully implementing
//      list/upload/download. Now fetches and renders real documents with
//      working downloads.
import { useState } from 'react';
import { useParams, Link } from 'wouter';
import { ArrowLeft, Mail, Phone, MapPin, Building, Calendar, Briefcase, FileText } from 'lucide-react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import * as z from 'zod';
import { toast } from 'sonner';
import { useGetEmployee, getGetEmployeeQueryKey } from '@workspace/api-client-react';

import { PageHeader } from '@/components/layout/PageHeader';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from '@/components/ui/card';
import { StatusBadge } from '@/components/shared/StatusBadge';
import { Skeleton } from '@/components/ui/skeleton';
import { SafeAvatar } from '@/components/shared/SafeAvatar';
import { Input } from '@/components/ui/input';
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter, DialogDescription } from '@/components/ui/dialog';
import { AlertDialog, AlertDialogAction, AlertDialogCancel, AlertDialogContent, AlertDialogDescription, AlertDialogFooter, AlertDialogHeader, AlertDialogTitle } from '@/components/ui/alert-dialog';
import { Form, FormControl, FormField, FormItem, FormLabel, FormMessage } from '@/components/ui/form';
import { csrfFetch } from '@/utils/csrfFetch';

const BASE = import.meta.env.BASE_URL?.replace(/\/$/, '') ?? '';

interface EmployeeDocumentRow {
  id: number;
  documentType?: string | null;
  fileName: string;
  uploadedAt: string;
}

const editSchema = z.object({
  firstName: z.string().min(1, 'First name is required.').max(100),
  lastName: z.string().min(1, 'Last name is required.').max(100),
  email: z.string().email('Valid email is required.'),
  phone: z.string().optional(),
  designation: z.string().optional(),
  department: z.string().optional(),
});
type EditForm = z.infer<typeof editSchema>;

const api = {
  update: (employeeId: string, values: EditForm) => {
    const form = new FormData();
    Object.entries(values).forEach(([k, v]) => { if (v) form.append(k, v); });
    return csrfFetch(`${BASE}/api/employees/${encodeURIComponent(employeeId)}`, {
      method: 'PUT', credentials: 'include', body: form,
    });
  },
  forgotPassword: (email: string) =>
    csrfFetch(`${BASE}/api/auth/forgot-password`, {
      method: 'POST', credentials: 'include',
      headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ email }),
    }),
  documents: async (employeeId: string): Promise<EmployeeDocumentRow[]> => {
    const r = await csrfFetch(`${BASE}/api/employees/${encodeURIComponent(employeeId)}/documents`, { credentials: 'include' });
    if (!r.ok) throw new Error('Failed to load documents.');
    const body = await r.json();
    return (body.data?.items ?? body.items ?? []);
  },
  downloadDocument: (employeeId: string, docId: number) =>
    csrfFetch(`${BASE}/api/employees/${encodeURIComponent(employeeId)}/documents/${docId}/download`, { credentials: 'include' }),
};

export default function EmployeeDetailPage() {
  const qc = useQueryClient();
  const params = useParams();
  const id = params.id as string;

  const { data: employee, isLoading, isError } = useGetEmployee(id, {
    query: {
      enabled: Boolean(id),
      queryKey: getGetEmployeeQueryKey(id)
    }
  });

  const [editOpen, setEditOpen] = useState(false);
  const [resetConfirmOpen, setResetConfirmOpen] = useState(false);
  const [resetPending, setResetPending] = useState(false);

  const editForm = useForm<EditForm>({
    resolver: zodResolver(editSchema),
    defaultValues: { firstName: '', lastName: '', email: '', phone: '', designation: '', department: '' },
  });

  const openEdit = () => {
    if (!employee) return;
    editForm.reset({
      firstName: employee.firstName ?? '',
      lastName: employee.lastName ?? '',
      email: employee.email ?? '',
      phone: employee.phone ?? '',
      designation: employee.designation ?? '',
      department: employee.departmentName ?? '',
    });
    setEditOpen(true);
  };

  const onSaveEdit = async (values: EditForm) => {
    try {
      const res = await api.update(id, values);
      if (!res.ok) {
        const body = await res.json().catch(() => ({}));
        throw new Error(body.message ?? 'Failed to update employee.');
      }
      toast.success('Employee profile updated.');
      qc.invalidateQueries({ queryKey: getGetEmployeeQueryKey(id) });
      setEditOpen(false);
    } catch (e) {
      toast.error(e instanceof Error ? e.message : 'Failed to update employee.');
    }
  };

  const onResetPassword = async () => {
    if (!employee?.email) {
      toast.error('This employee has no email on file.');
      setResetConfirmOpen(false);
      return;
    }
    setResetPending(true);
    try {
      const res = await api.forgotPassword(employee.email);
      // Backend deliberately returns 200 with a generic message regardless of
      // whether the email exists, to avoid leaking account existence.
      if (!res.ok) throw new Error('Failed to send reset link.');
      toast.success(`Password reset link sent to ${employee.email}.`);
    } catch (e) {
      toast.error(e instanceof Error ? e.message : 'Failed to send reset link.');
    } finally {
      setResetPending(false);
      setResetConfirmOpen(false);
    }
  };

  const { data: documents, isLoading: loadingDocs } = useQuery({
    queryKey: ['/api/employees', id, 'documents'],
    queryFn: () => api.documents(id),
    enabled: Boolean(id),
  });

  const handleDownload = async (docId: number, fileName: string) => {
    try {
      const res = await api.downloadDocument(id, docId);
      if (!res.ok) throw new Error('Failed to download document.');
      const blob = await res.blob();
      const link = document.createElement('a');
      link.href = URL.createObjectURL(blob);
      link.download = fileName;
      link.click();
      URL.revokeObjectURL(link.href);
    } catch (e) {
      toast.error(e instanceof Error ? e.message : 'Failed to download document.');
    }
  };

  if (isLoading) {
    return (
      <div className="space-y-6">
        <Skeleton className="h-10 w-64" />
        <div className="grid grid-cols-1 md:grid-cols-3 gap-6">
          <Skeleton className="h-[400px] col-span-1 rounded-xl" />
          <Skeleton className="h-[400px] col-span-1 md:col-span-2 rounded-xl" />
        </div>
      </div>
    );
  }

  if (isError || !employee) {
    return (
      <div className="flex flex-col items-center justify-center min-h-[50vh]">
        <h2 className="text-2xl font-bold mb-2">Employee Not Found</h2>
        <p className="text-muted-foreground mb-4">The employee record you are looking for does not exist or has been removed.</p>
        <Button asChild>
          <Link href="/employees">Back to Employees</Link>
        </Button>
      </div>
    );
  }

  // Defensive: safely compose display name so null/undefined parts don't render as "null null"
  const displayName = [employee.firstName, employee.lastName].filter(Boolean).join(' ') || 'Unknown Employee';

  return (
    <div className="space-y-6">
      <PageHeader 
        title={displayName}
        breadcrumbs={
          <div className="flex items-center text-sm text-muted-foreground">
            <Link href="/employees" className="hover:text-foreground flex items-center transition-colors">
              <ArrowLeft className="mr-1 h-3 w-3" />
              Employees
            </Link>
            <span className="mx-2">/</span>
            <span className="text-foreground font-medium">{employee.employeeId}</span>
          </div>
        }
        actions={
          <div className="flex gap-2">
            <Button variant="outline" onClick={() => setResetConfirmOpen(true)}>Reset Password</Button>
            <Button onClick={openEdit}>Edit Profile</Button>
          </div>
        }
      />

      <div className="grid grid-cols-1 md:grid-cols-3 gap-6">
        <Card className="col-span-1">
          <CardContent className="pt-6">
            <div className="flex flex-col items-center text-center">
              <SafeAvatar
                profile={{
                  firstName: employee.firstName,
                  lastName: employee.lastName,
                  avatarUrl: employee.avatarUrl,
                }}
                size="h-32 w-32"
                className="mb-4 border-4 border-background shadow-md text-4xl"
              />
              <h2 className="text-2xl font-bold">{displayName}</h2>
              <p className="text-muted-foreground mb-3">{employee.designation ?? 'No Designation'}</p>
              
              <StatusBadge status={employee.status} className="mb-6 px-3 py-1" />
              
              <div className="w-full space-y-4 text-sm text-left">
                <div className="flex items-center gap-3">
                  <div className="bg-muted p-2 rounded-md"><Mail className="h-4 w-4 text-muted-foreground" /></div>
                  <div className="flex flex-col">
                    <span className="text-xs text-muted-foreground">Email</span>
                    <span className="font-medium">{employee.email ?? 'No Email'}</span>
                  </div>
                </div>
                <div className="flex items-center gap-3">
                  <div className="bg-muted p-2 rounded-md"><Phone className="h-4 w-4 text-muted-foreground" /></div>
                  <div className="flex flex-col">
                    <span className="text-xs text-muted-foreground">Phone</span>
                    <span className="font-medium">{employee.phone || 'Not provided'}</span>
                  </div>
                </div>
                <div className="flex items-center gap-3">
                  <div className="bg-muted p-2 rounded-md"><MapPin className="h-4 w-4 text-muted-foreground" /></div>
                  <div className="flex flex-col">
                    <span className="text-xs text-muted-foreground">Location</span>
                    <span className="font-medium">Headquarters</span>
                  </div>
                </div>
              </div>
            </div>
          </CardContent>
        </Card>

        <div className="col-span-1 md:col-span-2 space-y-6">
          <Card>
            <CardHeader>
              <CardTitle className="text-lg">Employment Details</CardTitle>
              <CardDescription>Role, department, and tenure information</CardDescription>
            </CardHeader>
            <CardContent className="grid grid-cols-1 sm:grid-cols-2 gap-y-6 gap-x-8">
              <div className="flex flex-col gap-1">
                <span className="text-sm flex items-center text-muted-foreground"><Briefcase className="mr-2 h-4 w-4" /> Employee ID</span>
                <span className="font-medium font-mono">{employee.employeeId}</span>
              </div>
              <div className="flex flex-col gap-1">
                <span className="text-sm flex items-center text-muted-foreground"><Building className="mr-2 h-4 w-4" /> Department</span>
                <span className="font-medium">{employee.departmentName || 'Not Assigned'}</span>
              </div>
              <div className="flex flex-col gap-1">
                <span className="text-sm flex items-center text-muted-foreground"><Calendar className="mr-2 h-4 w-4" /> Joining Date</span>
                <span className="font-medium">{employee.joinDate ? new Date(employee.joinDate).toLocaleDateString() : 'Not provided'}</span>
              </div>
              <div className="flex flex-col gap-1">
                <span className="text-sm flex items-center text-muted-foreground"><FileText className="mr-2 h-4 w-4" /> Employment Type</span>
                <span className="font-medium">Full-Time</span>
              </div>
            </CardContent>
          </Card>

          <Card>
            <CardHeader>
              <CardTitle className="text-lg">Recent Documents</CardTitle>
            </CardHeader>
            <CardContent>
              {loadingDocs ? (
                <Skeleton className="h-16 w-full" />
              ) : !documents?.length ? (
                <p className="text-sm text-muted-foreground">No documents uploaded yet.</p>
              ) : (
                <div className="border rounded-md divide-y">
                  {documents.map((doc) => (
                    <div key={doc.id} className="flex items-center justify-between p-3 hover:bg-muted/50 transition-colors">
                      <div className="flex items-center gap-3">
                        <FileText className="h-5 w-5 text-blue-500" />
                        <span className="text-sm font-medium">{doc.fileName}</span>
                      </div>
                      <Button variant="ghost" size="sm" onClick={() => handleDownload(doc.id, doc.fileName)}>Download</Button>
                    </div>
                  ))}
                </div>
              )}
            </CardContent>
          </Card>
        </div>
      </div>

      {/* Edit Profile dialog */}
      <Dialog open={editOpen} onOpenChange={setEditOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Edit Profile</DialogTitle>
            <DialogDescription>Update {displayName}&apos;s details.</DialogDescription>
          </DialogHeader>
          <Form {...editForm}>
            <form onSubmit={editForm.handleSubmit(onSaveEdit)} className="space-y-4">
              <div className="grid grid-cols-2 gap-3">
                <FormField control={editForm.control} name="firstName" render={({ field }) => (
                  <FormItem><FormLabel>First Name</FormLabel><FormControl><Input {...field} /></FormControl><FormMessage /></FormItem>
                )} />
                <FormField control={editForm.control} name="lastName" render={({ field }) => (
                  <FormItem><FormLabel>Last Name</FormLabel><FormControl><Input {...field} /></FormControl><FormMessage /></FormItem>
                )} />
              </div>
              <FormField control={editForm.control} name="email" render={({ field }) => (
                <FormItem><FormLabel>Email</FormLabel><FormControl><Input type="email" {...field} /></FormControl><FormMessage /></FormItem>
              )} />
              <FormField control={editForm.control} name="phone" render={({ field }) => (
                <FormItem><FormLabel>Phone (optional)</FormLabel><FormControl><Input {...field} /></FormControl><FormMessage /></FormItem>
              )} />
              <div className="grid grid-cols-2 gap-3">
                <FormField control={editForm.control} name="designation" render={({ field }) => (
                  <FormItem><FormLabel>Designation (optional)</FormLabel><FormControl><Input {...field} /></FormControl><FormMessage /></FormItem>
                )} />
                <FormField control={editForm.control} name="department" render={({ field }) => (
                  <FormItem><FormLabel>Department (optional)</FormLabel><FormControl><Input {...field} /></FormControl><FormMessage /></FormItem>
                )} />
              </div>
              <DialogFooter>
                <Button variant="outline" type="button" onClick={() => setEditOpen(false)}>Cancel</Button>
                <Button type="submit" disabled={editForm.formState.isSubmitting}>Save Changes</Button>
              </DialogFooter>
            </form>
          </Form>
        </DialogContent>
      </Dialog>

      {/* Reset Password confirmation */}
      <AlertDialog open={resetConfirmOpen} onOpenChange={setResetConfirmOpen}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Send password reset link?</AlertDialogTitle>
            <AlertDialogDescription>
              This will email a one-time password reset link to {employee.email ?? 'this employee'}.
              There is no direct admin-set-password action — the employee must complete the reset via the emailed link.
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>Cancel</AlertDialogCancel>
            <AlertDialogAction disabled={resetPending} onClick={onResetPassword}>
              {resetPending ? 'Sending\u2026' : 'Send Reset Link'}
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </div>
  );
}
