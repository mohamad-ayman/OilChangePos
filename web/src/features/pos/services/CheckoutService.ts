import type { StockLedger } from '@/features/inventory/domain/inventory.engine'
import { validateCartStock } from '@/features/pos/engine/posEngine'
import type { POSCartState } from '@/features/pos/engine/posEngine'
import { calculateTotal } from '@/features/pos/engine/posEngine'
import { submitInvoice, type InvoiceDto } from '@/shared/api/pos.api'

export type CheckoutPaymentMethod = 'cash' | 'card'

export type CheckoutInput = {
  cart: POSCartState
  ledger: StockLedger
  warehouseId: number
  userId: number
  paymentMethod: CheckoutPaymentMethod
}

export type CheckoutResult =
  | { ok: true; invoice: InvoiceDto }
  | { ok: false; reason: 'EMPTY_CART' | 'STOCK' | 'API'; message: string }

function cartToStockLines(cart: POSCartState) {
  return cart.lines.map((l) => ({ productId: l.productId, quantity: l.quantity, name: l.name }))
}

/**
 * Offline-safe design: validate with engine first, then post invoice (idempotent receipt id from server in live mode).
 */
export async function runCheckout(input: CheckoutInput): Promise<CheckoutResult> {
  if (!input.cart.lines.length) {
    return { ok: false, reason: 'EMPTY_CART', message: 'Cart is empty.' }
  }

  const stock = validateCartStock(input.ledger, input.warehouseId, cartToStockLines(input.cart))
  if (!stock.ok) {
    return { ok: false, reason: 'STOCK', message: `${stock.name}: ${stock.message}` }
  }

  const totals = calculateTotal(input.cart)
  if (totals.grandTotal <= 0) {
    return { ok: false, reason: 'API', message: 'Grand total must be positive.' }
  }

  try {
    const { discountValue, grandTotal } = totals
    const invoice = await submitInvoice({
      warehouseId: input.warehouseId,
      userId: input.userId,
      paymentMethod: input.paymentMethod,
      lines: input.cart.lines.map((l) => ({
        productId: l.productId,
        quantity: l.quantity,
        unitPrice: l.unitPrice,
        name: l.name,
      })),
      discountAmount: discountValue,
      grandTotal,
      notes: 'POS checkout',
    })
    return { ok: true, invoice }
  } catch {
    return { ok: false, reason: 'API', message: 'Could not submit invoice.' }
  }
}
