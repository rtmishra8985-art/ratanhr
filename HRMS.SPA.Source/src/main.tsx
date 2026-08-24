import * as Sentry from '@sentry/react';
import { createRoot } from 'react-dom/client';
import App from './App';
import './index.css';

// ── Sentry error tracking ─────────────────────────────────────────────────────
// Activates only when VITE_SENTRY_DSN is set. No-ops silently in development.
const sentryDsn = import.meta.env.VITE_SENTRY_DSN;
if (sentryDsn) {
  Sentry.init({
    dsn: sentryDsn,
    environment: import.meta.env.MODE,
    integrations: [Sentry.browserTracingIntegration()],
    tracesSampleRate: 0.2,
    enabled: import.meta.env.PROD,
  });
}

createRoot(document.getElementById('root')!).render(<App />);
