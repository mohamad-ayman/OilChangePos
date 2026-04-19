import { DomainStockMovementType, type StockMovement } from '@/entities/stock-movement'
import type { Warehouse } from '@/entities/warehouse'
import type { InventoryGridRow, WarehouseScope } from '@/features/inventory/hooks/useInventory'
import { useStockHistoryForProduct } from '@/features/inventory/hooks/useInventory'
import { t } from '@/i18n'

function movementKindLabel(kind: StockMovement['movementType']): string {
  switch (kind) {
    case DomainStockMovementType.Purchase:
      return t('inv.move.purchase')
    case DomainStockMovementType.Sale:
      return t('inv.move.sale')
    case DomainStockMovementType.Transfer:
      return t('inv.move.transfer')
    case DomainStockMovementType.Adjust:
      return t('inv.move.adjust')
    default:
      return String(kind)
  }
}

type ProductDetailsDrawerProps = {
  open: boolean
  row: InventoryGridRow | null
  warehouses: Warehouse[]
  warehouseScope: WarehouseScope
  onClose: () => void
}

export function ProductDetailsDrawer({
  open,
  row,
  warehouses,
  warehouseScope,
  onClose,
}: ProductDetailsDrawerProps) {
  const history = useStockHistoryForProduct(open && row ? row.productId : null, warehouseScope)

  if (!open || !row) return null

  return (
    <>
      <button
        type="button"
        className="fixed inset-0 z-40 bg-slate-600/20"
        aria-label={t('inv.drawer.close')}
        onClick={onClose}
      />
      <aside
        className="fixed inset-y-0 end-0 z-50 flex w-full max-w-md flex-col border-s border-slate-200 bg-white shadow-2xl"
        role="dialog"
        aria-modal="true"
        aria-labelledby="inv-drawer-title"
      >
        <header className="flex items-start justify-between gap-3 border-b border-slate-200 px-4 py-3">
          <div className="min-w-0">
            <p id="inv-drawer-title" className="truncate text-sm font-semibold text-slate-900">
              {row.name}
            </p>
            <p className="mt-0.5 font-mono text-[11px] text-slate-500">{row.sku}</p>
          </div>
          <button
            type="button"
            onClick={onClose}
            className="rounded border border-slate-300 px-2 py-1 text-xs text-slate-700 hover:bg-slate-100"
          >
            {t('inv.drawer.close')}
          </button>
        </header>

        <div className="min-h-0 flex-1 overflow-y-auto px-4 py-3 text-xs text-slate-700">
          <section className="space-y-1 border-b border-slate-200 pb-3">
            <h3 className="text-[11px] font-semibold uppercase tracking-wide text-slate-500">{t('inv.drawer.product')}</h3>
            <dl className="grid grid-cols-2 gap-x-2 gap-y-1">
              <dt className="text-slate-500">{t('inv.drawer.category')}</dt>
              <dd className="text-end text-slate-800">{row.category}</dd>
              <dt className="text-slate-500">{t('inv.drawer.package')}</dt>
              <dd className="text-end text-slate-800">{row.packageSize}</dd>
              <dt className="text-slate-500">{t('inv.drawer.company')}</dt>
              <dd className="text-end text-slate-800">{row.companyName}</dd>
              <dt className="text-slate-500">{t('inv.drawer.listPrice')}</dt>
              <dd className="text-end tabular-nums text-slate-800">{row.saleUnitPrice.toLocaleString()}</dd>
            </dl>
          </section>

          <section className="mt-3 border-b border-slate-200 pb-3">
            <h3 className="text-[11px] font-semibold uppercase tracking-wide text-slate-500">{t('inv.drawer.stockByWh')}</h3>
            <table className="mt-2 w-full border-collapse text-start">
              <thead>
                <tr className="border-b border-slate-200 text-[10px] uppercase text-slate-500">
                  <th className="py-1">{t('inv.drawer.site')}</th>
                  <th className="py-1 text-end">{t('inv.drawer.qty')}</th>
                  <th className="py-1 text-end">{t('inv.drawer.value')}</th>
                </tr>
              </thead>
              <tbody>
                {warehouses.map((w) => {
                  const cell = row.byWarehouse[w.id]
                  return (
                    <tr key={w.id} className="border-b border-slate-200/80">
                      <td className="py-1 text-slate-800">{w.name}</td>
                      <td className="py-1 text-end tabular-nums">{cell?.qty?.toLocaleString() ?? '0'}</td>
                      <td className="py-1 text-end tabular-nums text-slate-600">
                        {(cell?.stockValue ?? 0).toLocaleString()}
                      </td>
                    </tr>
                  )
                })}
              </tbody>
            </table>
          </section>

          <section className="mt-3">
            <h3 className="text-[11px] font-semibold uppercase tracking-wide text-slate-500">{t('inv.drawer.movements')}</h3>
            {history.isPending ? (
              <p className="mt-2 text-slate-500">{t('inv.drawer.loadingHistory')}</p>
            ) : history.isError ? (
              <p className="mt-2 text-rose-700">{t('inv.drawer.historyError')}</p>
            ) : (
              <div className="mt-2 overflow-x-auto">
                <table className="w-full border-collapse text-start">
                  <thead>
                    <tr className="border-b border-slate-200 text-[10px] uppercase text-slate-500">
                      <th className="py-1">{t('inv.drawer.whenUtc')}</th>
                      <th className="py-1">{t('inv.drawer.type')}</th>
                      <th className="py-1 text-end">{t('inv.drawer.moveQty')}</th>
                    </tr>
                  </thead>
                  <tbody>
                    {(history.data ?? []).length === 0 ? (
                      <tr>
                        <td colSpan={3} className="py-2 text-slate-500">
                          {t('inv.drawer.noMovements')}
                        </td>
                      </tr>
                    ) : (
                      (history.data ?? []).map((m, i) => (
                        <tr key={`${m.movementDateUtc}-${i}`} className="border-b border-slate-200/80">
                          <td className="py-1 font-mono text-[10px] text-slate-600">
                            {new Date(m.movementDateUtc).toISOString().slice(0, 19)}
                          </td>
                          <td className="py-1 text-slate-700">{movementKindLabel(m.movementType)}</td>
                          <td className="py-1 text-end tabular-nums text-slate-800">{m.quantity.toLocaleString()}</td>
                        </tr>
                      ))
                    )}
                  </tbody>
                </table>
              </div>
            )}
          </section>
        </div>
      </aside>
    </>
  )
}
