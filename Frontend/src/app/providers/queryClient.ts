import { QueryClient } from '@tanstack/react-query'

import { env } from '@/shared/config/env'

const liveRefetchInterval = env.apiMode === 'live' ? 10_000 : false

export const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      staleTime: 10_000,
      refetchInterval: liveRefetchInterval,
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
