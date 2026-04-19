/**
 * Domain movement kinds used by the client engine.
 * Map to `StockMovementType` on the server: Purchase=1, Sale=2, Transfer=3, Adjust=4.
 */
export const DomainStockMovementType = {
  Purchase: 1,
  Sale: 2,
  Transfer: 3,
  Adjust: 4,
} as const

export type DomainStockMovementTypeCode =
  (typeof DomainStockMovementType)[keyof typeof DomainStockMovementType]

/** Engine-facing classification (SAP-style) — maps onto persisted `StockMovementType`. */
export type EngineMovementKind = 'SALE' | 'PURCHASE' | 'TRANSFER_OUT' | 'TRANSFER_IN' | 'ADJUSTMENT'

export type StockMovement = {
  id?: number
  productId: number
  movementType: DomainStockMovementTypeCode
  quantity: number
  movementDateUtc: string
  referenceId?: number | null
  fromWarehouseId?: number | null
  toWarehouseId?: number | null
  notes: string
}

export function engineKindToDomainType(kind: EngineMovementKind): DomainStockMovementTypeCode {
  switch (kind) {
    case 'PURCHASE':
      return DomainStockMovementType.Purchase
    case 'SALE':
      return DomainStockMovementType.Sale
    case 'TRANSFER_OUT':
    case 'TRANSFER_IN':
      return DomainStockMovementType.Transfer
    case 'ADJUSTMENT':
      return DomainStockMovementType.Adjust
    default: {
      const _exhaustive: never = kind
      return _exhaustive
    }
  }
}
