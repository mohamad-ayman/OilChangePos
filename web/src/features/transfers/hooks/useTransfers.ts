import { useQuery } from '@tanstack/react-query'
import { useMemo, useState } from 'react'
import type { TransferListFilters, TransferWorkflowStatus } from '@/shared/api/inventory.api'
import { getTransfers } from '@/shared/api/inventory.api'
import { transferKeys } from '@/features/transfers/services/transferQueryKeys'

export function useTransfers() {
  const [status, setStatus] = useState<TransferWorkflowStatus | 'all'>('all')
  const [warehouseId, setWarehouseId] = useState<number | 'all'>('all')
  const [dateFromUtc, setDateFromUtc] = useState('')
  const [dateToUtc, setDateToUtc] = useState('')

  const filters: TransferListFilters = useMemo(
    () => ({
      status,
      warehouseId,
      dateFromUtc: dateFromUtc.trim() || undefined,
      dateToUtc: dateToUtc.trim() || undefined,
    }),
    [status, warehouseId, dateFromUtc, dateToUtc],
  )

  const query = useQuery({
    queryKey: transferKeys.list(filters),
    queryFn: () => getTransfers(filters),
    staleTime: 15_000,
  })

  function resetFilters() {
    setStatus('all')
    setWarehouseId('all')
    setDateFromUtc('')
    setDateToUtc('')
  }

  return {
    rows: query.data ?? [],
    isLoading: query.isPending,
    isError: query.isError,
    refetch: query.refetch,
    status,
    setStatus,
    warehouseId,
    setWarehouseId,
    dateFromUtc,
    setDateFromUtc,
    dateToUtc,
    setDateToUtc,
    resetFilters,
  }
}
