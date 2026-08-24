// Sentry.init() is called in main.tsx only when VITE_SENTRY_DSN is set.
// This component gracefully no-ops when Sentry is absent.
import { Component, type ErrorInfo, type ReactNode } from 'react';
import * as Sentry from '@sentry/react';

interface Props {
  children: ReactNode;
  /** Custom fallback UI. If omitted, a professional default page is shown. */
  fallback?: ReactNode;
}

interface State {
  hasError: boolean;
  error: Error | null;
}

/**
 * ErrorBoundary
 *
 * Wraps any subtree and catches unexpected rendering errors so the
 * application never shows a white screen. Displays a professional
 * fallback page instead of crashing the whole UI.
 *
 * Usage:
 *   <ErrorBoundary>
 *     <MyComponent />
 *   </ErrorBoundary>
 */
export class ErrorBoundary extends Component<Props, State> {
  constructor(props: Props) {
    super(props);
    this.state = { hasError: false, error: null };
  }

  static getDerivedStateFromError(error: Error): State {
    return { hasError: true, error };
  }

  componentDidCatch(error: Error, info: ErrorInfo) {
    // Log to console in all environments for developer debugging
    console.error('[ErrorBoundary] Caught rendering error:', error, info.componentStack);

    // Forward to Sentry when initialised (no-ops if VITE_SENTRY_DSN is absent)
    Sentry.captureException(error, {
      contexts: {
        react: { componentStack: info.componentStack ?? '' },
      },
    });
  }

  handleReset = () => {
    this.setState({ hasError: false, error: null });
  };

  render() {
    if (this.state.hasError) {
      if (this.props.fallback) {
        return this.props.fallback;
      }

      return (
        <div className="flex min-h-screen w-full flex-col items-center justify-center bg-background p-8 text-center">
          <div className="h-16 w-16 rounded-full bg-destructive/10 flex items-center justify-center mb-6">
            <svg
              xmlns="http://www.w3.org/2000/svg"
              className="h-8 w-8 text-destructive"
              fill="none"
              viewBox="0 0 24 24"
              stroke="currentColor"
              strokeWidth={1.5}
              aria-hidden="true"
            >
              <path
                strokeLinecap="round"
                strokeLinejoin="round"
                d="M12 9v3.75m-9.303 3.376c-.866 1.5.217 3.374 1.948 3.374h14.71c1.73 0 2.813-1.874 1.948-3.374L13.949 3.378c-.866-1.5-3.032-1.5-3.898 0L2.697 16.126zM12 15.75h.007v.008H12v-.008z"
              />
            </svg>
          </div>

          <h1 className="text-2xl font-bold text-foreground mb-2">Something went wrong</h1>
          <p className="text-muted-foreground max-w-md mb-8">
            An unexpected error occurred while rendering this section of the application.
            Our team has been notified. You can try refreshing the page or return to the dashboard.
          </p>

          <div className="flex flex-col sm:flex-row gap-3">
            <button
              onClick={this.handleReset}
              className="inline-flex items-center justify-center rounded-md bg-primary px-6 py-2.5 text-sm font-semibold text-primary-foreground shadow hover:bg-primary/90 transition-colors"
            >
              Try again
            </button>
            <a
              href="/dashboard"
              className="inline-flex items-center justify-center rounded-md border border-input bg-background px-6 py-2.5 text-sm font-semibold text-foreground shadow-sm hover:bg-muted transition-colors"
            >
              Go to Dashboard
            </a>
          </div>

          {process.env.NODE_ENV !== 'production' && this.state.error && (
            <pre className="mt-8 max-w-2xl text-left text-xs text-destructive bg-destructive/5 border border-destructive/20 rounded-lg p-4 overflow-auto">
              {this.state.error.toString()}
            </pre>
          )}
        </div>
      );
    }

    return this.props.children;
  }
}
