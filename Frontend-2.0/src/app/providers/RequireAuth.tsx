import { Navigate, useLocation } from 'react-router-dom'
import { useAuthStore } from '@/app/store/auth-store'

export function RequireAuth({ children }: { children: React.ReactNode }) {
  const user = useAuthStore((state) => state.user)
  const location = useLocation()

  if (!user) {
    return <Navigate to="/login" state={{ from: location }} replace />
  }

  return <>{children}</>
}
