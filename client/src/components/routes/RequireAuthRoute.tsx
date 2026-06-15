import type { ReactNode } from 'react'
import { Navigate, useLocation } from 'react-router-dom'
import { useAuth } from '../../state/use-auth'
import { FullscreenLoader } from '../ui/FullscreenLoader'

type RequireAuthRouteProps = {
  children: ReactNode
}

export function RequireAuthRoute({ children }: RequireAuthRouteProps) {
  const { isAuthenticated, isInitializing } = useAuth()
  const location = useLocation()

  if (isInitializing) {
    return <FullscreenLoader label="Racking the balls..." />
  }

  if (!isAuthenticated) {
    return <Navigate to="/login" replace state={{ from: location.pathname }} />
  }

  return <>{children}</>
}
