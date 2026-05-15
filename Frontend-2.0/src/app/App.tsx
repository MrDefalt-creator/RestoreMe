import { useEffect } from 'react'
import { QueryClient, QueryClientProvider, useQueryClient } from '@tanstack/react-query'
import { RouterProvider } from 'react-router-dom'
import { Toaster } from 'sonner'

import { ThemeProvider } from '@/app/providers/ThemeProvider'
import { router } from '@/app/router/router'
import { env } from '@/shared/config/env'
import { getRefreshIntervalMs, I18nProvider, useI18n } from '@/shared/i18n'

const initialRefetchInterval = import.meta.env.VITE_API_MODE === 'live' ? getRefreshIntervalMs() : false

const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      staleTime: initialRefetchInterval === false ? Number.POSITIVE_INFINITY : 5_000,
      refetchInterval: initialRefetchInterval,
      refetchIntervalInBackground: false,
      refetchOnMount: initialRefetchInterval !== false,
      refetchOnReconnect: initialRefetchInterval !== false,
      refetchOnWindowFocus: initialRefetchInterval !== false,
      retry: 1,
    },
  },
})

export function App() {
  return (
    <QueryClientProvider client={queryClient}>
      <ThemeProvider defaultTheme="light">
        <I18nProvider>
          <QueryRefreshPreferences />
          <RouterProvider router={router} />
          <Toaster position="top-right" richColors duration={3000} />
        </I18nProvider>
      </ThemeProvider>
    </QueryClientProvider>
  )
}

function QueryRefreshPreferences() {
  const client = useQueryClient()
  const { refreshIntervalMs } = useI18n()

  useEffect(() => {
    const isManual = refreshIntervalMs === false
    const defaults = client.getDefaultOptions()
    client.setDefaultOptions({
      ...defaults,
      queries: {
        ...defaults.queries,
        staleTime: isManual ? Number.POSITIVE_INFINITY : 5_000,
        refetchInterval: env.isLive ? refreshIntervalMs : false,
        refetchOnMount: !isManual,
        refetchOnReconnect: !isManual,
        refetchOnWindowFocus: !isManual,
      },
    })
  }, [client, refreshIntervalMs])

  return null
}
