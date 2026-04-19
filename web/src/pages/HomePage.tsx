import { Link } from 'react-router-dom'
import { useHealthQuery } from '@/shared/api/health'
import { useUiStore } from '@/shared/store/uiStore'
import { t } from '@/i18n'

export function HomePage() {
  const appTitle = useUiStore((s) => s.appTitle)
  const health = useHealthQuery()

  return (
    <main className="mx-auto flex min-h-screen max-w-lg flex-col justify-center gap-6 px-6 py-16">
      <div>
        <p className="text-sm font-medium uppercase tracking-wide text-slate-600">{t('home.stepLabel')}</p>
        <h1 className="mt-2 text-3xl font-semibold tracking-tight text-slate-900">{appTitle}</h1>
        <p className="mt-2 text-slate-600">{t('home.stackLine')}</p>
      </div>

      <section className="rounded-xl border border-slate-200 bg-white p-5 shadow-sm">
        <h2 className="text-sm font-medium text-slate-700">{t('home.healthTitle')}</h2>
        <p className="mt-2 text-lg">
          {health.isPending && <span className="text-amber-800">{t('home.healthChecking')}</span>}
          {health.isSuccess && health.data.ok && <span className="text-emerald-700">{t('home.healthConnected')}</span>}
          {(health.isError || (health.isSuccess && !health.data.ok)) && (
            <span className="text-red-600">
              {t('home.healthUnreachable')}
              {health.error instanceof Error ? ` (${health.error.message})` : ''}
            </span>
          )}
        </p>
        <p className="mt-3 text-sm text-slate-600">
          {t('home.dev1')}{' '}
          <code className="rounded bg-slate-200 px-1 py-0.5">OilChangePOS.API</code>{' '}
          {t('home.dev2')}{' '}
          <code className="rounded bg-slate-200 px-1 py-0.5">npm run dev</code> {t('home.dev3')}
        </p>
      </section>

      <p className="text-center text-sm text-slate-500">
        <Link to="/this-route-does-not-exist" className="text-sky-700 underline-offset-4 hover:underline">
          {t('home.test404')}
        </Link>
      </p>
    </main>
  )
}
