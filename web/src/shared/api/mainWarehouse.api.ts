import { http } from '@/shared/api/client'

export type MainWarehouseGridRow = {
  productId: number
  purchaseId?: number | null
  batchNumber: number
  batchTotal: number
  batchLabel: string
  companyName: string
  inventoryName: string
  productionDate: string
  purchasedQuantity: number
  onHandAtMain?: number | null
  purchasePrice: number
  purchaseDate: string
  productCategory: string
  packageSize: string
}

export type MainWarehouseCatalogEntry = {
  id: number
  companyId: number
  companyName: string
  name: string
  productCategory: string
  packageSize: string
  isPlaceholder: boolean
  caption: string
}

export type UpdateMainWarehousePurchasePayload = {
  purchaseId: number
  productId: number
  productName: string
  companyId: number
  productCategory: string
  packageSize: string
  quantity: number
  purchasePrice: number
  productionDate: string
  purchaseDate: string
}

export type MainWarehouseExcelImportLine = {
  companyName: string
  productName: string
  category: string
  packageSize: string
  quantity: number
  purchasePrice: number
  productionDate: string
  purchaseDate: string
}

export async function fetchMainWarehouseGrid(): Promise<MainWarehouseGridRow[]> {
  const { data } = await http.get<MainWarehouseGridRow[]>('/api/main-warehouse/grid-rows')
  return data
}

export async function fetchMainWarehouseCatalog(): Promise<MainWarehouseCatalogEntry[]> {
  const { data } = await http.get<MainWarehouseCatalogEntry[]>('/api/main-warehouse/catalog')
  return data
}

export async function updateMainWarehousePurchase(body: UpdateMainWarehousePurchasePayload): Promise<void> {
  await http.put('/api/main-warehouse/purchase', body)
}

export async function deleteMainWarehousePurchase(purchaseId: number): Promise<void> {
  await http.delete(`/api/main-warehouse/purchase/${purchaseId}`)
}

export async function importMainWarehouseLines(mainWarehouseId: number, lines: MainWarehouseExcelImportLine[]): Promise<number> {
  const { data } = await http.post<number>('/api/main-warehouse/import', {
    mainWarehouseId,
    lines,
  })
  return data
}
