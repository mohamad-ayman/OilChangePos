import { type ReactNode, useMemo, useState } from 'react'
import { NavLink } from 'react-router-dom'
import { getNavSectionsForRole } from '@/shared/config/navigation'
import type { AppAuthRole } from '@/shared/api/auth.api'
import { useAuthStore } from '@/shared/store/auth.store'
import { t } from '@/i18n'
import { NavItemIcon } from '@/app/layouts/navIcons'

const roleBadgeLabelKey: Record<AppAuthRole, string> = {
  admin: 'role.admin',
  manager: 'role.manager',
  cashier: 'role.cashier',
}

function MenuOpenIcon() {
  return (
    <svg viewBox="0 0 24 24" className="h-5 w-5" fill="none" stroke="currentColor" strokeWidth="2" aria-hidden>
      <path d="M4 6h16M4 12h16M4 18h16" strokeLinecap="round" />
    </svg>
  )
}

function ChevronToggleIcon({ collapsed }: { collapsed: boolean }) {
  return (
    <svg
      viewBox="0 0 24 24"
      className={['h-4 w-4 transition-transform duration-200 rtl:rotate-180', collapsed ? 'rotate-180 rtl:rotate-0' : ''].join(' ')}
      fill="none"
      stroke="currentColor"
      strokeWidth="2"
      aria-hidden
    >
      <path d="M15 6l-6 6 6 6" strokeLinecap="round" strokeLinejoin="round" />
    </svg>
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

  const userInitial = user?.username?.trim()?.slice(0, 2).toUpperCase() || '—'

  return (
    <div className="flex h-dvh max-h-dvh flex-col bg-slate-200/35 text-slate-900 antialiased">
      <header className="relative sticky top-0 z-30 flex h-14 shrink-0 items-center gap-3 border-b border-slate-200/90 bg-white/95 px-3 shadow-sm backdrop-blur-md sm:gap-4 sm:px-5">
        <div
          className="pointer-events-none absolute inset-x-0 bottom-0 h-px bg-gradient-to-r from-transparent via-sky-300/50 to-transparent"
          aria-hidden
        />
        <button
          type="button"
          className="relative inline-flex h-10 w-10 items-center justify-center rounded-xl border border-slate-200/90 bg-white text-slate-700 shadow-sm transition-colors hover:border-slate-300 hover:bg-slate-50 lg:hidden"
          aria-label={t('layout.toggleNav')}
          onClick={() => setMobileNavOpen((o) => !o)}
        >
          <MenuOpenIcon />
        </button>
        <button
          type="button"
          className="relative hidden h-10 w-10 items-center justify-center rounded-xl border border-slate-200/90 bg-white text-slate-700 shadow-sm transition-colors hover:border-slate-300 hover:bg-slate-50 lg:inline-flex"
          aria-label={sidebarCollapsed ? t('layout.expandSidebar') : t('layout.toggleSidebar')}
          onClick={() => setSidebarCollapsed((c) => !c)}
        >
          <ChevronToggleIcon collapsed={sidebarCollapsed} />
        </button>
        <div className="relative flex min-w-0 flex-1 items-center gap-3">
          <span className="hidden h-10 w-10 shrink-0 items-center justify-center rounded-xl bg-gradient-to-br from-sky-600 to-sky-800 text-xs font-bold text-white shadow-md ring-1 ring-sky-700/25 sm:flex">
            OC
          </span>
          <div className="min-w-0">
            <div className="truncate text-sm font-bold tracking-tight text-slate-900">{t('layout.appTitle')}</div>
            <div className="hidden truncate text-[11px] text-slate-500 sm:block">{t('layout.tagline')}</div>
          </div>
        </div>
        <div className="relative ms-auto hidden items-center gap-2 md:flex">
          <select
            disabled
            className="max-w-[11rem] cursor-not-allowed rounded-xl border border-slate-200 bg-slate-50/90 px-3 py-2 text-xs font-medium text-slate-500 shadow-inner"
            aria-label={t('layout.branchSoon')}
            title={t('layout.branchSoonTitle')}
          >
            <option>{t('layout.branchSoon')}</option>
          </select>
        </div>
        {user ? (
          <div className="relative flex items-center gap-2 sm:gap-3">
            <span className="max-w-[6rem] truncate text-xs font-medium text-slate-800 sm:hidden">{user.username}</span>
            <div className="hidden items-center gap-2 rounded-2xl border border-slate-200/90 bg-gradient-to-b from-white to-slate-50 py-1 ps-1 pe-3 shadow-sm sm:flex">
              <span className="flex h-9 w-9 shrink-0 items-center justify-center rounded-xl bg-sky-600 text-[11px] font-bold text-white shadow-sm ring-1 ring-sky-700/20">
                {userInitial}
              </span>
              <div className="min-w-0 text-end leading-tight">
                <div className="max-w-[10rem] truncate text-xs font-semibold text-slate-900">{user.username}</div>
                <div className="text-[10px] font-medium uppercase tracking-wide text-sky-700/90">
                  {t(roleBadgeLabelKey[user.role])}
                </div>
              </div>
            </div>
            <span className="rounded-lg bg-sky-50 px-2 py-0.5 text-[10px] font-bold uppercase text-sky-900 ring-1 ring-sky-200/80 sm:hidden">
              {t(roleBadgeLabelKey[user.role])}
            </span>
            <button
              type="button"
              onClick={() => {
                logout()
                window.location.assign('/login')
              }}
              className="relative rounded-xl border border-slate-300/90 bg-white px-3 py-2 text-xs font-semibold text-slate-800 shadow-sm transition-colors hover:border-sky-300 hover:bg-sky-50/80 hover:text-sky-950"
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
            className="fixed inset-0 z-40 bg-slate-900/25 backdrop-blur-[1px] lg:hidden"
            aria-label={t('layout.closeMenu')}
            onClick={() => setMobileNavOpen(false)}
          />
        ) : null}

        <aside
          className={[
            'z-50 flex shrink-0 flex-col border-slate-200/90 bg-gradient-to-b from-white via-slate-50/40 to-slate-100/60 shadow-[inset_-1px_0_0_0_rgb(226_232_240)] transition-[width,transform] duration-200 ease-out',
            'fixed bottom-0 start-0 top-14 w-[15.5rem] max-w-[min(18rem,88vw)] lg:static lg:max-w-none lg:self-stretch',
            mobileNavOpen ? 'translate-x-0' : 'max-lg:-translate-x-full max-lg:rtl:translate-x-full lg:translate-x-0',
            sidebarCollapsed ? 'lg:w-[4.25rem]' : 'lg:w-[15.5rem]',
            'border-e',
          ].join(' ')}
        >
          <div
            className={[
              'shrink-0 border-b border-slate-200/80 bg-white/60 px-3 py-3 backdrop-blur-sm',
              sidebarCollapsed ? 'lg:px-2 lg:py-2.5' : '',
            ].join(' ')}
          >
            <div className={['flex items-center gap-2', sidebarCollapsed ? 'lg:justify-center' : ''].join(' ')}>
              <span className="flex h-9 w-9 shrink-0 items-center justify-center rounded-xl bg-gradient-to-br from-sky-600 to-sky-800 text-xs font-bold text-white shadow-sm ring-1 ring-sky-700/30">
                OC
              </span>
              <div className={['min-w-0', sidebarCollapsed ? 'lg:hidden' : ''].join(' ')}>
                <p className="truncate text-xs font-bold text-slate-900">{t('layout.appTitle')}</p>
                <p className="truncate text-[10px] text-slate-500">{t('layout.tagline')}</p>
              </div>
            </div>
          </div>

          <nav className="flex flex-1 flex-col gap-0 overflow-y-auto overscroll-contain px-2 py-3" aria-label={t('layout.mainNav')}>
            {navSections.map((section, si) => (
              <div
                key={section.id}
                className={[
                  'flex flex-col gap-1',
                  si > 0 ? 'mt-2 border-t border-slate-200/80 pt-3' : '',
                ].join(' ')}
              >
                <p
                  className={[
                    'px-2.5 pb-1 text-[10px] font-bold uppercase tracking-wider text-slate-400',
                    sidebarCollapsed ? 'lg:sr-only' : '',
                  ].join(' ')}
                >
                  {t(section.labelKey)}
                </p>
                <div className="flex flex-col gap-0.5">
                  {section.items.map((item) => (
                    <NavLink
                      key={item.id}
                      to={item.path}
                      title={t(item.labelKey)}
                      onClick={() => setMobileNavOpen(false)}
                      className={({ isActive }) =>
                        [
                          'group relative flex items-center gap-3 rounded-xl border-s-[3px] border-transparent py-2 text-sm font-medium outline-none ring-sky-500/25 transition-[background,border-color,box-shadow] focus-visible:ring-2',
                          sidebarCollapsed ? 'lg:justify-center lg:px-1.5' : 'ps-2 pe-2.5',
                          isActive
                            ? 'border-sky-600 bg-sky-50 text-sky-950 shadow-sm ring-1 ring-sky-200/60'
                            : 'text-slate-600 hover:border-slate-200 hover:bg-white hover:text-slate-900',
                        ].join(' ')
                      }
                    >
                      {({ isActive }) => (
                        <>
                          <NavItemIcon icon={item.icon} active={isActive} />
                          <span
                            className={[
                              'min-w-0 flex-1 truncate text-start',
                              sidebarCollapsed ? 'lg:sr-only' : '',
                            ].join(' ')}
                          >
                            {t(item.labelKey)}
                          </span>
                        </>
                      )}
                    </NavLink>
                  ))}
                </div>
              </div>
            ))}
          </nav>
        </aside>

        <main className="flex min-h-0 min-w-0 flex-1 flex-col overflow-auto bg-[linear-gradient(165deg,_rgb(248_250_252)_0%,_rgb(238_242_248)_55%,_rgb(241_245_249)_100%)] lg:border-s lg:border-slate-200/80">
          <div className="mx-auto flex min-h-0 w-full max-w-[1600px] flex-1 flex-col px-4 py-5 sm:px-6 sm:py-7">{children}</div>
        </main>
      </div>
    </div>
  )
}
