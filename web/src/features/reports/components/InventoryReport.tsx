import { memo, useMemo } from 'react'
import type { InventoryStats } from '@/shared/api/reports.api'
import { SimpleBarChart } from '@/features/reports/charts/SimpleBarChart'
import { t } from '@/i18n'

type InventoryReportProps = {
  stats: InventoryStats | undefined
  loading?: boolean
}

function money(n: number) {
  return n.toLocaleString(undefined, { maximumFractionDigits: 0 })
}

export const InventoryReport = memo(function InventoryReport({ stats, loading }: InventoryReportProps) {
  const whBars = useMemo(
    () => (stats?.valueByWarehouse ?? []).map((w) => ({ label: w.warehouseName.slice(0, 8), value: w.stockValue })),
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
    <section className="border border-slate-200 bg-white">
      <header className="border-b border-slate-200 bg-slate-100 px-3 py-2">
        <h2 className="text-xs font-semibold uppercase tracking-wide text-slate-700">{t('rep.inv.title')}</h2>
        <p className="text-[10px] text-slate-500">{t('rep.inv.subtitle')}</p>
      </header>
      <div className="grid gap-3 p-3 lg:grid-cols-2">
        <div>
          <h3 className="text-[10px] font-semibold uppercase text-slate-500">{t('rep.inv.whValue')}</h3>
          <div className="mt-2 h-36">
            {loading || !stats ? (
              <div className="text-xs text-slate-600">{t('rep.loading')}</div>
            ) : (
              <SimpleBarChart data={whBars} valueFormatter={money} barClassName="bg-amber-700/70" />
            )}
          </div>
        </div>
        <div>
          <h3 className="text-[10px] font-semibold uppercase text-slate-500">{t('rep.inv.moveFreq')}</h3>
          <div className="mt-2 h-36">
            {loading || !stats ? (
              <div className="text-xs text-slate-600">{t('rep.loading')}</div>
            ) : (
              <SimpleBarChart data={movementBars} barClassName="bg-violet-700/70" />
            )}
          </div>
        </div>
        <div className="lg:col-span-2">
          <h3 className="text-[10px] font-semibold uppercase text-slate-500">{t('rep.inv.low')}</h3>
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
        <div className="lg:col-span-2">
          <h3 className="text-[10px] font-semibold uppercase text-slate-500">{t('rep.inv.dead')}</h3>
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
