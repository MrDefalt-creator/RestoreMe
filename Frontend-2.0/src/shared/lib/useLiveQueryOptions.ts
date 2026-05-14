import { useMemo } from 'react'

import { env } from '@/shared/config/env'
import { useI18n } from '@/shared/i18n'

export function useLiveQueryOptions() {
  const { refreshIntervalMs } = useI18n()

  return useMemo(
    () => ({
      staleTime: refreshIntervalMs === false ? 60_000 : 5_000,
      refetchInterval: env.isLive ? refreshIntervalMs : false,
      refetchIntervalInBackground: false,
    }),
    [refreshIntervalMs],
  )
}
