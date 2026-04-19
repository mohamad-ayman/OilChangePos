import type { TransferListFilters } from '@/shared/api/inventory.api'

export const transferKeys = {
  root: ['transfers', 'workflow'] as const,
  list: (filters: TransferListFilters) => [...transferKeys.root, 'list', filters] as const,
  detail: (id: string) => [...transferKeys.root, 'detail', id] as const,
}
