import { Navigate, Outlet, useLocation } from 'react-router-dom'
import type { AppAuthRole } from '@/shared/api/auth.api'
import { useAuthStore } from '@/shared/store/auth.store'
import { t } from '@/i18n'

export type ProtectedRouteProps = {
  /** If set, only these roles may access child routes (others redirected home). */
  allowedRoles?: AppAuthRole[]
}

export function ProtectedRoute({ allowedRoles }: ProtectedRouteProps) {
  const location = useLocation()
  const hasHydrated = useAuthStore((s) => s.hasHydrated)
  const isAuthenticated = useAuthStore((s) => s.isAuthenticated)
  const user = useAuthStore((s) => s.user)

  if (!hasHydrated) {
    return (
      <div className="flex min-h-screen items-center justify-center bg-white text-sm text-slate-600">
        {t('protected.restoringSession')}
      </div>
    )
  }

  if (!isAuthenticated) {
    return <Navigate to="/login" replace state={{ from: `${location.pathname}${location.search}` }} />
  }

  if (allowedRoles?.length && user && !allowedRoles.includes(user.role)) {
    return <Navigate to="/app" replace />
  }

  return (
    <div className="min-h-screen bg-white">
      <Outlet />
    </div>
  )
}
