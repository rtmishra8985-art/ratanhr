/**
 * SkipToContent.tsx — Keyboard accessibility skip link.
 *
 * Renders a visually hidden link that becomes visible on keyboard focus.
 * It jumps the user straight to the main content area, bypassing the
 * sidebar and navbar — a WCAG 2.1 Level AA requirement (Success Criterion 2.4.1).
 *
 * Usage:
 *   1. Add <SkipToContent /> as the very first element inside <body>.
 *   2. Add id="main-content" to the <main> element.
 */

export function SkipToContent() {
  return (
    <a
      href="#main-content"
      className={[
        'sr-only',
        'focus:not-sr-only',
        'focus:fixed',
        'focus:top-4',
        'focus:left-4',
        'focus:z-[9999]',
        'focus:px-4',
        'focus:py-2',
        'focus:rounded-md',
        'focus:bg-primary',
        'focus:text-primary-foreground',
        'focus:shadow-lg',
        'focus:outline-none',
        'focus:ring-2',
        'focus:ring-primary-foreground',
        'focus:text-sm',
        'focus:font-medium',
      ].join(' ')}
    >
      Skip to main content
    </a>
  );
}
