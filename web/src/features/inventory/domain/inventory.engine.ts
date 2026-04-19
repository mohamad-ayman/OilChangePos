import type { StockItem } from '@/entities/stock-item'
import type { TransferDocument } from '@/entities/transfer-document'

/** Immutable logical ledger: quantity per product+warehouse. */
export type StockLedger = ReadonlyMap<string, number>

export function ledgerKey(productId: number, warehouseId: number): string {
  return `${productId}:${warehouseId}`
}

export function stockItemsToLedger(items: readonly StockItem[]): StockLedger {
  const m = new Map<string, number>()
  for (const it of items) {
    m.set(ledgerKey(it.productId, it.warehouseId), it.quantityOnHand)
  }
  return m
}

export function getAvailableStock(
  ledger: StockLedger,
  productId: number,
  warehouseId: number,
): number {
  return ledger.get(ledgerKey(productId, warehouseId)) ?? 0
}

export type EngineStockMovement =
  | { kind: 'SALE'; productId: number; warehouseId: number; quantity: number }
  | { kind: 'PURCHASE'; productId: number; warehouseId: number; quantity: number }
  | { kind: 'TRANSFER_OUT'; productId: number; warehouseId: number; quantity: number }
  | { kind: 'TRANSFER_IN'; productId: number; warehouseId: number; quantity: number }
  | { kind: 'ADJUSTMENT'; productId: number; warehouseId: number; targetOnHand: number }

export type StockValidationContext = {
  ledger: StockLedger
  movement: EngineStockMovement
}

export type StockValidationFailure = {
  ok: false
  code: 'INSUFFICIENT_STOCK' | 'INVALID_QUANTITY' | 'NEGATIVE_RESULT'
  message: string
}

export type StockValidationResult = { ok: true } | StockValidationFailure

export function validateStockBeforeOperation(ctx: StockValidationContext): StockValidationResult {
  const { ledger, movement } = ctx

  if ('quantity' in movement && movement.quantity <= 0) {
    return { ok: false, code: 'INVALID_QUANTITY', message: 'Quantity must be greater than zero.' }
  }

  switch (movement.kind) {
    case 'SALE':
    case 'TRANSFER_OUT': {
      const available = getAvailableStock(ledger, movement.productId, movement.warehouseId)
      if (movement.quantity > available) {
        return {
          ok: false,
          code: 'INSUFFICIENT_STOCK',
          message: `Cannot ${movement.kind === 'SALE' ? 'sell' : 'transfer'} more than available (${available}).`,
        }
      }
      return { ok: true }
    }
    case 'PURCHASE':
    case 'TRANSFER_IN':
      return { ok: true }
    case 'ADJUSTMENT': {
      if (movement.targetOnHand < 0) {
        return { ok: false, code: 'NEGATIVE_RESULT', message: 'On-hand quantity cannot be negative.' }
      }
      return { ok: true }
    }
    default: {
      const _exhaustive: never = movement
      throw new Error(`Unhandled movement: ${String(_exhaustive)}`)
    }
  }
}

function cloneLedger(ledger: StockLedger): Map<string, number> {
  return new Map(ledger)
}

function writeQty(m: Map<string, number>, productId: number, warehouseId: number, qty: number): void {
  m.set(ledgerKey(productId, warehouseId), qty)
}

function readQty(m: Map<string, number>, productId: number, warehouseId: number): number {
  return m.get(ledgerKey(productId, warehouseId)) ?? 0
}

/**
 * Applies a single logical movement to a ledger copy (pure).
 * Call `validateStockBeforeOperation` first for SALE / TRANSFER_OUT / ADJUSTMENT safety.
 */
export type ApplyStockMovementResult = { ok: true; ledger: StockLedger } | StockValidationFailure

export function applyStockMovement(ledger: StockLedger, movement: EngineStockMovement): ApplyStockMovementResult {
  const pre = validateStockBeforeOperation({ ledger, movement })
  if (pre.ok === false) return pre

  const next = cloneLedger(ledger)

  switch (movement.kind) {
    case 'SALE':
    case 'TRANSFER_OUT': {
      const cur = readQty(next, movement.productId, movement.warehouseId)
      writeQty(next, movement.productId, movement.warehouseId, cur - movement.quantity)
      break
    }
    case 'PURCHASE':
    case 'TRANSFER_IN': {
      const cur = readQty(next, movement.productId, movement.warehouseId)
      writeQty(next, movement.productId, movement.warehouseId, cur + movement.quantity)
      break
    }
    case 'ADJUSTMENT':
      writeQty(next, movement.productId, movement.warehouseId, movement.targetOnHand)
      break
    default: {
      const _exhaustive: never = movement
      throw new Error(`Unhandled movement: ${String(_exhaustive)}`)
    }
  }

  for (const [, v] of next) {
    if (v < 0) {
      return {
        ok: false,
        code: 'NEGATIVE_RESULT',
        message: 'Operation would result in negative stock.',
      }
    }
  }

  return { ok: true, ledger: next }
}

export type TransferValidationResult =
  | { ok: true }
  | {
      ok: false
      code:
        | 'EMPTY_LINES'
        | 'SAME_WAREHOUSE'
        | 'INSUFFICIENT_STOCK'
        | 'INVALID_QUANTITY'
        | 'NEGATIVE_RESULT'
      message: string
    }

/** Validates a multi-line transfer against a ledger (main→branch or branch↔branch). */
export function validateTransferDocument(ledger: StockLedger, doc: TransferDocument): TransferValidationResult {
  if (!doc.lines.length) {
    return { ok: false, code: 'EMPTY_LINES', message: 'Transfer must include at least one line.' }
  }
  if (doc.fromWarehouseId === doc.toWarehouseId) {
    return { ok: false, code: 'SAME_WAREHOUSE', message: 'Source and destination warehouses must differ.' }
  }

  const demand = new Map<number, number>()
  for (const line of doc.lines) {
    if (line.quantity <= 0) {
      return { ok: false, code: 'INVALID_QUANTITY', message: 'Each line quantity must be positive.' }
    }
    demand.set(line.productId, (demand.get(line.productId) ?? 0) + line.quantity)
  }

  for (const [productId, qty] of demand) {
    const available = getAvailableStock(ledger, productId, doc.fromWarehouseId)
    if (qty > available) {
      return {
        ok: false,
        code: 'INSUFFICIENT_STOCK',
        message: `Insufficient stock for product ${productId} at source (need ${qty}, have ${available}).`,
      }
    }
  }

  return { ok: true }
}

/** Applies full transfer document to ledger (OUT from source, IN to destination) atomically. */
export function applyTransferDocument(
  ledger: StockLedger,
  doc: TransferDocument,
): { ok: true; ledger: StockLedger } | TransferValidationResult {
  const v = validateTransferDocument(ledger, doc)
  if (!v.ok) return v

  let current: StockLedger = ledger
  for (const line of doc.lines) {
    const out = applyStockMovement(current, {
      kind: 'TRANSFER_OUT',
      productId: line.productId,
      warehouseId: doc.fromWarehouseId,
      quantity: line.quantity,
    })
    if (!out.ok) {
      return { ok: false, code: out.code, message: out.message }
    }
    current = out.ledger

    const inn = applyStockMovement(current, {
      kind: 'TRANSFER_IN',
      productId: line.productId,
      warehouseId: doc.toWarehouseId,
      quantity: line.quantity,
    })
    if (!inn.ok) {
      return { ok: false, code: inn.code, message: inn.message }
    }
    current = inn.ledger
  }

  return { ok: true, ledger: current }
}

export function buildTransferDocument(input: {
  fromWarehouseId: number
  toWarehouseId: number
  userId: number
  notes: string
  lines: { productId: number; quantity: number }[]
  branchSalePriceForDestination?: number | null
}): TransferDocument {
  return {
    clientRequestId: typeof crypto !== 'undefined' && 'randomUUID' in crypto ? crypto.randomUUID() : `tr-${Date.now()}`,
    fromWarehouseId: input.fromWarehouseId,
    toWarehouseId: input.toWarehouseId,
    userId: input.userId,
    notes: input.notes,
    lines: input.lines.map((l) => ({ productId: l.productId, quantity: l.quantity })),
    branchSalePriceForDestination: input.branchSalePriceForDestination ?? null,
    createdAtUtc: new Date().toISOString(),
  }
}
