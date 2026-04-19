import { useMemo } from 'react'
import { Link } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import { TransfersTable } from '@/features/transfers/components/TransfersTable'
import { useTransfers } from '@/features/transfers/hooks/useTransfers'
import { transferStatusLabel } from '@/features/transfers/workflow/transferWorkflow'
import type { TransferWorkflowStatus } from '@/shared/api/inventory.api'
import { getWarehouses } from '@/shared/api/inventory.api'
import { inventoryKeys } from '@/features/inventory/services/inventoryQueryKeys'
import { t } from '@/i18n'

const statusOptions: (TransferWorkflowStatus | 'all')[] = [
  'all',
  'pending_approval',
  'approved',
  'in_transit',
  'completed',
  'rejected',
]

export function TransfersPage() {
  const xfer = useTransfers()
  const whQuery = useQuery({
    queryKey: inventoryKeys.warehouses(),
    queryFn: getWarehouses,
    staleTime: 300_000,
  })

  const warehouseName = useMemo(() => {
    const map = new Map((whQuery.data ?? []).map((w) => [w.id, w.name]))
    return (id: number) => map.get(id) ?? `#${id}`
  }, [whQuery.data])

  return (
    <div className="space-y-4 rounded-2xl border border-slate-200/90 bg-white p-4 shadow-sm shadow-slate-900/[0.04] ring-1 ring-slate-900/[0.02] sm:p-5">
      <div className="flex flex-col gap-3 border-b border-slate-200 pb-4 sm:flex-row sm:items-end sm:justify-between">
        <div>
          <Link to="/app/transfers" className="mb-2 inline-block text-[11px] text-sky-700 hover:underline">
            {t('xfer.list.backDirect')}
          </Link>
          <h1 className="text-base font-semibold text-slate-900">{t('xfer.list.title')}</h1>
          <p className="text-xs text-slate-500">{t('xfer.list.subtitle')}</p>
        </div>
        <Link
          to="/app/transfers/workflow/new"
          className="inline-flex h-8 items-center justify-center rounded border border-sky-600 bg-sky-600 px-3 text-xs font-semibold text-white hover:bg-sky-700"
        >
          {t('xfer.new')}
        </Link>
      </div>

      <div className="mt-4 flex flex-wrap items-end gap-2">
        <label className="text-[10px] font-semibold uppercase text-slate-500">
          {t('xfer.status')}
          <select
            value={xfer.status}
            onChange={(e) => xfer.setStatus(e.target.value as TransferWorkflowStatus | 'all')}
            className="mt-1 block h-8 min-w-[10rem] rounded border border-slate-300 bg-slate-100 px-2 text-xs text-slate-900"
          >
            {statusOptions.map((s) => (
              <option key={s} value={s}>
                {s === 'all' ? t('xfer.filter.allStatuses') : transferStatusLabel(s)}
              </option>
            ))}
          </select>
        </label>
        <label className="text-[10px] font-semibold uppercase text-slate-500">
          {t('xfer.warehouse')}
          <select
            value={xfer.warehouseId === 'all' ? 'all' : String(xfer.warehouseId)}
            onChange={(e) => xfer.setWarehouseId(e.target.value === 'all' ? 'all' : Number(e.target.value))}
            className="mt-1 block h-8 min-w-[10rem] rounded border border-slate-300 bg-slate-100 px-2 text-xs text-slate-900"
          >
            <option value="all">{t('xfer.anySite')}</option>
            {(whQuery.data ?? []).map((w) => (
              <option key={w.id} value={String(w.id)}>
                {w.name}
              </option>
            ))}
          </select>
        </label>
        <label className="text-[10px] font-semibold uppercase text-slate-500">
          {t('xfer.fromUtc')}
          <input
            type="datetime-local"
            value={xfer.dateFromUtc}
            onChange={(e) => xfer.setDateFromUtc(e.target.value)}
            className="mt-1 block h-8 rounded border border-slate-300 bg-slate-100 px-2 text-xs text-slate-900"
          />
        </label>
        <label className="text-[10px] font-semibold uppercase text-slate-500">
          {t('xfer.toUtc')}
          <input
            type="datetime-local"
            value={xfer.dateToUtc}
            onChange={(e) => xfer.setDateToUtc(e.target.value)}
            className="mt-1 block h-8 rounded border border-slate-300 bg-slate-100 px-2 text-xs text-slate-900"
          />
        </label>
        <button
          type="button"
          onClick={() => xfer.resetFilters()}
          className="h-8 rounded border border-slate-300 px-2 text-xs text-slate-600 hover:bg-slate-100"
        >
          {t('common.reset')}
        </button>
      </div>

      <div className="mt-4">
        <TransfersTable rows={xfer.rows} warehouseName={warehouseName} loading={xfer.isLoading} />
      </div>
    </div>
  )
}
