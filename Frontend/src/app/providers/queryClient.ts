import { QueryClient } from '@tanstack/react-query'

import { env } from '@/shared/config/env'
import { getRefreshIntervalMs } from '@/shared/i18n'

export function getLiveRefetchInterval() {
  return env.apiMode === 'live' ? getRefreshIntervalMs() : false
}

export const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      staleTime: 10_000,
      refetchInterval: getLiveRefetchInterval(),
      refetchIntervalInBackground: false,
      refetchOnWindowFocus: true,
      refetchOnReconnect: true,
      retry: 1,
    },
    mutations: {
      retry: 0,
    },
  },
})
