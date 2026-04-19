import { Outlet } from 'react-router-dom'
import { MainLayout } from '@/app/layouts/MainLayout'
import { t } from '@/i18n'

/** Authenticated ERP wrapper: sidebar + topbar + module outlet (SAP/Odoo-style chrome). */
export function AppShell() {
  return (
    <MainLayout>
      <Outlet />
    </MainLayout>
  )
}

/** Dashboard home (placeholder until reporting module). */
export function ShellDashboardPage() {
  return (
    <div className="border-b border-slate-200 px-4 py-6 sm:px-6">
      <h1 className="text-xl font-semibold text-slate-900">{t('dashboard.title')}</h1>
      <p className="mt-2 max-w-2xl text-base leading-relaxed text-slate-600">{t('dashboard.body')}</p>
    </div>
  )
}

type ShellModuleId = 'products' | 'purchases' | 'users'

/** Generic placeholder for menu targets before business modules exist. */
export function ShellModulePlaceholder({ moduleId }: { moduleId: ShellModuleId }) {
  return (
    <div className="border-b border-slate-200 px-4 py-6 sm:px-6">
      <h1 className="text-xl font-semibold text-slate-900">{t(`module.${moduleId}.title`)}</h1>
      <p className="mt-2 text-base leading-relaxed text-slate-600">{t(`module.${moduleId}.desc`)}</p>
    </div>
  )
}
