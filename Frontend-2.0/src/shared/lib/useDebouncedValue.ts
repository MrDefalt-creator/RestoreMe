import { useEffect, useState } from 'react'

/**
 * Returns a copy of `value` that only updates after it has stayed unchanged
 * for `delayMs`. Used to keep rapid input (typing a cron time, dragging a
 * number) from firing a network request on every keystroke.
 */
export function useDebouncedValue<T>(value: T, delayMs: number): T {
  const [debounced, setDebounced] = useState(value)

  useEffect(() => {
    const timer = setTimeout(() => setDebounced(value), delayMs)
    return () => clearTimeout(timer)
  }, [value, delayMs])

  return debounced
}
