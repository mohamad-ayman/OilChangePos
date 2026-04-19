import { http } from '@/shared/api/client'

export type ExpenseReportRow = {
  id: number
  expenseDateUtc: string
  amount: number
  category: string
  description: string
  warehouseName: string | null
  createdByUsername: string | null
}

export type RecordExpenseBody = {
  amount: number
  category: string
  description: string
  /** Local calendar date `YYYY-MM-DD` (server normalizes to start of local day). */
  expenseDateLocal: string
  /** `null` = company-wide (admin only). */
  warehouseId: number | null
}

/** Lists expenses in local-date range. Omit `warehouseId` for admin company-wide list. */
export async function getExpensesReport(
  fromLocalDate: string,
  toLocalDate: string,
  warehouseId?: number,
): Promise<ExpenseReportRow[]> {
  const params: Record<string, string> = { fromLocalDate, toLocalDate }
  if (warehouseId !== undefined) params.warehouseId = String(warehouseId)
  const { data } = await http.get<ExpenseReportRow[]>('/api/Reports/expenses', { params })
  return data
}

export async function recordExpense(body: RecordExpenseBody): Promise<number> {
  const { data } = await http.post<number>('/api/Expenses', body)
  return typeof data === 'number' ? data : Number(data)
}
