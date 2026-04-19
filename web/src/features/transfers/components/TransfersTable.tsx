import { Link } from 'react-router-dom'
import type { TransferSummary } from '@/shared/api/inventory.api'
import { TransferStatusBadge } from '@/features/transfers/components/TransferStatusBadge'
import { t } from '@/i18n'

type TransfersTableProps = {
  rows: TransferSummary[]
  warehouseName: (id: number) => string
  loading?: boolean
}

export function TransfersTable({ rows, warehouseName, loading }: TransfersTableProps) {
  return (
    <div className="overflow-x-auto border border-slate-200 bg-white">
      <table className="min-w-full border-collapse text-start text-xs text-slate-800">
        <thead>
          <tr className="border-b border-slate-200 bg-slate-100 text-[11px] font-semibold uppercase tracking-wide text-slate-600">
            <th className="px-2 py-2">{t('xfer.col.id')}</th>
            <th className="px-2 py-2">{t('xfer.col.from')}</th>
            <th className="px-2 py-2">{t('xfer.col.to')}</th>
            <th className="px-2 py-2">{t('xfer.col.status')}</th>
            <th className="px-2 py-2">{t('xfer.col.by')}</th>
            <th className="px-2 py-2">{t('xfer.col.created')}</th>
            <th className="px-2 py-2 text-end">{t('xfer.col.lines')}</th>
          </tr>
        </thead>
        <tbody>
          {loading ? (
            <tr>
              <td colSpan={7} className="px-3 py-8 text-center text-slate-500">
                {t('xfer.list.loading')}
              </td>
            </tr>
          ) : rows.length === 0 ? (
            <tr>
              <td colSpan={7} className="px-3 py-8 text-center text-slate-500">
                {t('xfer.list.empty')}
              </td>
            </tr>
          ) : (
            rows.map((r) => (
              <tr key={r.id} className="border-b border-slate-200 odd:bg-slate-50 hover:bg-slate-50">
                <td className="px-2 py-1.5 font-mono text-[11px]">
                  <Link className="text-sky-700 hover:underline" to={`/app/transfers/workflow/${r.id}`}>
                    {r.id}
                  </Link>
                </td>
                <td className="whitespace-nowrap px-2 py-1.5 text-slate-700">{warehouseName(r.fromWarehouseId)}</td>
                <td className="whitespace-nowrap px-2 py-1.5 text-slate-700">{warehouseName(r.toWarehouseId)}</td>
                <td className="whitespace-nowrap px-2 py-1.5">
                  <TransferStatusBadge status={r.status} />
                </td>
                <td className="whitespace-nowrap px-2 py-1.5 text-slate-600">
                  {r.createdByUsername ?? `${t('xfer.audit.user')} #${r.createdByUserId}`}
                </td>
                <td className="whitespace-nowrap px-2 py-1.5 font-mono text-[10px] text-slate-500">
                  {r.createdAtUtc.slice(0, 19).replace('T', ' ')}
                </td>
                <td className="whitespace-nowrap px-2 py-1.5 text-end tabular-nums text-slate-700">{r.lineCount}</td>
              </tr>
            ))
          )}
        </tbody>
      </table>
    </div>
  )
}
