import { useQuery } from '@tanstack/react-query'
import { useParams } from 'react-router-dom'
import { getTransferById } from '@/shared/api/inventory.api'
import { transferKeys } from '@/features/transfers/services/transferQueryKeys'

export function useTransferDetail(transferId?: string) {
  const params = useParams<{ transferId: string }>()
  const id = transferId ?? params.transferId ?? ''

  const query = useQuery({
    queryKey: transferKeys.detail(id),
    queryFn: () => getTransferById(id),
    enabled: Boolean(id),
    staleTime: 10_000,
  })

  return {
    transferId: id,
    detail: query.data ?? null,
    isLoading: query.isPending,
    isError: query.isError,
    refetch: query.refetch,
  }
}
