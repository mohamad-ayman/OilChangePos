import { InventoryTable } from '@/features/inventory/components/InventoryTable'
import { ProductDetailsDrawer } from '@/features/inventory/components/ProductDetailsDrawer'
import { useInventory } from '@/features/inventory/hooks/useInventory'
import { t } from '@/i18n'

export function InventoryPage() {
  const inv = useInventory()
  const drawerOpen = inv.selectedProductId != null

  return (
    <div className="overflow-hidden rounded-2xl border border-slate-200/90 bg-white shadow-sm shadow-slate-900/[0.04] ring-1 ring-slate-900/[0.02]">
      <div className="flex flex-col gap-1 border-b border-slate-200/80 bg-slate-50/50 px-4 py-4 sm:flex-row sm:items-end sm:justify-between sm:px-5">
        <div>
          <h1 className="text-base font-semibold text-slate-900">{t('inv.title')}</h1>
          <p className="text-xs text-slate-500">{t('inv.subtitle')}</p>
        </div>
        <div className="flex flex-wrap items-center gap-2">
          <label className="sr-only" htmlFor="inv-search">
            {t('common.search')}
          </label>
          <input
            id="inv-search"
            value={inv.search}
            onChange={(e) => inv.setSearch(e.target.value)}
            placeholder={t('inv.searchPlaceholder')}
            className="h-8 min-w-[12rem] flex-1 rounded border border-slate-300 bg-slate-100 px-2 text-xs text-slate-900 outline-none ring-sky-500/30 focus:ring-1 sm:max-w-xs"
          />
          <label className="sr-only" htmlFor="inv-cat">
            {t('inv.category')}
          </label>
          <select
            id="inv-cat"
            value={inv.category}
            onChange={(e) => inv.setCategory(e.target.value)}
            className="h-8 rounded border border-slate-300 bg-slate-100 px-2 text-xs text-slate-800"
          >
            {inv.categories.map((c) => (
              <option key={c} value={c}>
                {c === 'all' ? t('inv.allCategories') : c}
              </option>
            ))}
          </select>
          <label className="sr-only" htmlFor="inv-wh">
            {t('inv.warehouse')}
          </label>
          <select
            id="inv-wh"
            value={inv.warehouseScope === 'all' ? 'all' : String(inv.warehouseScope)}
            onChange={(e) => {
              const v = e.target.value
              inv.setWarehouseScope(v === 'all' ? 'all' : Number(v))
            }}
            className="h-8 rounded border border-slate-300 bg-slate-100 px-2 text-xs text-slate-800"
          >
            {inv.canPickAllWarehouses ? <option value="all">{t('inv.allWarehouses')}</option> : null}
            {inv.warehouses.map((w) => (
              <option key={w.id} value={String(w.id)}>
                {w.name}
              </option>
            ))}
          </select>
        </div>
      </div>

      <div className="mt-2 px-4 pb-4 sm:px-5">
        <InventoryTable
          rows={inv.rows}
          sortColumn={inv.sortColumn}
          sortDir={inv.sortDir}
          onSort={inv.toggleSort}
          loading={inv.isLoading}
          onRowClick={(r) => inv.setSelectedProductId(r.productId)}
        />
      </div>

      <ProductDetailsDrawer
        open={drawerOpen}
        row={inv.selectedRow}
        warehouses={inv.warehouses}
        warehouseScope={inv.warehouseScope}
        onClose={() => inv.setSelectedProductId(null)}
      />
    </div>
  )
}
