import { memo, useMemo } from 'react'
import { t } from '@/i18n'

export type LineDatum = { x: string; y: number }

type SimpleLineChartProps = {
  data: readonly LineDatum[]
  strokeClass?: string
  height?: number
  valueFormatter?: (n: number) => string
}

export const SimpleLineChart = memo(function SimpleLineChart({
  data,
  strokeClass = 'stroke-sky-400',
  height = 120,
  valueFormatter = (n) => n.toLocaleString(),
}: SimpleLineChartProps) {
  const { points, minY, maxY } = useMemo(() => {
    if (!data.length) return { points: '', minY: 0, maxY: 1 }
    const ys = data.map((d) => d.y)
    const min = Math.min(...ys)
    const max = Math.max(...ys)
    const span = Math.max(1, max - min)
    const w = 100
    const h = 100
    const pts = data
      .map((d, i) => {
        const x = (i / Math.max(1, data.length - 1)) * w
        const y = h - ((d.y - min) / span) * h
        return `${x},${y}`
      })
      .join(' ')
    return { points: pts, minY: min, maxY: max }
  }, [data])

  if (!data.length) {
    return (
      <div className="text-xs text-slate-500" style={{ height }}>
        {t('rep.chart.noSeries')}
      </div>
    )
  }

  return (
    <div className="relative" style={{ height }}>
      <svg viewBox="0 0 100 100" className="h-full w-full overflow-visible" preserveAspectRatio="none">
        <polyline
          fill="none"
          className={strokeClass}
          strokeWidth={1.5}
          vectorEffect="non-scaling-stroke"
          points={points}
        />
      </svg>
      <div className="pointer-events-none absolute left-0 top-0 text-[9px] font-mono text-slate-500">
        {valueFormatter(maxY)}
      </div>
      <div className="pointer-events-none absolute bottom-6 left-0 text-[9px] font-mono text-slate-500">
        {valueFormatter(minY)}
      </div>
    </div>
  )
})
