import { useEffect, useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import * as z from 'zod';
import QRCode from 'qrcode';
import { useGetProfile, getGetProfileQueryKey, useUpdateProfile } from '@workspace/api-client-react';
import { useQueryClient } from '@tanstack/react-query';
import { PageHeader } from '@/components/layout/PageHeader';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import {
  Form, FormControl, FormField, FormItem, FormLabel, FormMessage,
} from '@/components/ui/form';
import { useToast } from '@/hooks/use-toast';
import { getErrorMessage } from '@/utils/apiError';
import { csrfFetch } from '@/utils/csrfFetch';

const BASE = import.meta.env.BASE_URL.replace(/\/$/, '');

// ─── Profile schema ────────────────────────────────────────────────────────────

const profileSchema = z.object({
  fullName: z.string().min(1, 'Full name is required.').max(100),
});
type ProfileFormValues = z.infer<typeof profileSchema>;

// ─── Password schema ───────────────────────────────────────────────────────────

const passwordSchema = z.object({
  currentPassword: z.string().min(1, 'Current password is required.'),
  newPassword: z
    .string()
    .min(8, 'At least 8 characters.')
    .regex(/[A-Z]/, 'Must contain an uppercase letter.')
    .regex(/[0-9]/, 'Must contain a digit.'),
  confirmPassword: z.string().min(1, 'Please confirm your password.'),
}).refine((d) => d.newPassword === d.confirmPassword, {
  message: 'Passwords do not match.',
  path: ['confirmPassword'],
});
type PasswordFormValues = z.infer<typeof passwordSchema>;

// ─── Profile card ──────────────────────────────────────────────────────────────

function ProfileCard() {
  const { toast } = useToast();
  const queryClient = useQueryClient();
  const { data: profile, isLoading } = useGetProfile({ query: { queryKey: getGetProfileQueryKey() } });
  const updateMutation = useUpdateProfile();

  const form = useForm<ProfileFormValues>({
    resolver: zodResolver(profileSchema),
    defaultValues: { fullName: '' },
  });

  useEffect(() => {
    if (profile?.fullName) form.reset({ fullName: profile.fullName });
  }, [profile, form]);

  const onSubmit = async (values: ProfileFormValues) => {
    try {
      await updateMutation.mutateAsync({ data: values });
      await queryClient.invalidateQueries({ queryKey: getGetProfileQueryKey() });
      toast({ title: 'Profile updated', description: 'Your changes have been saved.' });
    } catch (error: unknown) {
      toast({
        title: 'Update failed',
        description: getErrorMessage(error, 'Could not save your changes. Please try again.'),
        variant: 'destructive',
      });
    }
  };

  return (
    <Card>
      <CardHeader>
        <CardTitle>Profile Information</CardTitle>
        <CardDescription>Update your personal details and contact information.</CardDescription>
      </CardHeader>
      <CardContent>
        <Form {...form}>
          <form onSubmit={form.handleSubmit(onSubmit)} className="space-y-4">
            <div className="grid grid-cols-2 gap-4">
              <FormField control={form.control} name="fullName" render={({ field }) => (
                <FormItem>
                  <FormLabel>Full Name</FormLabel>
                  <FormControl>
                    <Input placeholder="Your name" disabled={isLoading} {...field} />
                  </FormControl>
                  <FormMessage />
                </FormItem>
              )} />
              <div className="space-y-2">
                <label htmlFor="profile-email" className="text-sm font-medium leading-none">
                  Email
                </label>
                <Input id="profile-email" type="email" value={profile?.email ?? ''} disabled className="cursor-not-allowed" />
              </div>
            </div>
            <div className="space-y-2">
              <label htmlFor="profile-role" className="text-sm font-medium leading-none">
                Role
              </label>
              <Input id="profile-role" value={profile?.role ?? ''} disabled className="cursor-not-allowed" />
            </div>
            <div className="pt-4">
              <Button type="submit" disabled={updateMutation.isPending || isLoading}>
                {updateMutation.isPending ? (
                  <span className="flex items-center gap-2">
                    <span className="h-4 w-4 animate-spin rounded-full border-2 border-primary-foreground border-t-transparent" />
                    Saving…
                  </span>
                ) : 'Save Changes'}
              </Button>
            </div>
          </form>
        </Form>
      </CardContent>
    </Card>
  );
}

// ─── Change Password card ──────────────────────────────────────────────────────

function ChangePasswordCard() {
  const { toast } = useToast();
  const form = useForm<PasswordFormValues>({
    resolver: zodResolver(passwordSchema),
    defaultValues: { currentPassword: '', newPassword: '', confirmPassword: '' },
  });

  const onSubmit = async (values: PasswordFormValues) => {
    try {
      const res = await csrfFetch(`${BASE}/api/auth/change-password`, {
        method: 'POST',
        credentials: 'include',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ currentPassword: values.currentPassword, newPassword: values.newPassword }),
      });
      const json = await res.json();
      if (!res.ok) throw new Error(json.message ?? 'Failed to change password.');
      toast({ title: 'Password changed', description: 'Your password has been updated.' });
      form.reset();
    } catch (error: unknown) {
      toast({
        title: 'Failed to change password',
        description: error instanceof Error ? error.message : 'An error occurred.',
        variant: 'destructive',
      });
    }
  };

  return (
    <Card>
      <CardHeader>
        <CardTitle>Change Password</CardTitle>
        <CardDescription>Must be at least 8 characters with an uppercase letter and a digit.</CardDescription>
      </CardHeader>
      <CardContent>
        <Form {...form}>
          <form onSubmit={form.handleSubmit(onSubmit)} className="space-y-4">
            <FormField control={form.control} name="currentPassword" render={({ field }) => (
              <FormItem>
                <FormLabel>Current Password</FormLabel>
                <FormControl><Input type="password" {...field} /></FormControl>
                <FormMessage />
              </FormItem>
            )} />
            <FormField control={form.control} name="newPassword" render={({ field }) => (
              <FormItem>
                <FormLabel>New Password</FormLabel>
                <FormControl><Input type="password" {...field} /></FormControl>
                <FormMessage />
              </FormItem>
            )} />
            <FormField control={form.control} name="confirmPassword" render={({ field }) => (
              <FormItem>
                <FormLabel>Confirm New Password</FormLabel>
                <FormControl><Input type="password" {...field} /></FormControl>
                <FormMessage />
              </FormItem>
            )} />
            <Button type="submit" variant="outline" disabled={form.formState.isSubmitting}>
              {form.formState.isSubmitting ? 'Changing…' : 'Change Password'}
            </Button>
          </form>
        </Form>
      </CardContent>
    </Card>
  );
}

// ─── MFA Setup card ────────────────────────────────────────────────────────────

type MfaStep = 'idle' | 'qr' | 'confirm' | 'done';

function MfaCard() {
  const { toast } = useToast();
  const [step, setStep] = useState<MfaStep>('idle');
  const [qrDataUrl, setQrDataUrl] = useState('');
  const [manualKey, setManualKey] = useState('');
  const [code, setCode] = useState('');
  const [loading, setLoading] = useState(false);

  const startSetup = async () => {
    setLoading(true);
    try {
      const res = await csrfFetch(`${BASE}/api/auth/mfa/setup`, {
        method: 'POST', credentials: 'include',
      });
      const json = await res.json();
      if (!res.ok) throw new Error(json.message);
      setManualKey(json.data.manualEntryKey);
      const dataUrl = await QRCode.toDataURL(json.data.qrCodeUri, { width: 200 });
      setQrDataUrl(dataUrl);
      setStep('qr');
    } catch (e) {
      toast({ title: 'Setup failed', description: String(e), variant: 'destructive' });
    } finally {
      setLoading(false);
    }
  };

  const confirmCode = async () => {
    if (code.length !== 6) return;
    setLoading(true);
    try {
      const res = await csrfFetch(`${BASE}/api/auth/mfa/confirm`, {
        method: 'POST',
        credentials: 'include',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ code }),
      });
      const json = await res.json();
      if (!res.ok) throw new Error(json.message);
      setStep('done');
      toast({ title: 'MFA enabled', description: 'Two-factor authentication is now active.' });
    } catch (e) {
      toast({ title: 'Invalid code', description: String(e), variant: 'destructive' });
    } finally {
      setLoading(false);
    }
  };

  return (
    <Card>
      <CardHeader>
        <CardTitle>Two-Factor Authentication (TOTP)</CardTitle>
        <CardDescription>Use an authenticator app (Google Authenticator, Authy) for extra security.</CardDescription>
      </CardHeader>
      <CardContent className="space-y-4">
        {step === 'idle' && (
          <Button variant="outline" onClick={startSetup} disabled={loading}>
            {loading ? 'Starting…' : 'Set Up MFA'}
          </Button>
        )}

        {step === 'qr' && (
          <div className="space-y-4">
            <p className="text-sm text-muted-foreground">
              Scan this QR code with your authenticator app, then enter the 6-digit code below to confirm.
            </p>
            {qrDataUrl && <img src={qrDataUrl} alt="TOTP QR code" className="rounded-lg border" />}
            <p className="text-xs text-muted-foreground">
              Or enter manually: <code className="bg-muted px-1 py-0.5 rounded">{manualKey}</code>
            </p>
            <div className="flex items-center gap-3">
              <Input
                placeholder="6-digit code"
                maxLength={6}
                value={code}
                onChange={(e) => setCode(e.target.value.replace(/\D/g, ''))}
                className="w-40"
              />
              <Button onClick={confirmCode} disabled={code.length !== 6 || loading}>
                {loading ? 'Verifying…' : 'Confirm'}
              </Button>
              <Button variant="ghost" onClick={() => setStep('idle')}>Cancel</Button>
            </div>
          </div>
        )}

        {step === 'done' && (
          <div className="flex items-center gap-2 text-green-600">
            <span className="h-4 w-4 rounded-full bg-green-500 inline-block" />
            <span className="text-sm font-medium">MFA is enabled on your account.</span>
          </div>
        )}
      </CardContent>
    </Card>
  );
}


// ─── Page ──────────────────────────────────────────────────────────────────────

export default function SettingsPage() {
  return (
    <div className="space-y-6 max-w-4xl mx-auto">
      <PageHeader
        title="Settings"
        description="Manage your account settings and preferences."
      />

      <ProfileCard />
      <ChangePasswordCard />   {/* Fixed: S1 */}
      <MfaCard />              {/* Fixed: M3 */}

      <Card>
        <CardHeader>
          <CardTitle>Company Preferences</CardTitle>
          <CardDescription>Global settings for HRMS Pro.</CardDescription>
        </CardHeader>
        <CardContent className="space-y-4">
          <p className="text-sm text-muted-foreground">
            Company configuration is managed by System Administrators.
          </p>
          <Button variant="outline" disabled>
            Go to Admin Console
          </Button>
        </CardContent>
      </Card>
    </div>
  );
}
