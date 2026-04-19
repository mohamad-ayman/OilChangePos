import { http } from '@/shared/api/client'

export type CatalogCompanyListRow = {
  id: number
  name: string
  isActive: boolean
  productCount: number
}

export type CatalogProductListRow = {
  id: number
  companyId: number
  name: string
  productCategory: string
  packageSize: string
  isActive: boolean
}

export type SaveCompanyBody = {
  createNew: boolean
  existingCompanyId: number | null
  name: string
  isActive: boolean
}

export type SaveProductBody = {
  createNew: boolean
  companyId: number
  existingProductId: number | null
  name: string
  category: string
  package: string
  isActive: boolean
}

export async function listCatalogCompanies(): Promise<CatalogCompanyListRow[]> {
  const { data } = await http.get<CatalogCompanyListRow[]>('/api/catalog-admin/companies')
  return data
}

export async function listCatalogProducts(companyId: number): Promise<CatalogProductListRow[]> {
  const { data } = await http.get<CatalogProductListRow[]>(`/api/catalog-admin/companies/${companyId}/products`)
  return data
}

export async function saveCatalogCompany(body: SaveCompanyBody): Promise<void> {
  await http.post('/api/catalog-admin/companies', body)
}

export async function saveCatalogProduct(body: SaveProductBody): Promise<void> {
  await http.post('/api/catalog-admin/products', body)
}
