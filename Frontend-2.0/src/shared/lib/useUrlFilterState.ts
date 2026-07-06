import { useCallback } from 'react'
import { useSearchParams } from 'react-router-dom'

/**
 * Filter state that lives in the URL search params so filtered views are
 * deep-linkable (e.g. /jobs?status=failed from the dashboard) and survive
 * reloads. The value is derived straight from the URL — no mirrored state.
 *
 * - `defaultValue` is represented by the *absence* of the param (kept out of
 *   the URL to keep links clean).
 * - Updates use the functional `setSearchParams` form with `replace: true`
 *   so sibling params (like the job drawer's `?id=`) are preserved and the
 *   history stack isn't spammed by filter toggles.
 * - When `validValues` is given, unknown raw values fall back to the default
 *   instead of leaking arbitrary strings into typed filter state.
 */
export function useUrlFilterState<T extends string>(
  param: string,
  defaultValue: T,
  validValues?: readonly T[],
): [T, (next: T) => void] {
  const [searchParams, setSearchParams] = useSearchParams()

  const raw = searchParams.get(param)
  const value =
    raw === null || (validValues && !validValues.includes(raw as T))
      ? defaultValue
      : (raw as T)

  const setValue = useCallback(
    (next: T) => {
      setSearchParams(
        (prev) => {
          const params = new URLSearchParams(prev)
          if (next === defaultValue) {
            params.delete(param)
          } else {
            params.set(param, next)
          }
          return params
        },
        { replace: true },
      )
    },
    [defaultValue, param, setSearchParams],
  )

  return [value, setValue]
}
