import { Navigate, Outlet, useLocation } from 'react-router-dom'
import type { AppAuthRole } from '@/shared/api/auth.api'
import { useAuthStore } from '@/shared/store/auth.store'
import { t } from '@/i18n'

export type RequireRoleProps = {
  allowedRoles: AppAuthRole[]
}

/** In-app gate for `/app/*` children; use under `AppShell` where session is already authenticated. */
export function RequireRole({ allowedRoles }: RequireRoleProps) {
  const location = useLocation()
  const hasHydrated = useAuthStore((s) => s.hasHydrated)
  const user = useAuthStore((s) => s.user)

  if (!hasHydrated) {
    return (
      <div className="flex min-h-[40vh] items-center justify-center bg-white text-sm text-slate-600">
        {t('protected.restoringSession')}
      </div>
    )
  }

  if (!user || !allowedRoles.includes(user.role)) {
    return <Navigate to="/app" replace state={{ from: `${location.pathname}${location.search}` }} />
  }

  return <Outlet />
}
