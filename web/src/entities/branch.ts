import type { Warehouse } from '@/entities/warehouse'
import { isBranchWarehouse } from '@/entities/warehouse'

/** Active branch site (same physical row as `Warehouse` with type Branch). */
export type Branch = Warehouse & { type: 2 }

export function asBranch(w: Warehouse): Branch | null {
  return isBranchWarehouse(w) ? (w as Branch) : null
}
