/**
 * ForgotPasswordPage.tsx
 *
 * FIX: the backend has always implemented POST /api/auth/forgot-password
 * (AuthController.ForgotPassword -> AuthService.ForgotPasswordAsync), but the
 * frontend never had a page to call it — LoginPage's "Forgot password?" link
 * only called e.preventDefault() and did nothing. This page closes that gap.
 *
 * The endpoint always responds with the same generic success message
 * regardless of whether the email exists (anti-enumeration), so this page
 * never reveals account existence either.
 */
import { useState } from 'react';
import { Link } from 'wouter';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import * as z from 'zod';
import { MailCheck } from 'lucide-react';
import { getErrorMessage } from '@/utils/apiError';
import { csrfFetch } from '@/utils/csrfFetch';

import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import {
  Form, FormControl, FormField, FormItem, FormLabel, FormMessage,
} from '@/components/ui/form';
import { useToast } from '@/hooks/use-toast';

const BASE = import.meta.env.BASE_URL.replace(/\/$/, '');

const forgotSchema = z.object({
  email: z.string().email('Please enter a valid email address.'),
});
type ForgotFormValues = z.infer<typeof forgotSchema>;

export default function ForgotPasswordPage() {
  const { toast } = useToast();
  const [submitted, setSubmitted] = useState(false);
  const [loading, setLoading] = useState(false);

  const form = useForm<ForgotFormValues>({
    resolver: zodResolver(forgotSchema),
    defaultValues: { email: '' },
  });

  const onSubmit = async (values: ForgotFormValues) => {
    setLoading(true);
    try {
      const res = await csrfFetch(`${BASE}/api/auth/forgot-password`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(values),
      });
      if (!res.ok) {
        const json = await res.json().catch(() => null);
        throw new Error(json?.message ?? 'Something went wrong. Please try again.');
      }
      // Always show the same success state — the API never reveals whether
      // the email is registered.
      setSubmitted(true);
    } catch (error: unknown) {
      toast({
        title: 'Request failed',
        description: getErrorMessage(error, 'Something went wrong. Please try again.'),
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
          <h2 className="text-3xl font-bold tracking-tight">Forgot password?</h2>
          <p className="text-sm text-muted-foreground">
            Enter your account email and we&apos;ll send you a link to reset your password.
          </p>
        </div>

        <div className="bg-card border shadow-sm rounded-xl p-6">
          {submitted ? (
            <div className="flex flex-col items-center gap-3 text-center py-4">
              <MailCheck className="h-10 w-10 text-primary" />
              <p className="font-medium">Check your inbox</p>
              <p className="text-sm text-muted-foreground">
                If that email is registered, a password reset link has been sent.
                The link expires in 30 minutes.
              </p>
            </div>
          ) : (
            <Form {...form}>
              <form onSubmit={form.handleSubmit(onSubmit)} className="space-y-4">
                <FormField
                  control={form.control}
                  name="email"
                  render={({ field }) => (
                    <FormItem>
                      <FormLabel>Email</FormLabel>
                      <FormControl>
                        <Input placeholder="name@company.com" {...field} />
                      </FormControl>
                      <FormMessage />
                    </FormItem>
                  )}
                />
                <Button type="submit" className="w-full" disabled={loading}>
                  {loading ? (
                    <div className="flex items-center gap-2">
                      <div className="h-4 w-4 animate-spin rounded-full border-2 border-primary-foreground border-t-transparent" />
                      <span>Sending…</span>
                    </div>
                  ) : (
                    'Send reset link'
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
