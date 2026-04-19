import { type ReactNode, useMemo, useState } from 'react'
import { NavLink } from 'react-router-dom'
import { getNavSectionsForRole } from '@/shared/config/navigation'
import type { AppAuthRole } from '@/shared/api/auth.api'
import { useAuthStore } from '@/shared/store/auth.store'
import { t } from '@/i18n'

const roleBadgeLabelKey: Record<AppAuthRole, string> = {
  admin: 'role.admin',
  manager: 'role.manager',
  cashier: 'role.cashier',
}

function NavIconPlaceholder() {
  return (
    <span
      aria-hidden
      className="flex h-8 w-8 shrink-0 items-center justify-center rounded border border-slate-300/80 bg-slate-100 text-[10px] font-medium text-slate-500"
    >
      —
    </span>
  )
}

type MainLayoutProps = {
  children: ReactNode
}

export function MainLayout({ children }: MainLayoutProps) {
  const user = useAuthStore((s) => s.user)
  const logout = useAuthStore((s) => s.logout)
  const [sidebarCollapsed, setSidebarCollapsed] = useState(false)
  const [mobileNavOpen, setMobileNavOpen] = useState(false)

  const navSections = useMemo(() => {
    if (!user) return []
    return getNavSectionsForRole(user.role)
  }, [user])

  return (
    <div className="flex h-dvh max-h-dvh flex-col bg-slate-50 text-slate-900 antialiased">
      <header className="sticky top-0 z-30 flex h-12 shrink-0 items-center gap-3 border-b border-slate-200 bg-white px-3 shadow-sm sm:px-4">
        <button
          type="button"
          className="inline-flex h-9 w-9 items-center justify-center rounded border border-slate-300 text-slate-700 hover:bg-slate-100 lg:hidden"
          aria-label={t('layout.toggleNav')}
          onClick={() => setMobileNavOpen((o) => !o)}
        >
          <span className="text-lg leading-none">≡</span>
        </button>
        <button
          type="button"
          className="hidden h-9 w-9 items-center justify-center rounded border border-slate-300 text-slate-700 hover:bg-slate-100 lg:inline-flex"
          aria-label={sidebarCollapsed ? t('layout.expandSidebar') : t('layout.toggleSidebar')}
          onClick={() => setSidebarCollapsed((c) => !c)}
        >
          <span className="text-xs font-medium">{sidebarCollapsed ? '»' : '«'}</span>
        </button>
        <div className="min-w-0 flex-1 text-sm font-semibold tracking-tight text-slate-800">{t('layout.appTitle')}</div>
        <div className="hidden items-center gap-2 sm:flex">
          <select
            disabled
            className="max-w-[10rem] cursor-not-allowed rounded border border-slate-200 bg-slate-100 px-2 py-1 text-xs text-slate-500"
            aria-label={t('layout.branchSoon')}
            title={t('layout.branchSoonTitle')}
          >
            <option>{t('layout.branchSoon')}</option>
          </select>
        </div>
        {user ? (
          <div className="flex items-center gap-2 sm:gap-3">
            <span className="max-w-[5.5rem] truncate text-xs text-slate-700 sm:hidden">{user.username}</span>
            <div className="hidden min-w-0 text-end sm:block">
              <div className="truncate text-xs font-medium text-slate-900">{user.username}</div>
              <div className="text-[10px] uppercase tracking-wide text-slate-500">{t(roleBadgeLabelKey[user.role])}</div>
            </div>
            <span className="rounded bg-slate-200 px-2 py-0.5 text-[10px] font-semibold uppercase text-slate-700 sm:hidden">
              {t(roleBadgeLabelKey[user.role])}
            </span>
            <button
              type="button"
              onClick={() => {
                logout()
                window.location.assign('/login')
              }}
              className="rounded border border-slate-400 px-2.5 py-1 text-xs font-medium text-slate-800 hover:bg-slate-100"
            >
              {t('layout.logout')}
            </button>
          </div>
        ) : null}
      </header>

      <div className="relative flex min-h-0 flex-1">
        {mobileNavOpen ? (
          <button
            type="button"
            className="fixed inset-0 z-40 bg-slate-600/20 lg:hidden"
            aria-label={t('layout.closeMenu')}
            onClick={() => setMobileNavOpen(false)}
          />
        ) : null}

        <aside
          className={[
            'z-50 flex shrink-0 flex-col border-e border-slate-200 bg-white shadow-sm transition-[width,transform] duration-200 ease-out',
            'fixed bottom-0 start-0 top-12 w-56 max-w-[85vw] lg:static lg:max-w-none lg:self-stretch',
            mobileNavOpen ? 'translate-x-0' : 'max-lg:-translate-x-full max-lg:rtl:translate-x-full lg:translate-x-0',
            sidebarCollapsed ? 'lg:w-16' : 'lg:w-56',
          ].join(' ')}
        >
          <nav className="flex flex-1 flex-col gap-2 overflow-y-auto p-2" aria-label={t('layout.mainNav')}>
            {navSections.map((section) => (
              <div key={section.id} className="flex flex-col gap-0.5">
                <p
                  className={[
                    'px-2 pb-0.5 text-[11px] font-semibold text-slate-500',
                    sidebarCollapsed ? 'lg:sr-only' : '',
                  ].join(' ')}
                >
                  {t(section.labelKey)}
                </p>
                {section.items.map((item) => (
                  <NavLink
                    key={item.id}
                    to={item.path}
                    end={item.path === '/app'}
                    onClick={() => setMobileNavOpen(false)}
                    className={({ isActive }) =>
                      [
                        'flex items-center gap-2 rounded-md px-2 py-2 text-sm font-medium outline-none ring-sky-500/30 focus-visible:ring-2',
                        isActive
                          ? 'border border-sky-200 bg-sky-50 text-sky-950'
                          : 'text-slate-700 hover:bg-slate-100 hover:text-slate-900',
                      ].join(' ')
                    }
                  >
                    <NavIconPlaceholder />
                    <span className={sidebarCollapsed ? 'lg:sr-only' : ''}>{t(item.labelKey)}</span>
                  </NavLink>
                ))}
              </div>
            ))}
          </nav>
        </aside>

        <main className="min-w-0 flex-1 overflow-auto bg-slate-50 lg:border-s lg:border-slate-200">
          <div className="mx-auto max-w-[1600px]">{children}</div>
        </main>
      </div>
    </div>
  )
}
