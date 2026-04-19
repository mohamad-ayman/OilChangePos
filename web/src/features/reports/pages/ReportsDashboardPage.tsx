import { useMemo } from 'react'
import { InventoryReport } from '@/features/reports/components/InventoryReport'
import { KpiCard } from '@/features/reports/components/KpiCard'
import { SalesReport } from '@/features/reports/components/SalesReport'
import { TransferReport } from '@/features/reports/components/TransferReport'
import { useReportsData } from '@/features/reports/hooks/useReportsData'
import { t } from '@/i18n'

function money(n: number | null) {
  if (n == null) return '—'
  return n.toLocaleString(undefined, { maximumFractionDigits: 0 })
}

export function ReportsDashboardPage() {
  const { sales, inventory, transfers, topProducts, kpis, loading } = useReportsData()

  const topRows = useMemo(() => topProducts.data?.items ?? [], [topProducts.data])

  return (
    <div className="space-y-4 border-b border-slate-200 px-3 py-4 sm:px-4">
      <header className="border-b border-slate-200 pb-3">
        <h1 className="text-base font-semibold text-slate-900">{t('rep.title')}</h1>
        <p className="text-xs text-slate-500">{t('rep.subtitle')}</p>
      </header>

      <div className="grid gap-2 sm:grid-cols-2 lg:grid-cols-4">
        <KpiCard title={t('rep.kpi.sales')} value={money(kpis.totalSales)} hint={t('rep.kpi.salesHint')} loading={loading} />
        <KpiCard title={t('rep.kpi.profit')} value={money(kpis.totalProfit)} hint={t('rep.kpi.profitHint')} loading={loading} />
        <KpiCard
          title={t('rep.kpi.tx')}
          value={kpis.transactionCount != null ? String(kpis.transactionCount) : '—'}
          hint={t('rep.kpi.txHint')}
          loading={loading}
        />
        <KpiCard title={t('rep.kpi.stock')} value={money(kpis.stockValue)} hint={t('rep.kpi.stockHint')} loading={loading} />
      </div>

      <SalesReport summary={sales.data} loading={sales.isPending} />
      <div className="grid gap-4 lg:grid-cols-3">
        <div className="lg:col-span-2">
          <InventoryReport stats={inventory.data} loading={inventory.isPending} />
        </div>
        <aside className="border border-slate-200 bg-white">
          <header className="border-b border-slate-200 bg-slate-100 px-3 py-2">
            <h2 className="text-xs font-semibold uppercase tracking-wide text-slate-700">{t('rep.top.title')}</h2>
            <p className="text-[10px] text-slate-500">{topProducts.data?.periodLabel ?? '—'}</p>
          </header>
          <ol className="divide-y divide-slate-200 text-xs">
            {topProducts.isPending ? (
              <li className="px-3 py-4 text-slate-600">{t('common.loading')}</li>
            ) : topRows.length === 0 ? (
              <li className="px-3 py-4 text-slate-600">{t('rep.noRanked')}</li>
            ) : (
              topRows.map((r) => (
                <li key={r.productId} className="flex items-center justify-between gap-2 px-3 py-2">
                  <span className="text-slate-500">{r.rank}.</span>
                  <span className="min-w-0 flex-1 truncate font-medium text-slate-800">{r.name}</span>
                  <span className="shrink-0 font-mono text-[10px] text-slate-500">
                    {r.qtySold} {t('rep.top.qty')}
                  </span>
                  <span className="shrink-0 font-mono text-slate-700">{money(r.revenue)}</span>
                </li>
              ))
            )}
          </ol>
        </aside>
      </div>

      <TransferReport stats={transfers.data} loading={transfers.isPending} />
    </div>
  )
}
