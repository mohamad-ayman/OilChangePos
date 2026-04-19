import { memo, useMemo } from 'react'

export type BarDatum = { label: string; value: number }

type SimpleBarChartProps = {
  data: readonly BarDatum[]
  valueFormatter?: (n: number) => string
  barClassName?: string
  height?: number
}

export const SimpleBarChart = memo(function SimpleBarChart({
  data,
  valueFormatter = (n) => n.toLocaleString(),
  barClassName = 'bg-sky-600/80',
  height = 140,
}: SimpleBarChartProps) {
  const max = useMemo(() => Math.max(1, ...data.map((d) => d.value)), [data])

  return (
    <div className="flex h-full min-h-0 flex-col justify-end gap-1" style={{ height }}>
      <div className="flex min-h-0 flex-1 items-end gap-1">
        {data.map((d) => {
          const pct = (d.value / max) * 100
          return (
            <div key={d.label} className="flex min-w-0 flex-1 flex-col items-center justify-end gap-1">
              <span className="text-[9px] font-mono tabular-nums text-slate-500">{valueFormatter(d.value)}</span>
              <div
                className={['w-full max-w-[2.5rem] rounded-t border border-slate-200/80', barClassName].join(' ')}
                style={{ height: `${Math.max(4, pct)}%` }}
                title={`${d.label}: ${d.value}`}
              />
            </div>
          )
        })}
      </div>
      <div className="flex gap-1 border-t border-slate-200 pt-1">
        {data.map((d) => (
          <div key={d.label} className="min-w-0 flex-1 truncate text-center text-[9px] text-slate-500" title={d.label}>
            {d.label}
          </div>
        ))}
      </div>
    </div>
  )
})
