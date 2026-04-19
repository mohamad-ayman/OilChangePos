import { Link } from 'react-router-dom'
import { t } from '@/i18n'

export function NotFoundPage() {
  return (
    <div className="mx-auto flex max-w-lg flex-col items-center justify-center gap-6 rounded-2xl border border-slate-200/90 bg-white px-8 py-14 text-center shadow-md shadow-slate-900/10 ring-1 ring-slate-900/[0.03] sm:py-16">
      <p className="text-6xl font-black tabular-nums text-slate-300">404</p>
      <h1 className="text-lg font-bold text-slate-900 sm:text-xl">{t('nf.title')}</h1>
      <Link
        to="/"
        className="rounded-xl border border-sky-200 bg-sky-50 px-4 py-2 text-sm font-semibold text-sky-900 transition-colors hover:bg-sky-100"
      >
        {t('nf.home')}
      </Link>
    </div>
  )
}
