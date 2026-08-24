import { useState, useEffect } from 'react';

/**
 * useDebounce — Returns a debounced copy of `value` that only updates
 * after `delay` ms have elapsed without the value changing.
 *
 * Used by search inputs to avoid firing a query on every keystroke.
 */
export function useDebounce<T>(value: T, delay: number): T {
  const [debounced, setDebounced] = useState<T>(value);

  useEffect(() => {
    const id = setTimeout(() => setDebounced(value), delay);
    return () => clearTimeout(id);
  }, [value, delay]);

  return debounced;
}
