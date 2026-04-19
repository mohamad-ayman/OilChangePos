export const stockRequestKeys = {
  all: ['stock-requests'] as const,
  list: (branchWarehouseId?: number) => [...stockRequestKeys.all, 'list', branchWarehouseId ?? 'all'] as const,
}
