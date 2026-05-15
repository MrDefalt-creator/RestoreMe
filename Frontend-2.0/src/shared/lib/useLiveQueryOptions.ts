import { useMemo } from 'react'

import { env } from '@/shared/config/env'
import { useI18n } from '@/shared/i18n'

export function useLiveQueryOptions() {
  const { refreshIntervalMs } = useI18n()
  const isManual = refreshIntervalMs === false

  return useMemo(
    () => ({
      staleTime: isManual ? Number.POSITIVE_INFINITY : 5_000,
      refetchInterval: env.isLive ? refreshIntervalMs : false,
      refetchIntervalInBackground: false,
      refetchOnMount: !isManual,
      refetchOnReconnect: !isManual,
      refetchOnWindowFocus: !isManual,
    }),
    [isManual, refreshIntervalMs],
  )
}
