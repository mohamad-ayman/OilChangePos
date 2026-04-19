import { type FormEvent, useState } from 'react'
import { t } from '@/i18n'

export type LoginFormValues = {
  username: string
  password: string
}

type LoginFormProps = {
  disabled?: boolean
  errorMessage?: string | null
  onSubmit: (values: LoginFormValues) => Promise<void>
}

export function LoginForm({ disabled, errorMessage, onSubmit }: LoginFormProps) {
  const [username, setUsername] = useState('')
  const [password, setPassword] = useState('')
  const [submitting, setSubmitting] = useState(false)

  const busy = Boolean(disabled || submitting)

  async function handleSubmit(e: FormEvent) {
    e.preventDefault()
    if (busy) return
    setSubmitting(true)
    try {
      await onSubmit({ username: username.trim(), password })
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <form
      onSubmit={(e) => void handleSubmit(e)}
      className="w-full max-w-md space-y-5 rounded-2xl border border-slate-200 bg-white p-8 shadow-lg"
    >
      <div className="space-y-1 text-center">
        <h1 className="text-2xl font-semibold tracking-tight text-slate-900">{t('login.title')}</h1>
        <p className="text-base text-slate-600">{t('login.subtitle')}</p>
      </div>

      {errorMessage ? (
        <div
          role="alert"
          className="rounded-lg border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-800"
        >
          {errorMessage}
        </div>
      ) : null}

      <div className="space-y-4">
        <div className="space-y-1.5">
          <label htmlFor="username" className="block text-start text-sm font-medium text-slate-700">
            {t('login.username')}
          </label>
          <input
            id="username"
            name="username"
            autoComplete="username"
            value={username}
            onChange={(e) => setUsername(e.target.value)}
            disabled={busy}
            required
            className="w-full rounded-lg border border-slate-300 bg-white px-3 py-3 text-base text-slate-900 outline-none ring-sky-500/40 transition focus:border-sky-500 focus:ring-2 disabled:opacity-50"
          />
        </div>
        <div className="space-y-1.5">
          <label htmlFor="password" className="block text-start text-sm font-medium text-slate-700">
            {t('login.password')}
          </label>
          <input
            id="password"
            name="password"
            type="password"
            autoComplete="current-password"
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            disabled={busy}
            required
            className="w-full rounded-lg border border-slate-300 bg-white px-3 py-3 text-base text-slate-900 outline-none ring-sky-500/40 transition focus:border-sky-500 focus:ring-2 disabled:opacity-50"
          />
        </div>
      </div>

      <button
        type="submit"
        disabled={busy}
        className="flex w-full justify-center rounded-lg bg-sky-600 px-4 py-2.5 text-base font-semibold text-white shadow-sm transition hover:bg-sky-500 disabled:cursor-not-allowed disabled:bg-slate-300 disabled:text-slate-600"
      >
        {busy ? t('login.submitting') : t('login.submit')}
      </button>
    </form>
  )
}
