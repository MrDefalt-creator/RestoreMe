import { lazy, Suspense, useEffect, type PropsWithChildren } from 'react'
import { QueryClientProvider, useQueryClient } from '@tanstack/react-query'
import { Toaster } from 'sonner'

import { queryClient } from '@/app/providers/queryClient'
import { env } from '@/shared/config/env'
import { I18nProvider, useI18n } from '@/shared/i18n'

const ReactQueryDevtools = lazy(() =>
  import('@tanstack/react-query-devtools').then((module) => ({
    default: module.ReactQueryDevtools,
  })),
)

export function AppProviders({ children }: PropsWithChildren) {
  return (
    <QueryClientProvider client={queryClient}>
      <I18nProvider>
        <QueryRefreshPreferences />
        {children}
      </I18nProvider>
      <Toaster richColors position="top-right" />
      {!env.isProduction ? (
        <Suspense fallback={null}>
          <ReactQueryDevtools initialIsOpen={false} />
        </Suspense>
      ) : null}
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
        staleTime: refreshIntervalMs === false ? 60_000 : 10_000,
        refetchInterval: env.apiMode === 'live' ? refreshIntervalMs : false,
      },
    })
  }, [client, refreshIntervalMs])

  return null
}
