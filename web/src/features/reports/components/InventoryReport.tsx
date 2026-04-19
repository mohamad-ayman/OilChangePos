import { memo, useMemo } from 'react'
import type { InventoryStats } from '@/shared/api/reports.api'
import { SimpleBarChart } from '@/features/reports/charts/SimpleBarChart'
import { t } from '@/i18n'

type InventoryReportProps = {
  stats: InventoryStats | undefined
  loading?: boolean
  /** Branch: operational view (SKU coverage, movement) without money-based warehouse valuation. */
  variant?: 'admin' | 'branch'
  /** When true, hides the low-stock table (detail lives in operational reports → Low stock tab). */
  omitLowStock?: boolean
}

function money(n: number) {
  return n.toLocaleString(undefined, { maximumFractionDigits: 0 })
}

function fmtCount(n: number) {
  return String(Math.round(n))
}

export const InventoryReport = memo(function InventoryReport({
  stats,
  loading,
  variant = 'admin',
  omitLowStock = false,
}: InventoryReportProps) {
  const whBars = useMemo(
    () => (stats?.valueByWarehouse ?? []).map((w) => ({ label: w.warehouseName.slice(0, 8), value: w.stockValue })),
    [stats],
  )

  const skuBars = useMemo(
    () => (stats?.valueByWarehouse ?? []).map((w) => ({ label: w.warehouseName.slice(0, 8), value: w.skuCount })),
    [stats],
  )

  const movementBars = useMemo(
    () =>
      (stats?.movementFrequency ?? []).map((m) => ({
        label: `#${m.productId}`,
        value: m.movements30d,
      })),
    [stats],
  )

  return (
    <section className="overflow-hidden rounded-xl border border-slate-200 bg-white shadow-sm">
      <header className="border-b border-slate-200 bg-slate-50 px-4 py-3">
        <h2 className="text-sm font-semibold text-slate-900">{t('rep.inv.title')}</h2>
        <p className="mt-0.5 text-xs text-slate-600">
          {t(variant === 'admin' ? 'rep.inv.subtitle' : 'rep.inv.subtitleBranch')}
        </p>
      </header>
      <div className="grid gap-4 p-4 lg:grid-cols-2">
        <div>
          <h3 className="text-[11px] font-semibold uppercase tracking-wide text-slate-500">
            {t(variant === 'admin' ? 'rep.inv.whValue' : 'rep.inv.skuBySite')}
          </h3>
          <div className="mt-2 h-40">
            {loading || !stats ? (
              <div className="text-xs text-slate-600">{t('rep.loading')}</div>
            ) : variant === 'admin' ? (
              <SimpleBarChart data={whBars} valueFormatter={money} barClassName="bg-amber-700/70" />
            ) : (
              <SimpleBarChart data={skuBars} valueFormatter={fmtCount} barClassName="bg-sky-700/70" />
            )}
          </div>
        </div>
        <div>
          <h3 className="text-[11px] font-semibold uppercase tracking-wide text-slate-500">{t('rep.inv.moveFreq')}</h3>
          <div className="mt-2 h-40">
            {loading || !stats ? (
              <div className="text-xs text-slate-600">{t('rep.loading')}</div>
            ) : (
              <SimpleBarChart data={movementBars} barClassName="bg-violet-700/70" />
            )}
          </div>
        </div>
        {omitLowStock ? (
          <div className="rounded-lg border border-dashed border-slate-200 bg-slate-50/80 px-3 py-3 lg:col-span-2">
            <p className="text-xs leading-relaxed text-slate-600">{t('rep.inv.lowStockInOperational')}</p>
          </div>
        ) : (
          <div className="lg:col-span-2">
            <h3 className="text-[11px] font-semibold uppercase tracking-wide text-slate-500">{t('rep.inv.low')}</h3>
            <div className="mt-1 overflow-x-auto border border-slate-200">
              <table className="w-full border-collapse text-start text-xs">
                <thead className="border-b border-slate-200 bg-slate-100 text-[10px] uppercase text-slate-500">
                  <tr>
                    <th className="px-2 py-1">{t('rep.inv.col.product')}</th>
                    <th className="px-2 py-1">{t('rep.inv.col.site')}</th>
                    <th className="px-2 py-1 text-end">{t('rep.inv.col.oh')}</th>
                    <th className="px-2 py-1 text-end">{t('rep.inv.col.min')}</th>
                  </tr>
                </thead>
                <tbody>
                  {loading || !stats ? (
                    <tr>
                      <td colSpan={4} className="px-2 py-4 text-slate-600">
                        {t('rep.loading')}
                      </td>
                    </tr>
                  ) : stats.lowStock.length === 0 ? (
                    <tr>
                      <td colSpan={4} className="px-2 py-4 text-slate-600">
                        {t('rep.inv.lowEmpty')}
                      </td>
                    </tr>
                  ) : (
                    stats.lowStock.map((r) => (
                      <tr key={`${r.productId}-${r.warehouseId}`} className="border-b border-slate-200">
                        <td className="px-2 py-1 text-slate-800">{r.productName}</td>
                        <td className="px-2 py-1 text-slate-500">{r.warehouseName}</td>
                        <td className="px-2 py-1 text-end font-mono text-rose-800">{r.quantityOnHand}</td>
                        <td className="px-2 py-1 text-end font-mono text-slate-500">{r.threshold}</td>
                      </tr>
                    ))
                  )}
                </tbody>
              </table>
            </div>
          </div>
        )}
        <div className="lg:col-span-2">
          <h3 className="text-[11px] font-semibold uppercase tracking-wide text-slate-500">{t('rep.inv.dead')}</h3>
          <div className="mt-1 overflow-x-auto border border-slate-200">
            <table className="w-full border-collapse text-start text-xs">
              <thead className="border-b border-slate-200 bg-slate-100 text-[10px] uppercase text-slate-500">
                <tr>
                  <th className="px-2 py-1">{t('rep.inv.col.product')}</th>
                  <th className="px-2 py-1">{t('rep.inv.col.site')}</th>
                  <th className="px-2 py-1 text-end">{t('rep.inv.col.oh')}</th>
                  <th className="px-2 py-1 text-end">{t('rep.inv.col.daysIdle')}</th>
                </tr>
              </thead>
              <tbody>
                {loading || !stats ? (
                  <tr>
                    <td colSpan={4} className="px-2 py-4 text-slate-600">
                      {t('rep.loading')}
                    </td>
                  </tr>
                ) : stats.deadStock.length === 0 ? (
                  <tr>
                    <td colSpan={4} className="px-2 py-4 text-slate-600">
                      {t('rep.inv.deadEmpty')}
                    </td>
                  </tr>
                ) : (
                  stats.deadStock.map((r) => (
                    <tr key={`${r.productId}-${r.warehouseId}-dead`} className="border-b border-slate-200">
                      <td className="px-2 py-1 text-slate-800">{r.productName}</td>
                      <td className="px-2 py-1 text-slate-500">{r.warehouseName}</td>
                      <td className="px-2 py-1 text-end font-mono">{r.quantityOnHand}</td>
                      <td className="px-2 py-1 text-end font-mono text-amber-800">{r.daysSinceMovement}</td>
                    </tr>
                  ))
                )}
              </tbody>
            </table>
          </div>
        </div>
      </div>
    </section>
  )
})
