/**
 * Logical on-hand bucket for one SKU at one warehouse (derived from movements server-side).
 * Used by the client engine for validation against a local ledger snapshot.
 */
export type StockItem = {
  productId: number
  warehouseId: number
  quantityOnHand: number
}
