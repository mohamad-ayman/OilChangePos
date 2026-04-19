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

type ShellModuleId = 'products' | 'purchases' | 'users'

/** Generic placeholder for menu targets before business modules exist. */
export function ShellModulePlaceholder({ moduleId }: { moduleId: ShellModuleId }) {
  return (
    <div className="rounded-2xl border border-slate-200/90 bg-white p-6 shadow-sm shadow-slate-900/5 sm:p-8">
      <div className="mx-auto max-w-2xl">
        <h1 className="text-lg font-bold tracking-tight text-slate-900 sm:text-xl">{t(`module.${moduleId}.title`)}</h1>
        <p className="mt-3 text-sm leading-relaxed text-slate-600 sm:text-base">{t(`module.${moduleId}.desc`)}</p>
      </div>
    </div>
  )
}
