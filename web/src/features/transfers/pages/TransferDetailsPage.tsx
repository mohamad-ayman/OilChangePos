import { useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import { TransferStatusBadge } from '@/features/transfers/components/TransferStatusBadge'
import { TransferTimeline } from '@/features/transfers/components/TransferTimeline'
import { useTransferDetail } from '@/features/transfers/hooks/useTransferDetail'
import { useTransferApproval } from '@/features/transfers/hooks/useTransferApproval'
import {
  allowedTransferActions,
  transferStatusLabel,
} from '@/features/transfers/workflow/transferWorkflow'
import { getWarehouses } from '@/shared/api/inventory.api'
import { inventoryKeys } from '@/features/inventory/services/inventoryQueryKeys'
import { useAuthStore } from '@/shared/store/auth.store'
import { t } from '@/i18n'

export function TransferDetailsPage() {
  const { detail, isLoading, transferId } = useTransferDetail()
  const approval = useTransferApproval()
  const user = useAuthStore((s) => s.user)
  const [rejectReason, setRejectReason] = useState('')

  const whQuery = useQuery({
    queryKey: inventoryKeys.warehouses(),
    queryFn: getWarehouses,
    staleTime: 300_000,
  })

  const whName = useMemo(() => {
    const m = new Map((whQuery.data ?? []).map((w) => [w.id, w.name]))
    return (id: number) => m.get(id) ?? `#${id}`
  }, [whQuery.data])

  const actions = useMemo(() => {
    if (!detail || !user) return []
    return allowedTransferActions(detail.status, user.role)
  }, [detail, user])

  if (isLoading && !detail) {
    return <p className="px-4 py-8 text-sm text-slate-500">{t('xfer.detail.loading')}</p>
  }

  if (!detail) {
    return (
      <div className="px-4 py-8 text-sm text-slate-500">
        {t('xfer.detail.notFound')}{' '}
        <Link className="text-sky-700 hover:underline" to="/app/transfers/requests">
          {t('xfer.detail.backLink')}
        </Link>
      </div>
    )
  }

  return (
    <div className="border-b border-slate-200 px-3 py-4 sm:px-4">
      <div className="flex flex-wrap items-start justify-between gap-3 border-b border-slate-200 pb-4">
        <div>
          <Link to="/app/transfers/requests" className="text-[11px] text-sky-700 hover:underline">
            {t('xfer.detail.back')}
          </Link>
          <h1 className="mt-2 font-mono text-sm font-semibold text-slate-900">{detail.id}</h1>
          <p className="mt-1 text-xs text-slate-500">
            {whName(detail.fromWarehouseId)} · {whName(detail.toWarehouseId)}
          </p>
        </div>
        <div className="flex flex-col items-end gap-2">
          <TransferStatusBadge status={detail.status} />
          <span className="text-[10px] uppercase tracking-wide text-slate-500">
            {t('xfer.detail.stateLabel')} {transferStatusLabel(detail.status)}
          </span>
        </div>
      </div>

      <div className="mt-4 grid gap-4 lg:grid-cols-2">
        <section className="border border-slate-200 bg-white">
          <header className="border-b border-slate-200 bg-slate-100 px-3 py-2 text-[11px] font-semibold uppercase tracking-wide text-slate-600">
            {t('xfer.detail.linesStock')}
          </header>
          <table className="w-full border-collapse text-start text-xs">
            <thead>
              <tr className="border-b border-slate-200 text-[10px] uppercase text-slate-500">
                <th className="px-2 py-1">{t('xfer.detail.col.product')}</th>
                <th className="px-2 py-1 text-end">{t('xfer.detail.col.qty')}</th>
                <th className="px-2 py-1 text-end text-rose-700/90">
                  {whName(detail.fromWarehouseId)} {t('xfer.detail.col.out')}
                </th>
                <th className="px-2 py-1 text-end text-emerald-300/90">
                  {whName(detail.toWarehouseId)} {t('xfer.detail.col.in')}
                </th>
              </tr>
            </thead>
            <tbody>
              {detail.lines.map((l) => (
                <tr key={l.productId} className="border-b border-slate-200">
                  <td className="px-2 py-1.5 text-slate-800">
                    {l.productName ?? `${t('inv.col.product')} #${l.productId}`}
                  </td>
                  <td className="px-2 py-1.5 text-end font-mono tabular-nums">{l.quantity}</td>
                  <td className="px-2 py-1.5 text-end font-mono tabular-nums text-rose-700">−{l.quantity}</td>
                  <td className="px-2 py-1.5 text-end font-mono tabular-nums text-emerald-800">
                    {detail.status === 'completed' ? `+${l.quantity}` : '—'}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
          {detail.remoteLineIds?.length ? (
            <p className="border-t border-slate-200 px-2 py-2 text-[10px] text-slate-500">
              {t('xfer.detail.remotePrefix')} {detail.remoteLineIds.join(', ')}
            </p>
          ) : null}
        </section>

        <TransferTimeline detail={detail} />
      </div>

      {actions.length > 0 ? (
        <section className="mt-4 border border-slate-200 bg-white px-3 py-3">
          <h2 className="text-[11px] font-semibold uppercase tracking-wide text-slate-600">{t('xfer.detail.approval')}</h2>
          {!approval.mayApproveReject && !approval.mayOperateShipment ? (
            <p className="mt-2 text-xs text-slate-500">{t('xfer.detail.noRole')}</p>
          ) : (
            <div className="mt-2 flex flex-wrap gap-2">
              {actions.includes('approve') ? (
                <button
                  type="button"
                  disabled={approval.approve.isPending}
                  onClick={() => approval.approve.mutate({ transferId })}
                  className="rounded border border-emerald-300 bg-emerald-50 px-3 py-1.5 text-xs font-semibold text-emerald-900 hover:bg-emerald-100 disabled:opacity-40"
                >
                  {t('xfer.detail.approve')}
                </button>
              ) : null}
              {actions.includes('reject') ? (
                <span className="flex flex-wrap items-center gap-2">
                  <input
                    value={rejectReason}
                    onChange={(e) => setRejectReason(e.target.value)}
                    placeholder={t('xfer.rejectReason')}
                    className="min-w-[10rem] rounded border border-slate-300 bg-slate-100 px-2 py-1 text-xs text-slate-900"
                  />
                  <button
                    type="button"
                    disabled={approval.reject.isPending || !rejectReason.trim()}
                    onClick={() => {
                      approval.reject.mutate({ transferId, reason: rejectReason.trim() })
                      setRejectReason('')
                    }}
                    className="rounded border border-rose-300 bg-rose-50 px-3 py-1.5 text-xs font-semibold text-rose-900 hover:bg-rose-100 disabled:opacity-40"
                  >
                    {t('xfer.detail.reject')}
                  </button>
                </span>
              ) : null}
              {actions.includes('advance_shipment') ? (
                <button
                  type="button"
                  disabled={approval.complete.isPending}
                  onClick={() => approval.complete.mutate({ transferId, note: t('xfer.note.inTransit') })}
                  className="rounded border border-violet-300 bg-violet-50 px-3 py-1.5 text-xs font-semibold text-violet-900 hover:bg-violet-100 disabled:opacity-40"
                >
                  {t('xfer.detail.inTransit')}
                </button>
              ) : null}
              {actions.includes('finalize_receipt') ? (
                <button
                  type="button"
                  disabled={approval.complete.isPending}
                  onClick={() => approval.complete.mutate({ transferId, note: t('xfer.note.receipt') })}
                  className="rounded border border-sky-300 bg-sky-50 px-3 py-1.5 text-xs font-semibold text-sky-900 hover:bg-sky-100 disabled:opacity-40"
                >
                  {t('xfer.detail.postReceipt')}
                </button>
              ) : null}
            </div>
          )}
        </section>
      ) : null}

      {approval.approve.isError || approval.reject.isError || approval.complete.isError ? (
        <p className="mt-2 text-xs text-rose-700">{t('xfer.detail.actionFail')}</p>
      ) : null}
    </div>
  )
}
