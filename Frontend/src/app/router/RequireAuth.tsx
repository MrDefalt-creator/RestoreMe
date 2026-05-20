import { Navigate, useLocation } from 'react-router-dom'
import type { PropsWithChildren } from 'react'

import { useAuthStore } from '@/app/store/auth-store'

export function RequireAuth({ children }: PropsWithChildren) {
  const user = useAuthStore((state) => state.user)
  const location = useLocation()

  if (!user) {
    return <Navigate to="/login" replace />
  }

  // Force the bootstrap admin (and anyone whose password was admin-reset)
  // to rotate before reaching the rest of the workspace.
  if (user.mustChangePassword && location.pathname !== '/account') {
    return <Navigate to="/account" replace />
  }

  return <>{children}</>
}
