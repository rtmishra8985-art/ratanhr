/**
 * Layout.tsx — Main authenticated shell.
 *
 * Changes:
 *  - SkipToContent link inserted as the very first DOM element (WCAG 2.4.1).
 *  - <main> gets id="main-content" so the skip link target resolves.
 *  - aria-live="polite" region on <main> announces page changes to screen readers.
 *  - NetworkStatus offline/online banner rendered inside the shell.
 */

import { ReactNode } from 'react';
import { Sidebar }         from './Sidebar';
import { Navbar }          from './Navbar';
import { SidebarProvider } from '@/components/ui/sidebar';
import { AuthGuard }       from './AuthGuard';
import { SkipToContent }   from '@/components/shared/SkipToContent';
import { NetworkStatus }   from '@/components/shared/NetworkStatus';

interface LayoutProps {
  children: ReactNode;
}

export function Layout({ children }: LayoutProps) {
  return (
    <AuthGuard>
      {/* a11y: keyboard users can skip sidebar + navbar */}
      <SkipToContent />

      <SidebarProvider>
        <div className="flex min-h-screen w-full bg-background overflow-hidden">
          <Sidebar />
          <div className="flex-1 flex flex-col w-full overflow-hidden">
            <Navbar />
            {/*
              id="main-content" is the skip-link target.
              aria-live="polite" lets screen readers announce route changes.
            */}
            <main
              id="main-content"
              aria-live="polite"
              className="flex-1 overflow-auto p-4 md:p-6 lg:p-8"
            >
              <div className="mx-auto max-w-7xl w-full h-full">
                {children}
              </div>
            </main>
          </div>
        </div>
      </SidebarProvider>

      {/* Global online/offline banner */}
      <NetworkStatus />
    </AuthGuard>
  );
}
