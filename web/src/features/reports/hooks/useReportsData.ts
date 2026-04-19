import { useQueries } from '@tanstack/react-query'
import { useMemo } from 'react'
import { reportKeys } from '@/features/reports/services/reportQueryKeys'
import {
  getInventoryStats,
  getSalesSummary,
  getTopProducts,
  getTransferStats,
} from '@/shared/api/reports.api'

const stale = 60_000

/**
 * Parallel cached fetches for dashboard + sub-reports — aggregations stay in API DTOs;
 * this hook only derives KPI view-model once per snapshot.
 */
export function useReportsData() {
  const results = useQueries({
    queries: [
      { queryKey: reportKeys.salesSummary(), queryFn: getSalesSummary, staleTime: stale },
      { queryKey: reportKeys.inventoryStats(), queryFn: getInventoryStats, staleTime: stale },
      { queryKey: reportKeys.transferStats(), queryFn: getTransferStats, staleTime: stale },
      { queryKey: reportKeys.topProducts(), queryFn: getTopProducts, staleTime: stale },
    ],
  })

  const [salesQ, invQ, xferQ, topQ] = results

  const kpis = useMemo(() => {
    const sales = salesQ.data
    const inv = invQ.data
    return {
      totalSales: sales?.totalSales ?? null,
      totalProfit: sales?.totalProfit ?? null,
      transactionCount: sales?.transactionCount ?? null,
      stockValue: inv?.stockValueTotal ?? null,
    }
  }, [salesQ.data, invQ.data])

  const loading = results.some((q) => q.isPending)
  const error = results.find((q) => q.isError)?.error

  return {
    sales: salesQ,
    inventory: invQ,
    transfers: xferQ,
    topProducts: topQ,
    kpis,
    loading,
    error,
  }
}
