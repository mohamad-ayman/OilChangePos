export const posKeys = {
  root: ['pos'] as const,
  catalog: (warehouseId: number) => [...posKeys.root, 'catalog', warehouseId] as const,
}
