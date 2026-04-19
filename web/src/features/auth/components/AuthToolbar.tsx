import { useAuthStore } from '@/shared/store/auth.store'
import type { AppAuthRole } from '@/shared/api/auth.api'
import { t } from '@/i18n'

const roleLabelKey: Record<AppAuthRole, string> = {
  admin: 'role.admin',
  manager: 'role.manager',
  cashier: 'role.cashier',
}

export function AuthToolbar() {
  const user = useAuthStore((s) => s.user)
  const logout = useAuthStore((s) => s.logout)

  if (!user) return null

  return (
    <header className="flex items-center justify-between border-b border-slate-200 bg-slate-100 px-4 py-3 text-sm text-slate-800 backdrop-blur">
      <div className="flex items-center gap-3">
        <span className="font-medium text-slate-900">{user.username}</span>
        <span className="rounded-md bg-slate-200 px-2 py-0.5 text-xs font-medium text-slate-700">
          {t(roleLabelKey[user.role])}
        </span>
      </div>
      <button
        type="button"
        onClick={() => {
          logout()
          window.location.assign('/login')
        }}
        className="rounded-md border border-slate-400 px-3 py-1.5 text-xs font-medium text-slate-800 transition hover:bg-slate-200"
      >
        {t('auth.signOut')}
      </button>
    </header>
  )
}
