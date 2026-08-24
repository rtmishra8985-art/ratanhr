// Wired to existing backend endpoints: GET/POST/PUT/DELETE /api/biometric/devices
// SEC-BIOMETRIC-01: All API calls use credentials: 'include' (cookie-based auth).
import { useState } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import * as z from 'zod';
import { Plus, Pencil, Trash2, Wifi, WifiOff, RefreshCw } from 'lucide-react';
import { toast } from 'sonner';

import { PageHeader }  from '@/components/layout/PageHeader';
import { Button }      from '@/components/ui/button';
import { Badge }       from '@/components/ui/badge';
import { Input }       from '@/components/ui/input';
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter } from '@/components/ui/dialog';
import { AlertDialog, AlertDialogAction, AlertDialogCancel, AlertDialogContent,
  AlertDialogDescription, AlertDialogFooter, AlertDialogHeader, AlertDialogTitle } from '@/components/ui/alert-dialog';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Form, FormControl, FormField, FormItem, FormLabel, FormMessage } from '@/components/ui/form';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Skeleton } from '@/components/ui/skeleton';
import { csrfFetch } from '@/utils/csrfFetch';

const BASE = import.meta.env.BASE_URL?.replace(/\/$/, '') ?? '';

// ─── Types ────────────────────────────────────────────────────────────────────

interface BiometricDevice {
  id: number;
  name: string;
  vendor: string;
  ipAddress: string;
  port: number;
  isEnabled: boolean;
  isOnline?: boolean;
  lastSyncAt?: string;
}

interface PagedDevices { items: BiometricDevice[]; totalCount: number; page: number; pageSize: number; }

// ─── Schema ───────────────────────────────────────────────────────────────────

const deviceSchema = z.object({
  name:      z.string().min(1, 'Device name is required').max(100),
  vendor:    z.string().min(1, 'Vendor is required'),
  ipAddress: z.string().min(1, 'IP address is required').max(50),
  port:      z.coerce.number().int().min(1).max(65535),
});
type DeviceForm = z.infer<typeof deviceSchema>;

const VENDORS = ['ZKTeco', 'eSSL', 'Matrix', 'Suprema', 'Realtime', 'Anviz', 'Hikvision'];

// ─── API helpers ──────────────────────────────────────────────────────────────

const api = {
  list:   (page = 1, size = 25) =>
    csrfFetch(`${BASE}/api/biometric/devices?page=${page}&pageSize=${size}`, { credentials: 'include' }),
  create: (body: DeviceForm) =>
    csrfFetch(`${BASE}/api/biometric/devices`, { method: 'POST', credentials: 'include',
      headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body) }),
  update: (id: number, body: DeviceForm) =>
    csrfFetch(`${BASE}/api/biometric/devices/${id}`, { method: 'PUT', credentials: 'include',
      headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body) }),
  delete: (id: number) =>
    csrfFetch(`${BASE}/api/biometric/devices/${id}`, { method: 'DELETE', credentials: 'include' }),
  test:   (id: number) =>
    csrfFetch(`${BASE}/api/biometric/devices/${id}/test`, { method: 'POST', credentials: 'include' }),
  enable: (id: number) =>
    csrfFetch(`${BASE}/api/biometric/devices/${id}/enable`, { method: 'POST', credentials: 'include' }),
  disable:(id: number) =>
    csrfFetch(`${BASE}/api/biometric/devices/${id}/disable`, { method: 'POST', credentials: 'include' }),
};

// ─── Component ────────────────────────────────────────────────────────────────

export default function BiometricDevicesPage() {
  const qc = useQueryClient();
  const [page,       setPage]     = useState(1);
  const [dialogOpen, setDialog]   = useState(false);
  const [deleteId,   setDeleteId] = useState<number | null>(null);
  const [editing,    setEditing]  = useState<BiometricDevice | null>(null);
  const [testingId,  setTestingId]= useState<number | null>(null);

  const { data, isLoading } = useQuery<PagedDevices>({
    queryKey: ['biometric-devices', page],
    queryFn: async () => {
      const r = await api.list(page, 25);
      if (!r.ok) throw new Error('Failed to load devices');
      return r.json();
    },
  });

  const form = useForm<DeviceForm>({
    resolver: zodResolver(deviceSchema),
    defaultValues: { name: '', vendor: '', ipAddress: '', port: 4370 },
  });

  const closeDialog = () => {
    setDialog(false);
    setEditing(null);
    form.reset({ name: '', vendor: '', ipAddress: '', port: 4370 });
  };

  const openCreate = () => {
    setEditing(null);
    form.reset({ name: '', vendor: '', ipAddress: '', port: 4370 });
    setDialog(true);
  };

  const openEdit = (device: BiometricDevice) => {
    setEditing(device);
    form.reset({ name: device.name, vendor: device.vendor, ipAddress: device.ipAddress, port: device.port });
    setDialog(true);
  };

  const saveMut = useMutation({
    mutationFn: async (values: DeviceForm) => {
      const res = editing ? await api.update(editing.id, values) : await api.create(values);
      if (!res.ok) {
        const d = await res.json().catch(() => ({}));
        throw new Error(d?.message ?? 'Save failed');
      }
      return res.json();
    },
    onSuccess: () => {
      toast.success(editing ? 'Device updated.' : 'Device created.');
      qc.invalidateQueries({ queryKey: ['biometric-devices'] });
      closeDialog();
    },
    onError: (e: Error) => toast.error(e.message),
  });

  const deleteMut = useMutation({
    mutationFn: async (id: number) => {
      const res = await api.delete(id);
      if (!res.ok) throw new Error('Delete failed');
    },
    onSuccess: () => {
      toast.success('Device removed.');
      qc.invalidateQueries({ queryKey: ['biometric-devices'] });
      setDeleteId(null);
    },
    onError: (e: Error) => toast.error(e.message),
  });

  const testMut = useMutation({
    mutationFn: async (id: number) => {
      setTestingId(id);
      const res = await api.test(id);
      if (!res.ok) throw new Error('Connectivity test failed');
      return res.json();
    },
    onSuccess: () => {
      toast.success('Device is reachable.');
      qc.invalidateQueries({ queryKey: ['biometric-devices'] });
    },
    onError: (e: Error) => toast.error(e.message),
    onSettled: () => setTestingId(null),
  });

  const toggleMut = useMutation({
    mutationFn: async ({ id, enabled }: { id: number; enabled: boolean }) => {
      const res = enabled ? await api.disable(id) : await api.enable(id);
      if (!res.ok) throw new Error('Toggle failed');
    },
    onSuccess: () => {
      toast.success('Device status updated.');
      qc.invalidateQueries({ queryKey: ['biometric-devices'] });
    },
    onError: (e: Error) => toast.error(e.message),
  });

  const devices    = data?.items ?? [];
  const total      = data?.totalCount ?? 0;
  const totalPages = Math.ceil(total / 25);

  return (
    <div className="space-y-6">
      <PageHeader
        title="Biometric Devices"
        description="Manage registered biometric devices for attendance capture."
        actions={
          <Button onClick={openCreate}>
            <Plus className="h-4 w-4 mr-2" />Add Device
          </Button>
        }
      />

      <div className="rounded-md border">
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>Device Name</TableHead>
              <TableHead>Vendor</TableHead>
              <TableHead>IP Address</TableHead>
              <TableHead>Port</TableHead>
              <TableHead>Status</TableHead>
              <TableHead>Last Sync</TableHead>
              <TableHead className="text-right">Actions</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {isLoading
              ? Array.from({ length: 5 }).map((_, i) => (
                  <TableRow key={i}>
                    {Array.from({ length: 7 }).map((__, j) => (
                      <TableCell key={j}><Skeleton className="h-4 w-full" /></TableCell>
                    ))}
                  </TableRow>
                ))
              : devices.length === 0
                ? <TableRow><TableCell colSpan={7} className="text-center text-muted-foreground py-8">No biometric devices registered.</TableCell></TableRow>
                : devices.map(device => (
                    <TableRow key={device.id}>
                      <TableCell className="font-medium">{device.name}</TableCell>
                      <TableCell>{device.vendor}</TableCell>
                      <TableCell className="font-mono text-sm">{device.ipAddress}</TableCell>
                      <TableCell className="font-mono text-sm">{device.port}</TableCell>
                      <TableCell>
                        <div className="flex items-center gap-2">
                          <Badge variant={device.isEnabled ? 'default' : 'secondary'}>
                            {device.isEnabled ? 'Enabled' : 'Disabled'}
                          </Badge>
                          {device.isOnline !== undefined && (
                            device.isOnline
                              ? <Wifi className="h-3 w-3 text-green-500" />
                              : <WifiOff className="h-3 w-3 text-muted-foreground" />
                          )}
                        </div>
                      </TableCell>
                      <TableCell className="text-sm text-muted-foreground">
                        {device.lastSyncAt ? new Date(device.lastSyncAt).toLocaleString('en-IN', { dateStyle: 'medium', timeStyle: 'short' }) : '—'}
                      </TableCell>
                      <TableCell className="text-right">
                        <div className="flex justify-end gap-1">
                          <Button
                            size="icon" variant="ghost" className="h-8 w-8"
                            title="Test connectivity"
                            disabled={testingId === device.id}
                            onClick={() => testMut.mutate(device.id)}
                          >
                            <RefreshCw className={`h-4 w-4 ${testingId === device.id ? 'animate-spin' : ''}`} />
                          </Button>
                          <Button
                            size="icon" variant="ghost" className="h-8 w-8"
                            title="Edit device"
                            onClick={() => openEdit(device)}
                          >
                            <Pencil className="h-4 w-4" />
                          </Button>
                          <Button
                            size="sm" variant="outline"
                            className="h-8 text-xs"
                            onClick={() => toggleMut.mutate({ id: device.id, enabled: device.isEnabled })}
                          >
                            {device.isEnabled ? 'Disable' : 'Enable'}
                          </Button>
                          <Button
                            size="icon" variant="ghost"
                            className="h-8 w-8 text-destructive hover:text-destructive"
                            title="Delete device"
                            onClick={() => setDeleteId(device.id)}
                          >
                            <Trash2 className="h-4 w-4" />
                          </Button>
                        </div>
                      </TableCell>
                    </TableRow>
                  ))
            }
          </TableBody>
        </Table>
      </div>

      {/* Pagination */}
      {totalPages > 1 && (
        <div className="flex items-center justify-between text-sm text-muted-foreground">
          <span>{total} device{total !== 1 ? 's' : ''}</span>
          <div className="flex gap-2">
            <Button variant="outline" size="sm" disabled={page <= 1} onClick={() => setPage(p => p - 1)}>Previous</Button>
            <span className="self-center">Page {page} of {totalPages}</span>
            <Button variant="outline" size="sm" disabled={page >= totalPages} onClick={() => setPage(p => p + 1)}>Next</Button>
          </div>
        </div>
      )}

      {/* Create / Edit Dialog */}
      <Dialog open={dialogOpen} onOpenChange={open => { if (!open) closeDialog(); else setDialog(true); }}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>{editing ? 'Edit Device' : 'Add Biometric Device'}</DialogTitle>
          </DialogHeader>
          <Form {...form}>
            <form onSubmit={form.handleSubmit(v => saveMut.mutate(v))} className="space-y-4">
              <FormField control={form.control} name="name" render={({ field }) => (
                <FormItem>
                  <FormLabel>Device Name</FormLabel>
                  <FormControl><Input {...field} placeholder="Main Entrance" /></FormControl>
                  <FormMessage />
                </FormItem>
              )} />
              <FormField control={form.control} name="vendor" render={({ field }) => (
                <FormItem>
                  <FormLabel>Vendor</FormLabel>
                  <Select onValueChange={field.onChange} value={field.value}>
                    <FormControl>
                      <SelectTrigger><SelectValue placeholder="Select vendor…" /></SelectTrigger>
                    </FormControl>
                    <SelectContent>
                      {VENDORS.map(v => <SelectItem key={v} value={v}>{v}</SelectItem>)}
                    </SelectContent>
                  </Select>
                  <FormMessage />
                </FormItem>
              )} />
              <div className="grid grid-cols-2 gap-4">
                <FormField control={form.control} name="ipAddress" render={({ field }) => (
                  <FormItem>
                    <FormLabel>IP Address</FormLabel>
                    <FormControl><Input {...field} placeholder="192.168.1.100" /></FormControl>
                    <FormMessage />
                  </FormItem>
                )} />
                <FormField control={form.control} name="port" render={({ field }) => (
                  <FormItem>
                    <FormLabel>Port</FormLabel>
                    <FormControl><Input {...field} type="number" placeholder="4370" /></FormControl>
                    <FormMessage />
                  </FormItem>
                )} />
              </div>
              <DialogFooter>
                <Button variant="outline" type="button" onClick={closeDialog}>Cancel</Button>
                <Button type="submit" disabled={saveMut.isPending}>
                  {saveMut.isPending ? 'Saving…' : 'Save'}
                </Button>
              </DialogFooter>
            </form>
          </Form>
        </DialogContent>
      </Dialog>

      {/* Delete confirmation */}
      <AlertDialog open={deleteId !== null} onOpenChange={open => { if (!open) setDeleteId(null); }}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Remove Device?</AlertDialogTitle>
            <AlertDialogDescription>
              This will permanently remove the biometric device. Existing attendance records will not be affected.
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>Cancel</AlertDialogCancel>
            <AlertDialogAction
              className="bg-destructive text-destructive-foreground hover:bg-destructive/90"
              onClick={() => deleteId !== null && deleteMut.mutate(deleteId)}
            >
              Remove
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </div>
  );
}
