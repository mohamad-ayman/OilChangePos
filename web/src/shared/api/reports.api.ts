import { http } from '@/shared/api/client'
import { getWarehouses } from '@/shared/api/inventory.api'

const useReportsMock =
  import.meta.env.VITE_REPORTS_MOCK === 'true' || import.meta.env.VITE_INVENTORY_MOCK === 'true'

async function mockDelay<T>(v: T, ms = 50): Promise<T> {
  await new Promise((r) => setTimeout(r, ms))
  return v
}

/** Daily bucket for sales trend charts. */
export type SalesDailyPoint = { date: string; sales: number; profit: number; invoices: number }

/** Month label `YYYY-MM` for executive trend. */
export type SalesMonthlyPoint = { month: string; sales: number; profit: number; invoices: number }

export type SalesSummary = {
  totalSales: number
  totalProfit: number
  transactionCount: number
  avgOrderValue: number
  daily: SalesDailyPoint[]
  monthly: SalesMonthlyPoint[]
}

export type LowStockRow = {
  productId: number
  productName: string
  warehouseId: number
  warehouseName: string
  quantityOnHand: number
  threshold: number
}

export type WarehouseValueRow = {
  warehouseId: number
  warehouseName: string
  stockValue: number
  skuCount: number
}

export type MovementFrequencyRow = {
  productId: number
  productName: string
  movements30d: number
}

export type DeadStockRow = {
  productId: number
  productName: string
  warehouseId: number
  warehouseName: string
  quantityOnHand: number
  daysSinceMovement: number
}

export type InventoryStats = {
  stockValueTotal: number
  lowStock: LowStockRow[]
  valueByWarehouse: WarehouseValueRow[]
  movementFrequency: MovementFrequencyRow[]
  deadStock: DeadStockRow[]
}

export type TransferByBranchRow = {
  warehouseId: number
  warehouseName: string
  outbound: number
  inbound: number
}

export type TransferFlowRow = {
  fromWarehouseId: number
  fromName: string
  toWarehouseId: number
  toName: string
  count: number
}

export type TransferStats = {
  pendingApproval: number
  approved: number
  inTransit: number
  completed: number
  rejected: number
  byBranch: TransferByBranchRow[]
  flows: TransferFlowRow[]
}

export type TopProductRow = {
  rank: number
  productId: number
  name: string
  qtySold: number
  revenue: number
}

export type TopProductsResult = {
  periodLabel: string
  items: TopProductRow[]
}

function mockSalesSummary(): SalesSummary {
  const daily: SalesDailyPoint[] = []
  const base = new Date()
  for (let i = 13; i >= 0; i--) {
    const d = new Date(base)
    d.setDate(d.getDate() - i)
    const date = d.toISOString().slice(0, 10)
    const sales = 4000 + i * 220 + (i % 3) * 150
    const profit = Math.round(sales * 0.22)
    daily.push({ date, sales, profit, invoices: 12 + (i % 5) })
  }
  const monthly: SalesMonthlyPoint[] = [
    { month: '2026-01', sales: 118000, profit: 25200, invoices: 420 },
    { month: '2026-02', sales: 124500, profit: 26800, invoices: 445 },
    { month: '2026-03', sales: 131200, profit: 28100, invoices: 468 },
    { month: '2026-04', sales: 98500, profit: 21200, invoices: 352 },
  ]
  const totalSales = daily.reduce((s, x) => s + x.sales, 0)
  const totalProfit = daily.reduce((s, x) => s + x.profit, 0)
  const transactionCount = daily.reduce((s, x) => s + x.invoices, 0)
  return {
    totalSales,
    totalProfit,
    transactionCount,
    avgOrderValue: Math.round((totalSales / Math.max(1, transactionCount)) * 100) / 100,
    daily,
    monthly,
  }
}

function mockInventoryStats(): InventoryStats {
  return {
    stockValueTotal: 582_400,
    lowStock: [
      {
        productId: 2,
        productName: 'Mock Filter',
        warehouseId: 2,
        warehouseName: 'Branch A',
        quantityOnHand: 1,
        threshold: 5,
      },
      {
        productId: 5,
        productName: 'Wiper blade',
        warehouseId: 1,
        warehouseName: 'Main',
        quantityOnHand: 4,
        threshold: 10,
      },
    ],
    valueByWarehouse: [
      { warehouseId: 1, warehouseName: 'Main', stockValue: 412_000, skuCount: 128 },
      { warehouseId: 2, warehouseName: 'Branch A', stockValue: 170_400, skuCount: 96 },
    ],
    movementFrequency: [
      { productId: 1, productName: 'Mock Oil 4L', movements30d: 142 },
      { productId: 2, productName: 'Mock Filter', movements30d: 88 },
      { productId: 3, productName: 'Brake fluid 1L', movements30d: 41 },
    ],
    deadStock: [
      {
        productId: 99,
        productName: 'Legacy gasket kit',
        warehouseId: 2,
        warehouseName: 'Branch A',
        quantityOnHand: 6,
        daysSinceMovement: 187,
      },
    ],
  }
}

function mockTransferStats(): TransferStats {
  return {
    pendingApproval: 3,
    approved: 2,
    inTransit: 1,
    completed: 48,
    rejected: 2,
    byBranch: [
      { warehouseId: 1, warehouseName: 'Main', outbound: 32, inbound: 4 },
      { warehouseId: 2, warehouseName: 'Branch A', outbound: 6, inbound: 28 },
    ],
    flows: [
      { fromWarehouseId: 1, fromName: 'Main', toWarehouseId: 2, toName: 'Branch A', count: 38 },
      { fromWarehouseId: 2, fromName: 'Branch A', toWarehouseId: 1, toName: 'Main', count: 4 },
    ],
  }
}

function mockTopProducts(): TopProductsResult {
  return {
    periodLabel: 'Last 30 days',
    items: [
      { rank: 1, productId: 1, name: 'Mock Oil 4L', qtySold: 420, revenue: 42000 },
      { rank: 2, productId: 2, name: 'Mock Filter', qtySold: 310, revenue: 12400 },
      { rank: 3, productId: 3, name: 'Brake fluid 1L', qtySold: 95, revenue: 2850 },
    ],
  }
}

// --- Live adapters: map OilChangePOS.API `ReportsController` + `Inventory` to dashboard DTOs ---

function toLocalDateString(d: Date): string {
  const y = d.getFullYear()
  const m = String(d.getMonth() + 1).padStart(2, '0')
  const day = String(d.getDate()).padStart(2, '0')
  return `${y}-${m}-${day}`
}

function addDays(date: Date, days: number): Date {
  const d = new Date(date.getTime())
  d.setDate(d.getDate() + days)
  return d
}

type DailySalesDto = {
  dateUtc: string
  invoiceCount: number
  totalSales: number
  totalDiscounts: number
}

type SalesPeriodSummaryDto = {
  invoiceCount: number
  grossSales: number
  totalDiscounts: number
  netSales: number
  averageInvoiceValue: number
}

type ProfitRollupDto = {
  totalRevenue: number
  totalEstimatedCogs: number
  totalEstimatedGrossProfit: number
}

type StockFromMovementsRow = {
  productId: number
  productName: string
  warehouseId: number
  warehouseName: string
  quantityOnHand: number
  retailStockValue: number
}

type LowStockApiRow = {
  productId: number
  productName: string
  currentStock: number
  threshold: number
}

type SlowMovingRow = {
  productName: string
  onHandAtWarehouse: number
  quantitySoldInPeriod: number
}

type TransferLedgerRow = {
  movementUtc: string
  productName: string
  quantity: number
  fromWarehouseName: string
  toWarehouseName: string
  notes?: string | null
}

type TopSellingRow = {
  productName: string
  quantitySold: number
  salesAmount: number
}

function stableNameId(name: string): number {
  let h = 0
  for (let i = 0; i < name.length; i++) h = (h * 31 + name.charCodeAt(i)) | 0
  return Math.abs(h) % 1_000_000
}

async function fetchSalesSummaryLive(): Promise<SalesSummary> {
  const today = new Date()
  today.setHours(0, 0, 0, 0)
  const fromDay = addDays(today, -13)
  const fromStr = toLocalDateString(fromDay)
  const toStr = toLocalDateString(today)

  const dailyRows = await Promise.all(
    Array.from({ length: 14 }, (_, i) => {
      const d = addDays(fromDay, i)
      const dateUtc = new Date(Date.UTC(d.getFullYear(), d.getMonth(), d.getDate(), 12, 0, 0)).toISOString()
      return http.get<DailySalesDto>('/api/Reports/daily-sales', { params: { dateUtc } }).then((r) => {
        const sales = Number(r.data.totalSales ?? 0)
        const invoices = Number(r.data.invoiceCount ?? 0)
        const profit = Math.round(sales * 0.18)
        return {
          date: toLocalDateString(d),
          sales,
          profit,
          invoices,
        } satisfies SalesDailyPoint
      })
    }),
  )

  const [periodRes, profitRes] = await Promise.all([
    http.get<SalesPeriodSummaryDto>('/api/Reports/sales-period-summary', {
      params: { fromLocalDate: fromStr, toLocalDate: toStr },
    }),
    http.get<ProfitRollupDto>('/api/Reports/profit-rollup', {
      params: { fromLocalDate: fromStr, toLocalDate: toStr },
    }),
  ])

  const monthlyMap = new Map<string, { sales: number; profit: number; invoices: number }>()
  for (const p of dailyRows) {
    const month = p.date.slice(0, 7)
    const cur = monthlyMap.get(month) ?? { sales: 0, profit: 0, invoices: 0 }
    cur.sales += p.sales
    cur.profit += p.profit
    cur.invoices += p.invoices
    monthlyMap.set(month, cur)
  }
  const monthly: SalesMonthlyPoint[] = [...monthlyMap.entries()]
    .sort(([a], [b]) => a.localeCompare(b))
    .map(([month, v]) => ({ month, ...v }))

  const period = periodRes.data
  const profit = profitRes.data
  const totalSales = Number(period.netSales ?? 0)
  const totalProfit = Number(profit.totalEstimatedGrossProfit ?? 0)
  const transactionCount = Number(period.invoiceCount ?? 0)
  const avgOrderValue =
    transactionCount > 0 ? Math.round((totalSales / transactionCount) * 100) / 100 : Number(period.averageInvoiceValue ?? 0)

  return {
    totalSales,
    totalProfit,
    transactionCount,
    avgOrderValue,
    daily: dailyRows,
    monthly,
  }
}

async function fetchInventoryStatsLive(): Promise<InventoryStats> {
  const { data: rows } = await http.get<StockFromMovementsRow[]>('/api/Reports/stock-from-movements')
  const whAgg = new Map<number, { warehouseName: string; stockValue: number; skus: Set<number> }>()
  let stockValueTotal = 0
  for (const r of rows) {
    stockValueTotal += Number(r.retailStockValue ?? 0)
    const cur = whAgg.get(r.warehouseId) ?? {
      warehouseName: r.warehouseName,
      stockValue: 0,
      skus: new Set<number>(),
    }
    cur.stockValue += Number(r.retailStockValue ?? 0)
    cur.skus.add(r.productId)
    whAgg.set(r.warehouseId, cur)
  }
  const valueByWarehouse: WarehouseValueRow[] = [...whAgg.entries()].map(([warehouseId, v]) => ({
    warehouseId,
    warehouseName: v.warehouseName,
    stockValue: Math.round(v.stockValue * 100) / 100,
    skuCount: v.skus.size,
  }))

  const warehouses = await getWarehouses()
  const lowStock: LowStockRow[] = []
  for (const w of warehouses.filter((x) => x.isActive)) {
    const { data: lows } = await http.get<LowStockApiRow[]>(`/api/Inventory/low-stock/${w.id}`)
    for (const row of lows) {
      lowStock.push({
        productId: row.productId,
        productName: row.productName,
        warehouseId: w.id,
        warehouseName: w.name,
        quantityOnHand: Number(row.currentStock ?? 0),
        threshold: Number(row.threshold ?? 0),
      })
    }
  }

  const today = new Date()
  today.setHours(0, 0, 0, 0)
  const from90 = toLocalDateString(addDays(today, -90))
  const toStr = toLocalDateString(today)
  const branch = warehouses.find((x) => x.type === 2 && x.isActive) ?? warehouses.find((x) => x.isActive)
  let deadStock: DeadStockRow[] = []
  if (branch) {
    try {
      const { data: slow } = await http.get<SlowMovingRow[]>('/api/Reports/slow-moving', {
        params: {
          fromLocalDate: from90,
          toLocalDate: toStr,
          warehouseId: branch.id,
          take: 12,
        },
      })
      deadStock = slow.map((s, i) => ({
        productId: stableNameId(s.productName) + i,
        productName: s.productName,
        warehouseId: branch.id,
        warehouseName: branch.name,
        quantityOnHand: Number(s.onHandAtWarehouse ?? 0),
        daysSinceMovement: 90,
      }))
    } catch {
      deadStock = []
    }
  }

  return {
    stockValueTotal: Math.round(stockValueTotal * 100) / 100,
    lowStock,
    valueByWarehouse,
    movementFrequency: [],
    deadStock,
  }
}

async function fetchTransferStatsLive(): Promise<TransferStats> {
  const today = new Date()
  today.setHours(0, 0, 0, 0)
  const fromStr = toLocalDateString(addDays(today, -30))
  const toStr = toLocalDateString(today)
  const { data: ledger } = await http.get<TransferLedgerRow[]>('/api/Reports/transfers', {
    params: { fromLocalDate: fromStr, toLocalDate: toStr },
  })

  const flowMap = new Map<string, { fromName: string; toName: string; count: number }>()
  const branchMap = new Map<string, { warehouseName: string; outbound: number; inbound: number }>()

  for (const row of ledger) {
    const fromName = row.fromWarehouseName ?? '—'
    const toName = row.toWarehouseName ?? '—'
    const fk = `${fromName}→${toName}`
    const f = flowMap.get(fk) ?? { fromName, toName, count: 0 }
    f.count += 1
    flowMap.set(fk, f)

    const outB = branchMap.get(fromName) ?? { warehouseName: fromName, outbound: 0, inbound: 0 }
    outB.outbound += 1
    branchMap.set(fromName, outB)

    const inB = branchMap.get(toName) ?? { warehouseName: toName, outbound: 0, inbound: 0 }
    inB.inbound += 1
    branchMap.set(toName, inB)
  }

  const flows: TransferFlowRow[] = [...flowMap.values()].map((f) => ({
    fromWarehouseId: stableNameId(f.fromName),
    fromName: f.fromName,
    toWarehouseId: stableNameId(f.toName),
    toName: f.toName,
    count: f.count,
  }))

  const byBranch: TransferByBranchRow[] = [...branchMap.values()].map((b) => ({
    warehouseId: stableNameId(b.warehouseName),
    warehouseName: b.warehouseName,
    outbound: b.outbound,
    inbound: b.inbound,
  }))

  const completed = ledger.length
  return {
    pendingApproval: 0,
    approved: 0,
    inTransit: 0,
    completed,
    rejected: 0,
    byBranch,
    flows,
  }
}

async function fetchTopProductsLive(): Promise<TopProductsResult> {
  const today = new Date()
  today.setHours(0, 0, 0, 0)
  const fromStr = toLocalDateString(addDays(today, -29))
  const toStr = toLocalDateString(today)
  const { data } = await http.get<TopSellingRow[]>('/api/Reports/top-selling', {
    params: { fromLocalDate: fromStr, toLocalDate: toStr, take: 15 },
  })
  const items: TopProductRow[] = data.map((row, i) => ({
    rank: i + 1,
    productId: stableNameId(row.productName),
    name: row.productName,
    qtySold: Number(row.quantitySold ?? 0),
    revenue: Number(row.salesAmount ?? 0),
  }))
  return { periodLabel: `${fromStr} → ${toStr}`, items }
}

/** Sales KPIs + charts — backed by `daily-sales`, `sales-period-summary`, and `profit-rollup`. */
export async function getSalesSummary(): Promise<SalesSummary> {
  if (useReportsMock) return mockDelay(mockSalesSummary())
  return fetchSalesSummaryLive()
}

/** Inventory valuation + low stock — `stock-from-movements` + per-warehouse `low-stock`. */
export async function getInventoryStats(): Promise<InventoryStats> {
  if (useReportsMock) return mockDelay(mockInventoryStats())
  return fetchInventoryStatsLive()
}

/**
 * Transfer volume — `GET /api/Reports/transfers` (immediate stock postings; no approval pipeline in API).
 */
export async function getTransferStats(): Promise<TransferStats> {
  if (useReportsMock) return mockDelay(mockTransferStats())
  return fetchTransferStatsLive()
}

/** Top sellers — `GET /api/Reports/top-selling`. */
export async function getTopProducts(): Promise<TopProductsResult> {
  if (useReportsMock) return mockDelay(mockTopProducts())
  return fetchTopProductsLive()
}
