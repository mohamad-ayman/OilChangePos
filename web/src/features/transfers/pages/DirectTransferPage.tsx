import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import axios from 'axios'
import { useEffect, useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import { WarehouseType } from '@/entities/warehouse'
import {
  createTransferBulk,
  getEffectiveSalePrice,
  getInventorySnapshot,
  getProducts,
  getTransferHistory,
  getWarehouses,
} from '@/shared/api/inventory.api'
import { useAuthStore } from '@/shared/store/auth.store'
import { catalogDisplayName } from '@/shared/utils/catalogLine'
import { t } from '@/i18n'

type TransferProductOption = {
  productId: number
  availableQty: number
  caption: string
}

type LineRow = { key: string; productId: number | ''; quantity: string; branchPrice: string }

export function DirectTransferPage() {
  const user = useAuthStore((s) => s.user)
  const qc = useQueryClient()

  const whQ = useQuery({ queryKey: ['warehouses'], queryFn: getWarehouses })
  const warehouses = whQ.data ?? []

  const [fromId, setFromId] = useState<number | ''>('')
  const [toId, setToId] = useState<number | ''>('')
  const [lines, setLines] = useState<LineRow[]>([{ key: 'line-0', productId: '', quantity: '', branchPrice: '' }])
  const [applyBranchPrice, setApplyBranchPrice] = useState(false)
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
        const label = catalogDisplayName({
          companyName: p?.companyName,
          name: p?.name ?? r.productName,
          packageSize: p?.packageSize,
        })
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

  useEffect(() => {
    if (!mainToBranch) {
      setApplyBranchPrice(false)
      setLines((rows) => rows.map((r) => ({ ...r, branchPrice: '' })))
    }
  }, [mainToBranch])

  /** When destination branch changes while price updates are enabled, refresh effective prices for all rows that already have a product. */
  useEffect(() => {
    if (!applyBranchPrice || !mainToBranch || typeof toId !== 'number') return
    let cancelled = false
    void (async () => {
      setLines((current) => {
        void (async () => {
          const filled = await Promise.all(
            current.map(async (r) => {
              if (r.productId === '') return { ...r, branchPrice: '' }
              try {
                const p = await getEffectiveSalePrice(r.productId as number, toId)
                if (cancelled) return r
                return { ...r, branchPrice: Number.isFinite(p) ? String(p) : '' }
              } catch {
                if (cancelled) return r
                return { ...r, branchPrice: '' }
              }
            }),
          )
          if (!cancelled) setLines(filled)
        })()
        return current
      })
    })()
    return () => {
      cancelled = true
    }
  }, [toId, applyBranchPrice, mainToBranch])

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

  function addLine() {
    setLines((rows) => [
      ...rows,
      { key: `line-${Date.now()}-${rows.length}`, productId: '', quantity: '', branchPrice: '' },
    ])
  }

  function removeLine(key: string) {
    setLines((rows) => (rows.length <= 1 ? rows : rows.filter((r) => r.key !== key)))
  }

  function handleApplyBranchPriceToggle(on: boolean) {
    setApplyBranchPrice(on)
    if (!on) {
      setLines((rows) => rows.map((r) => ({ ...r, branchPrice: '' })))
    }
  }

  const transferMut = useMutation({
    mutationFn: async () => {
      if (!user || typeof fromId !== 'number' || typeof toId !== 'number') {
        throw new Error('invalid')
      }
      if (fromWh?.type === WarehouseType.Branch && toWh?.type === WarehouseType.Branch) {
        throw new Error('b2b')
      }
      if (fromId === toId) throw new Error('same')

      const payload: { productId: number; quantity: number; branchSalePriceForDestination?: number }[] = []
      for (const row of lines) {
        const emptyLine = row.productId === '' && !row.quantity.trim() && !row.branchPrice.trim()
        if (emptyLine) continue
        if (row.productId === '' || !row.quantity.trim()) throw new Error('lineInvalid')
        const qty = Number(row.quantity.replace(',', '.'))
        if (!Number.isFinite(qty) || qty <= 0) throw new Error('lineInvalid')
        const opt = options.find((o) => o.productId === row.productId)
        if (!opt || qty > opt.availableQty) throw new Error('lineInvalid')

        let branchSale: number | undefined
        if (applyBranchPrice && mainToBranch && row.branchPrice.trim()) {
          const bp = Number(row.branchPrice.replace(',', '.'))
          if (!Number.isFinite(bp) || bp < 0) throw new Error('linePrice')
          branchSale = bp
        }

        payload.push({
          productId: row.productId as number,
          quantity: qty,
          ...(branchSale !== undefined ? { branchSalePriceForDestination: branchSale } : {}),
        })
      }
      if (payload.length === 0) throw new Error('noLines')

      await createTransferBulk({
        fromWarehouseId: fromId,
        toWarehouseId: toId,
        notes: t('xfer.direct.notes'),
        userId: user.id,
        lines: payload,
      })
    },
    onSuccess: async () => {
      await qc.invalidateQueries({ queryKey: ['directXfer'] })
      await qc.invalidateQueries({ queryKey: ['mainWarehouse'] })
      await qc.invalidateQueries({ queryKey: ['warehouses'] })
      await qc.invalidateQueries({ queryKey: ['directXfer', 'history'] })
      setApplyBranchPrice(false)
      setLines([{ key: `line-${Date.now()}`, productId: '', quantity: '', branchPrice: '' }])
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
      if (m === 'noLines') return t('xfer.direct.bulkErrEmpty')
      if (m === 'lineInvalid') return t('xfer.direct.bulkErrRow')
      if (m === 'linePrice') return t('xfer.direct.bulkErrPrice')
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

  const tableMinW = mainToBranch ? 'min-w-[720px]' : 'min-w-[560px]'

  return (
    <div className="space-y-4 rounded-2xl border border-slate-200/90 bg-white p-4 shadow-sm shadow-slate-900/[0.04] ring-1 ring-slate-900/[0.02] sm:p-5">
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
                  setApplyBranchPrice(false)
                  setLines([{ key: `line-${Date.now()}`, productId: '', quantity: '', branchPrice: '' }])
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
          </div>

          {mainToBranch ? (
            <div className="mt-4 space-y-2 rounded border border-slate-200 bg-slate-50/80 p-3">
              <label className="flex cursor-pointer items-center gap-2 text-xs text-slate-800">
                <input
                  type="checkbox"
                  checked={applyBranchPrice}
                  disabled={typeof toId !== 'number'}
                  onChange={(e) => handleApplyBranchPriceToggle(e.target.checked)}
                />
                {t('xfer.direct.applyBranchPrice')}
              </label>
              <p className="text-[11px] leading-relaxed text-slate-600">{t('xfer.direct.bulkBranchPriceHint')}</p>
            </div>
          ) : null}

          <h3 className="mb-2 mt-5 text-xs font-semibold uppercase tracking-wide text-slate-700">
            {t('xfer.direct.linesTitle')}
          </h3>

          <div className="overflow-x-auto rounded border border-slate-200">
            <table className={['w-full border-collapse text-start text-xs', tableMinW].join(' ')}>
              <thead className="border-b border-slate-200 bg-slate-50 text-[10px] text-slate-500">
                <tr>
                  <th className="px-2 py-2">{t('xfer.direct.bulkProduct')}</th>
                  <th className="px-2 py-2 w-24 text-end">{t('xfer.direct.colAvailable')}</th>
                  <th className="px-2 py-2 w-28">{t('xfer.direct.bulkQty')}</th>
                  {mainToBranch ? (
                    <th className="px-2 py-2 w-32">{t('xfer.direct.bulkBranchPrice')}</th>
                  ) : null}
                  <th className="w-16 px-2 py-2" />
                </tr>
              </thead>
              <tbody>
                {lines.map((row) => {
                  const opt = row.productId === '' ? undefined : options.find((o) => o.productId === row.productId)
                  return (
                    <tr key={row.key} className="border-b border-slate-100">
                      <td className="px-2 py-1.5">
                        <select
                          value={row.productId === '' ? '' : String(row.productId)}
                          onChange={(e) => {
                            const v = e.target.value ? Number(e.target.value) : ''
                            setLines((rows) =>
                              rows.map((r) =>
                                r.key === row.key ? { ...r, productId: v, quantity: '', branchPrice: '' } : r,
                              ),
                            )
                            if (applyBranchPrice && mainToBranch && typeof toId === 'number' && v !== '') {
                              void (async () => {
                                try {
                                  const p = await getEffectiveSalePrice(v, toId)
                                  if (!Number.isFinite(p)) return
                                  setLines((rows) =>
                                    rows.map((r) =>
                                      r.key === row.key ? { ...r, branchPrice: String(p) } : r,
                                    ),
                                  )
                                } catch {
                                  /* keep empty */
                                }
                              })()
                            }
                          }}
                          disabled={!fromId || productOptionsQ.isPending}
                          className="w-full max-w-md rounded border border-slate-300 bg-white px-2 py-1.5 text-xs text-slate-900 disabled:opacity-50"
                        >
                          <option value="">{productOptionsQ.isPending ? t('common.loading') : t('xfer.select')}</option>
                          {options.map((o) => (
                            <option key={o.productId} value={o.productId}>
                              {o.caption}
                            </option>
                          ))}
                        </select>
                      </td>
                      <td className="px-2 py-1.5 text-end font-mono text-[11px] text-slate-600">
                        {opt ? opt.availableQty.toFixed(3) : '—'}
                      </td>
                      <td className="px-2 py-1.5">
                        <input
                          value={row.quantity}
                          onChange={(e) => {
                            const v = e.target.value
                            setLines((rows) => rows.map((r) => (r.key === row.key ? { ...r, quantity: v } : r)))
                          }}
                          inputMode="decimal"
                          className="w-full rounded border border-slate-300 bg-white px-2 py-1.5 font-mono text-xs text-slate-900"
                          placeholder="0"
                        />
                      </td>
                      {mainToBranch ? (
                        <td className="px-2 py-1.5">
                          <input
                            value={row.branchPrice}
                            onChange={(e) => {
                              const v = e.target.value
                              setLines((rows) =>
                                rows.map((r) => (r.key === row.key ? { ...r, branchPrice: v } : r)),
                              )
                            }}
                            inputMode="decimal"
                            placeholder="—"
                            disabled={!applyBranchPrice}
                            className="w-full rounded border border-slate-300 bg-white px-2 py-1.5 font-mono text-xs text-slate-900 disabled:opacity-50"
                          />
                        </td>
                      ) : null}
                      <td className="px-2 py-1.5">
                        <button
                          type="button"
                          onClick={() => removeLine(row.key)}
                          className="rounded border border-slate-300 px-1.5 py-1 text-[10px] text-slate-600 hover:bg-slate-100"
                        >
                          {t('xfer.direct.bulkRemoveRow')}
                        </button>
                      </td>
                    </tr>
                  )
                })}
              </tbody>
            </table>
          </div>

          <button
            type="button"
            onClick={addLine}
            disabled={!fromId}
            className="mt-2 rounded border border-slate-300 px-2 py-1 text-xs text-slate-700 hover:bg-slate-100 disabled:opacity-40"
          >
            {t('xfer.direct.bulkAddRow')}
          </button>

          {lastSuccessAt ? (
            <p className="mt-3 rounded border border-emerald-200 bg-emerald-50 px-2 py-1.5 text-xs text-emerald-900">
              {t('xfer.direct.bulkSuccess').replace('{n}', String(transferMut.data?.length ?? '—'))}{' '}
              <span className="font-mono">{lastSuccessAt.slice(0, 19).replace('T', ' ')} UTC</span>
            </p>
          ) : null}

          {errMsg ? <p className="mt-3 text-xs text-rose-700">{errMsg}</p> : null}

          <button
            type="button"
            disabled={transferMut.isPending || !user || !fromId}
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
