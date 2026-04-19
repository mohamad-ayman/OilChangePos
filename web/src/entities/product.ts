/**
 * Active catalog row — aligned with `ProductListDto` / `ProductSummaryDto` usage in API.
 * Quantities and prices use `number` (matches JSON); treat money as decimal-safe at boundaries.
 */
export type Product = {
  id: number
  name: string
  productCategory: string
  packageSize: string
  unitPrice: number
  companyName: string
}
