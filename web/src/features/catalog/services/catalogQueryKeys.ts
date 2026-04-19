export const catalogKeys = {
  root: ['catalog-admin'] as const,
  companies: () => [...catalogKeys.root, 'companies'] as const,
  products: (companyId: number) => [...catalogKeys.root, 'products', companyId] as const,
}
