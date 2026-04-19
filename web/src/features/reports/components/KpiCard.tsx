import { memo } from 'react'

type KpiCardProps = {
  title: string
  value: string
  hint?: string
  loading?: boolean
}

export const KpiCard = memo(function KpiCard({ title, value, hint, loading }: KpiCardProps) {
  return (
    <div className="border border-slate-200 bg-white px-3 py-3">
      <div className="text-[10px] font-semibold uppercase tracking-wide text-slate-500">{title}</div>
      <div className="mt-1 font-mono text-xl font-semibold tabular-nums text-slate-900">
        {loading ? '—' : value}
      </div>
      {hint ? <div className="mt-1 text-[10px] text-slate-600">{hint}</div> : null}
    </div>
  )
})
