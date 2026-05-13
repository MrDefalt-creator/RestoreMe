import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { RouterProvider } from 'react-router-dom'
import { Toaster } from 'sonner'

import { ThemeProvider } from '@/app/providers/ThemeProvider'
import { router } from '@/app/router/router'

const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      staleTime: import.meta.env.VITE_API_MODE === 'live' ? 5_000 : 60_000,
      refetchInterval: import.meta.env.VITE_API_MODE === 'live' ? 10_000 : false,
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
        <RouterProvider router={router} />
        <Toaster position="top-right" richColors duration={3000} />
      </ThemeProvider>
    </QueryClientProvider>
  )
}
