import { Navigate } from 'react-router-dom'
import type { PropsWithChildren } from 'react'

import { useAuthStore } from '@/app/store/auth-store'

export function RequireAuth({ children }: PropsWithChildren) {
  const accessToken = useAuthStore((state) => state.accessToken)

  if (!accessToken) {
    return <Navigate to="/login" replace />
  }

  return <>{children}</>
}
