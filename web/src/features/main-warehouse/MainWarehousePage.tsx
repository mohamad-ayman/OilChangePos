import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Fragment, type ChangeEventHandler, useCallback, useMemo, useRef, useState } from 'react'
import { WarehouseType } from '@/entities/warehouse'
import {
  createStockMovement,
  getCurrentStock,
  getWarehouses,
  type PurchaseStockPayload,
} from '@/shared/api/inventory.api'
import {
  deleteMainWarehousePurchase,
  fetchMainWarehouseCatalog,
  fetchMainWarehouseGrid,
  importMainWarehouseLines,
  type MainWarehouseCatalogEntry,
  type MainWarehouseExcelImportLine,
  type MainWarehouseGridRow,
  updateMainWarehousePurchase,
} from '@/shared/api/mainWarehouse.api'
import { formatApiError } from '@/shared/utils/formatApiError'
import { catalogDisplayName } from '@/shared/utils/catalogLine'
import { useAuthStore } from '@/shared/store/auth.store'
import { t } from '@/i18n'

const qk = {
  grid: ['mainWarehouse', 'grid'] as const,
  catalog: ['mainWarehouse', 'catalog'] as const,
}

function toYmd(d: Date): string {
  return d.toISOString().slice(0, 10)
}

function parseLocalDate(s: string): string {
  const t = s.trim()
  if (/^\d{4}-\d{2}-\d{2}$/.test(t)) return t
  const m = /^(\d{1,2})\/(\d{1,2})\/(\d{4})$/.exec(t)
  if (m) {
    const mm = m[1]!.padStart(2, '0')
    const dd = m[2]!.padStart(2, '0')
    return `${m[3]}-${mm}-${dd}`
  }
  return toYmd(new Date(t))
}

function displayCaption(c: MainWarehouseCatalogEntry): string {
  if (c.caption) return c.caption
  if (c.isPlaceholder) return t('mw.catalogPlaceholder')
  const main = catalogDisplayName({
    companyName: c.companyName,
    name: c.name,
    packageSize: c.packageSize,
  })
  const cat = c.productCategory?.trim()
  if (!cat) return main
  return `${main} (${cat})`
}

function parseImportLines(text: string): MainWarehouseExcelImportLine[] {
  const lines = text.split(/\r?\n/).map((l) => l.trim()).filter(Boolean)
  const out: MainWarehouseExcelImportLine[] = []
  for (let i = 0; i < lines.length; i++) {
    const parts = lines[i]!.split(/[,;]/).map((p) => p.trim().replace(/^"|"$/g, ''))
    if (parts.length < 8) continue
    if (i === 0 && /company|الشركة/i.test(parts[0] ?? '')) continue
    const [
      companyName,
      productName,
      category,
      packageSize,
      qtyS,
      priceS,
      prodS,
      purS,
    ] = parts
    const quantity = Number(qtyS?.replace(',', '.') ?? '0')
    const purchasePrice = Number(priceS?.replace(',', '.') ?? '0')
    if (!companyName || !productName || !Number.isFinite(quantity)) continue
    out.push({
      companyName: companyName!,
      productName: productName!,
      category: category ?? '',
      packageSize: packageSize ?? '',
      quantity,
      purchasePrice,
      productionDate: parseLocalDate(prodS ?? ''),
      purchaseDate: parseLocalDate(purS ?? ''),
    })
  }
  return out
}

type ProductGroup = {
  productId: number
  companyName: string
  inventoryName: string
  packageSize: string
  productCategory: string
  lines: MainWarehouseGridRow[]
  purchaseLines: MainWarehouseGridRow[]
  totalPurchasedQty: number
  weightedAvgPrice: number | null
  onHandAtMain: number | null
  lastPurchaseDate: string
  purchaseCount: number
}

function buildProductGroups(rows: MainWarehouseGridRow[]): ProductGroup[] {
  const map = new Map<number, MainWarehouseGridRow[]>()
  const order: number[] = []
  for (const r of rows) {
    if (!map.has(r.productId)) {
      map.set(r.productId, [])
      order.push(r.productId)
    }
    map.get(r.productId)!.push(r)
  }
  const groups = order.map((productId) => {
    const lines = map.get(productId)!
    const purchaseLines = lines.filter((l) => l.purchaseId != null)
    const totalPurchasedQty = lines.reduce((s, l) => s + l.purchasedQuantity, 0)
    const qtyForAvg = purchaseLines.reduce((s, l) => s + l.purchasedQuantity, 0)
    const costSum = purchaseLines.reduce((s, l) => s + l.purchasedQuantity * l.purchasePrice, 0)
    const weightedAvgPrice = qtyForAvg > 0 ? costSum / qtyForAvg : null
    const onHandAtMain = lines.find((l) => l.onHandAtMain != null)?.onHandAtMain ?? null
    const lastPurchaseDate = lines.reduce(
      (max, l) => (l.purchaseDate > max ? l.purchaseDate : max),
      lines[0]!.purchaseDate,
    )
    return {
      productId,
      companyName: lines[0]!.companyName,
      inventoryName: lines[0]!.inventoryName,
      packageSize: lines[0]!.packageSize,
      productCategory: lines[0]!.productCategory,
      lines,
      purchaseLines,
      totalPurchasedQty,
      weightedAvgPrice,
      onHandAtMain,
      lastPurchaseDate,
      purchaseCount: purchaseLines.length,
    }
  })
  groups.sort((a, b) => {
    const d = b.lastPurchaseDate.localeCompare(a.lastPurchaseDate)
    if (d !== 0) return d
    return a.inventoryName.localeCompare(b.inventoryName, undefined, { sensitivity: 'base' })
  })
  return groups
}

function batchSummaryLabel(g: ProductGroup): string {
  if (g.purchaseCount === 0) return t('mw.groupNoPurchaseLine')
  if (g.purchaseCount === 1) return t('mw.groupBatchOne')
  return t('mw.groupBatchSummary').replace('{count}', String(g.purchaseCount))
}

export function MainWarehousePage() {
  const user = useAuthStore((s) => s.user)
  const qc = useQueryClient()
  const fileRef = useRef<HTMLInputElement>(null)

  const { data: warehouses = [] } = useQuery({ queryKey: ['warehouses'], queryFn: getWarehouses })
  const mainWh = useMemo(
    () => warehouses.find((w) => w.type === WarehouseType.Main && w.isActive) ?? null,
    [warehouses],
  )

  const catalogQ = useQuery({ queryKey: qk.catalog, queryFn: fetchMainWarehouseCatalog })
  const gridQ = useQuery({ queryKey: qk.grid, queryFn: fetchMainWarehouseGrid })

  const catalog = catalogQ.data ?? []
  const rows = gridQ.data ?? []
  const productGroups = useMemo(() => buildProductGroups(rows), [rows])
  const selectableCatalog = useMemo(() => catalog.filter((c) => !c.isPlaceholder), [catalog])

  const [catalogProductId, setCatalogProductId] = useState<number | ''>('')
  const [quantity, setQuantity] = useState('')
  const [purchasePrice, setPurchasePrice] = useState('')
  const [productionDate, setProductionDate] = useState(toYmd(new Date()))
  const [purchaseDate, setPurchaseDate] = useState(toYmd(new Date()))
  const [selectedRow, setSelectedRow] = useState<MainWarehouseGridRow | null>(null)
  const [expandedProductIds, setExpandedProductIds] = useState<Set<number>>(new Set())
  const [page, setPage] = useState(1)
  const [pageSize, setPageSize] = useState(25)

  const selectedCatalog = useMemo(
    () => selectableCatalog.find((c) => c.id === catalogProductId) ?? null,
    [selectableCatalog, catalogProductId],
  )

  const onHandQ = useQuery({
    queryKey: ['mainWarehouse', 'onHand', mainWh?.id, catalogProductId],
    queryFn: () => getCurrentStock(catalogProductId as number, mainWh!.id),
    enabled: Boolean(mainWh && catalogProductId !== ''),
  })

  const invalidate = () => {
    void qc.invalidateQueries({ queryKey: qk.grid })
    void qc.invalidateQueries({ queryKey: qk.catalog })
  }

  const addMut = useMutation({
    mutationFn: async (payload: PurchaseStockPayload) => {
      await createStockMovement({ kind: 'PURCHASE', payload })
    },
    onSuccess: () => {
      invalidate()
      clearForm()
    },
  })

  const updateMut = useMutation({
    mutationFn: updateMainWarehousePurchase,
    onSuccess: () => {
      invalidate()
      clearForm()
    },
  })

  const deleteMut = useMutation({
    mutationFn: deleteMainWarehousePurchase,
    onSuccess: () => {
      invalidate()
      clearForm()
    },
  })

  const importMut = useMutation({
    mutationFn: ({ lines }: { lines: MainWarehouseExcelImportLine[] }) => importMainWarehouseLines(mainWh!.id, lines),
    onSuccess: invalidate,
  })

  function clearForm() {
    setCatalogProductId('')
    setQuantity('')
    setPurchasePrice('')
    setProductionDate(toYmd(new Date()))
    setPurchaseDate(toYmd(new Date()))
    setSelectedRow(null)
    setExpandedProductIds(new Set())
  }

  const loadRow = useCallback(
    (r: MainWarehouseGridRow) => {
      setSelectedRow(r)
      setCatalogProductId(r.productId)
      setQuantity(String(r.purchasedQuantity))
      setPurchasePrice(String(r.purchasePrice))
      setProductionDate(r.productionDate.slice(0, 10))
      setPurchaseDate(r.purchaseDate.slice(0, 10))
      const sameProduct = rows.filter((x) => x.productId === r.productId).length
      if (sameProduct > 1) {
        setExpandedProductIds((prev) => {
          const n = new Set(prev)
          n.add(r.productId)
          return n
        })
      }
    },
    [rows],
  )

  const pageGroups = useMemo(() => {
    const start = (page - 1) * pageSize
    return productGroups.slice(start, start + pageSize)
  }, [productGroups, page, pageSize])

  const totalPages = Math.max(1, Math.ceil(productGroups.length / pageSize))

  const toggleProductExpand = useCallback((productId: number) => {
    setExpandedProductIds((prev) => {
      const n = new Set(prev)
      if (n.has(productId)) n.delete(productId)
      else n.add(productId)
      return n
    })
  }, [])

  const onParentGroupClick = useCallback((g: ProductGroup) => {
    if (g.lines.length < 2) return
    toggleProductExpand(g.productId)
    setCatalogProductId(g.productId)
    setSelectedRow(null)
    setQuantity('')
    setPurchasePrice('')
    setProductionDate(toYmd(new Date()))
    setPurchaseDate(toYmd(new Date()))
  }, [toggleProductExpand])

  const rowKey = (r: MainWarehouseGridRow) =>
    `${r.productId}|${r.purchaseId ?? 'np'}|${r.batchNumber}|${r.purchaseDate}|${r.productionDate}|${r.purchasedQuantity}`

  const selectedKey = selectedRow ? rowKey(selectedRow) : null

  const onAdd = () => {
    if (!user || !mainWh || !selectedCatalog) return
    const q = Number(quantity.replace(',', '.'))
    const p = Number(purchasePrice.replace(',', '.'))
    if (!Number.isFinite(q) || q <= 0 || !Number.isFinite(p) || p < 0) return
    addMut.mutate({
      productId: selectedCatalog.id,
      quantity: q,
      purchasePrice: p,
      productionDate: `${productionDate}T00:00:00`,
      purchaseDate: `${purchaseDate}T00:00:00`,
      warehouseId: mainWh.id,
      notes: t('mw.purchaseNoteManual'),
      userId: user.id,
    })
  }

  const onUpdate = () => {
    if (!user || !selectedCatalog || !selectedRow?.purchaseId) return
    const q = Number(quantity.replace(',', '.'))
    const p = Number(purchasePrice.replace(',', '.'))
    if (!Number.isFinite(q) || q <= 0 || !Number.isFinite(p)) return
    if (selectedRow.productId !== selectedCatalog.id) {
      window.alert(t('mw.errProductMismatch'))
      return
    }
    updateMut.mutate({
      purchaseId: selectedRow.purchaseId,
      productId: selectedCatalog.id,
      productName: selectedCatalog.name,
      companyId: selectedCatalog.companyId,
      productCategory: selectedCatalog.productCategory,
      packageSize: selectedCatalog.packageSize,
      quantity: q,
      purchasePrice: p,
      productionDate: `${productionDate}T00:00:00`,
      purchaseDate: `${purchaseDate}T00:00:00`,
    })
  }

  const onDelete = () => {
    const pid = selectedRow?.purchaseId
    if (pid == null) {
      window.alert(t('mw.errNoPurchaseRow'))
      return
    }
    if (!window.confirm(t('mw.confirmDelete'))) return
    deleteMut.mutate(pid)
  }

  const downloadImportTemplate = () => {
    const header =
      'Company,Product,Category,PackageSize,Quantity,PurchasePrice,ProductionDate,PurchaseDate'
    const example =
      'Shell,Helix Ultra 5W-40,Oil,4L,24,185.50,2026-01-15,2026-04-01'
    const csv = `\uFEFF${header}\n${example}\n`
    const blob = new Blob([csv], { type: 'text/csv;charset=utf-8' })
    const a = document.createElement('a')
    a.href = URL.createObjectURL(blob)
    a.download = 'main-warehouse-purchase-import-template.csv'
    a.click()
    URL.revokeObjectURL(a.href)
  }

  const exportCsv = () => {
    const headers = [
      t('mw.col.companyProduct'),
      t('mw.col.batch'),
      t('mw.col.production'),
      t('mw.col.purchasedQty'),
      t('mw.col.remaining'),
      t('mw.col.price'),
      t('mw.col.purchaseDate'),
      t('mw.col.category'),
    ]
    const esc = (v: string | number | null | undefined) => {
      const s = v == null ? '' : String(v)
      if (/[",\n]/.test(s)) return `"${s.replace(/"/g, '""')}"`
      return s
    }
    const body = rows.map((r) =>
      [
        catalogDisplayName({
          companyName: r.companyName,
          name: r.inventoryName,
          packageSize: r.packageSize,
        }),
        r.batchLabel,
        r.productionDate.slice(0, 10),
        r.purchasedQuantity,
        r.onHandAtMain ?? '',
        r.purchasePrice,
        r.purchaseDate.slice(0, 10),
        r.productCategory,
      ]
        .map(esc)
        .join(','),
    )
    const csv = '\uFEFF' + [headers.join(','), ...body].join('\n')
    const blob = new Blob([csv], { type: 'text/csv;charset=utf-8' })
    const a = document.createElement('a')
    a.href = URL.createObjectURL(blob)
    a.download = `main-warehouse-${toYmd(new Date())}.csv`
    a.click()
    URL.revokeObjectURL(a.href)
  }

  const onPickImport = () => fileRef.current?.click()

  const onImportFile: ChangeEventHandler<HTMLInputElement> = (e) => {
    const f = e.target.files?.[0]
    e.target.value = ''
    if (!f || !user || !mainWh) return
    const reader = new FileReader()
    reader.onload = () => {
      const text = String(reader.result ?? '')
      const lines = parseImportLines(text)
      if (!lines.length) {
        window.alert(t('mw.importEmpty'))
        return
      }
      importMut.mutate({ lines })
    }
    reader.readAsText(f, 'UTF-8')
  }

  const busy = addMut.isPending || updateMut.isPending || deleteMut.isPending || importMut.isPending

  const mutationErrorText = useMemo(() => {
    const err = addMut.error ?? updateMut.error ?? deleteMut.error ?? importMut.error
    return err ? formatApiError(err) : null
  }, [addMut.error, updateMut.error, deleteMut.error, importMut.error])

  if (!mainWh) {
    return (
      <div className="rounded-2xl border border-amber-200/90 bg-amber-50/90 px-4 py-10 text-center text-sm text-amber-900 shadow-sm sm:px-6">
        {t('mw.noMainWarehouse')}
      </div>
    )
  }

  return (
    <div className="space-y-4 rounded-2xl border border-slate-200/90 bg-white p-4 shadow-sm shadow-slate-900/[0.04] ring-1 ring-slate-900/[0.02] sm:p-5">
      <input ref={fileRef} type="file" accept=".csv,text/csv,text/plain" className="hidden" onChange={onImportFile} />

      <div className="flex flex-col gap-2 border-b border-slate-200 pb-4 sm:flex-row sm:items-start sm:justify-between">
        <div>
          <h1 className="text-base font-semibold text-slate-900">{t('mw.title')}</h1>
          <p className="text-xs text-slate-500">{t('mw.subtitle')}</p>
          <p className="mt-1 max-w-2xl text-[11px] leading-relaxed text-slate-500">{t('mw.gridHint')}</p>
        </div>
        <div className="flex flex-wrap gap-2">
          <button
            type="button"
            onClick={exportCsv}
            disabled={!productGroups.length}
            className="rounded border border-sky-600 bg-sky-600 px-3 py-1.5 text-xs font-medium text-white hover:bg-sky-700 disabled:opacity-40"
          >
            {t('mw.exportCsv')}
          </button>
          <button
            type="button"
            onClick={onPickImport}
            disabled={busy}
            className="rounded border border-emerald-600 bg-emerald-600 px-3 py-1.5 text-xs font-medium text-white hover:bg-emerald-700 disabled:opacity-40"
          >
            {t('mw.importCsv')}
          </button>
          <button
            type="button"
            onClick={downloadImportTemplate}
            className="rounded border border-slate-400 bg-white px-3 py-1.5 text-xs font-medium text-slate-800 shadow-sm hover:bg-slate-50"
          >
            {t('mw.importTemplate')}
          </button>
        </div>
      </div>
      <p className="mt-2 max-w-3xl text-[11px] leading-relaxed text-slate-600">{t('mw.importHint')}</p>

      <section className="mt-4 space-y-3 rounded border border-slate-200 bg-white p-3">
        <h2 className="text-xs font-semibold text-slate-700">{t('mw.formSection')}</h2>
        <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
          <label className="block text-[11px] text-slate-600">
            <span>{t('mw.fieldProduct')}</span>
            <select
              value={catalogProductId === '' ? '' : String(catalogProductId)}
              onChange={(e) => {
                const v = e.target.value
                setCatalogProductId(v === '' ? '' : Number(v))
                setSelectedRow(null)
              }}
              className="mt-1 w-full rounded border border-slate-300 bg-white px-2 py-1.5 text-xs text-slate-900"
            >
              <option value="">{t('mw.selectProduct')}</option>
              {selectableCatalog.map((c) => (
                <option key={c.id} value={c.id}>
                  {displayCaption(c)}
                </option>
              ))}
            </select>
          </label>
          <label className="block text-[11px] text-slate-600">
            <span>{t('mw.fieldQty')}</span>
            <input
              value={quantity}
              onChange={(e) => setQuantity(e.target.value)}
              inputMode="decimal"
              className="mt-1 w-full rounded border border-slate-300 bg-white px-2 py-1.5 text-xs text-slate-900"
            />
          </label>
          <label className="block text-[11px] text-slate-600">
            <span>{t('mw.fieldPrice')}</span>
            <input
              value={purchasePrice}
              onChange={(e) => setPurchasePrice(e.target.value)}
              inputMode="decimal"
              className="mt-1 w-full rounded border border-slate-300 bg-white px-2 py-1.5 text-xs text-slate-900"
            />
          </label>
          <div className="text-[11px] text-slate-600">
            <span>{t('mw.fieldOnHand')}</span>
            <p className="mt-1 font-mono text-sm text-sky-800/90">
              {catalogProductId === '' ? '—' : onHandQ.isPending ? t('common.loading') : (onHandQ.data ?? 0).toFixed(2)}
            </p>
          </div>
          <label className="block text-[11px] text-slate-600">
            <span>{t('mw.fieldProduction')}</span>
            <input
              type="date"
              value={productionDate}
              onChange={(e) => setProductionDate(e.target.value)}
              className="mt-1 w-full rounded border border-slate-300 bg-white px-2 py-1.5 text-xs text-slate-900"
            />
          </label>
          <label className="block text-[11px] text-slate-600">
            <span>{t('mw.fieldPurchaseDate')}</span>
            <input
              type="date"
              value={purchaseDate}
              onChange={(e) => setPurchaseDate(e.target.value)}
              className="mt-1 w-full rounded border border-slate-300 bg-white px-2 py-1.5 text-xs text-slate-900"
            />
          </label>
        </div>
        <div className="flex flex-wrap gap-2">
          <button
            type="button"
            disabled={busy || !selectedCatalog}
            onClick={onAdd}
            className="rounded border border-emerald-200 bg-emerald-50 px-3 py-1.5 text-xs font-medium text-emerald-900 disabled:opacity-40"
          >
            {t('mw.btnAdd')}
          </button>
          <button
            type="button"
            disabled={busy || !selectedRow?.purchaseId || !selectedCatalog}
            onClick={onUpdate}
            className="rounded border border-sky-200 bg-sky-50 px-3 py-1.5 text-xs font-medium text-sky-900 disabled:opacity-40"
          >
            {t('mw.btnUpdate')}
          </button>
          <button
            type="button"
            disabled={busy || !selectedRow?.purchaseId}
            onClick={onDelete}
            className="rounded border border-rose-200 bg-rose-50 px-3 py-1.5 text-xs font-medium text-rose-900 disabled:opacity-40"
          >
            {t('mw.btnDelete')}
          </button>
          <button
            type="button"
            onClick={clearForm}
            className="rounded border border-slate-400 bg-slate-100 px-3 py-1.5 text-xs text-slate-800 hover:bg-slate-200"
          >
            {t('mw.btnClear')}
          </button>
        </div>
        {mutationErrorText && (
          <p className="whitespace-pre-wrap rounded border border-rose-200 bg-rose-50/90 px-2 py-1.5 text-xs text-rose-900">
            {mutationErrorText}
          </p>
        )}
      </section>

      <div className="mt-4 overflow-x-auto rounded-lg border border-slate-200 bg-white shadow-sm">
        <div className="max-h-[min(70vh,560px)] overflow-y-auto overflow-x-auto">
          <table className="w-full min-w-[640px] border-collapse text-start text-xs">
            <thead className="sticky top-0 z-[1] border-b border-slate-200 bg-slate-100 text-[10px] text-slate-500 shadow-[0_1px_0_0_rgb(226_232_240)]">
              <tr>
                <th className="w-8 px-1 py-2" aria-label={t('mw.expandShow')} />
                <th className="min-w-[10rem] px-2 py-2">{t('mw.col.companyProduct')}</th>
                <th className="px-2 py-2">{t('mw.col.batch')}</th>
                <th className="px-2 py-2">{t('mw.col.production')}</th>
                <th className="px-2 py-2 text-end">{t('mw.col.purchasedQty')}</th>
                <th className="px-2 py-2 text-end">{t('mw.col.remaining')}</th>
                <th className="px-2 py-2 text-end">{t('mw.col.price')}</th>
                <th className="px-2 py-2">{t('mw.col.purchaseDate')}</th>
                <th className="px-2 py-2">{t('mw.col.category')}</th>
            </tr>
          </thead>
          <tbody>
            {gridQ.isPending ? (
              <tr>
                <td colSpan={9} className="px-2 py-6 text-center text-slate-500">
                  {t('common.loading')}
                </td>
              </tr>
            ) : pageGroups.length === 0 ? (
              <tr>
                <td colSpan={9} className="px-2 py-6 text-center text-slate-500">
                  {t('mw.gridEmpty')}
                </td>
              </tr>
            ) : (
              pageGroups.map((g) => {
                const multi = g.lines.length > 1
                const expanded = expandedProductIds.has(g.productId)
                const groupSelected = Boolean(selectedRow && selectedRow.productId === g.productId)

                if (!multi) {
                  const r = g.lines[0]!
                  const active = selectedKey === rowKey(r)
                  return (
                    <tr
                      key={`single-${r.productId}-${r.purchaseId ?? 'np'}`}
                      onClick={() => loadRow(r)}
                      className={[
                        'cursor-pointer border-b border-slate-200 hover:bg-slate-100',
                        active ? 'bg-sky-100' : '',
                      ].join(' ')}
                    >
                      <td className="px-1 py-1.5" />
                      <td className="px-2 py-1.5 text-slate-800">
                        {catalogDisplayName({
                          companyName: r.companyName,
                          name: r.inventoryName,
                          packageSize: r.packageSize,
                        })}
                      </td>
                      <td className="px-2 py-1.5 font-mono text-slate-600">{r.batchLabel}</td>
                      <td className="px-2 py-1.5 text-slate-600">{r.productionDate.slice(0, 10)}</td>
                      <td className="px-2 py-1.5 text-end font-mono text-slate-800">{r.purchasedQuantity}</td>
                      <td className="px-2 py-1.5 text-end font-mono text-slate-700">
                        {r.onHandAtMain == null ? '—' : r.onHandAtMain}
                      </td>
                      <td className="px-2 py-1.5 text-end font-mono text-slate-800">{r.purchasePrice}</td>
                      <td className="px-2 py-1.5 text-slate-600">{r.purchaseDate.slice(0, 10)}</td>
                      <td className="px-2 py-1.5 text-slate-600">{r.productCategory}</td>
                    </tr>
                  )
                }

                return (
                  <Fragment key={`grp-${g.productId}`}>
                    <tr
                      onClick={() => onParentGroupClick(g)}
                      className={[
                        'cursor-pointer border-b border-slate-200 bg-slate-50/80 hover:bg-amber-50/90',
                        groupSelected ? 'bg-amber-50/70' : '',
                        expanded ? 'ring-1 ring-inset ring-amber-200/80' : '',
                      ].join(' ')}
                      title={t('mw.expandShow')}
                    >
                      <td className="px-1 py-1.5 align-middle">
                        <button
                          type="button"
                          aria-expanded={expanded}
                          aria-label={expanded ? t('mw.expandHide') : t('mw.expandShow')}
                          onClick={(e) => {
                            e.stopPropagation()
                            onParentGroupClick(g)
                          }}
                          className="inline-flex h-7 w-7 items-center justify-center rounded border border-slate-300 bg-white text-[10px] text-slate-700 hover:bg-slate-100 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-sky-500/60"
                        >
                          {expanded ? '▼' : '▶'}
                        </button>
                      </td>
                      <td className="px-2 py-1.5 font-medium text-slate-900">
                        {catalogDisplayName({
                          companyName: g.companyName,
                          name: g.inventoryName,
                          packageSize: g.packageSize,
                        })}
                      </td>
                      <td className="px-2 py-1.5 text-xs font-medium text-amber-900/90">{batchSummaryLabel(g)}</td>
                      <td className="px-2 py-1.5 text-slate-500">{t('mw.mergedCell')}</td>
                      <td className="px-2 py-1.5 text-end font-mono font-medium text-slate-900">
                        {g.totalPurchasedQty}
                      </td>
                      <td className="px-2 py-1.5 text-end font-mono font-medium text-slate-800">
                        {g.onHandAtMain == null ? '—' : g.onHandAtMain}
                      </td>
                      <td className="px-2 py-1.5 text-end font-mono text-xs text-slate-700">
                        {g.weightedAvgPrice == null ? '—' : g.weightedAvgPrice.toFixed(2)}
                      </td>
                      <td className="px-2 py-1.5 text-xs text-slate-700">{g.lastPurchaseDate.slice(0, 10)}</td>
                      <td className="px-2 py-1.5 text-slate-600">{g.productCategory}</td>
                    </tr>
                    {expanded &&
                      g.lines.map((r, idx) => {
                        const active = selectedKey === rowKey(r)
                        return (
                          <tr
                            key={`${g.productId}-d-${r.purchaseId ?? 'np'}-${r.batchNumber}-${idx}`}
                            onClick={() => loadRow(r)}
                            title={t('mw.detailRowHint')}
                            className={[
                              'cursor-pointer border-b border-slate-100 bg-white hover:bg-sky-50/80',
                              active ? 'bg-sky-100' : '',
                            ].join(' ')}
                          >
                            <td className="px-1 py-1" />
                            <td className="px-2 py-1 ps-5 text-[11px] text-slate-700">
                              {catalogDisplayName({
                                companyName: r.companyName,
                                name: r.inventoryName,
                                packageSize: r.packageSize,
                              })}
                            </td>
                            <td className="px-2 py-1 font-mono text-[11px] text-slate-600">{r.batchLabel || '—'}</td>
                            <td className="px-2 py-1 text-[11px] text-slate-600">{r.productionDate.slice(0, 10)}</td>
                            <td className="px-2 py-1 text-end font-mono text-[11px] text-slate-800">
                              {r.purchasedQuantity}
                            </td>
                            <td className="px-2 py-1 text-end font-mono text-[11px] text-slate-600">
                              {r.onHandAtMain == null ? '—' : r.onHandAtMain}
                            </td>
                            <td className="px-2 py-1 text-end font-mono text-[11px] text-slate-800">{r.purchasePrice}</td>
                            <td className="px-2 py-1 text-[11px] text-slate-600">{r.purchaseDate.slice(0, 10)}</td>
                            <td className="px-2 py-1 text-[11px] text-slate-600">{r.productCategory}</td>
                          </tr>
                        )
                      })}
                  </Fragment>
                )
              })
            )}
            </tbody>
          </table>
        </div>
      </div>

      <div className="mt-3 flex flex-wrap items-center justify-between gap-2 text-[11px] text-slate-500">
        <span>
          {t('mw.pageLabel')} {page} {t('mw.pageSep')} {totalPages} — {productGroups.length}{' '}
          {t('mw.rowsLabel')}
        </span>
        <div className="flex items-center gap-2">
          <label className="flex items-center gap-1">
            <span>{t('mw.pageSize')}</span>
            <select
              value={pageSize}
              onChange={(e) => {
                setPageSize(Number(e.target.value))
                setPage(1)
              }}
              className="rounded border border-slate-300 bg-slate-100 px-1 py-0.5 text-xs text-slate-800"
            >
              {[10, 25, 50, 100].map((n) => (
                <option key={n} value={n}>
                  {n}
                </option>
              ))}
            </select>
          </label>
          <button
            type="button"
            disabled={page <= 1}
            onClick={() => setPage((p) => Math.max(1, p - 1))}
            className="rounded border border-slate-300 px-2 py-0.5 text-xs text-slate-700 disabled:opacity-40"
          >
            {t('mw.prev')}
          </button>
          <button
            type="button"
            disabled={page >= totalPages}
            onClick={() => setPage((p) => Math.min(totalPages, p + 1))}
            className="rounded border border-slate-300 px-2 py-0.5 text-xs text-slate-700 disabled:opacity-40"
          >
            {t('mw.next')}
          </button>
        </div>
      </div>
    </div>
  )
}
