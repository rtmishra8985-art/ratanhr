import { useState } from 'react';
import { flushSync } from 'react-dom';
import { useLocation, Link } from 'wouter';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import * as z from 'zod';
import { Eye, EyeOff, ShieldCheck } from 'lucide-react';
import { useLogin } from '@workspace/api-client-react';
import type { LoginPortal } from '@/types/domain';
import { useAuth } from '@/hooks/useAuth';
import { getErrorMessage } from '@/utils/apiError';

import { Button }  from '@/components/ui/button';
import { Input }   from '@/components/ui/input';
import { Label }   from '@/components/ui/label';
import { Tabs, TabsList, TabsTrigger } from '@/components/ui/tabs';
import {
  Form, FormControl, FormField, FormItem, FormLabel, FormMessage,
} from '@/components/ui/form';
import { useToast } from '@/hooks/use-toast';
import { csrfFetch } from '@/utils/csrfFetch';
import { COOKIE_MODE_SENTINEL } from '@/utils/tokenStorage';

const BASE = import.meta.env.BASE_URL.replace(/\/$/, '');

const PORTALS: { value: LoginPortal; label: string }[] = [
  { value: 'employee',   label: 'Employee' },
  { value: 'admin',      label: 'Admin' },
  { value: 'superadmin', label: 'Super Admin' },
];

const loginSchema = z.object({
  email:    z.string().email('Please enter a valid email address.'),
  password: z.string().min(1, 'Password is required.'),
});
type LoginFormValues = z.infer<typeof loginSchema>;

// ─── MFA step ─────────────────────────────────────────────────────────────────

interface MfaStepProps {
  tempToken: string;
  onSuccess: () => void;
  onCancel: () => void;
}

function MfaStep({ tempToken, onSuccess, onCancel }: MfaStepProps) {
  const { toast } = useToast();
  const [code, setCode] = useState('');
  const [loading, setLoading] = useState(false);

  const verify = async () => {
    if (code.length !== 6) return;
    setLoading(true);
    try {
      const res = await csrfFetch(`${BASE}/api/auth/mfa/verify`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ tempToken, code }),
      });
      const json = await res.json();
      if (!res.ok) throw new Error(json.message ?? 'Invalid code.');
      // MFA verification sets the HttpOnly access-token cookie server-side.
      onSuccess();
    } catch (e) {
      toast({ title: 'Invalid code', description: String(e), variant: 'destructive' });
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="space-y-5">
      <div className="flex flex-col items-center gap-2 text-center">
        <ShieldCheck className="h-10 w-10 text-primary" />
        <h3 className="text-lg font-semibold">Two-Factor Verification</h3>
        <p className="text-sm text-muted-foreground">
          Enter the 6-digit code from your authenticator app.
        </p>
      </div>
      <Input
        type="text"
        inputMode="numeric"
        pattern="[0-9]*"
        maxLength={6}
        placeholder="______"
        className="text-center text-2xl tracking-[0.5em] font-mono h-14"
        value={code}
        onChange={(e) => setCode(e.target.value.replace(/\D/g, ''))}
        autoFocus
      />
      <Button className="w-full" onClick={verify} disabled={code.length !== 6 || loading}>
        {loading ? (
          <span className="flex items-center gap-2">
            <span className="h-4 w-4 animate-spin rounded-full border-2 border-primary-foreground border-t-transparent" />
            Verifying…
          </span>
        ) : 'Verify'}
      </Button>
      <Button variant="ghost" className="w-full" onClick={onCancel}>
        Back to Login
      </Button>
    </div>
  );
}

// ─── Login form ────────────────────────────────────────────────────────────────

export default function LoginPage() {
  const [showPassword, setShowPassword] = useState(false);
  const [portal, setPortal] = useState<LoginPortal>('employee');
  const [, setLocation] = useLocation();
  const { setToken } = useAuth();
  const { toast } = useToast();

  const [mfaState, setMfaState] = useState<{ tempToken: string } | null>(null);

  const loginMutation = useLogin();

  const form = useForm<LoginFormValues>({
    resolver: zodResolver(loginSchema),
    defaultValues: { email: '', password: '' },
  });

  const onSubmit = async (values: LoginFormValues) => {
    try {
      // FIX: portal must be sent on every login request. The backend's
      // LoginDto.Portal defaults to "employee" and AuthService rejects any
      // request whose Portal does not match the account's Role with 401 —
      // previously this field was never sent, so only Employee accounts
      // could ever authenticate through this form.
      const data = await loginMutation.mutateAsync({ data: { ...values, portal } });

      if (data.mfaRequired && data.tempToken) {
        setMfaState({ tempToken: data.tempToken });
        return;
      }

      // Login sets the HttpOnly access-token cookie server-side. Keep the
      // client state in cookie mode regardless of the response casing.
      //
      // BUGFIX (redirect-after-second-login race): setToken() updates AuthContext's
      // state, which AuthGuard reads via context to decide whether to redirect to
      // /login. React 18 batches this setState with the setLocation('/dashboard')
      // call below into a single render pass — but wouter's setLocation triggers an
      // immediate, synchronous route switch (it uses useSyncExternalStore under the
      // hood), which can mount the /dashboard route's <AuthGuard> BEFORE the batched
      // setToken update has actually committed through context. When that happens,
      // AuthGuard briefly reads the STALE isAuthenticated (false, left over from the
      // just-completed logout) and its own redirect effect immediately bounces the
      // user straight back to /login — even though the login API call itself
      // succeeded and the toast correctly says "Login successful". This only
      // surfaces on a SECOND login (logout → login) because the token state has to
      // transition false→true; the very first login never has this problem because
      // the app already starts in the authenticated (COOKIE_MODE_SENTINEL) state, so
      // there is no stale "false" render to race against. flushSync forces the
      // setToken update to commit synchronously BEFORE setLocation runs, so
      // AuthGuard's context read is always up to date by the time it mounts.
      flushSync(() => setToken(COOKIE_MODE_SENTINEL));
      toast({ title: 'Login successful', description: 'Welcome back to HRMS Pro.' });
      setLocation('/dashboard');
    } catch (error: unknown) {
      toast({
        title: 'Login failed',
        description: getErrorMessage(error, 'Invalid email or password. Please try again.'),
        variant: 'destructive',
      });
    }
  };

  const handleMfaSuccess = () => {
    // See the BUGFIX comment in onSubmit above — same race, same fix.
    flushSync(() => setToken(COOKIE_MODE_SENTINEL));
    toast({ title: 'Login successful', description: 'Welcome back to HRMS Pro.' });
    setLocation('/dashboard');
  };

  return (
    <div className="flex min-h-screen w-full flex-col md:flex-row bg-muted/30">
      {/* Left panel */}
      <div className="hidden md:flex flex-1 flex-col justify-between bg-primary p-10 text-primary-foreground">
        <div className="flex items-center gap-2">
          <div className="h-10 w-10 rounded-md bg-white flex items-center justify-center">
            <span className="text-primary font-bold text-2xl">H</span>
          </div>
          <span className="font-bold text-2xl tracking-tight">HRMS Pro</span>
        </div>

        <div className="max-w-md">
          <h1 className="text-4xl font-bold mb-4 leading-tight">
            Enterprise HR Management for Modern Teams
          </h1>
          <p className="text-primary-foreground/80 text-lg">
            Manage your workforce lifecycle — hiring, attendance, payroll, performance, assets,
            and helpdesk — all from one precision-engineered control center.
          </p>
        </div>

        <div className="text-primary-foreground/60 text-sm">
          &copy; {new Date().getFullYear()} HRMS Pro Inc. All rights reserved.
        </div>
      </div>

      {/* Right panel */}
      <div className="flex flex-1 items-center justify-center p-6 md:p-10">
        <div className="mx-auto w-full max-w-md space-y-6">
          <div className="flex flex-col space-y-2 text-center md:text-left">
            <div className="flex items-center gap-2 justify-center md:hidden mb-4">
              <div className="h-10 w-10 rounded-md bg-primary flex items-center justify-center">
                <span className="text-primary-foreground font-bold text-2xl">H</span>
              </div>
              <span className="font-bold text-2xl tracking-tight">HRMS Pro</span>
            </div>
            <h2 className="text-3xl font-bold tracking-tight">Welcome back</h2>
            <p className="text-sm text-muted-foreground">
              {mfaState ? 'Complete two-factor verification' : 'Enter your credentials to access your account'}
            </p>
          </div>

          <div className="bg-card border shadow-sm rounded-xl p-6">
            {/* Fixed: M3 — show MFA step instead of form when tempToken present */}
            {mfaState ? (
              <MfaStep
                tempToken={mfaState.tempToken}
                onSuccess={handleMfaSuccess}
                onCancel={() => setMfaState(null)}
              />
            ) : (
              <Form {...form}>
                <form onSubmit={form.handleSubmit(onSubmit)} className="space-y-4">
                  {/* FIX: portal selector — the backend enforces an exact match between
                      this value and the account's Role, so it must be explicit rather than
                      silently defaulting to "employee". Uses plain Label (not FormLabel),
                      since FormLabel requires react-hook-form's FormField context and this
                      value is local component state, not a form field. */}
                  <div className="space-y-2">
                    <Label>Portal</Label>
                    <Tabs value={portal} onValueChange={(v) => setPortal(v as LoginPortal)}>
                      <TabsList className="grid w-full grid-cols-3">
                        {PORTALS.map((p) => (
                          <TabsTrigger key={p.value} value={p.value}>
                            {p.label}
                          </TabsTrigger>
                        ))}
                      </TabsList>
                    </Tabs>
                  </div>

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

                  <FormField
                    control={form.control}
                    name="password"
                    render={({ field }) => (
                      <FormItem>
                        <div className="flex items-center justify-between">
                          <FormLabel>Password</FormLabel>
                          {/* FIX: this link only called e.preventDefault() and did nothing.
                              The backend has always supported forgot-password
                              (POST /api/auth/forgot-password); it just had no frontend
                              entry point. Now routes to the real ForgotPasswordPage. */}
                          <Link
                            href="/forgot-password"
                            className="text-sm font-medium text-primary hover:underline"
                          >
                            Forgot password?
                          </Link>
                        </div>
                        <div className="relative">
                          <FormControl>
                            <Input
                              type={showPassword ? 'text' : 'password'}
                              placeholder="••••••••"
                              {...field}
                            />
                          </FormControl>
                          <Button
                            type="button"
                            variant="ghost"
                            size="icon"
                            className="absolute right-0 top-0 h-full px-3 py-2 text-muted-foreground hover:text-foreground"
                            onClick={() => setShowPassword(!showPassword)}
                            aria-label={showPassword ? 'Hide password' : 'Show password'}
                          >
                            {showPassword ? <EyeOff className="h-4 w-4" /> : <Eye className="h-4 w-4" />}
                          </Button>
                        </div>
                        <FormMessage />
                      </FormItem>
                    )}
                  />

                  <Button type="submit" className="w-full" disabled={loginMutation.isPending}>
                    {loginMutation.isPending ? (
                      <div className="flex items-center gap-2">
                        <div className="h-4 w-4 animate-spin rounded-full border-2 border-primary-foreground border-t-transparent" />
                        <span>Signing in...</span>
                      </div>
                    ) : (
                      'Sign In'
                    )}
                  </Button>
                </form>
              </Form>
            )}
          </div>

          {/* Fix #12: demo credentials only in DEV */}
          {import.meta.env.DEV && !mfaState && (
            <div className="rounded-lg border border-amber-200 bg-amber-50 dark:bg-amber-950/20 dark:border-amber-800 p-4">
              <p className="text-xs font-semibold text-amber-700 dark:text-amber-300 mb-2">
                DEV — Demo Credentials
              </p>
              <div className="space-y-1 text-xs text-amber-600 dark:text-amber-400 font-mono">
                {/* FIX [4] — Literal passwords removed. Use the seed credentials
                    documented in your environment configuration (never committed). */}
                <p>superadmin@hrms.com</p>
                <p>admin@hrms.com</p>
                <p>employee@hrms.com</p>
              </div>
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
