import { http } from '@/shared/api/client'

export type BranchSalesLineRow = {
  invoiceDateUtc: string
  invoiceNumber: string
  warehouseName: string
  customerDisplay: string
  sellerUsername: string
  productName: string
  quantity: number
  unitPrice: number
  lineTotal: number
  invoiceSubtotal: number
  invoiceDiscount: number
  invoiceTotal: number
}

export type BranchIncomingRow = {
  entryDateUtc: string
  entryType: string
  productName: string
  quantity: number
  amountValue: number
  sourceDetail: string
  notes: string | null
  createdByDisplay: string
}

export type BranchTransferLedgerRow = {
  movementUtc: string
  productName: string
  quantity: number
  fromWarehouseName: string
  toWarehouseName: string
  notes: string | null
}

export type ExpenseReportRow = {
  id: number
  expenseDateUtc: string
  amount: number
  category: string
  description: string
  warehouseName: string | null
  createdByUsername: string | null
}

export type BranchSellerSummaryRow = {
  sellerUsername: string
  invoiceCount: number
  lineItemCount: number
  invoicesGrossSubtotal: number
  invoicesDiscountTotal: number
  invoicesNetTotal: number
}

export async function getBranchSalesLineRegister(
  fromLocalDate: string,
  toLocalDate: string,
  warehouseId: number,
): Promise<BranchSalesLineRow[]> {
  const { data } = await http.get<BranchSalesLineRow[]>('/api/Reports/branch-sales-lines', {
    params: { fromLocalDate, toLocalDate, warehouseId },
  })
  return data
}

export async function getBranchIncomingRegister(
  fromLocalDate: string,
  toLocalDate: string,
  warehouseId: number,
): Promise<BranchIncomingRow[]> {
  const { data } = await http.get<BranchIncomingRow[]>('/api/Reports/branch-incoming', {
    params: { fromLocalDate, toLocalDate, warehouseId },
  })
  return data
}

export async function getBranchTransferLedger(
  fromLocalDate: string,
  toLocalDate: string,
  warehouseId: number,
): Promise<BranchTransferLedgerRow[]> {
  const { data } = await http.get<BranchTransferLedgerRow[]>('/api/Reports/branch-transfers', {
    params: { fromLocalDate, toLocalDate, warehouseId },
  })
  return data
}

export async function getBranchExpenses(
  fromLocalDate: string,
  toLocalDate: string,
  warehouseId: number,
): Promise<ExpenseReportRow[]> {
  const { data } = await http.get<ExpenseReportRow[]>('/api/Reports/expenses', {
    params: { fromLocalDate, toLocalDate, warehouseId },
  })
  return data
}

export async function getBranchSellerSummaries(
  fromLocalDate: string,
  toLocalDate: string,
  warehouseId: number,
): Promise<BranchSellerSummaryRow[]> {
  const { data } = await http.get<BranchSellerSummaryRow[]>('/api/Reports/branch-sellers', {
    params: { fromLocalDate, toLocalDate, warehouseId },
  })
  return data
}
