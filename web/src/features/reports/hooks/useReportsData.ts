import { useQueries } from '@tanstack/react-query'
import { useMemo } from 'react'
import { reportKeys } from '@/features/reports/services/reportQueryKeys'
import {
  getInventoryStats,
  getSalesSummary,
  getTopProducts,
  getTransferStats,
  type ReportsAccess,
} from '@/shared/api/reports.api'
import { useAuthStore } from '@/shared/store/auth.store'

const stale = 60_000

/**
 * Parallel cached fetches for dashboard + sub-reports — aggregations stay in API DTOs;
 * this hook only derives KPI view-model once per snapshot.
 */
export function useReportsData() {
  const user = useAuthStore((s) => s.user)

  const access: ReportsAccess = useMemo(
    () => ({
      isAdmin: user?.role === 'admin',
      branchWarehouseId: user?.role !== 'admin' ? user?.homeBranchWarehouseId ?? null : null,
    }),
    [user],
  )

  const canFetchBranchScoped = access.isAdmin || access.branchWarehouseId != null

  const results = useQueries({
    queries: [
      {
        queryKey: [...reportKeys.salesSummary(), access.isAdmin, access.branchWarehouseId],
        queryFn: () => getSalesSummary(access),
        staleTime: stale,
        enabled: canFetchBranchScoped,
      },
      {
        queryKey: [...reportKeys.inventoryStats(), access.isAdmin, access.branchWarehouseId],
        queryFn: () => getInventoryStats(access),
        staleTime: stale,
        enabled: canFetchBranchScoped,
      },
      {
        queryKey: [...reportKeys.transferStats(), access.isAdmin],
        queryFn: () => getTransferStats(access),
        staleTime: stale,
        enabled: access.isAdmin && canFetchBranchScoped,
      },
      {
        queryKey: [...reportKeys.topProducts(), access.isAdmin, access.branchWarehouseId],
        queryFn: () => getTopProducts(access),
        staleTime: stale,
        enabled: canFetchBranchScoped,
      },
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
    showTransferAnalytics: access.isAdmin,
  }
}
