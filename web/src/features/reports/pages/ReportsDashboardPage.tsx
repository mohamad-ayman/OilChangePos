import { useQuery } from '@tanstack/react-query'
import { useMemo, useState, type ReactNode } from 'react'
import { BranchOperationalReports } from '@/features/reports/components/BranchOperationalReports'
import { InventoryReport } from '@/features/reports/components/InventoryReport'
import { KpiCard } from '@/features/reports/components/KpiCard'
import { SalesReport } from '@/features/reports/components/SalesReport'
import { TransferReport } from '@/features/reports/components/TransferReport'
import { useReportsData } from '@/features/reports/hooks/useReportsData'
import { inventoryKeys } from '@/features/inventory/services/inventoryQueryKeys'
import { t } from '@/i18n'
import { getWarehouses } from '@/shared/api/inventory.api'
import { useAuthStore } from '@/shared/store/auth.store'

function money(n: number | null) {
  if (n == null) return '—'
  return n.toLocaleString(undefined, { maximumFractionDigits: 0 })
}

export function ReportsDashboardPage() {
  const user = useAuthStore((s) => s.user)
  const {
    sales,
    inventory,
    transfers,
    topProducts,
    kpis,
    skuCountTotal,
    loading,
    showTransferAnalytics,
    showProfitMetrics,
  } = useReportsData()

  /** When set, admin picked a warehouse for the operational reports block; when null, first active branch (else first site) is used. */
  const [adminOpWhOverride, setAdminOpWhOverride] = useState<number | null>(null)

  const warehousesQuery = useQuery({
    queryKey: inventoryKeys.warehouses(),
    queryFn: getWarehouses,
    enabled: showProfitMetrics,
    staleTime: 300_000,
  })

  const adminDefaultOperationalWhId = useMemo(() => {
    const list = warehousesQuery.data
    if (!list?.length) return null
    return (list.find((w) => w.type === 2 && w.isActive) ?? list.find((w) => w.isActive) ?? list[0])?.id ?? null
  }, [warehousesQuery.data])

  const operationalWarehouseId = showProfitMetrics
    ? (adminOpWhOverride ?? adminDefaultOperationalWhId)
    : (user?.homeBranchWarehouseId ?? null)

  const topRows = useMemo(() => topProducts.data?.items ?? [], [topProducts.data])

  const adminWarehouseTopBar: ReactNode =
    showProfitMetrics && adminDefaultOperationalWhId != null ? (
      <div className="flex flex-col gap-3 sm:flex-row sm:items-end sm:justify-between">
        <div className="min-w-0">
          <label className="block text-xs font-semibold text-slate-800" htmlFor="rep-op-wh">
            {t('rep.branch.warehouseScope')}
          </label>
          <p className="mt-1 text-xs leading-relaxed text-slate-600">{t('rep.branch.adminWarehouseHint')}</p>
        </div>
        <select
          id="rep-op-wh"
          className="h-10 w-full max-w-md shrink-0 rounded-lg border border-slate-300 bg-white px-3 text-sm text-slate-900 shadow-sm sm:w-auto"
          value={String(operationalWarehouseId ?? adminDefaultOperationalWhId)}
          onChange={(e) => setAdminOpWhOverride(Number(e.target.value))}
        >
          {(warehousesQuery.data ?? []).map((w) => (
            <option key={w.id} value={w.id} disabled={!w.isActive}>
              {w.name}
              {!w.isActive ? ` (${t('rep.branch.inactive')})` : ''}
            </option>
          ))}
        </select>
      </div>
    ) : null

  return (
    <div className="space-y-8 border-b border-slate-200 px-3 py-5 sm:px-5">
      <header className="max-w-3xl">
        <h1 className="text-lg font-bold tracking-tight text-slate-900 sm:text-xl">{t('rep.title')}</h1>
        <p className="mt-1 text-sm text-slate-600">{t(showProfitMetrics ? 'rep.subtitle' : 'rep.subtitleBranch')}</p>
      </header>

      {showProfitMetrics && warehousesQuery.isPending ? (
        <p className="text-sm text-slate-500">{t('rep.branch.loadingWarehouses')}</p>
      ) : null}

      {showProfitMetrics && warehousesQuery.isSuccess && !warehousesQuery.data?.length ? (
        <p className="rounded-xl border border-amber-200 bg-amber-50 px-4 py-3 text-sm text-amber-950">
          {t('rep.branch.adminNoWarehouses')}
        </p>
      ) : null}

      {operationalWarehouseId != null ? (
        <div className="space-y-2">
          <p className="text-[11px] font-semibold uppercase tracking-wider text-sky-800">{t('rep.section.primaryBadge')}</p>
          <BranchOperationalReports
            warehouseId={operationalWarehouseId}
            topBar={showProfitMetrics ? adminWarehouseTopBar : undefined}
          />
        </div>
      ) : !showProfitMetrics ? (
        <p className="rounded-xl border border-amber-200 bg-amber-50 px-4 py-3 text-sm text-amber-950">{t('rep.branch.noHomeWarehouse')}</p>
      ) : null}

      <section
        className="space-y-5 rounded-2xl border border-slate-200 bg-slate-50/80 p-4 shadow-inner sm:p-6"
        aria-labelledby="rep-analytics-heading"
      >
        <div>
          <h2 id="rep-analytics-heading" className="text-base font-bold text-slate-900">
            {t('rep.section.analyticsTitle')}
          </h2>
          <p className="mt-1 text-sm text-slate-600">{t('rep.section.analyticsSubtitle')}</p>
        </div>

        <div
          className={[
            'grid gap-3 sm:grid-cols-2',
            showProfitMetrics ? 'lg:grid-cols-4' : 'lg:grid-cols-3',
          ].join(' ')}
        >
          <KpiCard title={t('rep.kpi.sales')} value={money(kpis.totalSales)} hint={t('rep.kpi.salesHint')} loading={loading} />
          {showProfitMetrics ? (
            <KpiCard title={t('rep.kpi.profit')} value={money(kpis.totalProfit)} hint={t('rep.kpi.profitHint')} loading={loading} />
          ) : null}
          <KpiCard
            title={t('rep.kpi.tx')}
            value={kpis.transactionCount != null ? String(kpis.transactionCount) : '—'}
            hint={t('rep.kpi.txHint')}
            loading={loading}
          />
          {showProfitMetrics ? (
            <KpiCard title={t('rep.kpi.stock')} value={money(kpis.stockValue)} hint={t('rep.kpi.stockHint')} loading={loading} />
          ) : (
            <KpiCard
              title={t('rep.kpi.skus')}
              value={skuCountTotal != null ? String(skuCountTotal) : '—'}
              hint={t('rep.kpi.skusHint')}
              loading={loading}
            />
          )}
        </div>

        <SalesReport summary={sales.data} loading={sales.isPending} showProfitMetrics={showProfitMetrics} />

        <div className="grid gap-4 lg:grid-cols-3">
          <div className="lg:col-span-2">
            <InventoryReport
              stats={inventory.data}
              loading={inventory.isPending}
              variant={showProfitMetrics ? 'admin' : 'branch'}
              omitLowStock={operationalWarehouseId != null}
            />
          </div>
          <aside className="overflow-hidden rounded-xl border border-slate-200 bg-white shadow-sm">
            <header className="border-b border-slate-200 bg-slate-50 px-4 py-3">
              <h3 className="text-sm font-semibold text-slate-900">{t('rep.top.title')}</h3>
              <p className="mt-0.5 text-xs text-slate-600">{topProducts.data?.periodLabel ?? '—'}</p>
            </header>
            <ol className="divide-y divide-slate-200 text-sm">
              {topProducts.isPending ? (
                <li className="px-4 py-5 text-slate-600">{t('common.loading')}</li>
              ) : topRows.length === 0 ? (
                <li className="px-4 py-5 text-slate-600">{t('rep.noRanked')}</li>
              ) : (
                topRows.map((r) => (
                  <li key={r.productId} className="flex items-center justify-between gap-2 px-4 py-2.5">
                    <span className="tabular-nums text-slate-500">{r.rank}.</span>
                    <span className="min-w-0 flex-1 truncate font-medium text-slate-800">{r.name}</span>
                    <span className="shrink-0 font-mono text-xs text-slate-500">
                      {r.qtySold} {t('rep.top.qty')}
                    </span>
                    <span className="shrink-0 font-mono text-sm font-medium text-slate-800">{money(r.revenue)}</span>
                  </li>
                ))
              )}
            </ol>
          </aside>
        </div>

        {showTransferAnalytics ? <TransferReport stats={transfers.data} loading={transfers.isPending} /> : null}
      </section>
    </div>
  )
}
