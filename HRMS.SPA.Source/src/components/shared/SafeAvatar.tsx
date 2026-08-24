/**
 * SafeAvatar — plain-HTML avatar that is fully testable in jsdom.
 *
 * - Shows the image when it exists and loads without error.
 * - Falls back to a colour circle with initials on load failure,
 *   missing/null URL, or empty string.
 * - Never shows a broken-image icon.
 * - Never throws regardless of what profile contains.
 */

import { useState } from 'react';
import { getUserInitials, getAvatarUrl, type ProfileLike } from '@/utils/profileHelpers';
import { cn } from '@/lib/utils';

interface SafeAvatarProps {
  profile?: ProfileLike | null;
  /** Override the displayed initials (optional). */
  initials?: string;
  /** Override the image URL (optional). */
  src?: string | null;
  /** Tailwind size classes, e.g. "h-9 w-9". Defaults to "h-9 w-9". */
  size?: string;
  className?: string;
}

export function SafeAvatar({
  profile,
  initials,
  src,
  size = 'h-9 w-9',
  className,
}: SafeAvatarProps) {
  const [imgError, setImgError] = useState(false);

  const resolvedSrc      = src ?? getAvatarUrl(profile);
  // No profile at all → '?' placeholder. getUserInitials' own 'U' fallback
  // applies only when a profile object exists but carries no usable name.
  const resolvedInitials = initials ?? (profile ? getUserInitials(profile) : '?');

  const showImage        = Boolean(resolvedSrc) && !imgError;

  return (
    <div
      className={cn(
        'relative rounded-full overflow-hidden flex items-center justify-center bg-primary/10 shrink-0',
        size,
        className,
      )}
    >
      {showImage ? (
        <img
          src={resolvedSrc ?? undefined}
          alt={resolvedInitials}
          role="img"
          className="h-full w-full object-cover"
          onError={() => setImgError(true)}
        />
      ) : (
        <span
          className="text-primary font-semibold text-xs leading-none select-none"
          aria-label={resolvedInitials}
        >
          {resolvedInitials}
        </span>
      )}
    </div>
  );
}
