import { http } from '@/shared/api/client'
import type { Product } from '@/entities/product'
import { getBranchSalePriceOverrides, getInventorySnapshot, getProducts } from '@/shared/api/inventory.api'

const usePosMock = import.meta.env.VITE_POS_MOCK === 'true' || import.meta.env.VITE_INVENTORY_MOCK === 'true'

export type POSProductRow = Product & {
  /** Synthetic scan key for cashier search (EAN-style mock). */
  barcode: string
  quantityOnHand: number
}

export type InvoiceLineDto = {
  productId: number
  name: string
  quantity: number
  unitPrice: number
  lineTotal: number
}

export type InvoiceDto = {
  id: string
  receiptNo: string
  createdAtUtc: string
  warehouseId: number
  userId: number
  lines: InvoiceLineDto[]
  subtotal: number
  discountTotal: number
  grandTotal: number
  paymentMethod: 'cash' | 'card'
}

/** Matches OilChangePOS.API `CompleteSaleRequest` (camelCase JSON). */
export type CompleteSaleRequestDto = {
  customerId: number | null
  discountAmount: number
  userId: number
  warehouseId: number
  items: { productId: number; quantity: number }[]
}

export type SubmitInvoicePayload = {
  warehouseId: number
  userId: number
  paymentMethod: 'cash' | 'card'
  lines: { productId: number; quantity: number; unitPrice: number; name: string }[]
  /** Subtracted from line subtotal after server-side pricing (API contract). */
  discountAmount: number
  /** Client-computed total for receipt UI (server uses catalog prices on lines). */
  grandTotal: number
  notes?: string
}

export type CreateSaleLinePayload = {
  warehouseId: number
  userId: number
  productId: number
  quantity: number
  unitPrice: number
  paymentMethod: 'cash' | 'card'
}

function padBarcode(id: number): string {
  return `200${String(id).padStart(10, '0')}`
}

async function mockDelay<T>(v: T, ms = 40): Promise<T> {
  await new Promise((r) => setTimeout(r, ms))
  return v
}

/** Catalog + on-hand for active branch / warehouse (delegates to inventory APIs). */
export async function getProductsForPOS(warehouseId: number): Promise<POSProductRow[]> {
  const [products, snap] = await Promise.all([getProducts(), getInventorySnapshot(warehouseId)])
  const onHand = new Map(snap.map((r) => [r.productId, r.currentStock]))
  const ids = products.map((p) => p.id)
  const overrides = await getBranchSalePriceOverrides(warehouseId, ids)
  return products.map((p) => ({
    ...p,
    unitPrice: overrides.has(p.id) ? overrides.get(p.id)! : p.unitPrice,
    barcode: padBarcode(p.id),
    quantityOnHand: onHand.get(p.id) ?? 0,
  }))
}

/** Single-line sale — `POST /api/Sales` (`CompleteSaleRequest`). */
export async function createSale(payload: CreateSaleLinePayload): Promise<number> {
  if (usePosMock) {
    return mockDelay(70000 + payload.productId)
  }
  const { data } = await http.post<{ invoiceId: number }>('/api/Sales', {
    customerId: null,
    discountAmount: 0,
    userId: payload.userId,
    warehouseId: payload.warehouseId,
    items: [{ productId: payload.productId, quantity: payload.quantity }],
  } satisfies CompleteSaleRequestDto)
  return data.invoiceId
}

function roundMoney(n: number): number {
  return Math.round(n * 100) / 100
}

function receiptFromCompleteSale(invoiceId: number, payload: SubmitInvoicePayload): InvoiceDto {
  const now = new Date().toISOString()
  const subtotal = roundMoney(payload.lines.reduce((s, l) => s + l.quantity * l.unitPrice, 0))
  return {
    id: String(invoiceId),
    receiptNo: `INV-${invoiceId}`,
    createdAtUtc: now,
    warehouseId: payload.warehouseId,
    userId: payload.userId,
    paymentMethod: payload.paymentMethod,
    subtotal,
    discountTotal: roundMoney(Math.min(subtotal, Math.max(0, payload.discountAmount))),
    grandTotal: roundMoney(Math.max(0, payload.grandTotal)),
    lines: payload.lines.map((l) => ({
      productId: l.productId,
      name: l.name,
      quantity: l.quantity,
      unitPrice: l.unitPrice,
      lineTotal: roundMoney(l.quantity * l.unitPrice),
    })),
  }
}

/**
 * Full cashier checkout — `POST /api/Sales` with `CompleteSaleRequest`.
 * Server prices lines from catalog + branch overrides; client receipt uses cart line prices for display.
 */
export async function submitInvoice(payload: SubmitInvoicePayload): Promise<InvoiceDto> {
  if (usePosMock) {
    const now = new Date().toISOString()
    const subtotal = payload.lines.reduce((s, l) => s + l.quantity * l.unitPrice, 0)
    const grand = Math.max(0, roundMoney(payload.grandTotal))
    const discTotal = roundMoney(subtotal - grand)
    const inv: InvoiceDto = {
      id: `inv-${Date.now()}`,
      receiptNo: `R-${String(Math.floor(Math.random() * 1e6)).padStart(6, '0')}`,
      createdAtUtc: now,
      warehouseId: payload.warehouseId,
      userId: payload.userId,
      paymentMethod: payload.paymentMethod,
      subtotal,
      discountTotal: discTotal,
      grandTotal: grand,
      lines: payload.lines.map((l) => ({
        productId: l.productId,
        name: l.name,
        quantity: l.quantity,
        unitPrice: l.unitPrice,
        lineTotal: roundMoney(l.quantity * l.unitPrice),
      })),
    }
    return mockDelay(inv, 70)
  }
  const body: CompleteSaleRequestDto = {
    customerId: null,
    discountAmount: roundMoney(Math.max(0, payload.discountAmount)),
    userId: payload.userId,
    warehouseId: payload.warehouseId,
    items: payload.lines.map((l) => ({
      productId: l.productId,
      quantity: l.quantity,
    })),
  }
  const { data } = await http.post<{ invoiceId: number }>('/api/Sales', body)
  return receiptFromCompleteSale(data.invoiceId, payload)
}
