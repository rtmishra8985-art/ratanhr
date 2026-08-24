/**
 * ResetPasswordPage.tsx
 *
 * FIX: closes the same gap as ForgotPasswordPage — the backend's password
 * reset email (AuthService.ForgotPasswordAsync) has always linked to
 * "{AppBaseUrl}/reset-password.html?token=..." but that .html page was
 * removed from wwwroot (see Program.cs: "the legacy *.html pages ... were
 * removed from wwwroot and archived under /legacy-ui") and never replaced
 * with an SPA route. Every password-reset email sent a link to a 404.
 *
 * This page reads the token from the query string, posts it with the new
 * password to POST /api/auth/reset-password, and redirects to /login on
 * success. Password policy (min length, must-match confirm) mirrors the
 * server-side PasswordPolicy checked by AuthController.ResetPassword.
 */
import { useState } from 'react';
import { Link, useLocation, useSearch } from 'wouter';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import * as z from 'zod';
import { CheckCircle2, XCircle } from 'lucide-react';
import { getErrorMessage } from '@/utils/apiError';
import { csrfFetch } from '@/utils/csrfFetch';

import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import {
  Form, FormControl, FormField, FormItem, FormLabel, FormMessage,
} from '@/components/ui/form';
import { useToast } from '@/hooks/use-toast';

const BASE = import.meta.env.BASE_URL.replace(/\/$/, '');

// Mirrors the server-side floor (Security:PasswordPolicy.MinLength may only be
// raised, never lowered, below 8 — see PasswordPolicyOptions). The server is
// still the authoritative gate; this is a client-side convenience check only.
const resetSchema = z
  .object({
    newPassword: z.string().min(8, 'Password must be at least 8 characters.'),
    confirmPassword: z.string().min(1, 'Please confirm your new password.'),
  })
  .refine((data) => data.newPassword === data.confirmPassword, {
    message: 'Passwords do not match.',
    path: ['confirmPassword'],
  });
type ResetFormValues = z.infer<typeof resetSchema>;

export default function ResetPasswordPage() {
  const { toast } = useToast();
  const [, setLocation] = useLocation();
  const search = useSearch();
  const token = new URLSearchParams(search).get('token') ?? '';

  const [loading, setLoading] = useState(false);
  const [success, setSuccess] = useState(false);

  const form = useForm<ResetFormValues>({
    resolver: zodResolver(resetSchema),
    defaultValues: { newPassword: '', confirmPassword: '' },
  });

  const onSubmit = async (values: ResetFormValues) => {
    setLoading(true);
    try {
      const res = await csrfFetch(`${BASE}/api/auth/reset-password`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ token, ...values }),
      });
      const json = await res.json().catch(() => null);
      if (!res.ok) throw new Error(json?.message ?? 'Invalid or expired reset link.');

      setSuccess(true);
      toast({ title: 'Password reset', description: 'You can now log in with your new password.' });
      setTimeout(() => setLocation('/login'), 2000);
    } catch (error: unknown) {
      toast({
        title: 'Reset failed',
        description: getErrorMessage(error, 'Invalid or expired reset link.'),
        variant: 'destructive',
      });
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="flex min-h-screen w-full items-center justify-center bg-muted/30 p-6">
      <div className="mx-auto w-full max-w-md space-y-6">
        <div className="flex flex-col space-y-2 text-center">
          <div className="flex items-center gap-2 justify-center mb-4">
            <div className="h-10 w-10 rounded-md bg-primary flex items-center justify-center">
              <span className="text-primary-foreground font-bold text-2xl">H</span>
            </div>
            <span className="font-bold text-2xl tracking-tight">HRMS Pro</span>
          </div>
          <h2 className="text-3xl font-bold tracking-tight">Reset password</h2>
          <p className="text-sm text-muted-foreground">
            Choose a new password for your account.
          </p>
        </div>

        <div className="bg-card border shadow-sm rounded-xl p-6">
          {!token ? (
            <div className="flex flex-col items-center gap-3 text-center py-4">
              <XCircle className="h-10 w-10 text-destructive" />
              <p className="font-medium">Invalid reset link</p>
              <p className="text-sm text-muted-foreground">
                This link is missing its reset token. Request a new one from the
                forgot-password page.
              </p>
            </div>
          ) : success ? (
            <div className="flex flex-col items-center gap-3 text-center py-4">
              <CheckCircle2 className="h-10 w-10 text-primary" />
              <p className="font-medium">Password updated</p>
              <p className="text-sm text-muted-foreground">Redirecting you to login…</p>
            </div>
          ) : (
            <Form {...form}>
              <form onSubmit={form.handleSubmit(onSubmit)} className="space-y-4">
                <FormField
                  control={form.control}
                  name="newPassword"
                  render={({ field }) => (
                    <FormItem>
                      <FormLabel>New password</FormLabel>
                      <FormControl>
                        <Input type="password" placeholder="••••••••" {...field} />
                      </FormControl>
                      <FormMessage />
                    </FormItem>
                  )}
                />
                <FormField
                  control={form.control}
                  name="confirmPassword"
                  render={({ field }) => (
                    <FormItem>
                      <FormLabel>Confirm new password</FormLabel>
                      <FormControl>
                        <Input type="password" placeholder="••••••••" {...field} />
                      </FormControl>
                      <FormMessage />
                    </FormItem>
                  )}
                />
                <Button type="submit" className="w-full" disabled={loading}>
                  {loading ? (
                    <div className="flex items-center gap-2">
                      <div className="h-4 w-4 animate-spin rounded-full border-2 border-primary-foreground border-t-transparent" />
                      <span>Resetting…</span>
                    </div>
                  ) : (
                    'Reset password'
                  )}
                </Button>
              </form>
            </Form>
          )}
        </div>

        <p className="text-center text-sm text-muted-foreground">
          <Link href="/login" className="font-medium text-primary hover:underline">
            Back to login
          </Link>
        </p>
      </div>
    </div>
  );
}
