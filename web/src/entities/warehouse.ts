/** Mirrors `OilChangePOS.Domain.WarehouseType` (JSON numeric from API). */
export const WarehouseType = {
  Main: 1,
  Branch: 2,
} as const

export type WarehouseTypeCode = (typeof WarehouseType)[keyof typeof WarehouseType]

export type Warehouse = {
  id: number
  name: string
  type: WarehouseTypeCode
  isActive: boolean
}

export function isMainWarehouse(w: Pick<Warehouse, 'type'>): boolean {
  return w.type === WarehouseType.Main
}

export function isBranchWarehouse(w: Pick<Warehouse, 'type'>): boolean {
  return w.type === WarehouseType.Branch
}
