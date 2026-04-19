export const reportKeys = {
  root: ['reports'] as const,
  salesSummary: () => [...reportKeys.root, 'sales-summary'] as const,
  inventoryStats: () => [...reportKeys.root, 'inventory-stats'] as const,
  transferStats: () => [...reportKeys.root, 'transfer-stats'] as const,
  topProducts: () => [...reportKeys.root, 'top-products'] as const,
}
