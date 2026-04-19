import { memo, useMemo } from 'react'
import type { SalesSummary } from '@/shared/api/reports.api'
import { SimpleBarChart } from '@/features/reports/charts/SimpleBarChart'
import { SimpleLineChart } from '@/features/reports/charts/SimpleLineChart'
import { t } from '@/i18n'

type SalesReportProps = {
  summary: SalesSummary | undefined
  loading?: boolean
  /** When false (branch), hide margin / profit footers — sales & throughput only. */
  showProfitMetrics?: boolean
}

function money(n: number) {
  return n.toLocaleString(undefined, { maximumFractionDigits: 0 })
}

export const SalesReport = memo(function SalesReport({
  summary,
  loading,
  showProfitMetrics = true,
}: SalesReportProps) {
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
    <section className="overflow-hidden rounded-xl border border-slate-200 bg-white shadow-sm">
      <header className="border-b border-slate-200 bg-slate-50 px-4 py-3">
        <h2 className="text-sm font-semibold text-slate-900">{t('rep.sales.title')}</h2>
        <p className="mt-0.5 text-xs text-slate-600">
          {t(showProfitMetrics ? 'rep.sales.subtitle' : 'rep.sales.subtitleBranch')}
        </p>
      </header>
      <div className="grid gap-4 p-4 lg:grid-cols-2">
        <div>
          <h3 className="text-[11px] font-semibold uppercase tracking-wide text-slate-500">{t('rep.sales.daily')}</h3>
          <div className="mt-2 h-40">
            {loading || !summary ? (
              <div className="text-xs text-slate-600">{t('rep.loading')}</div>
            ) : (
              <SimpleLineChart data={dailyLine} valueFormatter={money} />
            )}
          </div>
        </div>
        <div>
          <h3 className="text-[11px] font-semibold uppercase tracking-wide text-slate-500">{t('rep.sales.monthly')}</h3>
          <div className="mt-2 h-44">
            {loading || !summary ? (
              <div className="text-xs text-slate-600">{t('rep.loading')}</div>
            ) : (
              <SimpleBarChart data={monthlyBars} valueFormatter={money} barClassName="bg-emerald-700/70" height={152} />
            )}
          </div>
        </div>
        <div
          className={[
            'grid grid-cols-2 gap-3 border-t border-slate-200 pt-4 lg:col-span-2',
            showProfitMetrics ? 'lg:grid-cols-4' : 'lg:grid-cols-3',
          ].join(' ')}
        >
          <div className="rounded-lg border border-slate-200 bg-slate-50/50 px-3 py-2.5">
            <div className="text-[11px] font-medium uppercase tracking-wide text-slate-500">{t('rep.sales.invoices')}</div>
            <div className="mt-1 font-mono text-xl font-semibold tabular-nums text-slate-900">{loading ? '—' : invoiceTotal}</div>
          </div>
          <div className="rounded-lg border border-slate-200 bg-slate-50/50 px-3 py-2.5">
            <div className="text-[11px] font-medium uppercase tracking-wide text-slate-500">{t('rep.sales.aov')}</div>
            <div className="mt-1 font-mono text-xl font-semibold tabular-nums text-slate-900">
              {loading || !summary ? '—' : money(summary.avgOrderValue)}
            </div>
          </div>
          <div className="rounded-lg border border-slate-200 bg-slate-50/50 px-3 py-2.5">
            <div className="text-[11px] font-medium uppercase tracking-wide text-slate-500">{t('rep.sales.windowSales')}</div>
            <div className="mt-1 font-mono text-xl font-semibold tabular-nums text-slate-900">
              {loading || !summary ? '—' : money(summary.totalSales)}
            </div>
          </div>
          {showProfitMetrics ? (
            <div className="rounded-lg border border-slate-200 bg-slate-50/50 px-3 py-2.5">
              <div className="text-[11px] font-medium uppercase tracking-wide text-slate-500">{t('rep.sales.windowProfit')}</div>
              <div className="mt-1 font-mono text-xl font-semibold tabular-nums text-emerald-800">
                {loading || !summary ? '—' : money(summary.totalProfit)}
              </div>
            </div>
          ) : null}
        </div>
      </div>
    </section>
  )
})
