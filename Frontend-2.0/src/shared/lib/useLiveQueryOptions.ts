import { useMemo } from 'react'

import { env } from '@/shared/config/env'
import { useI18n } from '@/shared/i18n'
import { useServerEventsConnected } from '@/shared/lib/useServerEventsConnected'

export function useLiveQueryOptions() {
  const { refreshIntervalMs } = useI18n()
  const sseConnected = useServerEventsConnected()
  const isManual = refreshIntervalMs === false

  return useMemo(
    () => ({
      staleTime: isManual ? Number.POSITIVE_INFINITY : 5_000,
      // While the SSE stream is up, pushed invalidations drive freshness —
      // interval polling would only duplicate the same requests. It resumes
      // automatically the moment the stream drops.
      refetchInterval: env.isLive && !sseConnected ? refreshIntervalMs : false,
      refetchIntervalInBackground: false,
      refetchOnMount: !isManual,
      refetchOnReconnect: !isManual,
      refetchOnWindowFocus: !isManual,
    }),
    [isManual, refreshIntervalMs, sseConnected],
  )
}

// Same options shape as useLiveQueryOptions but with a floor on the
// refetch cadence. Pages where instant freshness is not worth the
// per-operator request rate (dashboard summary) use this to avoid
// polling every 5 seconds when the operator's global setting is 5s.
// "Manual refresh only" still wins.
export function useLiveQueryOptionsWithFloor(minIntervalMs: number) {
  const { refreshIntervalMs } = useI18n()
  const sseConnected = useServerEventsConnected()
  const isManual = refreshIntervalMs === false

  return useMemo(() => {
    const effectiveInterval: number | false = isManual
      ? false
      : Math.max(refreshIntervalMs as number, minIntervalMs)

    return {
      staleTime: isManual ? Number.POSITIVE_INFINITY : 5_000,
      refetchInterval: env.isLive && !sseConnected ? effectiveInterval : (false as const),
      refetchIntervalInBackground: false,
      refetchOnMount: !isManual,
      refetchOnReconnect: !isManual,
      refetchOnWindowFocus: !isManual,
    }
  }, [isManual, refreshIntervalMs, minIntervalMs, sseConnected])
}
