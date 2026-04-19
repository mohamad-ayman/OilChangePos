import { memo } from 'react'

type KpiCardProps = {
  title: string
  value: string
  hint?: string
  loading?: boolean
}

export const KpiCard = memo(function KpiCard({ title, value, hint, loading }: KpiCardProps) {
  return (
    <div className="rounded-xl border border-slate-200/90 bg-white px-4 py-3 shadow-sm">
      <div className="text-[11px] font-semibold uppercase tracking-wide text-slate-500">{title}</div>
      <div className="mt-1.5 font-mono text-xl font-semibold tabular-nums text-slate-900 sm:text-2xl">
        {loading ? '—' : value}
      </div>
      {hint ? <div className="mt-1.5 text-[11px] leading-snug text-slate-600">{hint}</div> : null}
    </div>
  )
})
