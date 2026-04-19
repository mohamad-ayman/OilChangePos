import { memo, useMemo } from 'react'
import type { SalesSummary } from '@/shared/api/reports.api'
import { SimpleBarChart } from '@/features/reports/charts/SimpleBarChart'
import { SimpleLineChart } from '@/features/reports/charts/SimpleLineChart'
import { t } from '@/i18n'

type SalesReportProps = {
  summary: SalesSummary | undefined
  loading?: boolean
}

function money(n: number) {
  return n.toLocaleString(undefined, { maximumFractionDigits: 0 })
}

export const SalesReport = memo(function SalesReport({ summary, loading }: SalesReportProps) {
  const dailyLine = useMemo(
    () => (summary?.daily ?? []).map((d) => ({ x: d.date.slice(5), y: d.sales })),
    [summary],
  )

  const monthlyBars = useMemo(
    () => (summary?.monthly ?? []).map((m) => ({ label: m.month.slice(5), value: m.sales })),
    [summary],
  )

  const invoiceTotal = useMemo(() => (summary?.daily ?? []).reduce((s, d) => s + d.invoices, 0), [summary])

  return (
    <section className="border border-slate-200 bg-white">
      <header className="border-b border-slate-200 bg-slate-100 px-3 py-2">
        <h2 className="text-xs font-semibold uppercase tracking-wide text-slate-700">{t('rep.sales.title')}</h2>
        <p className="text-[10px] text-slate-500">{t('rep.sales.subtitle')}</p>
      </header>
      <div className="grid gap-3 p-3 lg:grid-cols-2">
        <div>
          <h3 className="text-[10px] font-semibold uppercase text-slate-500">{t('rep.sales.daily')}</h3>
          <div className="mt-2 h-32">
            {loading || !summary ? (
              <div className="text-xs text-slate-600">{t('rep.loading')}</div>
            ) : (
              <SimpleLineChart data={dailyLine} valueFormatter={money} />
            )}
          </div>
        </div>
        <div>
          <h3 className="text-[10px] font-semibold uppercase text-slate-500">{t('rep.sales.monthly')}</h3>
          <div className="mt-2 h-36">
            {loading || !summary ? (
              <div className="text-xs text-slate-600">{t('rep.loading')}</div>
            ) : (
              <SimpleBarChart data={monthlyBars} valueFormatter={money} barClassName="bg-emerald-700/70" height={128} />
            )}
          </div>
        </div>
        <div className="grid grid-cols-2 gap-2 border-t border-slate-200 pt-3 lg:col-span-2 lg:grid-cols-4">
          <div className="rounded border border-slate-200 px-2 py-2">
            <div className="text-[10px] uppercase text-slate-500">{t('rep.sales.invoices')}</div>
            <div className="font-mono text-lg text-slate-900">{loading ? '—' : invoiceTotal}</div>
          </div>
          <div className="rounded border border-slate-200 px-2 py-2">
            <div className="text-[10px] uppercase text-slate-500">{t('rep.sales.aov')}</div>
            <div className="font-mono text-lg text-slate-900">
              {loading || !summary ? '—' : money(summary.avgOrderValue)}
            </div>
          </div>
          <div className="rounded border border-slate-200 px-2 py-2">
            <div className="text-[10px] uppercase text-slate-500">{t('rep.sales.windowSales')}</div>
            <div className="font-mono text-lg text-slate-900">
              {loading || !summary ? '—' : money(summary.totalSales)}
            </div>
          </div>
          <div className="rounded border border-slate-200 px-2 py-2">
            <div className="text-[10px] uppercase text-slate-500">{t('rep.sales.windowProfit')}</div>
            <div className="font-mono text-lg text-emerald-800">
              {loading || !summary ? '—' : money(summary.totalProfit)}
            </div>
          </div>
        </div>
      </div>
    </section>
  )
})
