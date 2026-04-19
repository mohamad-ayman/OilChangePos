import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import axios from 'axios'
import { useEffect, useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import { WarehouseType } from '@/entities/warehouse'
import {
  createTransferLine,
  getEffectiveSalePrice,
  getInventorySnapshot,
  getProducts,
  getTransferHistory,
  getWarehouses,
} from '@/shared/api/inventory.api'
import { useAuthStore } from '@/shared/store/auth.store'
import { t } from '@/i18n'

type TransferProductOption = {
  productId: number
  availableQty: number
  caption: string
}

export function DirectTransferPage() {
  const user = useAuthStore((s) => s.user)
  const qc = useQueryClient()

  const whQ = useQuery({ queryKey: ['warehouses'], queryFn: getWarehouses })
  const warehouses = whQ.data ?? []

  const [fromId, setFromId] = useState<number | ''>('')
  const [toId, setToId] = useState<number | ''>('')
  const [productId, setProductId] = useState<number | ''>('')
  const [quantity, setQuantity] = useState('')
  const [applyBranchPrice, setApplyBranchPrice] = useState(false)
  const [branchPrice, setBranchPrice] = useState('')
  const [lastSuccessAt, setLastSuccessAt] = useState<string | null>(null)
  const [hFromDate, setHFromDate] = useState(() => {
    const d = new Date()
    d.setDate(d.getDate() - 14)
    return d.toISOString().slice(0, 10)
  })
  const [hToDate, setHToDate] = useState(() => new Date().toISOString().slice(0, 10))
  const [hFromWh, setHFromWh] = useState<number | 'all'>('all')
  const [hToWh, setHToWh] = useState<number | 'all'>('all')

  useEffect(() => {
    if (!warehouses.length || fromId !== '') return
    const main = warehouses.find((w) => w.type === WarehouseType.Main && w.isActive)
    const branch = warehouses.find((w) => w.type === WarehouseType.Branch && w.isActive)
    if (main) setFromId(main.id)
    if (branch) setToId(branch.id)
  }, [warehouses, fromId])

  const fromWh = useMemo(() => warehouses.find((w) => w.id === fromId), [warehouses, fromId])
  const toWh = useMemo(() => warehouses.find((w) => w.id === toId), [warehouses, toId])

  const mainToBranch = Boolean(
    fromWh?.type === WarehouseType.Main && toWh?.type === WarehouseType.Branch,
  )

  const productOptionsQ = useQuery({
    queryKey: ['directXfer', 'productOptions', fromId],
    enabled: typeof fromId === 'number',
    queryFn: async (): Promise<TransferProductOption[]> => {
      const [snap, products] = await Promise.all([
        getInventorySnapshot(fromId as number),
        getProducts(),
      ])
      const pmap = new Map(products.map((p) => [p.id, p]))
      const opts: TransferProductOption[] = []
      for (const r of snap) {
        if (r.currentStock <= 0) continue
        const p = pmap.get(r.productId)
        const cname = p?.companyName ?? ''
        const baseName = p?.name ?? r.productName
        const label = !cname ? baseName : `${cname} — ${baseName}`
        opts.push({
          productId: r.productId,
          availableQty: r.currentStock,
          caption: `${label}  —  ${Number(r.currentStock).toFixed(3)} ${t('xfer.direct.availableSuffix')}`,
        })
      }
      return opts
    },
  })

  const options = productOptionsQ.data ?? []
  const selectedRow = options.find((o) => o.productId === productId)

  const effPriceQ = useQuery({
    queryKey: ['directXfer', 'effPrice', toId, productId],
    queryFn: () => getEffectiveSalePrice(productId as number, toId as number),
    enabled: mainToBranch && productId !== '' && typeof toId === 'number',
  })

  const historyQ = useQuery({
    queryKey: ['directXfer', 'history', hFromDate, hToDate, hFromWh, hToWh],
    queryFn: () =>
      getTransferHistory({
        fromLocalDate: hFromDate,
        toLocalDate: hToDate,
        fromWarehouseId: hFromWh === 'all' ? null : hFromWh,
        toWarehouseId: hToWh === 'all' ? null : hToWh,
      }),
  })

  const historyRows = historyQ.data ?? []
  const totalQty = useMemo(
    () => historyRows.reduce((sum, row) => sum + Number(row.quantity ?? 0), 0),
    [historyRows],
  )

  useEffect(() => {
    if (!mainToBranch) {
      setApplyBranchPrice(false)
      setBranchPrice('')
    }
  }, [mainToBranch])

  const transferMut = useMutation({
    mutationFn: async () => {
      if (!user || typeof fromId !== 'number' || typeof toId !== 'number' || productId === '') {
        throw new Error('invalid')
      }
      const qty = Number(quantity.replace(',', '.'))
      if (!Number.isFinite(qty) || qty <= 0) throw new Error('qty')
      const row = selectedRow
      if (!row || qty > row.availableQty) throw new Error('max')

      if (fromWh?.type === WarehouseType.Branch && toWh?.type === WarehouseType.Branch) {
        throw new Error('b2b')
      }
      if (fromId === toId) throw new Error('same')

      let branchSale: number | null = null
      if (applyBranchPrice) {
        if (!mainToBranch) throw new Error('priceRule')
        const bp = Number(branchPrice.replace(',', '.'))
        if (!Number.isFinite(bp) || bp < 0) throw new Error('price')
        branchSale = bp
      }

      await createTransferLine({
        productId: productId as number,
        quantity: qty,
        fromWarehouseId: fromId,
        toWarehouseId: toId,
        notes: t('xfer.direct.notes'),
        userId: user.id,
        branchSalePriceForDestination: branchSale ?? undefined,
      })
    },
    onSuccess: async () => {
      await qc.invalidateQueries({ queryKey: ['directXfer'] })
      await qc.invalidateQueries({ queryKey: ['mainWarehouse'] })
      await qc.invalidateQueries({ queryKey: ['warehouses'] })
      await qc.invalidateQueries({ queryKey: ['directXfer', 'history'] })
      setQuantity('')
      setLastSuccessAt(new Date().toISOString())
    },
  })

  const errMsg = (() => {
    const e = transferMut.error
    if (!e) return null
    if (e instanceof Error) {
      const m = e.message
      if (m === 'invalid') return t('xfer.direct.errInvalid')
      if (m === 'b2b') return t('xfer.direct.errBranchToBranch')
      if (m === 'same') return t('xfer.direct.errSameWh')
      if (m === 'qty') return t('xfer.direct.errQty')
      if (m === 'max') return t('xfer.direct.errQtyMax')
      if (m === 'priceRule') return t('xfer.direct.errBranchPriceRule')
      if (m === 'price') return t('xfer.direct.errBranchPrice')
    }
    if (axios.isAxiosError(e)) {
      const d = e.response?.data as { error?: string } | undefined
      if (d && typeof d.error === 'string') return d.error
    }
    return e instanceof Error ? e.message : String(e)
  })()

  const onExecute = () => {
    setLastSuccessAt(null)
    transferMut.mutate()
  }

  return (
    <div className="border-b border-slate-200 px-3 py-4 sm:px-4">
      <div className="flex flex-col gap-2 border-b border-slate-200 pb-4 sm:flex-row sm:items-start sm:justify-between">
        <div>
          <h1 className="text-base font-semibold text-slate-900">{t('xfer.direct.title')}</h1>
          <p className="mt-1 max-w-3xl text-xs italic leading-relaxed text-slate-500">{t('xfer.direct.subtitle')}</p>
        </div>
        <div className="flex flex-wrap gap-2 text-xs">
          <Link
            to="/app/transfers/requests"
            className="rounded border border-slate-300 px-2.5 py-1 text-slate-700 hover:bg-slate-100"
          >
            {t('xfer.direct.linkLegacyLog')}
          </Link>
          <Link
            to="/app/transfers/workflow/new"
            className="rounded border border-sky-600 bg-sky-600 px-2.5 py-1 text-white hover:bg-sky-700"
          >
            {t('xfer.direct.linkWizard')}
          </Link>
        </div>
      </div>

      <div className="mt-4 grid gap-4 xl:grid-cols-3">
        <section className="xl:col-span-2 rounded border border-slate-200 bg-white p-4">
          <h2 className="mb-3 text-xs font-semibold uppercase tracking-wide text-slate-700">{t('xfer.direct.formTitle')}</h2>
          <div className="grid gap-3 sm:grid-cols-2">
            <label className="block text-xs text-slate-600">
              {t('xfer.direct.fromWh')}
              <select
                value={fromId === '' ? '' : String(fromId)}
                onChange={(e) => {
                  const v = e.target.value ? Number(e.target.value) : ''
                  setFromId(v)
                  setProductId('')
                  setQuantity('')
                }}
                className="mt-1 w-full rounded border border-slate-300 bg-white px-2 py-2 text-sm text-slate-900"
              >
                <option value="">{t('xfer.select')}</option>
                {warehouses.map((w) => (
                  <option key={w.id} value={w.id}>
                    {w.name}
                    {w.type === WarehouseType.Main ? ` (${t('xfer.main')})` : ''}
                    {w.type === WarehouseType.Branch ? ` (${t('xfer.branch')})` : ''}
                  </option>
                ))}
              </select>
            </label>

            <label className="block text-xs text-slate-600">
              {t('xfer.direct.toWh')}
              <select
                value={toId === '' ? '' : String(toId)}
                onChange={(e) => {
                  setToId(e.target.value ? Number(e.target.value) : '')
                }}
                className="mt-1 w-full rounded border border-slate-300 bg-white px-2 py-2 text-sm text-slate-900"
              >
                <option value="">{t('xfer.select')}</option>
                {warehouses.map((w) => (
                  <option key={w.id} value={w.id}>
                    {w.name}
                  </option>
                ))}
              </select>
            </label>

            <label className="block text-xs text-slate-600 sm:col-span-2">
              {t('xfer.direct.product')}
              <select
                value={productId === '' ? '' : String(productId)}
                onChange={(e) => {
                  setProductId(e.target.value ? Number(e.target.value) : '')
                  setQuantity('')
                }}
                disabled={!fromId || productOptionsQ.isPending}
                className="mt-1 w-full rounded border border-slate-300 bg-white px-2 py-2 text-sm text-slate-900 disabled:opacity-50"
              >
                <option value="">{productOptionsQ.isPending ? t('common.loading') : t('xfer.select')}</option>
                {options.map((o) => (
                  <option key={o.productId} value={o.productId}>
                    {o.caption}
                  </option>
                ))}
              </select>
            </label>

            <label className="block text-xs text-slate-600">
              {t('xfer.direct.qty')}
              <input
                value={quantity}
                onChange={(e) => setQuantity(e.target.value)}
                inputMode="decimal"
                className="mt-1 w-full rounded border border-slate-300 bg-white px-2 py-2 text-sm text-slate-900"
              />
            </label>

            <div className="rounded border border-slate-200 bg-slate-50 p-2 text-xs text-slate-600">
              <p>
                {t('xfer.direct.availableNow')}{' '}
                <span className="font-mono text-slate-800">{selectedRow ? selectedRow.availableQty.toFixed(3) : '—'}</span>
              </p>
              <p className="mt-1">
                {t('xfer.direct.maxQty')}{' '}
                <span className="font-mono text-slate-800">{selectedRow ? selectedRow.availableQty.toFixed(3) : '—'}</span>
              </p>
            </div>

            <label className="flex cursor-pointer items-center gap-2 text-xs text-slate-700 sm:col-span-2">
              <input
                type="checkbox"
                checked={applyBranchPrice}
                disabled={!mainToBranch}
                onChange={(e) => {
                  const on = e.target.checked
                  setApplyBranchPrice(on)
                  if (on && effPriceQ.data != null && Number.isFinite(effPriceQ.data)) {
                    setBranchPrice(String(effPriceQ.data))
                  }
                }}
              />
              {t('xfer.direct.applyBranchPrice')}
            </label>

            <label className="block text-xs text-slate-600 sm:col-span-2">
              {t('xfer.direct.branchPrice')}
              <input
                value={branchPrice}
                onChange={(e) => setBranchPrice(e.target.value)}
                disabled={!mainToBranch || !applyBranchPrice}
                inputMode="decimal"
                className="mt-1 w-full rounded border border-slate-300 bg-white px-2 py-2 text-sm text-slate-900 disabled:opacity-50"
              />
            </label>
          </div>

          {mainToBranch && productId !== '' && typeof toId === 'number' ? (
            <p className="mt-3 text-xs text-slate-500">
              {effPriceQ.isPending
                ? t('common.loading')
                : t('xfer.direct.hint').replace('{p}', (effPriceQ.data ?? 0).toFixed(2))}
            </p>
          ) : null}

          {lastSuccessAt ? (
            <p className="mt-3 rounded border border-emerald-200 bg-emerald-50 px-2 py-1.5 text-xs text-emerald-900">
              {t('xfer.direct.successBanner')} {lastSuccessAt.slice(0, 19).replace('T', ' ')} UTC
            </p>
          ) : null}

          {errMsg ? <p className="mt-3 text-xs text-rose-700">{errMsg}</p> : null}

          <button
            type="button"
            disabled={transferMut.isPending || !user}
            onClick={onExecute}
            className="mt-4 w-full rounded border border-sky-600 bg-sky-600 py-2.5 text-sm font-semibold text-white hover:bg-sky-700 disabled:opacity-40"
          >
            {transferMut.isPending ? t('common.loading') : t('xfer.direct.execute')}
          </button>
        </section>

        <section className="rounded border border-slate-200 bg-white p-4">
          <h3 className="text-xs font-semibold uppercase tracking-wide text-slate-700">{t('xfer.direct.historyTitle')}</h3>
          <p className="mt-1 text-[11px] text-slate-500">{t('xfer.direct.historySubtitle')}</p>
          <div className="mt-3 grid gap-2">
            <label className="text-[11px] text-slate-600">
              {t('xfer.direct.hFromDate')}
              <input
                type="date"
                value={hFromDate}
                onChange={(e) => setHFromDate(e.target.value)}
                className="mt-1 w-full rounded border border-slate-300 bg-white px-2 py-1.5 text-xs text-slate-900"
              />
            </label>
            <label className="text-[11px] text-slate-600">
              {t('xfer.direct.hToDate')}
              <input
                type="date"
                value={hToDate}
                onChange={(e) => setHToDate(e.target.value)}
                className="mt-1 w-full rounded border border-slate-300 bg-white px-2 py-1.5 text-xs text-slate-900"
              />
            </label>
            <label className="text-[11px] text-slate-600">
              {t('xfer.direct.hFromWh')}
              <select
                value={hFromWh === 'all' ? 'all' : String(hFromWh)}
                onChange={(e) => setHFromWh(e.target.value === 'all' ? 'all' : Number(e.target.value))}
                className="mt-1 w-full rounded border border-slate-300 bg-white px-2 py-1.5 text-xs text-slate-900"
              >
                <option value="all">{t('xfer.direct.hAll')}</option>
                {warehouses.map((w) => (
                  <option key={w.id} value={w.id}>
                    {w.name}
                  </option>
                ))}
              </select>
            </label>
            <label className="text-[11px] text-slate-600">
              {t('xfer.direct.hToWh')}
              <select
                value={hToWh === 'all' ? 'all' : String(hToWh)}
                onChange={(e) => setHToWh(e.target.value === 'all' ? 'all' : Number(e.target.value))}
                className="mt-1 w-full rounded border border-slate-300 bg-white px-2 py-1.5 text-xs text-slate-900"
              >
                <option value="all">{t('xfer.direct.hAll')}</option>
                {warehouses.map((w) => (
                  <option key={w.id} value={w.id}>
                    {w.name}
                  </option>
                ))}
              </select>
            </label>
            <button
              type="button"
              onClick={() => void historyQ.refetch()}
              className="rounded border border-slate-300 px-2 py-1.5 text-xs text-slate-700 hover:bg-slate-100"
            >
              {t('xfer.direct.refresh')}
            </button>
          </div>
          <p className="mt-2 text-[11px] text-slate-500">
            {historyRows.length} {t('xfer.direct.historyRows')} · {totalQty.toFixed(3)} {t('xfer.direct.historyQty')}
          </p>
        </section>
      </div>

      <section className="mt-4 overflow-x-auto rounded border border-slate-200 bg-slate-50">
        <table className="w-full min-w-[720px] border-collapse text-start text-xs">
          <thead className="border-b border-slate-200 bg-slate-50 text-[10px] text-slate-500">
            <tr>
              <th className="px-2 py-2">{t('xfer.direct.col.time')}</th>
              <th className="px-2 py-2">{t('xfer.direct.col.product')}</th>
              <th className="px-2 py-2 text-end">{t('xfer.direct.col.qty')}</th>
              <th className="px-2 py-2">{t('xfer.direct.col.from')}</th>
              <th className="px-2 py-2">{t('xfer.direct.col.to')}</th>
              <th className="px-2 py-2">{t('xfer.direct.col.notes')}</th>
            </tr>
          </thead>
          <tbody>
            {historyQ.isPending ? (
              <tr>
                <td colSpan={6} className="px-2 py-6 text-center text-slate-500">
                  {t('xfer.direct.historyLoading')}
                </td>
              </tr>
            ) : historyRows.length === 0 ? (
              <tr>
                <td colSpan={6} className="px-2 py-6 text-center text-slate-500">
                  {t('xfer.direct.historyEmpty')}
                </td>
              </tr>
            ) : (
              historyRows.map((r, idx) => (
                <tr key={`${r.movementUtc}-${r.productName}-${idx}`} className="border-b border-slate-200">
                  <td className="px-2 py-1.5 font-mono text-slate-600">{r.movementUtc.slice(0, 19).replace('T', ' ')}</td>
                  <td className="px-2 py-1.5 text-slate-800">{r.productName}</td>
                  <td className="px-2 py-1.5 text-end font-mono text-sky-800">{Number(r.quantity).toFixed(3)}</td>
                  <td className="px-2 py-1.5 text-slate-700">{r.fromWarehouseName}</td>
                  <td className="px-2 py-1.5 text-slate-700">{r.toWarehouseName}</td>
                  <td className="px-2 py-1.5 text-slate-600">{r.notes || '—'}</td>
                </tr>
              ))
            )}
          </tbody>
        </table>
      </section>
    </div>
  )
}
