import { useQuery } from '@tanstack/react-query'
import { useMemo } from 'react'
import { stockItemsToLedger } from '@/features/inventory/domain/inventory.engine'
import { inventoryKeys } from '@/features/inventory/services/inventoryQueryKeys'
import { posKeys } from '@/features/pos/services/posQueryKeys'
import { getProductsForPOS } from '@/shared/api/pos.api'
import { getWarehouses } from '@/shared/api/inventory.api'
import { useAuthStore } from '@/shared/store/auth.store'

/** One query pair for POS — avoids duplicate fetches while cashier works. */
export function usePOSCatalog(warehouseId: number | null) {
  const user = useAuthStore((s) => s.user)
  const wid = warehouseId ?? user?.homeBranchWarehouseId ?? 1

  const warehousesQuery = useQuery({
    queryKey: inventoryKeys.warehouses(),
    queryFn: getWarehouses,
    staleTime: 300_000,
  })

  const catalogQuery = useQuery({
    queryKey: posKeys.catalog(wid),
    queryFn: () => getProductsForPOS(wid),
    enabled: wid > 0,
    staleTime: 30_000,
  })

  const ledger = useMemo(() => {
    const rows = catalogQuery.data ?? []
    const items = rows.map((r) => ({
      productId: r.id,
      warehouseId: wid,
      quantityOnHand: r.quantityOnHand,
    }))
    return stockItemsToLedger(items)
  }, [catalogQuery.data, wid])

  const categories = useMemo(() => {
    const s = new Set<string>()
    for (const p of catalogQuery.data ?? []) {
      if (p.quantityOnHand > 0) s.add(p.productCategory)
    }
    return ['All', ...Array.from(s).sort((a, b) => a.localeCompare(b))]
  }, [catalogQuery.data])

  return {
    warehouseId: wid,
    warehouses: warehousesQuery.data ?? [],
    products: catalogQuery.data ?? [],
    ledger,
    categories,
    isLoading: catalogQuery.isPending || warehousesQuery.isPending,
    refetch: catalogQuery.refetch,
  }
}
