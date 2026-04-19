import { Link } from 'react-router-dom'
import { t } from '@/i18n'

export function NotFoundPage() {
  return (
    <main className="mx-auto flex min-h-screen max-w-lg flex-col justify-center gap-6 px-6 py-16 text-center">
      <p className="text-6xl font-bold text-slate-700">404</p>
      <h1 className="text-xl font-semibold text-slate-900">{t('nf.title')}</h1>
      <Link
        to="/"
        className="text-sky-700 underline-offset-4 hover:underline"
      >
        {t('nf.home')}
      </Link>
    </main>
  )
}
