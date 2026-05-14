import { useEffect } from 'react'
import { QueryClient, QueryClientProvider, useQueryClient } from '@tanstack/react-query'
import { RouterProvider } from 'react-router-dom'
import { Toaster } from 'sonner'

import { ThemeProvider } from '@/app/providers/ThemeProvider'
import { router } from '@/app/router/router'
import { env } from '@/shared/config/env'
import { getRefreshIntervalMs, I18nProvider, useI18n } from '@/shared/i18n'

const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      staleTime: import.meta.env.VITE_API_MODE === 'live' ? 5_000 : 60_000,
      refetchInterval: import.meta.env.VITE_API_MODE === 'live' ? getRefreshIntervalMs() : false,
      refetchIntervalInBackground: false,
      refetchOnMount: import.meta.env.VITE_API_MODE === 'live' ? 'always' : true,
      refetchOnReconnect: true,
      refetchOnWindowFocus: true,
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
    const defaults = client.getDefaultOptions()
    client.setDefaultOptions({
      ...defaults,
      queries: {
        ...defaults.queries,
        staleTime: refreshIntervalMs === false ? 60_000 : 5_000,
        refetchInterval: env.isLive ? refreshIntervalMs : false,
      },
    })
  }, [client, refreshIntervalMs])

  return null
}
