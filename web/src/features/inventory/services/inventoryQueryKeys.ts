import type { StockHistoryQuery } from '@/shared/api/inventory.api'

export const inventoryKeys = {
  root: ['inventory'] as const,
  products: () => [...inventoryKeys.root, 'products'] as const,
  warehouses: () => [...inventoryKeys.root, 'warehouses'] as const,
  snapshot: (warehouseId: number) => [...inventoryKeys.root, 'snapshot', warehouseId] as const,
  lowStock: (warehouseId: number) => [...inventoryKeys.root, 'lowStock', warehouseId] as const,
  history: (q: StockHistoryQuery) => [...inventoryKeys.root, 'history', q] as const,
}
