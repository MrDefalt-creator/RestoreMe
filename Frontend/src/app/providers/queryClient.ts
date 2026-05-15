import { QueryClient } from '@tanstack/react-query'

import { env } from '@/shared/config/env'
import { getRefreshIntervalMs } from '@/shared/i18n'

export function getLiveRefetchInterval() {
  return env.apiMode === 'live' ? getRefreshIntervalMs() : false
}

const initialRefetchInterval = getLiveRefetchInterval()

export const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      staleTime: initialRefetchInterval === false ? Number.POSITIVE_INFINITY : 10_000,
      refetchInterval: initialRefetchInterval,
      refetchIntervalInBackground: false,
      refetchOnMount: initialRefetchInterval !== false,
      refetchOnWindowFocus: initialRefetchInterval !== false,
      refetchOnReconnect: initialRefetchInterval !== false,
      retry: 1,
    },
    mutations: {
      retry: 0,
    },
  },
})
