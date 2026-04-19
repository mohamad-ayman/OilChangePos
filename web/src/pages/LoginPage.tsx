import { useMemo, useState } from 'react'
import { Navigate, useLocation, useNavigate, useSearchParams } from 'react-router-dom'
import { LoginForm, type LoginFormValues } from '@/features/auth/components/LoginForm'
import { AuthApiError } from '@/shared/api/auth.api'
import { useAuthStore } from '@/shared/store/auth.store'
import { t, translateAuthMessage } from '@/i18n'

export function LoginPage() {
  const navigate = useNavigate()
  const location = useLocation()
  const [searchParams] = useSearchParams()
  const hasHydrated = useAuthStore((s) => s.hasHydrated)
  const isAuthenticated = useAuthStore((s) => s.isAuthenticated)
  const login = useAuthStore((s) => s.login)
  const [error, setError] = useState<string | null>(null)

  const from = useMemo(() => {
    const st = location.state as { from?: string } | null
    return typeof st?.from === 'string' && st.from.startsWith('/') ? st.from : '/'
  }, [location.state])

  const sessionNotice = searchParams.get('reason') === 'session' ? t('login.sessionEnded') : null

  if (!hasHydrated) {
    return (
      <div className="flex min-h-screen items-center justify-center bg-slate-50 text-base text-slate-600">
        {t('login.loading')}
      </div>
    )
  }

  if (isAuthenticated) {
    return <Navigate to={from} replace />
  }

  async function handleLogin(values: LoginFormValues) {
    setError(null)
    try {
      await login(values)
      navigate(from, { replace: true })
    } catch (e) {
      if (e instanceof AuthApiError) {
        setError(translateAuthMessage(e.message))
        return
      }
      setError(t('login.genericError'))
    }
  }

  return (
    <div className="flex min-h-screen flex-col items-center justify-center bg-gradient-to-b from-slate-100 via-white to-slate-50 px-4 py-12">
      <div className="mb-8 text-center">
        <p className="text-sm font-semibold uppercase tracking-[0.15em] text-slate-600">{t('layout.appTitle')}</p>
        <p className="mt-2 text-base text-slate-700">{t('layout.tagline')}</p>
      </div>
      {sessionNotice ? (
        <p className="mb-4 max-w-md text-center text-sm text-amber-900">{sessionNotice}</p>
      ) : null}
      <LoginForm onSubmit={handleLogin} errorMessage={error} />
    </div>
  )
}
