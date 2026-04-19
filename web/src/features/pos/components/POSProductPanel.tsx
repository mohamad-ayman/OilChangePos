import { memo, useDeferredValue, useMemo, useState } from 'react'
import type { Warehouse } from '@/entities/warehouse'
import type { POSProductRow } from '@/shared/api/pos.api'
import { normalizePosQty } from '@/features/pos/engine/posEngine'
import { catalogDisplayName } from '@/shared/utils/catalogLine'
import { t } from '@/i18n'

function displayTitle(p: POSProductRow): string {
  return catalogDisplayName({ companyName: p.companyName, name: p.name, packageSize: p.packageSize })
}

function initials(p: POSProductRow): string {
  const src = (p.companyName?.trim() || p.name || '?').trim()
  return src.slice(0, Math.min(2, src.length)).toUpperCase()
}

function categoryChipLabel(category: string): string {
  if (category === 'All') return t('pos.catAll')
  const key = `pos.cat.${category}` as const
  const mapped = t(key)
  return mapped === key ? category : mapped
}

type POSProductPanelProps = {
  products: POSProductRow[]
  warehouses: Warehouse[]
  warehouseId: number
  onWarehouseChange: (id: number) => void
  categories: string[]
  category: string
  onCategory: (c: string) => void
  search: string
  onSearch: (v: string) => void
  onSearchEnter: () => void
  onAddProduct: (p: POSProductRow, qty: number) => void
  disabled?: boolean
}

const ProductCard = memo(function ProductCard({
  p,
  addQty,
  onAdd,
}: {
  p: POSProductRow
  addQty: number
  onAdd: (p: POSProductRow, qty: number) => void
}) {
  const stock = p.quantityOnHand
  const isLow = stock > 0 && stock <= 5
  const borderClass = isLow ? 'border-amber-400' : 'border-slate-300'

  return (
    <div
      className={[
        'flex w-[204px] shrink-0 flex-col border bg-white shadow-sm',
        borderClass,
      ].join(' ')}
      style={{ minHeight: 190 }}
    >
      <div
        className="flex h-[76px] items-center justify-center border-b border-slate-200 bg-slate-100 text-2xl font-bold text-slate-400"
        aria-hidden
      >
        {initials(p)}
      </div>
      <p className="line-clamp-2 min-h-[2.5rem] px-2 pt-2 text-end text-[11px] font-bold leading-snug text-slate-900">
        {displayTitle(p)}
      </p>
      <p className="px-2 pt-1 text-end text-sm font-bold text-sky-700">
        {p.unitPrice.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}
        {t('pos.currencySuffix')}
      </p>
      <div className="mt-auto flex items-center justify-between gap-1 px-2 pb-2 pt-1">
        <span
          className={[
            'min-w-0 flex-1 text-end text-[10px] font-medium',
            isLow ? 'text-amber-700' : 'text-emerald-800',
          ].join(' ')}
        >
          {isLow
            ? t('pos.stockLow').replace('{n}', stock.toLocaleString(undefined, { maximumFractionDigits: 3 }))
            : t('pos.stockOk').replace('{n}', stock.toLocaleString(undefined, { maximumFractionDigits: 3 }))}
        </span>
        <button
          type="button"
          disabled={stock <= 0}
          onClick={(e) => {
            e.stopPropagation()
            onAdd(p, addQty)
          }}
          className={[
            'flex h-[30px] w-8 shrink-0 items-center justify-center rounded border text-sm font-bold',
            stock <= 0
              ? 'cursor-not-allowed border-slate-200 bg-slate-100 text-slate-400'
              : 'border-sky-300 bg-sky-50 text-sky-700 hover:bg-sky-100',
          ].join(' ')}
          aria-label={t('pos.addToCart')}
        >
          +
        </button>
      </div>
    </div>
  )
})

export const POSProductPanel = memo(function POSProductPanel({
  products,
  warehouses,
  warehouseId,
  onWarehouseChange,
  categories,
  category,
  onCategory,
  search,
  onSearch,
  onSearchEnter,
  onAddProduct,
  disabled,
}: POSProductPanelProps) {
  const deferred = useDeferredValue(search.trim())
  const [qtyInput, setQtyInput] = useState('1')

  const addQty = useMemo(() => normalizePosQty(Number(qtyInput.replace(',', '.')) || 1), [qtyInput])

  const inStock = useMemo(() => {
    const rows = products.filter((p) => p.quantityOnHand > 0)
    rows.sort((a, b) => {
      const ca = (a.companyName || '').localeCompare(b.companyName || '', 'ar')
      if (ca !== 0) return ca
      return a.name.localeCompare(b.name, 'ar')
    })
    return rows
  }, [products])

  const filtered = useMemo(() => {
    let rows = inStock
    if (category !== 'All') {
      rows = rows.filter((p) => p.productCategory === category)
    }
    const q = deferred.toLowerCase()
    if (!q) return rows
    return rows.filter((p) => {
      const label = displayTitle(p).toLowerCase()
      return (
        label.includes(q) ||
        p.name.toLowerCase().includes(q) ||
        (p.companyName || '').toLowerCase().includes(q) ||
        (p.packageSize || '').toLowerCase().includes(q) ||
        p.barcode.includes(deferred) ||
        String(p.id).includes(deferred) ||
        p.productCategory.toLowerCase().includes(q)
      )
    })
  }, [inStock, category, deferred])

  return (
    <div className="flex min-h-0 flex-1 flex-col overflow-hidden border border-slate-300 bg-white shadow-sm">
      {/* Module header — matches WinForms prodHeader */}
      <header className="border-b border-slate-200 bg-white px-3 py-3 sm:px-4">
        <div className="border-r-4 border-sky-600 pe-3">
          <h2 className="text-end text-base font-bold text-slate-900">{t('pos.productsTitle')}</h2>
          <p className="mt-1 text-end text-sm text-slate-600">{t('pos.productsSubtitle')}</p>
        </div>
      </header>

      {/* Filter strip — WinForms filterStrip */}
      <div
        className="mx-2 mb-2 mt-2 grid gap-2 border border-slate-200 bg-slate-50 px-3 py-2.5 sm:grid-cols-2 lg:grid-cols-[minmax(0,12rem)_minmax(0,5.5rem)_1fr_minmax(0,12rem)]"
        dir="rtl"
      >
        <label className="flex min-w-0 flex-col gap-0.5 text-[10px] font-semibold text-slate-800">
          <span>{t('pos.branch')}</span>
          <select
            value={warehouseId}
            disabled={disabled}
            onChange={(e) => onWarehouseChange(Number(e.target.value))}
            className="h-8 w-full rounded border border-slate-300 bg-white px-2 text-sm text-slate-900"
          >
            {warehouses.map((w) => (
              <option key={w.id} value={w.id}>
                {w.name}
              </option>
            ))}
          </select>
        </label>
        <label className="flex min-w-0 flex-col gap-0.5 text-[10px] font-semibold text-slate-800">
          <span>{t('pos.qtyToAdd')}</span>
          <input
            type="number"
            min={0.001}
            step={0.001}
            disabled={disabled}
            value={qtyInput}
            onChange={(e) => setQtyInput(e.target.value)}
            className="h-8 w-full rounded border border-slate-300 bg-white px-2 text-sm text-slate-900"
          />
        </label>
        <div className="flex min-w-0 flex-col gap-1">
          <span className="text-[10px] font-semibold text-slate-800">{t('pos.categories')}</span>
          <div className="flex max-w-full flex-wrap justify-end gap-1 overflow-x-auto pb-0.5">
            {categories.map((c) => (
              <button
                key={c}
                type="button"
                disabled={disabled}
                onClick={() => onCategory(c)}
                className={[
                  'shrink-0 rounded border px-2 py-1 text-[11px] font-bold transition',
                  c === category
                    ? 'border-sky-600 bg-sky-600 text-white'
                    : 'border-slate-300 bg-white text-slate-700 hover:bg-slate-100',
                ].join(' ')}
              >
                {categoryChipLabel(c)}
              </button>
            ))}
          </div>
        </div>
        <label className="flex min-w-0 flex-col gap-0.5 text-[10px] font-semibold text-slate-800">
          <span>{t('pos.searchLabel')}</span>
          <input
            value={search}
            disabled={disabled}
            onChange={(e) => onSearch(e.target.value)}
            onKeyDown={(e) => {
              if (e.key === 'Enter') {
                e.preventDefault()
                onSearchEnter()
              }
            }}
            placeholder={t('pos.searchPlaceholder')}
            className="h-8 w-full rounded border border-slate-300 bg-white px-2 text-sm text-slate-900 outline-none ring-sky-500/30 focus:ring-1"
            autoComplete="off"
          />
        </label>
      </div>

      {/* Card grid — WinForms _productCardsPanel */}
      <div className="min-h-0 flex-1 overflow-y-auto overscroll-contain bg-[#f8f9fc] px-3 py-3">
        {filtered.length === 0 ? (
          <p className="px-2 py-8 text-center text-sm italic text-slate-600">{t('pos.emptyProductGrid')}</p>
        ) : (
          <div className="flex flex-wrap justify-center gap-2 sm:justify-start" dir="rtl">
            {filtered.map((p) => (
              <ProductCard key={p.id} p={p} addQty={addQty} onAdd={onAddProduct} />
            ))}
          </div>
        )}
      </div>
    </div>
  )
})
