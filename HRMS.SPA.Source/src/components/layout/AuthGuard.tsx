import { useEffect } from 'react';
import { useAuth } from '@/hooks/useAuth';
import { useGetProfile, getGetProfileQueryKey } from '@workspace/api-client-react';
import { useLocation } from 'wouter';
import { isApiError } from '@/utils/apiError';
import { Button } from '@/components/ui/button';

export function AuthGuard({ children }: { children: React.ReactNode }) {
  const { isAuthenticated, logout } = useAuth();
  const [, setLocation] = useLocation();

  const { isLoading, isError, error, refetch } = useGetProfile({
    query: {
      enabled: isAuthenticated,
      retry: false,
      queryKey: getGetProfileQueryKey(),
    },
  });

  useEffect(() => {
    if (!isAuthenticated) {
      setLocation('/login');
    }
  }, [isAuthenticated, setLocation]);

  useEffect(() => {
    if (isError && isApiError(error)) {
      // BLOCKER-13: Handle both 401 (expired/invalid session) and 403 (account
      // suspended or role removed server-side after login).
      // Both mean the current session can no longer access protected resources.
      // Logging out clears local auth state and redirects to /login.
      if (error.status === 401 || error.status === 403) {
        logout();
      }
    }
  }, [isError, error, logout]);

  if (!isAuthenticated) {
    return null;
  }

  if (isLoading) {
    return (
      <div className="flex min-h-screen items-center justify-center" aria-label="Verifying session">
        <div className="h-8 w-8 animate-spin rounded-full border-4 border-primary border-t-transparent" />
      </div>
    );
  }

  if (isError) {
    if (isApiError(error) && (error.status === 401 || error.status === 403)) {
      return null;
    }

    return (
      <div className="flex min-h-screen items-center justify-center bg-background px-4">
        <div className="max-w-md space-y-4 text-center">
          <h1 className="text-xl font-semibold">Unable to verify your session</h1>
          <p className="text-sm text-muted-foreground">
            We couldn&apos;t connect to the HRMS service. Check your connection and try again.
          </p>
          <Button onClick={() => void refetch()}>Try again</Button>
        </div>
      </div>
    );
  }

  return <>{children}</>;
}
