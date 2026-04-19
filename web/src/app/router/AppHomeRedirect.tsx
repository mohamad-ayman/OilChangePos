import { Navigate } from 'react-router-dom'
import { useAuthStore } from '@/shared/store/auth.store'
import { getNavItemsForRole } from '@/shared/config/navigation'

/** First accessible module for the signed-in role (no separate dashboard route). */
export function AppHomeRedirect() {
  const user = useAuthStore((s) => s.user)
  if (!user) return <Navigate to="/login" replace />
  const items = getNavItemsForRole(user.role)
  const path = items[0]?.path ?? '/app/pos'
  return <Navigate to={path} replace />
}
