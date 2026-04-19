export const branchReportKeys = {
  root: ['branch-reports'] as const,
  salesLines: (warehouseId: number, from: string, to: string) =>
    [...branchReportKeys.root, 'sales-lines', warehouseId, from, to] as const,
  incoming: (warehouseId: number, from: string, to: string) =>
    [...branchReportKeys.root, 'incoming', warehouseId, from, to] as const,
  transfers: (warehouseId: number, from: string, to: string) =>
    [...branchReportKeys.root, 'transfers', warehouseId, from, to] as const,
  expenses: (warehouseId: number, from: string, to: string) =>
    [...branchReportKeys.root, 'expenses', warehouseId, from, to] as const,
  sellers: (warehouseId: number, from: string, to: string) =>
    [...branchReportKeys.root, 'sellers', warehouseId, from, to] as const,
  lowStock: (warehouseId: number) => [...branchReportKeys.root, 'low-stock', warehouseId] as const,
  profitRollup: (warehouseId: number, from: string, to: string) =>
    [...branchReportKeys.root, 'profit-rollup', warehouseId, from, to] as const,
}
