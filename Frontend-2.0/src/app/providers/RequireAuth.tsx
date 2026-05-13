import { Navigate, useLocation } from 'react-router-dom'
import { useAuthStore } from '@/app/store/auth-store'

export function RequireAuth({ children }: { children: React.ReactNode }) {
  const accessToken = useAuthStore((state: any) => state.accessToken)
  const location = useLocation()

  if (!accessToken) {
    return <Navigate to="/login" state={{ from: location }} replace />
  }

  return <>{children}</>
}
