import { useQueries, useQuery } from '@tanstack/react-query'
import { useDeferredValue, useEffect, useMemo, useState } from 'react'
import type { Product } from '@/entities/product'
import { inventoryKeys } from '@/features/inventory/services/inventoryQueryKeys'
import {
  getInventorySnapshot,
  getLowStockItems,
  getProducts,
  getStockHistory,
  getWarehouses,
  type InventorySnapshotView,
} from '@/shared/api/inventory.api'
import { catalogDisplayName } from '@/shared/utils/catalogLine'
import { useAuthStore } from '@/shared/store/auth.store'

export type WarehouseScope = 'all' | number

export type InventorySortColumn = 'name' | 'sku' | 'category' | 'cost' | 'sale' | 'total' | 'status'

export type InventorySortDir = 'asc' | 'desc'

export type StockStatus = 'ok' | 'low' | 'out'

export type InventoryGridRow = {
  productId: number
  name: string
  sku: string
  category: string
  packageSize: string
  companyName: string
  saleUnitPrice: number
  costUnitApprox: number | null
  totalQty: number
  breakdown: string
  status: StockStatus
  byWarehouse: Record<number, { qty: number; unitPrice: number; stockValue: number }>
}

function formatSku(p: Product): string {
  return `${p.id}-${p.packageSize}`
}

function formatLocalYmd(d: Date): string {
  const y = d.getFullYear()
  const m = String(d.getMonth() + 1).padStart(2, '0')
  const day = String(d.getDate()).padStart(2, '0')
  return `${y}-${m}-${day}`
}

function statusForProduct(productId: number, total: number, lowProductIds: ReadonlySet<number>): StockStatus {
  if (total <= 0) return 'out'
  if (lowProductIds.has(productId)) return 'low'
  return 'ok'
}

export function useInventory() {
  const user = useAuthStore((s) => s.user)
  const canPickAllWarehouses = user?.role === 'admin'

  const [warehouseScope, setWarehouseScope] = useState<WarehouseScope>('all')

  useEffect(() => {
    if (!canPickAllWarehouses && user?.homeBranchWarehouseId != null) {
      setWarehouseScope(user.homeBranchWarehouseId)
    }
  }, [canPickAllWarehouses, user?.homeBranchWarehouseId])
  const [search, setSearch] = useState('')
  const deferredSearch = useDeferredValue(search.trim().toLowerCase())
  const [category, setCategory] = useState<string>('all')
  const [sortColumn, setSortColumn] = useState<InventorySortColumn>('name')
  const [sortDir, setSortDir] = useState<InventorySortDir>('asc')
  const [selectedProductId, setSelectedProductId] = useState<number | null>(null)

  const productsQuery = useQuery({
    queryKey: inventoryKeys.products(),
    queryFn: getProducts,
    staleTime: 60_000,
  })

  const warehousesQuery = useQuery({
    queryKey: inventoryKeys.warehouses(),
    queryFn: getWarehouses,
    staleTime: 300_000,
  })

  const warehouses = warehousesQuery.data ?? []
  const warehouseIdsForScope = useMemo(() => {
    if (warehouseScope === 'all') return warehouses.map((w) => w.id)
    return [warehouseScope]
  }, [warehouseScope, warehouses])

  const snapshotQueries = useQueries({
    queries: warehouseIdsForScope.map((wid) => ({
      queryKey: inventoryKeys.snapshot(wid),
      queryFn: () => getInventorySnapshot(wid),
      enabled: warehouseIdsForScope.length > 0,
      staleTime: 30_000,
    })),
  })

  const lowStockQueries = useQueries({
    queries: warehouseIdsForScope.map((wid) => ({
      queryKey: inventoryKeys.lowStock(wid),
      queryFn: () => getLowStockItems(wid),
      enabled: warehouseIdsForScope.length > 0,
      staleTime: 30_000,
    })),
  })

  const snapshotsByWarehouseId = useMemo(() => {
    const map = new Map<number, InventorySnapshotView[]>()
    warehouseIdsForScope.forEach((wid, i) => {
      const q = snapshotQueries[i]
      if (q?.data) map.set(wid, q.data)
    })
    return map
  }, [snapshotQueries, warehouseIdsForScope])

  const lowProductIds = useMemo(() => {
    const s = new Set<number>()
    for (const q of lowStockQueries) {
      for (const row of q.data ?? []) {
        s.add(row.productId)
      }
    }
    return s
  }, [lowStockQueries])

  const baseRows: InventoryGridRow[] = useMemo(() => {
    const products = productsQuery.data ?? []
    if (!products.length) return []

    const whName = (id: number) => warehouses.find((w) => w.id === id)?.name ?? `#${id}`

    return products.map((p) => {
      const byWarehouse: InventoryGridRow['byWarehouse'] = {}
      let totalQty = 0
      let totalValue = 0
      const parts: string[] = []

      for (const wid of warehouseIdsForScope) {
        const snap = snapshotsByWarehouseId.get(wid) ?? []
        const line = snap.find((x) => x.productId === p.id)
        const qty = line?.currentStock ?? 0
        const unitPrice = line?.unitPrice ?? p.unitPrice
        const stockValue = line?.stockValue ?? 0
        byWarehouse[wid] = { qty, unitPrice, stockValue }
        totalQty += qty
        totalValue += stockValue
        if (warehouseScope === 'all' && qty > 0) {
          parts.push(`${whName(wid)}: ${qty}`)
        }
      }

      const breakdown =
        warehouseScope === 'all'
          ? (parts.length ? parts.join(' · ') : '—')
          : (() => {
              const w = warehouseIdsForScope[0]
              if (w == null) return '—'
              return `${whName(w)}: ${byWarehouse[w]?.qty ?? 0}`
            })()

      /**
       * API snapshot `unitPrice` / `stockValue` use effective **retail** unit (catalog + branch override),
       * same as `GetEffectiveSalePriceAsync` — not landed COGS. Weighted sale = Σ value / Σ qty.
       */
      const saleUnitPrice = (() => {
        if (totalQty > 0 && totalValue > 0) {
          return Math.round((totalValue / totalQty) * 10000) / 10000
        }
        for (const wid of warehouseIdsForScope) {
          const line = (snapshotsByWarehouseId.get(wid) ?? []).find((x) => x.productId === p.id)
          if (line != null) return line.unitPrice ?? p.unitPrice
        }
        return p.unitPrice
      })()

      const costUnitApprox = null

      return {
        productId: p.id,
        name: p.name,
        sku: formatSku(p),
        category: p.productCategory,
        packageSize: p.packageSize,
        companyName: p.companyName,
        saleUnitPrice,
        costUnitApprox,
        totalQty,
        breakdown,
        status: statusForProduct(p.id, totalQty, lowProductIds),
        byWarehouse,
      }
    })
  }, [
    productsQuery.data,
    snapshotsByWarehouseId,
    warehouseIdsForScope,
    warehouses,
    warehouseScope,
    lowProductIds,
  ])

  const categories = useMemo(() => {
    const set = new Set<string>()
    for (const p of productsQuery.data ?? []) {
      set.add(p.productCategory)
    }
    return ['all', ...Array.from(set).sort((a, b) => a.localeCompare(b))]
  }, [productsQuery.data])

  const filteredRows = useMemo(() => {
    return baseRows.filter((r) => {
      if (category !== 'all' && r.category !== category) return false
      if (!deferredSearch) return true
      const label = catalogDisplayName({
        companyName: r.companyName,
        name: r.name,
        packageSize: r.packageSize,
      })
      const hay = `${label} ${r.name} ${r.sku} ${r.category} ${r.companyName} ${r.packageSize}`.toLowerCase()
      return hay.includes(deferredSearch)
    })
  }, [baseRows, category, deferredSearch])

  const sortedRows = useMemo(() => {
    const dir = sortDir === 'asc' ? 1 : -1
    const col = sortColumn
    return [...filteredRows].sort((a, b) => {
      const cmp = (() => {
        switch (col) {
          case 'name':
            return catalogDisplayName({
              companyName: a.companyName,
              name: a.name,
              packageSize: a.packageSize,
            }).localeCompare(
              catalogDisplayName({
                companyName: b.companyName,
                name: b.name,
                packageSize: b.packageSize,
              }),
            )
          case 'sku':
            return a.sku.localeCompare(b.sku)
          case 'category':
            return a.category.localeCompare(b.category)
          case 'cost':
            return (a.costUnitApprox ?? -1) - (b.costUnitApprox ?? -1)
          case 'sale':
            return a.saleUnitPrice - b.saleUnitPrice
          case 'total':
            return a.totalQty - b.totalQty
          case 'status':
            return a.status.localeCompare(b.status)
          default:
            return 0
        }
      })()
      return cmp * dir
    })
  }, [filteredRows, sortColumn, sortDir])

  const selectedRow = useMemo(
    () => sortedRows.find((r) => r.productId === selectedProductId) ?? null,
    [sortedRows, selectedProductId],
  )

  const loadingSnapshots = snapshotQueries.some((q) => q.isPending)
  const loadingLow = lowStockQueries.some((q) => q.isPending)
  const isLoading =
    productsQuery.isPending || warehousesQuery.isPending || loadingSnapshots || loadingLow

  function toggleSort(column: InventorySortColumn) {
    setSortColumn((prev) => {
      if (prev === column) {
        setSortDir((d) => (d === 'asc' ? 'desc' : 'asc'))
        return prev
      }
      setSortDir('asc')
      return column
    })
  }

  return {
    warehouses,
    canPickAllWarehouses,
    warehouseScope,
    setWarehouseScope,
    search,
    setSearch,
    category,
    setCategory,
    categories,
    rows: sortedRows,
    sortColumn,
    sortDir,
    toggleSort,
    selectedProductId,
    setSelectedProductId,
    selectedRow,
    isLoading,
    refetch: () => {
      void productsQuery.refetch()
      void warehousesQuery.refetch()
      for (const q of snapshotQueries) void q.refetch()
      for (const q of lowStockQueries) void q.refetch()
    },
  }
}

export function useStockHistoryForProduct(
  productId: number | null,
  warehouseScope: WarehouseScope,
) {
  const to = new Date()
  const from = new Date()
  from.setDate(from.getDate() - 30)

  const warehouseId =
    warehouseScope === 'all'
      ? undefined
      : warehouseScope

  return useQuery({
    queryKey:
      productId != null
        ? inventoryKeys.history({
            productId,
            fromLocalDate: formatLocalYmd(from),
            toLocalDate: formatLocalYmd(to),
            warehouseId: warehouseId ?? null,
          })
        : [...inventoryKeys.root, 'history', 'idle'],
    queryFn: () =>
      getStockHistory({
        productId: productId!,
        fromLocalDate: formatLocalYmd(from),
        toLocalDate: formatLocalYmd(to),
        warehouseId: warehouseId ?? null,
      }),
    enabled: productId != null,
    staleTime: 30_000,
  })
}
