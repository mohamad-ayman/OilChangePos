import type { Product } from '@/entities/product'
import type { StockLedger } from '@/features/inventory/domain/inventory.engine'
import { applyStockMovement, validateStockBeforeOperation } from '@/features/inventory/domain/inventory.engine'

export type CartDiscount =
  | { kind: 'none' }
  | { kind: 'percent'; value: number }
  | { kind: 'fixed'; value: number }

export type CartLine = {
  uid: string
  productId: number
  name: string
  category: string
  unitPrice: number
  quantity: number
}

export type POSCartState = {
  lines: CartLine[]
  discount: CartDiscount
}

export function emptyCart(): POSCartState {
  return { lines: [], discount: { kind: 'none' } }
}

/** Same resolution as WinForms POS quantity (NumericUpDown 3 decimal places). */
export function normalizePosQty(qty: number): number {
  const n = Number(qty)
  if (!Number.isFinite(n) || n <= 0) return 0.001
  return Math.round(Math.min(n, 1_000_000) * 1000) / 1000
}

function newUid(): string {
  return typeof crypto !== 'undefined' && 'randomUUID' in crypto ? crypto.randomUUID() : `l-${Date.now()}-${Math.random()}`
}

export function addItem(state: POSCartState, product: Product, qty = 1): POSCartState {
  const q = normalizePosQty(qty)
  const idx = state.lines.findIndex((l) => l.productId === product.id)
  if (idx >= 0) {
    const lines = state.lines.slice()
    const merged = normalizePosQty(lines[idx].quantity + q)
    lines[idx] = { ...lines[idx], quantity: merged }
    return { ...state, lines }
  }
  return {
    ...state,
    lines: [
      ...state.lines,
      {
        uid: newUid(),
        productId: product.id,
        name: product.name,
        category: product.productCategory,
        unitPrice: product.unitPrice,
        quantity: q,
      },
    ],
  }
}

export function removeItem(state: POSCartState, uid: string): POSCartState {
  return { ...state, lines: state.lines.filter((l) => l.uid !== uid) }
}

export function updateQuantity(state: POSCartState, uid: string, quantity: number): POSCartState {
  if (!Number.isFinite(quantity) || quantity <= 0) return removeItem(state, uid)
  const q = normalizePosQty(quantity)
  return {
    ...state,
    lines: state.lines.map((l) => (l.uid === uid ? { ...l, quantity: q } : l)),
  }
}

export function lineSubtotal(line: CartLine): number {
  return Math.round(line.unitPrice * line.quantity * 100) / 100
}

export function sumSubtotal(lines: readonly CartLine[]): number {
  let s = 0
  for (const l of lines) s += l.unitPrice * l.quantity
  return Math.round(s * 100) / 100
}

export function calculateTotal(state: POSCartState): {
  subtotal: number
  discountValue: number
  grandTotal: number
} {
  const subtotal = sumSubtotal(state.lines)
  let discountValue = 0
  let after = subtotal
  if (state.discount.kind === 'percent') {
    const p = Math.min(100, Math.max(0, state.discount.value))
    discountValue = Math.round(subtotal * (p / 100) * 100) / 100
    after = Math.round((subtotal - discountValue) * 100) / 100
  } else if (state.discount.kind === 'fixed') {
    discountValue = Math.min(subtotal, Math.max(0, state.discount.value))
    after = Math.round((subtotal - discountValue) * 100) / 100
  }
  return { subtotal, discountValue, grandTotal: Math.max(0, after) }
}

export function applyDiscountPercent(state: POSCartState, percent: number): POSCartState {
  return { ...state, discount: { kind: 'percent', value: percent } }
}

export function applyDiscountFixed(state: POSCartState, amount: number): POSCartState {
  return { ...state, discount: { kind: 'fixed', value: amount } }
}

export function clearDiscount(state: POSCartState): POSCartState {
  return { ...state, discount: { kind: 'none' } }
}

export type StockCheckLine = { productId: number; quantity: number; name: string }

export type ValidateCartStockResult =
  | { ok: true }
  | { ok: false; productId: number; name: string; message: string }

/**
 * Validates the whole cart against a ledger using sequential SALE movements
 * (same product on multiple lines is applied in order).
 */
export function validateCartStock(
  ledger: StockLedger,
  warehouseId: number,
  lines: readonly StockCheckLine[],
): ValidateCartStockResult {
  let current = ledger
  for (const line of lines) {
    const movement = { kind: 'SALE' as const, productId: line.productId, warehouseId, quantity: line.quantity }
    const pre = validateStockBeforeOperation({ ledger: current, movement })
    if (pre.ok === false) {
      return { ok: false, productId: line.productId, name: line.name, message: pre.message }
    }
    const next = applyStockMovement(current, movement)
    if (!next.ok) {
      return { ok: false, productId: line.productId, name: line.name, message: next.message }
    }
    current = next.ledger
  }
  return { ok: true }
}

/** Applies all cart lines as SALE movements to a ledger copy (pure). */
export function applySaleCartToLedger(
  ledger: StockLedger,
  warehouseId: number,
  lines: readonly StockCheckLine[],
): { ok: true; ledger: StockLedger } | ValidateCartStockResult {
  const v = validateCartStock(ledger, warehouseId, lines)
  if (!v.ok) return v
  let current = ledger
  for (const line of lines) {
    const out = applyStockMovement(current, {
      kind: 'SALE',
      productId: line.productId,
      warehouseId,
      quantity: line.quantity,
    })
    if (!out.ok) return { ok: false, productId: line.productId, name: line.name, message: out.message }
    current = out.ledger
  }
  return { ok: true, ledger: current }
}
