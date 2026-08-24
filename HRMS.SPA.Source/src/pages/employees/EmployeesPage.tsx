// UX      — ConfirmDialog shown before any Delete action is carried out.
import { memo, useState } from 'react';
import { Link } from 'wouter';
import { Plus, Filter, Download, MoreHorizontal, Pencil, Trash2, Eye } from 'lucide-react';
import {
  useListEmployees,
  useDeleteEmployee,
  useCreateEmployee,
  getListEmployeesQueryKey,
} from '@workspace/api-client-react';
import { useQueryClient } from '@tanstack/react-query';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import * as z from 'zod';

import { PageHeader }    from '@/components/layout/PageHeader';
import { Button }        from '@/components/ui/button';
import {
  Table, TableBody, TableCell, TableHead, TableHeader, TableRow,
} from '@/components/ui/table';
import {
  DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuLabel,
  DropdownMenuSeparator, DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu';
import {
  Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter, DialogDescription,
} from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import {
  Form, FormControl, FormField, FormItem, FormLabel, FormMessage,
} from '@/components/ui/form';
import { SkeletonTable } from '@/components/shared/SkeletonTable';
import { Pagination }    from '@/components/shared/Pagination';
import { StatusBadge }   from '@/components/shared/StatusBadge';
import { SearchInput }   from '@/components/shared/SearchInput';
import { EmptyState }    from '@/components/shared/EmptyState';
import { SafeAvatar }    from '@/components/shared/SafeAvatar';
import { ConfirmDialog } from '@/components/shared/ConfirmDialog';
import { usePaginationState } from '@/hooks/usePaginationState';
import { usePermissions }    from '@/hooks/usePermissions';
import { getErrorTitle, getErrorDescription } from '@/utils/apiError';
import { useToast } from '@/hooks/use-toast';

interface EmployeeListItem {
  employeeId: string;
  firstName?: string | null;
  lastName?: string | null;
  email?: string | null;
  avatarUrl?: string | null;
  departmentName?: string | null;
  designation?: string | null;
  status: string;
}

// ─── Add Employee form schema ──────────────────────────────────────────────────

const addEmployeeSchema = z.object({
  firstName:   z.string().min(1, 'First name is required.').max(100),
  lastName:    z.string().min(1, 'Last name is required.').max(100),
  email:       z.string().email('Valid email required.'),
  phone:       z.string().optional(),
  designation: z.string().optional(),
  department:  z.string().optional(),
  joiningDate: z.string().optional(),
});
type AddEmployeeFormValues = z.infer<typeof addEmployeeSchema>;

// ─── Memoised row ─────────────────────────────────────────────────────────────

const EmployeeRow = memo(function EmployeeRow({
  employee,
  isAdmin,
  onDelete,
}: {
  employee: EmployeeListItem;
  isAdmin: boolean;
  onDelete: (employee: EmployeeListItem) => void;
}) {
  const displayName = [employee.firstName, employee.lastName].filter(Boolean).join(' ');

  return (
    <TableRow className="group">
      <TableCell>
        <div className="flex items-center gap-3">
          <SafeAvatar
            profile={{ firstName: employee.firstName, lastName: employee.lastName, avatarUrl: employee.avatarUrl }}
            size="h-9 w-9"
            className="border shadow-sm"
          />
          <div className="flex flex-col">
            <Link href={`/employees/${employee.employeeId}`} className="font-medium hover:underline text-foreground">
              {displayName || '—'}
            </Link>
            <span className="text-xs text-muted-foreground">{employee.email ?? ''}</span>
          </div>
        </div>
      </TableCell>
      <TableCell className="text-muted-foreground font-mono text-sm">{employee.employeeId}</TableCell>
      <TableCell>{employee.departmentName || '—'}</TableCell>
      <TableCell>{employee.designation || '—'}</TableCell>
      <TableCell><StatusBadge status={employee.status} /></TableCell>
      <TableCell className="text-right">
        <DropdownMenu>
          <DropdownMenuTrigger asChild>
            <Button
              variant="ghost"
              size="icon"
              className="opacity-0 group-hover:opacity-100 transition-opacity"
              aria-label={`Actions for ${displayName || employee.employeeId}`}
            >
              <MoreHorizontal className="h-4 w-4" />
            </Button>
          </DropdownMenuTrigger>
          <DropdownMenuContent align="end">
            <DropdownMenuLabel>Actions</DropdownMenuLabel>
            <DropdownMenuItem asChild>
              <Link href={`/employees/${employee.employeeId}`} className="cursor-pointer w-full flex items-center">
                <Eye className="mr-2 h-4 w-4" /> View Profile
              </Link>
            </DropdownMenuItem>
            <DropdownMenuItem asChild>
              <Link href={`/employees/${employee.employeeId}`} className="cursor-pointer w-full flex items-center">
                <Pencil className="mr-2 h-4 w-4" /> Edit Details
              </Link>
            </DropdownMenuItem>
            {isAdmin && (
              <>
                <DropdownMenuSeparator />
                <DropdownMenuItem
                  className="text-destructive focus:text-destructive"
                  onClick={() => onDelete(employee)}
                >
                  <Trash2 className="mr-2 h-4 w-4" /> Delete
                </DropdownMenuItem>
              </>
            )}
          </DropdownMenuContent>
        </DropdownMenu>
      </TableCell>
    </TableRow>
  );
});

// ─── Add Employee dialog ───────────────────────────────────────────────────────

function AddEmployeeDialog({ open, onOpenChange }: { open: boolean; onOpenChange: (v: boolean) => void }) {
  const { toast } = useToast();
  const queryClient = useQueryClient();
  const createMutation = useCreateEmployee();

  const form = useForm<AddEmployeeFormValues>({
    resolver: zodResolver(addEmployeeSchema),
    defaultValues: { firstName: '', lastName: '', email: '', phone: '', designation: '', department: '', joiningDate: '' },
  });

  const onSubmit = async (values: AddEmployeeFormValues) => {
    // Build FormData for multipart/form-data required by the endpoint
    const fd = new FormData();
    Object.entries(values).forEach(([k, v]) => { if (v) fd.append(k, v); });

    try {
      await createMutation.mutateAsync({ data: fd as unknown as Parameters<typeof createMutation.mutateAsync>[0]['data'] });
      await queryClient.invalidateQueries({ queryKey: getListEmployeesQueryKey() });
      toast({ title: 'Employee added', description: `${values.firstName} ${values.lastName} has been registered.` });
      onOpenChange(false);
      form.reset();
    } catch (error: unknown) {
      toast({
        title: 'Failed to add employee',
        description: error instanceof Error ? error.message : 'Please try again.',
        variant: 'destructive',
      });
    }
  };

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-md">
        <DialogHeader>
          <DialogTitle>Add New Employee</DialogTitle>
          <DialogDescription>
            Fill in the required fields. A temporary password will be generated and shown after creation.
          </DialogDescription>
        </DialogHeader>
        <Form {...form}>
          <form onSubmit={form.handleSubmit(onSubmit)} className="space-y-4">
            <div className="grid grid-cols-2 gap-3">
              <FormField control={form.control} name="firstName" render={({ field }) => (
                <FormItem><FormLabel>First Name *</FormLabel>
                  <FormControl><Input {...field} /></FormControl>
                  <FormMessage />
                </FormItem>
              )} />
              <FormField control={form.control} name="lastName" render={({ field }) => (
                <FormItem><FormLabel>Last Name *</FormLabel>
                  <FormControl><Input {...field} /></FormControl>
                  <FormMessage />
                </FormItem>
              )} />
            </div>
            <FormField control={form.control} name="email" render={({ field }) => (
              <FormItem><FormLabel>Work Email *</FormLabel>
                <FormControl><Input type="email" {...field} /></FormControl>
                <FormMessage />
              </FormItem>
            )} />
            <div className="grid grid-cols-2 gap-3">
              <FormField control={form.control} name="designation" render={({ field }) => (
                <FormItem><FormLabel>Designation</FormLabel>
                  <FormControl><Input placeholder="Software Engineer" {...field} /></FormControl>
                  <FormMessage />
                </FormItem>
              )} />
              <FormField control={form.control} name="department" render={({ field }) => (
                <FormItem><FormLabel>Department</FormLabel>
                  <FormControl><Input placeholder="Engineering" {...field} /></FormControl>
                  <FormMessage />
                </FormItem>
              )} />
            </div>
            <FormField control={form.control} name="phone" render={({ field }) => (
              <FormItem><FormLabel>Phone</FormLabel>
                <FormControl><Input type="tel" {...field} /></FormControl>
                <FormMessage />
              </FormItem>
            )} />
            <FormField control={form.control} name="joiningDate" render={({ field }) => (
              <FormItem><FormLabel>Joining Date</FormLabel>
                <FormControl><Input type="date" {...field} /></FormControl>
                <FormMessage />
              </FormItem>
            )} />
            <DialogFooter>
              <Button type="button" variant="outline" onClick={() => onOpenChange(false)}>Cancel</Button>
              <Button type="submit" disabled={createMutation.isPending}>
                {createMutation.isPending ? 'Adding…' : 'Add Employee'}
              </Button>
            </DialogFooter>
          </form>
        </Form>
      </DialogContent>
    </Dialog>
  );
}

// ─── Page ─────────────────────────────────────────────────────────────────────

export default function EmployeesPage() {
  const [search, setSearch] = useState('');
  const [deleteTarget, setDeleteTarget] = useState<EmployeeListItem | null>(null);
  const [addOpen, setAddOpen] = useState(false);
  const [isDeleting, setIsDeleting] = useState(false);

  const { page, setPage, pageSize, resetPage } = usePaginationState();
  const { isAdmin } = usePermissions();
  const { toast } = useToast();
  const queryClient = useQueryClient();
  const deleteMutation = useDeleteEmployee();

  const { data, isLoading, isError, error, refetch } = useListEmployees({
    page,
    pageSize,
    search: search || undefined,
  });

 
  const handleDeleteConfirm = async () => {
    if (!deleteTarget) return;
    setIsDeleting(true);
    try {
      await deleteMutation.mutateAsync({ employeeId: deleteTarget.employeeId });
      await queryClient.invalidateQueries({ queryKey: getListEmployeesQueryKey() });
      toast({
        title: 'Employee deleted',
        description: `${[deleteTarget.firstName, deleteTarget.lastName].filter(Boolean).join(' ')} has been removed.`,
      });
    } catch (e: unknown) {
      toast({
        title: 'Delete failed',
        description: e instanceof Error ? e.message : 'An error occurred.',
        variant: 'destructive',
      });
    } finally {
      setIsDeleting(false);
      setDeleteTarget(null);
    }
  };

  return (
    <div className="space-y-6">
      <PageHeader
        title="Employees"
        description="Manage your workforce, view details, and update profiles."
        actions={
          isAdmin ? (
           
            <Button onClick={() => setAddOpen(true)}>
              <Plus className="mr-2 h-4 w-4" /> Add Employee
            </Button>
          ) : undefined
        }
      />

      <div className="flex flex-col sm:flex-row items-center gap-4 justify-between bg-card p-4 border rounded-lg shadow-sm">
        <div className="w-full sm:w-72">
          <SearchInput
            value={search}
            onChange={(val) => { setSearch(val); resetPage(); }}
            placeholder="Search employees…"
          />
        </div>
        <div className="flex items-center gap-2 w-full sm:w-auto">
          <Button variant="outline" size="sm"><Filter className="mr-2 h-4 w-4" /> Filter</Button>
          <Button variant="outline" size="sm"><Download className="mr-2 h-4 w-4" /> Export</Button>
        </div>
      </div>

      <div className="bg-card border rounded-lg shadow-sm overflow-hidden">
        {isLoading ? (
          <SkeletonTable columns={6} rows={10} />
        ) : isError ? (
          <EmptyState
            title={getErrorTitle(error, 'Failed to load employees')}
            description={getErrorDescription(error)}
            onRetry={refetch}
          />
        ) : !data?.items.length ? (
          <EmptyState
            title="No employees found"
            description={search ? `No results match "${search}"` : 'Get started by adding your first employee.'}
            action={
              !search && isAdmin ? (
                <Button onClick={() => setAddOpen(true)}>
                  <Plus className="mr-2 h-4 w-4" /> Add Employee
                </Button>
              ) : undefined
            }
          />
        ) : (
          <>
            <div className="overflow-x-auto">
              <Table>
                <TableHeader>
                  <TableRow className="bg-muted/50 hover:bg-muted/50">
                    <TableHead>Employee</TableHead>
                    <TableHead>ID</TableHead>
                    <TableHead>Department</TableHead>
                    <TableHead>Designation</TableHead>
                    <TableHead>Status</TableHead>
                    <TableHead className="text-right">Actions</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {data.items.map((emp) => (
                    <EmployeeRow
                      key={emp.employeeId}
                      employee={emp}
                      isAdmin={isAdmin}
                      onDelete={setDeleteTarget}
                    />
                  ))}
                </TableBody>
              </Table>
            </div>
            <Pagination
              page={data.page}
              pageSize={data.pageSize}
              totalCount={data.totalCount}
              totalPages={data.totalPages}
              onPageChange={setPage}
            />
          </>
        )}
      </div>

      {/* Fixed: B1 — ConfirmDialog with real delete wired in */}
      <ConfirmDialog
        open={deleteTarget !== null}
        onOpenChange={(open) => { if (!open) setDeleteTarget(null); }}
        title="Delete employee?"
        description={`This will permanently remove ${[deleteTarget?.firstName, deleteTarget?.lastName].filter(Boolean).join(' ') || 'this employee'} and all associated data. This action cannot be undone.`}
        confirmText={isDeleting ? 'Deleting…' : 'Delete'}
        cancelText="Cancel"
        variant="destructive"
        onConfirm={handleDeleteConfirm}
      />

      {/* Fixed: B3 — Add Employee dialog */}
      <AddEmployeeDialog open={addOpen} onOpenChange={setAddOpen} />
    </div>
  );
}
