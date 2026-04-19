import { memo, useMemo } from 'react'
import type { TransferStats } from '@/shared/api/reports.api'
import { SimpleBarChart } from '@/features/reports/charts/SimpleBarChart'
import { t } from '@/i18n'

type TransferReportProps = {
  stats: TransferStats | undefined
  loading?: boolean
}

export const TransferReport = memo(function TransferReport({ stats, loading }: TransferReportProps) {
  const pipeline = useMemo(() => {
    if (!stats) return []
    return [
      { label: t('rep.pipeline.pending'), value: stats.pendingApproval },
      { label: t('rep.pipeline.approved'), value: stats.approved },
      { label: t('rep.pipeline.transit'), value: stats.inTransit },
      { label: t('rep.pipeline.done'), value: stats.completed },
      { label: t('rep.pipeline.rej'), value: stats.rejected },
    ]
  }, [stats])

  const branchNet = useMemo(
    () =>
      (stats?.byBranch ?? []).map((b) => ({
        label: b.warehouseName.slice(0, 6),
        value: b.outbound + b.inbound,
      })),
    [stats],
  )

  return (
    <section className="border border-slate-200 bg-white">
      <header className="border-b border-slate-200 bg-slate-100 px-3 py-2">
        <h2 className="text-xs font-semibold uppercase tracking-wide text-slate-700">{t('rep.transfer.title')}</h2>
        <p className="text-[10px] text-slate-500">{t('rep.transfer.subtitle')}</p>
      </header>
      <div className="grid gap-3 p-3 lg:grid-cols-2">
        <div>
          <h3 className="text-[10px] font-semibold uppercase text-slate-500">{t('rep.transfer.pipeline')}</h3>
          <div className="mt-2 h-36">
            {loading || !stats ? (
              <div className="text-xs text-slate-600">{t('rep.loading')}</div>
            ) : (
              <SimpleBarChart data={pipeline} barClassName="bg-cyan-700/70" />
            )}
          </div>
        </div>
        <div>
          <h3 className="text-[10px] font-semibold uppercase text-slate-500">{t('rep.transfer.perBranch')}</h3>
          <div className="mt-2 h-36">
            {loading || !stats ? (
              <div className="text-xs text-slate-600">{t('rep.loading')}</div>
            ) : (
              <SimpleBarChart data={branchNet} barClassName="bg-slate-600/80" />
            )}
          </div>
        </div>
        <div className="lg:col-span-2">
          <h3 className="text-[10px] font-semibold uppercase text-slate-500">{t('rep.transfer.flow')}</h3>
          <div className="mt-1 overflow-x-auto border border-slate-200">
            <table className="w-full border-collapse text-start text-xs">
              <thead className="border-b border-slate-200 bg-slate-100 text-[10px] uppercase text-slate-500">
                <tr>
                  <th className="px-2 py-1">{t('rep.transfer.from')}</th>
                  <th className="px-2 py-1">{t('rep.transfer.to')}</th>
                  <th className="px-2 py-1 text-end">{t('rep.transfer.moves')}</th>
                </tr>
              </thead>
              <tbody>
                {loading || !stats ? (
                  <tr>
                    <td colSpan={3} className="px-2 py-4 text-slate-600">
                      {t('rep.loading')}
                    </td>
                  </tr>
                ) : stats.flows.length === 0 ? (
                  <tr>
                    <td colSpan={3} className="px-2 py-4 text-slate-600">
                      {t('rep.transfer.flowEmpty')}
                    </td>
                  </tr>
                ) : (
                  stats.flows.map((f) => (
                    <tr key={`${f.fromWarehouseId}-${f.toWarehouseId}`} className="border-b border-slate-200">
                      <td className="px-2 py-1 text-slate-800">{f.fromName}</td>
                      <td className="px-2 py-1 text-slate-800">{f.toName}</td>
                      <td className="px-2 py-1 text-end font-mono text-sky-800/90">{f.count}</td>
                    </tr>
                  ))
                )}
              </tbody>
            </table>
          </div>
          <p className="mt-2 text-[10px] text-slate-600">
            {t('xfer.detail.pendingSummary')}{' '}
            <span className="font-mono text-amber-900">{stats?.pendingApproval ?? '—'}</span>{' '}
            · {t('xfer.detail.completedVsRejected')}{' '}
            <span className="font-mono text-emerald-800">{stats?.completed ?? '—'}</span> /{' '}
            <span className="font-mono text-rose-700">{stats?.rejected ?? '—'}</span>
          </p>
        </div>
      </div>
    </section>
  )
})
