/**
 * Client-side transfer aggregate before POST `api/Transfers`.
 * Server persists movements; this structure is the ERP transfer header + lines.
 */
export type TransferLine = {
  productId: number
  quantity: number
}

export type TransferDocument = {
  /** Optional client-generated id for optimistic UI correlation */
  clientRequestId: string
  fromWarehouseId: number
  toWarehouseId: number
  userId: number
  notes: string
  lines: TransferLine[]
  /** Optional retail override when transferring into a branch */
  branchSalePriceForDestination?: number | null
  createdAtUtc: string
}
