/**
 * NetworkStatus.tsx — Online/offline banner.
 *
 * Listens to the browser's online/offline events and shows a non-intrusive
 * banner when the user loses their internet connection. The banner
 * auto-dismisses 3 seconds after connectivity is restored.
 *
 * Usage: place once in Layout.tsx, above the main content.
 */

import { useEffect, useState } from 'react';
import { WifiOff, Wifi } from 'lucide-react';
import { cn } from '@/lib/utils';

export function NetworkStatus() {
  const [isOnline, setIsOnline] = useState(navigator.onLine);
  const [showRestored, setShowRestored] = useState(false);

  useEffect(() => {
    let restoreTimer: ReturnType<typeof setTimeout>;

    const handleOnline = () => {
      setIsOnline(true);
      setShowRestored(true);
      restoreTimer = setTimeout(() => setShowRestored(false), 3000);
    };

    const handleOffline = () => {
      setIsOnline(false);
      setShowRestored(false);
    };

    window.addEventListener('online', handleOnline);
    window.addEventListener('offline', handleOffline);

    return () => {
      window.removeEventListener('online', handleOnline);
      window.removeEventListener('offline', handleOffline);
      clearTimeout(restoreTimer);
    };
  }, []);

  if (isOnline && !showRestored) return null;

  return (
    <div
      role="status"
      aria-live="assertive"
      aria-atomic="true"
      className={cn(
        'fixed bottom-4 left-1/2 -translate-x-1/2 z-50 flex items-center gap-2 rounded-full px-5 py-2.5 text-sm font-medium shadow-lg transition-all duration-300',
        isOnline
          ? 'bg-green-600 text-white'
          : 'bg-destructive text-destructive-foreground',
      )}
    >
      {isOnline ? (
        <>
          <Wifi className="h-4 w-4" aria-hidden="true" />
          <span>Connection restored</span>
        </>
      ) : (
        <>
          <WifiOff className="h-4 w-4" aria-hidden="true" />
          <span>No internet connection</span>
        </>
      )}
    </div>
  );
}
