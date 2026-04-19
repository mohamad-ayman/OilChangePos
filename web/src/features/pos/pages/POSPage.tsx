import { useCallback, useMemo, useState } from 'react'
import { useQueryClient } from '@tanstack/react-query'
import { POSCartPanel } from '@/features/pos/components/POSCartPanel'
import { POSCheckoutModal } from '@/features/pos/components/POSCheckoutModal'
import { POSProductPanel } from '@/features/pos/components/POSProductPanel'
import { usePOSCart } from '@/features/pos/hooks/usePOSCart'
import { usePOSCatalog } from '@/features/pos/hooks/usePOSCatalog'
import { runCheckout } from '@/features/pos/services/CheckoutService'
import { posKeys } from '@/features/pos/services/posQueryKeys'
import { inventoryKeys } from '@/features/inventory/services/inventoryQueryKeys'
import { normalizePosQty } from '@/features/pos/engine/posEngine'
import type { POSProductRow } from '@/shared/api/pos.api'
import type { InvoiceDto } from '@/shared/api/pos.api'
import { useAuthStore } from '@/shared/store/auth.store'
import { t } from '@/i18n'

export function POSPage() {
  const user = useAuthStore((s) => s.user)
  const qc = useQueryClient()
  const [warehouseId, setWarehouseId] = useState<number | null>(null)
  const { products, ledger, categories, warehouseId: wid, warehouses, isLoading, refetch } = usePOSCatalog(warehouseId)
  const cartApi = usePOSCart()
  const [search, setSearch] = useState('')
  const [category, setCategory] = useState('All')
  const [checkoutOpen, setCheckoutOpen] = useState(false)
  const [checkoutBusy, setCheckoutBusy] = useState(false)
  const [lastInvoice, setLastInvoice] = useState<InvoiceDto | null>(null)

  const stockByProduct = useMemo(() => {
    const m = new Map<number, number>()
    for (const p of products) {
      m.set(p.id, p.quantityOnHand)
    }
    return m
  }, [products])

  const resolveByBarcode = useCallback(
    (code: string): POSProductRow | undefined => {
      const c = code.trim()
      if (!c) return undefined
      return products.find((p) => p.barcode === c || String(p.id) === c)
    },
    [products],
  )

  const tryAddProduct = useCallback(
    (p: POSProductRow, qty: number) => {
      const q = normalizePosQty(qty)
      if (q <= 0 || q > p.quantityOnHand) {
        window.alert(t('pos.qtyInvalid'))
        return
      }
      const existing = cartApi.cart.lines.find((l) => l.productId === p.id)
      const inCart = existing?.quantity ?? 0
      if (inCart + q > p.quantityOnHand) {
        window.alert(t('pos.qtyExceedsStock'))
        return
      }
      cartApi.addProduct(p, q)
    },
    [cartApi.cart.lines, cartApi.addProduct],
  )

  const onSearchEnter = useCallback(() => {
    const p = resolveByBarcode(search.trim())
    if (p && p.quantityOnHand > 0) {
      tryAddProduct(p, normalizePosQty(1))
      setSearch('')
    }
  }, [search, resolveByBarcode, tryAddProduct])

  const startCheckout = useCallback(() => {
    setLastInvoice(null)
    setCheckoutOpen(true)
  }, [])

  const confirmCheckout = useCallback(
    async (paymentMethod: 'cash' | 'card') => {
      if (!user) return
      setCheckoutBusy(true)
      const result = await runCheckout({
        cart: cartApi.cart,
        ledger,
        warehouseId: wid,
        userId: user.id,
        paymentMethod,
      })
      setCheckoutBusy(false)
      if (result.ok) {
        setLastInvoice(result.invoice)
        cartApi.clearCart()
        await qc.invalidateQueries({ queryKey: posKeys.root })
        await qc.invalidateQueries({ queryKey: inventoryKeys.root })
        void refetch()
      } else {
        window.alert(`${t('common.error')}: ${result.message}`)
      }
    },
    [user, cartApi, ledger, wid, qc, refetch],
  )

  const closeModal = useCallback(() => {
    setCheckoutOpen(false)
    setLastInvoice(null)
  }, [])

  return (
    <div className="flex h-[calc(100dvh-3rem)] max-h-[900px] flex-col gap-2 border-b border-slate-200 px-2 py-2 sm:px-3">
      <header className="flex shrink-0 flex-wrap items-center justify-between gap-2 border-b border-slate-200 pb-2">
        <div>
          <h1 className="text-base font-bold text-slate-900">{t('pos.title')}</h1>
          <p className="mt-0.5 text-sm text-slate-600">{t('pos.subtitle')}</p>
        </div>
      </header>

      <div className="flex min-h-0 flex-1 flex-col gap-2 lg:flex-row">
        <div className="flex min-h-0 min-w-0 flex-1 flex-col lg:max-w-[58%]">
          <POSProductPanel
            products={products}
            warehouses={warehouses}
            warehouseId={wid}
            onWarehouseChange={setWarehouseId}
            categories={categories}
            category={category}
            onCategory={setCategory}
            search={search}
            onSearch={setSearch}
            onSearchEnter={onSearchEnter}
            onAddProduct={tryAddProduct}
            disabled={isLoading}
          />
        </div>
        <div className="flex min-h-0 flex-1 flex-col gap-2">
          <POSCartPanel
            lines={cartApi.cart.lines}
            stockByProduct={stockByProduct}
            onQty={cartApi.setLineQty}
            onRemove={cartApi.removeLine}
          />
          <footer className="shrink-0 border border-slate-200 bg-white p-2">
            <div className="flex flex-wrap items-end gap-3">
              <label className="text-[10px] font-semibold uppercase text-slate-500">
                {t('pos.discPct')}
                <input
                  type="number"
                  min={0}
                  max={100}
                  className="mt-0.5 block h-8 w-16 rounded border border-slate-300 bg-slate-100 px-1 text-xs text-slate-900"
                  value={cartApi.cart.discount.kind === 'percent' ? cartApi.cart.discount.value : 0}
                  onChange={(e) => cartApi.setDiscountPercent(Number(e.target.value) || 0)}
                />
              </label>
              <label className="text-[10px] font-semibold uppercase text-slate-500">
                {t('pos.discFixed')}
                <input
                  type="number"
                  min={0}
                  step="0.01"
                  className="mt-0.5 block h-8 w-20 rounded border border-slate-300 bg-slate-100 px-1 text-xs text-slate-900"
                  value={cartApi.cart.discount.kind === 'fixed' ? cartApi.cart.discount.value : 0}
                  onChange={(e) => cartApi.setDiscountFixed(Number(e.target.value) || 0)}
                />
              </label>
              <button
                type="button"
                onClick={() => cartApi.clearDiscount()}
                className="h-8 rounded border border-slate-300 px-2 text-[10px] uppercase text-slate-600 hover:bg-slate-100"
              >
                {t('pos.clearDisc')}
              </button>
            </div>
            <div className="mt-2 flex flex-wrap items-center justify-between gap-2 border-t border-slate-200 pt-2">
              <div className="font-mono text-xs text-slate-600">
                {t('pos.footer.sub')} {cartApi.totals.subtotal.toFixed(2)} · {t('pos.footer.disc')}{' '}
                {cartApi.totals.discountValue.toFixed(2)}
              </div>
              <div className="font-mono text-lg font-semibold text-slate-900">{cartApi.totals.grandTotal.toFixed(2)}</div>
              <button
                type="button"
                disabled={cartApi.cart.lines.length === 0}
                onClick={startCheckout}
                className="rounded border border-sky-600 bg-sky-600 px-4 py-2 text-xs font-bold uppercase tracking-wide text-white hover:bg-sky-700 disabled:opacity-40"
              >
                {t('pos.checkout')}
              </button>
            </div>
          </footer>
        </div>
      </div>

      <POSCheckoutModal
        open={checkoutOpen}
        busy={checkoutBusy}
        grandTotal={cartApi.totals.grandTotal}
        onClose={closeModal}
        onConfirm={(m) => void confirmCheckout(m)}
        lastInvoice={lastInvoice}
      />
    </div>
  )
}
