import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useMemo, useState } from 'react'
import { isAxiosError } from 'axios'
import { stockRequestKeys } from '@/features/stock-requests/services/stockRequestQueryKeys'
import { getProducts, getWarehouses } from '@/shared/api/inventory.api'
import {
  cancelStockRequest,
  createStockRequest,
  fulfillStockRequest,
  listStockRequests,
  rejectStockRequest,
} from '@/shared/api/stockRequests.api'
import { inventoryKeys } from '@/features/inventory/services/inventoryQueryKeys'
import { useAuthStore } from '@/shared/store/auth.store'
import { t } from '@/i18n'

function fmtWhen(iso: string) {
  try {
    return new Date(iso).toLocaleString()
  } catch {
    return iso
  }
}

function statusClass(status: string) {
  const s = status.toLowerCase()
  if (s === 'pending') return 'bg-amber-100 text-amber-950 ring-1 ring-amber-300/60'
  if (s === 'fulfilled') return 'bg-emerald-100 text-emerald-950 ring-1 ring-emerald-300/60'
  if (s === 'rejected') return 'bg-rose-100 text-rose-950 ring-1 ring-rose-300/60'
  if (s === 'cancelled') return 'bg-slate-200 text-slate-800 ring-1 ring-slate-400/40'
  return 'bg-slate-100 text-slate-800'
}

function statusLabelKey(status: string): string {
  switch (status.toLowerCase()) {
    case 'pending':
      return 'stockReq.status.pending'
    case 'rejected':
      return 'stockReq.status.rejected'
    case 'fulfilled':
      return 'stockReq.status.fulfilled'
    case 'cancelled':
      return 'stockReq.status.cancelled'
    default:
      return 'stockReq.status.unknown'
  }
}

export function StockRequestsPage() {
  const user = useAuthStore((s) => s.user)
  const isAdmin = user?.role === 'admin'
  const qc = useQueryClient()

  const [branchFilter, setBranchFilter] = useState<number | 'all'>('all')
  const [productId, setProductId] = useState<number | ''>('')
  const [qty, setQty] = useState<string>('1')
  const [notes, setNotes] = useState('')
  const [rejectNotes, setRejectNotes] = useState('')
  const [rejectingId, setRejectingId] = useState<number | null>(null)
  const [formError, setFormError] = useState<string | null>(null)
  const [actionError, setActionError] = useState<string | null>(null)

  const warehousesQuery = useQuery({
    queryKey: inventoryKeys.warehouses(),
    queryFn: getWarehouses,
    enabled: isAdmin,
    staleTime: 300_000,
  })

  const branchWarehouses = useMemo(
    () => (warehousesQuery.data ?? []).filter((w) => w.type === 2 && w.isActive),
    [warehousesQuery.data],
  )

  const listFilter = isAdmin && branchFilter !== 'all' ? branchFilter : undefined
  const listQuery = useQuery({
    queryKey: stockRequestKeys.list(listFilter),
    queryFn: () => listStockRequests(listFilter),
    staleTime: 15_000,
  })

  const productsQuery = useQuery({
    queryKey: ['products', 'stock-req'],
    queryFn: getProducts,
    staleTime: 120_000,
    enabled: !isAdmin,
  })

  const invalidate = () => {
    void qc.invalidateQueries({ queryKey: stockRequestKeys.all })
  }

  const createMut = useMutation({
    mutationFn: createStockRequest,
    onSuccess: () => {
      setFormError(null)
      setNotes('')
      setQty('1')
      invalidate()
    },
    onError: (e) => {
      setFormError(isAxiosError(e) ? (e.response?.data as string) || e.message : String(e))
    },
  })

  const fulfillMut = useMutation({
    mutationFn: fulfillStockRequest,
    onSuccess: () => {
      setActionError(null)
      invalidate()
    },
    onError: (e) => {
      setActionError(isAxiosError(e) ? (e.response?.data as string) || e.message : String(e))
    },
  })

  const rejectMut = useMutation({
    mutationFn: ({ id, n }: { id: number; n?: string }) => rejectStockRequest(id, n),
    onSuccess: () => {
      setActionError(null)
      setRejectingId(null)
      setRejectNotes('')
      invalidate()
    },
    onError: (e) => {
      setActionError(isAxiosError(e) ? (e.response?.data as string) || e.message : String(e))
    },
  })

  const cancelMut = useMutation({
    mutationFn: cancelStockRequest,
    onSuccess: () => {
      setActionError(null)
      invalidate()
    },
    onError: (e) => {
      setActionError(isAxiosError(e) ? (e.response?.data as string) || e.message : String(e))
    },
  })

  function submitRequest(e: React.FormEvent) {
    e.preventDefault()
    setFormError(null)
    if (productId === '' || Number(productId) <= 0) {
      setFormError(t('stockReq.err.product'))
      return
    }
    const q = Number(qty.replace(',', '.'))
    if (!Number.isFinite(q) || q <= 0) {
      setFormError(t('stockReq.err.qty'))
      return
    }
    createMut.mutate({ productId: Number(productId), quantity: q, notes: notes.trim() || undefined })
  }

  const rows = listQuery.data ?? []

  return (
    <div className="space-y-6 border-b border-slate-200 px-3 py-5 sm:px-5">
      <header className="max-w-3xl">
        <h1 className="text-lg font-bold text-slate-900 sm:text-xl">{t('stockReq.title')}</h1>
        <p className="mt-1 text-sm text-slate-600">
          {isAdmin ? t('stockReq.subtitleAdmin') : t('stockReq.subtitleBranch')}
        </p>
      </header>

      {!isAdmin ? (
        <section className="max-w-xl rounded-2xl border border-slate-200 bg-white p-4 shadow-sm sm:p-5">
          <h2 className="text-sm font-semibold text-slate-900">{t('stockReq.formTitle')}</h2>
          <p className="mt-1 text-xs text-slate-600">{t('stockReq.formHint')}</p>
          <form className="mt-4 space-y-3" onSubmit={submitRequest}>
            <div>
              <label className="text-xs font-medium text-slate-700" htmlFor="sr-prod">
                {t('stockReq.product')}
              </label>
              <select
                id="sr-prod"
                required
                className="mt-1 h-10 w-full rounded-lg border border-slate-300 bg-white px-2 text-sm"
                value={productId === '' ? '' : String(productId)}
                onChange={(e) => setProductId(e.target.value === '' ? '' : Number(e.target.value))}
              >
                <option value="">{t('stockReq.pickProduct')}</option>
                {(productsQuery.data ?? []).map((p) => (
                  <option key={p.id} value={p.id}>
                    {p.companyName ? `${p.companyName} — ${p.name}` : p.name} ({p.packageSize})
                  </option>
                ))}
              </select>
            </div>
            <div>
              <label className="text-xs font-medium text-slate-700" htmlFor="sr-qty">
                {t('stockReq.quantity')}
              </label>
              <input
                id="sr-qty"
                type="text"
                inputMode="decimal"
                className="mt-1 h-10 w-full rounded-lg border border-slate-300 bg-white px-2 text-sm"
                value={qty}
                onChange={(e) => setQty(e.target.value)}
              />
            </div>
            <div>
              <label className="text-xs font-medium text-slate-700" htmlFor="sr-notes">
                {t('stockReq.notes')}
              </label>
              <textarea
                id="sr-notes"
                rows={2}
                className="mt-1 w-full rounded-lg border border-slate-300 bg-white px-2 py-2 text-sm"
                value={notes}
                onChange={(e) => setNotes(e.target.value)}
              />
            </div>
            {formError ? <p className="text-sm text-rose-700">{formError}</p> : null}
            <button
              type="submit"
              disabled={createMut.isPending}
              className="h-10 rounded-lg bg-sky-600 px-4 text-sm font-semibold text-white hover:bg-sky-700 disabled:opacity-50"
            >
              {createMut.isPending ? t('common.loading') : t('stockReq.submit')}
            </button>
          </form>
        </section>
      ) : null}

      <section className="rounded-2xl border border-slate-200 bg-white shadow-sm">
        <div className="flex flex-col gap-3 border-b border-slate-200 px-4 py-3 sm:flex-row sm:items-center sm:justify-between">
          <h2 className="text-sm font-semibold text-slate-900">{t('stockReq.listTitle')}</h2>
          {isAdmin ? (
            <label className="flex flex-col text-[11px] font-medium text-slate-600">
              {t('stockReq.filterBranch')}
              <select
                className="mt-1 h-9 min-w-[12rem] rounded border border-slate-300 bg-white px-2 text-xs"
                value={branchFilter === 'all' ? 'all' : String(branchFilter)}
                onChange={(e) => setBranchFilter(e.target.value === 'all' ? 'all' : Number(e.target.value))}
              >
                <option value="all">{t('stockReq.allBranches')}</option>
                {branchWarehouses.map((w) => (
                  <option key={w.id} value={String(w.id)}>
                    {w.name}
                  </option>
                ))}
              </select>
            </label>
          ) : null}
        </div>

        {actionError ? <p className="px-4 py-2 text-sm text-rose-700">{actionError}</p> : null}

        <div className="overflow-x-auto">
          <table className="w-full min-w-[56rem] border-collapse text-start text-xs">
            <thead className="border-b border-slate-200 bg-slate-50 text-[11px] font-semibold uppercase tracking-wide text-slate-600">
              <tr>
                <th className="px-3 py-2">{t('stockReq.col.when')}</th>
                {isAdmin ? <th className="px-3 py-2">{t('stockReq.col.branch')}</th> : null}
                <th className="px-3 py-2">{t('stockReq.col.product')}</th>
                <th className="px-3 py-2 text-end">{t('stockReq.col.qty')}</th>
                <th className="px-3 py-2">{t('stockReq.col.by')}</th>
                <th className="px-3 py-2">{t('stockReq.col.status')}</th>
                <th className="px-3 py-2">{t('stockReq.col.notes')}</th>
                <th className="px-3 py-2 text-end">{t('stockReq.col.actions')}</th>
              </tr>
            </thead>
            <tbody>
              {listQuery.isPending ? (
                <tr>
                  <td colSpan={isAdmin ? 8 : 7} className="px-3 py-8 text-center text-slate-500">
                    {t('common.loading')}
                  </td>
                </tr>
              ) : rows.length === 0 ? (
                <tr>
                  <td colSpan={isAdmin ? 8 : 7} className="px-3 py-8 text-center text-slate-500">
                    {t('stockReq.empty')}
                  </td>
                </tr>
              ) : (
                rows.map((r) => (
                  <tr key={r.id} className="border-b border-slate-100 odd:bg-slate-50/60">
                    <td className="whitespace-nowrap px-3 py-2 text-slate-700">{fmtWhen(r.createdAtUtc)}</td>
                    {isAdmin ? <td className="px-3 py-2 font-medium text-slate-800">{r.branchWarehouseName}</td> : null}
                    <td className="max-w-[14rem] truncate px-3 py-2 text-slate-900">{r.productDisplayName}</td>
                    <td className="px-3 py-2 text-end tabular-nums">{r.quantity}</td>
                    <td className="px-3 py-2 text-slate-700">{r.requestedByUsername}</td>
                    <td className="px-3 py-2">
                      <span className={`inline-flex rounded-full px-2 py-0.5 text-[11px] font-semibold ${statusClass(r.status)}`}>
                        {t(statusLabelKey(r.status))}
                      </span>
                    </td>
                    <td className="max-w-[12rem] truncate px-3 py-2 text-slate-600">{r.notes || '—'}</td>
                    <td className="px-3 py-2 text-end">
                      {r.status.toLowerCase() === 'pending' && isAdmin ? (
                        <div className="flex flex-wrap justify-end gap-1">
                          {rejectingId === r.id ? (
                            <span className="flex flex-wrap items-center justify-end gap-1">
                              <input
                                className="h-8 w-36 rounded border border-slate-300 px-1 text-[11px]"
                                placeholder={t('stockReq.rejectReason')}
                                value={rejectNotes}
                                onChange={(e) => setRejectNotes(e.target.value)}
                              />
                              <button
                                type="button"
                                className="rounded border border-rose-600 bg-rose-600 px-2 py-1 text-[11px] font-medium text-white"
                                onClick={() => rejectMut.mutate({ id: r.id, n: rejectNotes.trim() || undefined })}
                              >
                                {t('stockReq.confirmReject')}
                              </button>
                              <button
                                type="button"
                                className="rounded border border-slate-300 px-2 py-1 text-[11px]"
                                onClick={() => {
                                  setRejectingId(null)
                                  setRejectNotes('')
                                }}
                              >
                                {t('common.cancel')}
                              </button>
                            </span>
                          ) : (
                            <>
                              <button
                                type="button"
                                className="rounded border border-emerald-600 bg-emerald-600 px-2 py-1 text-[11px] font-medium text-white disabled:opacity-50"
                                disabled={fulfillMut.isPending}
                                onClick={() => fulfillMut.mutate(r.id)}
                              >
                                {t('stockReq.fulfill')}
                              </button>
                              <button
                                type="button"
                                className="rounded border border-rose-300 bg-white px-2 py-1 text-[11px] font-medium text-rose-800"
                                onClick={() => {
                                  setRejectingId(r.id)
                                  setRejectNotes('')
                                }}
                              >
                                {t('stockReq.reject')}
                              </button>
                            </>
                          )}
                        </div>
                      ) : null}
                      {r.status.toLowerCase() === 'pending' && !isAdmin ? (
                        <button
                          type="button"
                          className="rounded border border-slate-400 px-2 py-1 text-[11px] font-medium text-slate-800 disabled:opacity-50"
                          disabled={cancelMut.isPending}
                          onClick={() => cancelMut.mutate(r.id)}
                        >
                          {t('stockReq.cancel')}
                        </button>
                      ) : null}
                    </td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>
      </section>
    </div>
  )
}
