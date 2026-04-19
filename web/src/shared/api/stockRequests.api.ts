import { http } from '@/shared/api/client'

export type StockRequestRow = {
  id: number
  branchWarehouseId: number
  branchWarehouseName: string
  productId: number
  productDisplayName: string
  quantity: number
  notes: string
  status: string
  requestedByUserId: number
  requestedByUsername: string
  createdAtUtc: string
  resolvedByUserId: number | null
  resolvedByUsername: string | null
  resolvedAtUtc: string | null
  resolutionNotes: string | null
  fulfillmentStockMovementId: number | null
}

export async function listStockRequests(branchWarehouseId?: number): Promise<StockRequestRow[]> {
  const { data } = await http.get<StockRequestRow[]>('/api/StockRequests', {
    params: branchWarehouseId != null ? { branchWarehouseId } : {},
  })
  return data
}

export async function createStockRequest(body: {
  productId: number
  quantity: number
  notes?: string
}): Promise<number> {
  const { data } = await http.post<number>('/api/StockRequests', body)
  return data
}

export async function rejectStockRequest(id: number, notes?: string): Promise<void> {
  await http.post(`/api/StockRequests/${id}/reject`, { notes })
}

export async function fulfillStockRequest(id: number): Promise<void> {
  await http.post(`/api/StockRequests/${id}/fulfill`)
}

export async function cancelStockRequest(id: number): Promise<void> {
  await http.post(`/api/StockRequests/${id}/cancel`)
}
