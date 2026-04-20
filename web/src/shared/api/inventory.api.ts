import { http } from '@/shared/api/client'
import type { Product } from '@/entities/product'
import type { StockItem } from '@/entities/stock-item'
import type { Warehouse } from '@/entities/warehouse'
import { DomainStockMovementType, type StockMovement } from '@/entities/stock-movement'
import type { TransferDocument } from '@/entities/transfer-document'
import { buildTransferDocument, ledgerKey } from '@/features/inventory/domain/inventory.engine'

const useInventoryMock = import.meta.env.VITE_INVENTORY_MOCK === 'true'

// ---------------------------------------------------------------------------
// TEMP MOCK IMPLEMENTATION — Replace when running against a live API is unwanted.
// Toggle with VITE_INVENTORY_MOCK=true. Same exports as real adapter.
// ---------------------------------------------------------------------------

function mockProducts(): Product[] {
  return [
    {
      id: 1,
      name: 'Mock Oil 4L',
      productCategory: 'Oil',
      packageSize: '4L',
      unitPrice: 100,
      companyName: 'MockCo',
    },
    {
      id: 2,
      name: 'Mock Filter',
      productCategory: 'Filter',
      packageSize: 'Std',
      unitPrice: 40,
      companyName: 'MockCo',
    },
  ]
}

function mockWarehouses(): Warehouse[] {
  return [
    { id: 1, name: 'Main', type: 1, isActive: true },
    { id: 2, name: 'Branch A', type: 2, isActive: true },
  ]
}

function mockSnapshot(warehouseId: number): InventorySnapshotView[] {
  return [
    {
      productId: 1,
      productName: 'Mock Oil 4L',
      currentStock: warehouseId === 1 ? 50 : 5,
      unitPrice: 100,
      stockValue: warehouseId === 1 ? 5000 : 500,
    },
    {
      productId: 2,
      productName: 'Mock Filter',
      currentStock: warehouseId === 1 ? 3 : 0,
      unitPrice: 40,
      stockValue: warehouseId === 1 ? 120 : 0,
    },
  ]
}

function mockLowStock(warehouseId: number): LowStockItemView[] {
  return warehouseId === 2
    ? [{ productId: 2, productName: 'Mock Filter', currentStock: 0, threshold: 2 }]
    : [{ productId: 2, productName: 'Mock Filter', currentStock: 3, threshold: 5 }]
}

async function mockDelay<T>(value: T, ms = 80): Promise<T> {
  await new Promise((r) => setTimeout(r, ms))
  return value
}

type InventorySnapshotRow = {
  productId: number
  productName: string
  currentStock: number
  unitPrice: number
  stockValue: number
}

export type InventorySnapshotView = {
  productId: number
  productName: string
  currentStock: number
  unitPrice: number
  stockValue: number
}

/** Cumulative qty adjustments for mock inventory snapshots after completed transfers. */
const mockTransferStockDeltas = new Map<string, number>()

function bumpMockStockDelta(productId: number, warehouseId: number, deltaQty: number): void {
  const k = ledgerKey(productId, warehouseId)
  mockTransferStockDeltas.set(k, (mockTransferStockDeltas.get(k) ?? 0) + deltaQty)
}

function applyMockDeltasToSnapshot(rows: InventorySnapshotView[], warehouseId: number): InventorySnapshotView[] {
  return rows.map((r) => {
    const adj = mockTransferStockDeltas.get(ledgerKey(r.productId, warehouseId)) ?? 0
    const currentStock = Math.max(0, r.currentStock + adj)
    const unitPrice = r.unitPrice
    return {
      ...r,
      currentStock,
      stockValue: Math.round(currentStock * unitPrice * 10000) / 10000,
    }
  })
}

type WarehouseRow = {
  id: number
  name: string
  type: number
  isActive?: boolean
}

export type LowStockItemView = {
  productId: number
  productName: string
  currentStock: number
  threshold: number
}

type ProductListRow = {
  id: number
  name: string
  productCategory: string
  packageSize: string
  unitPrice: number
  companyName: string
}

type StockMovementHistoryRow = {
  movementDateUtc: string
  movementType: string
  quantity: number
  fromWarehouseId?: number | null
  toWarehouseId?: number | null
  notes?: string | null
}

function mapProduct(row: ProductListRow): Product {
  return {
    id: row.id,
    name: row.name,
    productCategory: row.productCategory,
    packageSize: row.packageSize,
    unitPrice: row.unitPrice,
    companyName: row.companyName,
  }
}

function mapSnapshotToStockItems(rows: InventorySnapshotRow[], warehouseId: number): StockItem[] {
  return rows.map((r) => ({
    productId: r.productId,
    warehouseId,
    quantityOnHand: r.currentStock,
  }))
}

function mapMovementTypeLabel(label: string): StockMovement['movementType'] {
  const s = label.trim().toLowerCase()
  if (s === 'purchase') return DomainStockMovementType.Purchase
  if (s === 'sale') return DomainStockMovementType.Sale
  if (s === 'transfer') return DomainStockMovementType.Transfer
  return DomainStockMovementType.Adjust
}

function mapHistoryRow(r: StockMovementHistoryRow, productId: number): StockMovement {
  return {
    productId,
    movementDateUtc: r.movementDateUtc,
    movementType: mapMovementTypeLabel(r.movementType),
    quantity: r.quantity,
    fromWarehouseId: r.fromWarehouseId ?? null,
    toWarehouseId: r.toWarehouseId ?? null,
    notes: r.notes ?? '',
  }
}

export class InventoryApiError extends Error {
  readonly code: string

  constructor(message: string, code: string) {
    super(message)
    this.name = 'InventoryApiError'
    this.code = code
  }
}

export async function getProducts(): Promise<Product[]> {
  if (useInventoryMock) {
    return mockDelay(mockProducts())
  }
  const { data } = await http.get<ProductListRow[]>('/api/Products')
  return data.map(mapProduct)
}

/** Product rows for transfers / catalog pickers (`GET api/Products/summaries`). */
export type ProductSummary = {
  id: number
  companyId: number
  companyName: string
  name: string
  productCategory: string
  packageSize: string
  unitPrice: number
  isActive: boolean
}

type ProductSummaryRow = {
  id: number
  companyId: number
  companyName: string
  name: string
  productCategory: string
  packageSize: string
  unitPrice: number
  isActive: boolean
}

export async function getProductSummaries(activeOnly: boolean): Promise<ProductSummary[]> {
  if (useInventoryMock) {
    const rows = mockProducts().map((p) => ({
      id: p.id,
      companyId: 1,
      companyName: p.companyName,
      name: p.name,
      productCategory: p.productCategory,
      packageSize: p.packageSize,
      unitPrice: p.unitPrice,
      isActive: true,
    }))
    return mockDelay(activeOnly ? rows : rows)
  }
  const { data } = await http.get<ProductSummaryRow[]>('/api/Products/summaries', {
    params: { activeOnly },
  })
  return data.map((r) => ({
    id: r.id,
    companyId: r.companyId,
    companyName: r.companyName,
    name: r.name,
    productCategory: r.productCategory,
    packageSize: r.packageSize,
    unitPrice: r.unitPrice,
    isActive: r.isActive,
  }))
}

/** Effective POS sale price at a branch (`GET api/Inventory/effective-sale-price/...`). */
export async function getEffectiveSalePrice(productId: number, warehouseId: number): Promise<number> {
  if (useInventoryMock) {
    const p = mockProducts().find((x) => x.id === productId)
    return mockDelay(p?.unitPrice ?? 1300)
  }
  const { data } = await http.get<number>(`/api/Inventory/effective-sale-price/${productId}/${warehouseId}`)
  return Number(data)
}

export type BranchPriceOverrideItemView = {
  productId: number
  salePrice: number
}

/** Batch branch retail overrides (`POST api/Inventory/branch-overrides`) — same source as effective sale price. */
export async function getBranchSalePriceOverrides(
  warehouseId: number,
  productIds: number[],
): Promise<Map<number, number>> {
  if (useInventoryMock || productIds.length === 0) {
    return new Map()
  }
  const { data } = await http.post<BranchPriceOverrideItemView[]>('/api/Inventory/branch-overrides', {
    warehouseId,
    productIds,
  })
  const m = new Map<number, number>()
  for (const row of data) {
    m.set(row.productId, Number(row.salePrice))
  }
  return m
}

/** Warehouses for filters (`GET api/Warehouses`). */
export async function getWarehouses(): Promise<Warehouse[]> {
  if (useInventoryMock) {
    return mockDelay(mockWarehouses())
  }
  const { data } = await http.get<WarehouseRow[]>('/api/Warehouses')
  return data.map((w) => ({
    id: w.id,
    name: w.name,
    type: w.type as Warehouse['type'],
    isActive: w.isActive ?? true,
  }))
}

/** Full snapshot rows for ERP grid (`GET api/Inventory/{warehouseId}`). */
export async function getInventorySnapshot(warehouseId: number): Promise<InventorySnapshotView[]> {
  if (useInventoryMock) {
    const base = mockSnapshot(warehouseId)
    return mockDelay(applyMockDeltasToSnapshot(base, warehouseId))
  }
  const { data } = await http.get<InventorySnapshotRow[]>(`/api/Inventory/${warehouseId}`)
  return data.map((r) => ({
    productId: r.productId,
    productName: r.productName,
    currentStock: r.currentStock,
    unitPrice: r.unitPrice,
    stockValue: r.stockValue,
  }))
}

/** On-hand snapshot per SKU for one warehouse (`GET api/Inventory/{warehouseId}`). */
export async function getStockByWarehouse(warehouseId: number): Promise<StockItem[]> {
  const rows = await getInventorySnapshot(warehouseId)
  return mapSnapshotToStockItems(
    rows.map((r) => ({
      productId: r.productId,
      productName: r.productName,
      currentStock: r.currentStock,
      unitPrice: r.unitPrice,
      stockValue: r.stockValue,
    })),
    warehouseId,
  )
}

/** On-hand at a site (`GET api/Inventory/current-stock/{productId}/{warehouseId}`). */
export async function getCurrentStock(productId: number, warehouseId: number): Promise<number> {
  if (useInventoryMock) {
    const snap = mockSnapshot(warehouseId).find((r) => r.productId === productId)
    return mockDelay(snap?.currentStock ?? 0)
  }
  const { data } = await http.get<number>(`/api/Inventory/current-stock/${productId}/${warehouseId}`)
  return Number(data)
}

/** Low-stock alert list (`GET api/Inventory/low-stock/{warehouseId}`). */
export async function getLowStockItems(warehouseId: number): Promise<LowStockItemView[]> {
  if (useInventoryMock) {
    return mockDelay(mockLowStock(warehouseId))
  }
  const { data } = await http.get<LowStockItemView[]>(`/api/Inventory/low-stock/${warehouseId}`)
  return data
}

export type PurchaseStockPayload = {
  productId: number
  quantity: number
  purchasePrice: number
  productionDate: string
  purchaseDate: string
  warehouseId: number
  notes: string
  userId: number
}

export type AuditAdjustmentPayload = {
  userId: number
  warehouseId: number
  notes: string
  lines: { productId: number; actualQuantity: number; warehouseId?: number }[]
}

export type CreateStockMovementInput =
  | { kind: 'PURCHASE'; payload: PurchaseStockPayload }
  | { kind: 'ADJUSTMENT'; payload: AuditAdjustmentPayload }

export type StockAuditResult = {
  auditId: number
  adjustedProductsCount: number
}

/**
 * Creates stock through inventory APIs (purchase or audit adjustment).
 * Sales are handled by `api/Sales` — not invoked here to keep inventory boundary clean.
 */
export async function createStockMovement(
  input: CreateStockMovementInput,
): Promise<number | StockAuditResult> {
  if (useInventoryMock) {
    await mockDelay(null, 60)
    return input.kind === 'PURCHASE'
      ? 1001
      : { auditId: 2001, adjustedProductsCount: input.payload.lines.length }
  }
  if (input.kind === 'PURCHASE') {
    const p = input.payload
    const { data } = await http.post<number>('/api/Inventory/purchase', {
      productId: p.productId,
      quantity: p.quantity,
      purchasePrice: p.purchasePrice,
      productionDate: p.productionDate,
      purchaseDate: p.purchaseDate,
      warehouseId: p.warehouseId,
      notes: p.notes,
      userId: p.userId,
    })
    return data
  }
  const p = input.payload
  const { data } = await http.post<StockAuditResult>('/api/Inventory/audit', {
    userId: p.userId,
    warehouseId: p.warehouseId,
    notes: p.notes,
    lines: p.lines.map((l) => ({
      productId: l.productId,
      actualQuantity: l.actualQuantity,
      warehouseId: l.warehouseId ?? p.warehouseId,
    })),
  })
  return data
}

/** Persists one transfer line (`POST api/Transfers`) — backend accepts a single product per request. */
export async function createTransferLine(input: {
  productId: number
  quantity: number
  fromWarehouseId: number
  toWarehouseId: number
  notes: string
  userId: number
  branchSalePriceForDestination?: number | null
}): Promise<number> {
  if (useInventoryMock) {
    return mockDelay(9000 + input.productId)
  }
  const { data } = await http.post<number>('/api/Transfers', {
    productId: input.productId,
    quantity: input.quantity,
    fromWarehouseId: input.fromWarehouseId,
    toWarehouseId: input.toWarehouseId,
    notes: input.notes,
    userId: input.userId,
    branchSalePriceForDestination: input.branchSalePriceForDestination ?? undefined,
  })
  return data
}

/** Many SKUs in one request (`POST api/Transfers/bulk`) — one database transaction on the server. */
export async function createTransferBulk(input: {
  fromWarehouseId: number
  toWarehouseId: number
  notes: string
  userId: number
  lines: { productId: number; quantity: number; branchSalePriceForDestination?: number | null }[]
}): Promise<number[]> {
  if (useInventoryMock) {
    return mockDelay(input.lines.map((l) => 9200 + l.productId))
  }
  const { data } = await http.post<number[]>('/api/Transfers/bulk', {
    fromWarehouseId: input.fromWarehouseId,
    toWarehouseId: input.toWarehouseId,
    notes: input.notes,
    userId: input.userId,
    lines: input.lines.map((l) => ({
      productId: l.productId,
      quantity: l.quantity,
      branchSalePriceForDestination: l.branchSalePriceForDestination ?? undefined,
    })),
  })
  return data
}

/** Executes transfer lines: one bulk API call when possible. */
export async function executeTransferDocumentLines(doc: TransferDocument): Promise<number[]> {
  if (useInventoryMock) {
    return mockDelay(doc.lines.map((_, i) => 9100 + i))
  }
  if (doc.lines.length === 0) return []
  if (doc.lines.length === 1) {
    const line = doc.lines[0]!
    const id = await createTransferLine({
      productId: line.productId,
      quantity: line.quantity,
      fromWarehouseId: doc.fromWarehouseId,
      toWarehouseId: doc.toWarehouseId,
      notes: doc.notes,
      userId: doc.userId,
      branchSalePriceForDestination: doc.branchSalePriceForDestination ?? undefined,
    })
    return [id]
  }
  return createTransferBulk({
    fromWarehouseId: doc.fromWarehouseId,
    toWarehouseId: doc.toWarehouseId,
    notes: doc.notes,
    userId: doc.userId,
    lines: doc.lines.map((l) => ({
      productId: l.productId,
      quantity: l.quantity,
      branchSalePriceForDestination: doc.branchSalePriceForDestination ?? undefined,
    })),
  })
}

export type StockHistoryQuery = {
  productId: number
  fromLocalDate: string
  toLocalDate: string
  warehouseId?: number | null
}

/** Movement history (`GET api/Reports/stock-movement-history`). */
export async function getStockHistory(query: StockHistoryQuery): Promise<StockMovement[]> {
  if (useInventoryMock) {
    const rows: StockMovementHistoryRow[] = [
      {
        movementDateUtc: new Date().toISOString(),
        movementType: 'Transfer',
        quantity: 2,
        fromWarehouseId: 1,
        toWarehouseId: 2,
        notes: 'Mock transfer',
      },
    ]
    return mockDelay(rows.map((r) => mapHistoryRow(r, query.productId)))
  }
  const { data } = await http.get<StockMovementHistoryRow[]>('/api/Reports/stock-movement-history', {
    params: {
      productId: query.productId,
      fromLocalDate: query.fromLocalDate,
      toLocalDate: query.toLocalDate,
      warehouseId: query.warehouseId ?? undefined,
    },
  })
  return data.map((r) => mapHistoryRow(r, query.productId))
}

export type TransferHistoryQuery = {
  fromLocalDate: string
  toLocalDate: string
  fromWarehouseId?: number | null
  toWarehouseId?: number | null
}

type TransferLedgerApiRow = {
  movementUtc: string
  productName: string
  quantity: number
  fromWarehouseName: string
  toWarehouseName: string
  notes: string
}

export type TransferHistoryRow = {
  movementUtc: string
  productName: string
  quantity: number
  fromWarehouseName: string
  toWarehouseName: string
  notes: string
}

/** Transfer ledger history (`GET api/Reports/transfers`) used by direct transfer screen. */
export async function getTransferHistory(query: TransferHistoryQuery): Promise<TransferHistoryRow[]> {
  if (useInventoryMock) {
    return mockDelay([
      {
        movementUtc: new Date().toISOString(),
        productName: 'Mock Oil 4L',
        quantity: 2,
        fromWarehouseName: 'Main',
        toWarehouseName: 'Branch A',
        notes: 'تحويل يدوي',
      },
      {
        movementUtc: new Date(Date.now() - 86_400_000).toISOString(),
        productName: 'Mock Filter',
        quantity: 1,
        fromWarehouseName: 'Main',
        toWarehouseName: 'Branch A',
        notes: 'تحويل يدوي',
      },
    ])
  }
  const { data } = await http.get<TransferLedgerApiRow[]>('/api/Reports/transfers', {
    params: {
      fromLocalDate: query.fromLocalDate,
      toLocalDate: query.toLocalDate,
      fromWarehouseId: query.fromWarehouseId ?? undefined,
      toWarehouseId: query.toWarehouseId ?? undefined,
    },
  })
  return data.map((r) => ({
    movementUtc: r.movementUtc,
    productName: r.productName,
    quantity: r.quantity,
    fromWarehouseName: r.fromWarehouseName,
    toWarehouseName: r.toWarehouseName,
    notes: r.notes,
  }))
}

// ---------------------------------------------------------------------------
// Transfer workflow (logistics) — mock when `VITE_INVENTORY_MOCK=true`, else REST placeholders.
// ---------------------------------------------------------------------------

export type TransferWorkflowStatus =
  | 'draft'
  | 'pending_approval'
  | 'approved'
  | 'in_transit'
  | 'completed'
  | 'rejected'

export type TransferAuditEntryKind =
  | 'created'
  | 'submitted'
  | 'approved'
  | 'rejected'
  | 'dispatched'
  | 'completed'
  | 'stock_posted'

export type TransferAuditEntry = {
  id: string
  atUtc: string
  kind: TransferAuditEntryKind
  userId: number
  username?: string
  note?: string
  fromStatus?: TransferWorkflowStatus
  toStatus?: TransferWorkflowStatus
}

export type TransferLineView = {
  productId: number
  quantity: number
  productName?: string
}

export type TransferDetail = {
  id: string
  status: TransferWorkflowStatus
  fromWarehouseId: number
  toWarehouseId: number
  createdByUserId: number
  createdByUsername?: string
  notes: string
  lines: TransferLineView[]
  branchSalePriceForDestination?: number | null
  createdAtUtc: string
  updatedAtUtc: string
  auditTrail: TransferAuditEntry[]
  /** Server movement ids after stock is posted */
  remoteLineIds?: number[]
}

export type TransferSummary = {
  id: string
  status: TransferWorkflowStatus
  fromWarehouseId: number
  toWarehouseId: number
  createdByUserId: number
  createdByUsername?: string
  notes: string
  createdAtUtc: string
  lineCount: number
}

export type TransferListFilters = {
  status?: TransferWorkflowStatus | 'all'
  warehouseId?: number | 'all'
  dateFromUtc?: string
  dateToUtc?: string
}

export type CreateTransferWorkflowInput = {
  fromWarehouseId: number
  toWarehouseId: number
  userId: number
  username?: string
  notes: string
  lines: { productId: number; quantity: number }[]
  branchSalePriceForDestination?: number | null
}

function newAuditId(): string {
  return typeof crypto !== 'undefined' && 'randomUUID' in crypto ? crypto.randomUUID() : `a-${Date.now()}`
}

function mockProductName(productId: number): string {
  const p = mockProducts().find((x) => x.id === productId)
  return p?.name ?? `Product #${productId}`
}

const mockTransferStore: TransferDetail[] = []

function seedMockTransfersOnce(): void {
  if (mockTransferStore.length > 0) return
  const now = new Date().toISOString()
  mockTransferStore.push({
    id: 'tr-seed-1',
    status: 'completed',
    fromWarehouseId: 1,
    toWarehouseId: 2,
    createdByUserId: 1,
    createdByUsername: 'admin',
    notes: 'Seed transfer (completed)',
    lines: [
      { productId: 1, quantity: 2, productName: mockProductName(1) },
      { productId: 2, quantity: 1, productName: mockProductName(2) },
    ],
    branchSalePriceForDestination: null,
    createdAtUtc: now,
    updatedAtUtc: now,
    auditTrail: [
      {
        id: newAuditId(),
        atUtc: now,
        kind: 'submitted',
        userId: 1,
        username: 'admin',
        note: 'Submitted for approval',
        toStatus: 'pending_approval',
      },
      {
        id: newAuditId(),
        atUtc: now,
        kind: 'approved',
        userId: 1,
        username: 'admin',
        fromStatus: 'pending_approval',
        toStatus: 'approved',
      },
      {
        id: newAuditId(),
        atUtc: now,
        kind: 'dispatched',
        userId: 1,
        fromStatus: 'approved',
        toStatus: 'in_transit',
      },
      {
        id: newAuditId(),
        atUtc: now,
        kind: 'stock_posted',
        userId: 1,
        note: 'Movement ids: 9100,9101',
        fromStatus: 'in_transit',
        toStatus: 'completed',
      },
    ],
    remoteLineIds: [9100, 9101],
  })
}

function transferMatchesFilters(t: TransferDetail, f?: TransferListFilters): boolean {
  if (!f) return true
  if (f.status && f.status !== 'all' && t.status !== f.status) return false
  if (f.warehouseId && f.warehouseId !== 'all') {
    if (t.fromWarehouseId !== f.warehouseId && t.toWarehouseId !== f.warehouseId) return false
  }
  const rowMs = Date.parse(t.createdAtUtc)
  if (f.dateFromUtc) {
    const fromMs = Date.parse(f.dateFromUtc)
    if (!Number.isNaN(fromMs) && !Number.isNaN(rowMs) && rowMs < fromMs) return false
  }
  if (f.dateToUtc) {
    const toMs = Date.parse(f.dateToUtc)
    if (!Number.isNaN(toMs) && !Number.isNaN(rowMs) && rowMs > toMs) return false
  }
  return true
}

function toSummary(t: TransferDetail): TransferSummary {
  return {
    id: t.id,
    status: t.status,
    fromWarehouseId: t.fromWarehouseId,
    toWarehouseId: t.toWarehouseId,
    createdByUserId: t.createdByUserId,
    createdByUsername: t.createdByUsername,
    notes: t.notes,
    createdAtUtc: t.createdAtUtc,
    lineCount: t.lines.length,
  }
}

/** Logistics grid (`GET api/Transfers/workflow` when live). */
export async function getTransfers(filters?: TransferListFilters): Promise<TransferSummary[]> {
  if (useInventoryMock) {
    seedMockTransfersOnce()
    return mockDelay(
      mockTransferStore.filter((t) => transferMatchesFilters(t, filters)).map(toSummary),
      40,
    )
  }
  const { data } = await http.get<TransferSummary[]>('/api/Transfers/workflow', {
    params: {
      status: filters?.status === 'all' ? undefined : filters?.status,
      warehouseId: filters?.warehouseId === 'all' ? undefined : filters?.warehouseId,
      dateFromUtc: filters?.dateFromUtc,
      dateToUtc: filters?.dateToUtc,
    },
  })
  return data ?? []
}

/** Full transfer + audit (`GET api/Transfers/workflow/{id}`). */
export async function getTransferById(id: string): Promise<TransferDetail | null> {
  if (useInventoryMock) {
    seedMockTransfersOnce()
    return mockDelay(mockTransferStore.find((t) => t.id === id) ?? null, 40)
  }
  try {
    const { data } = await http.get<TransferDetail>(`/api/Transfers/workflow/${id}`)
    return data
  } catch {
    return null
  }
}

/** Submit a new internal transfer request (starts at `pending_approval`). */
export async function createTransfer(input: CreateTransferWorkflowInput): Promise<{ id: string }> {
  if (useInventoryMock) {
    seedMockTransfersOnce()
    const id = `tr-${Date.now()}-${Math.random().toString(36).slice(2, 8)}`
    const now = new Date().toISOString()
    const lines: TransferLineView[] = input.lines.map((l) => ({
      ...l,
      productName: mockProductName(l.productId),
    }))
    mockTransferStore.unshift({
      id,
      status: 'pending_approval',
      fromWarehouseId: input.fromWarehouseId,
      toWarehouseId: input.toWarehouseId,
      createdByUserId: input.userId,
      createdByUsername: input.username,
      notes: input.notes,
      lines,
      branchSalePriceForDestination: input.branchSalePriceForDestination ?? null,
      createdAtUtc: now,
      updatedAtUtc: now,
      auditTrail: [
        {
          id: newAuditId(),
          atUtc: now,
          kind: 'submitted',
          userId: input.userId,
          username: input.username,
          note: 'Submitted for approval',
          toStatus: 'pending_approval',
        },
      ],
    })
    return mockDelay({ id }, 60)
  }
  const { data } = await http.post<{ id: string }>('/api/Transfers/workflow', input)
  return data
}

export type TransferActor = {
  userId: number
  username?: string
  note?: string
}

/** Manager approval gate (`POST api/Transfers/workflow/{id}/approve`). */
export async function approveTransfer(transferId: string, actor: TransferActor): Promise<TransferDetail> {
  if (useInventoryMock) {
    seedMockTransfersOnce()
    const t = mockTransferStore.find((x) => x.id === transferId)
    if (!t) throw new InventoryApiError('Transfer not found', 'NOT_FOUND')
    if (t.status !== 'pending_approval') {
      throw new InventoryApiError('Only pending transfers can be approved.', 'INVALID_STATE')
    }
    const now = new Date().toISOString()
    t.status = 'approved'
    t.updatedAtUtc = now
    t.auditTrail.push({
      id: newAuditId(),
      atUtc: now,
      kind: 'approved',
      userId: actor.userId,
      username: actor.username,
      note: actor.note,
      fromStatus: 'pending_approval',
      toStatus: 'approved',
    })
    return mockDelay(t, 50)
  }
  const { data } = await http.post<TransferDetail>(`/api/Transfers/workflow/${transferId}/approve`, actor)
  return data
}

/** Reject pending request (`POST api/Transfers/workflow/{id}/reject`). */
export async function rejectTransfer(transferId: string, actor: TransferActor): Promise<TransferDetail> {
  if (useInventoryMock) {
    seedMockTransfersOnce()
    const t = mockTransferStore.find((x) => x.id === transferId)
    if (!t) throw new InventoryApiError('Transfer not found', 'NOT_FOUND')
    if (t.status !== 'pending_approval') {
      throw new InventoryApiError('Only pending transfers can be rejected.', 'INVALID_STATE')
    }
    const now = new Date().toISOString()
    t.status = 'rejected'
    t.updatedAtUtc = now
    t.auditTrail.push({
      id: newAuditId(),
      atUtc: now,
      kind: 'rejected',
      userId: actor.userId,
      username: actor.username,
      note: actor.note ?? 'Rejected',
      fromStatus: 'pending_approval',
      toStatus: 'rejected',
    })
    return mockDelay(t, 50)
  }
  const { data } = await http.post<TransferDetail>(`/api/Transfers/workflow/${transferId}/reject`, actor)
  return data
}

/**
 * Two-phase completion: `approved` → `in_transit`, then `in_transit` → `completed` + stock lines posted.
 * Mirrors dispatch + goods receipt in one API for lean backends.
 */
export async function completeTransfer(transferId: string, actor: TransferActor): Promise<TransferDetail> {
  if (useInventoryMock) {
    seedMockTransfersOnce()
    const t = mockTransferStore.find((x) => x.id === transferId)
    if (!t) throw new InventoryApiError('Transfer not found', 'NOT_FOUND')
    const now = new Date().toISOString()

    if (t.status === 'approved') {
      t.status = 'in_transit'
      t.updatedAtUtc = now
      t.auditTrail.push({
        id: newAuditId(),
        atUtc: now,
        kind: 'dispatched',
        userId: actor.userId,
        username: actor.username,
        note: actor.note ?? 'Released to carrier / in transit',
        fromStatus: 'approved',
        toStatus: 'in_transit',
      })
      return mockDelay(t, 50)
    }

    if (t.status === 'in_transit') {
      const doc = buildTransferDocument({
        fromWarehouseId: t.fromWarehouseId,
        toWarehouseId: t.toWarehouseId,
        userId: t.createdByUserId,
        notes: t.notes,
        lines: t.lines.map((l) => ({ productId: l.productId, quantity: l.quantity })),
        branchSalePriceForDestination: t.branchSalePriceForDestination ?? undefined,
      })
      const ids = await executeTransferDocumentLines(doc)
      for (const line of t.lines) {
        bumpMockStockDelta(line.productId, t.fromWarehouseId, -line.quantity)
        bumpMockStockDelta(line.productId, t.toWarehouseId, line.quantity)
      }
      t.status = 'completed'
      t.updatedAtUtc = new Date().toISOString()
      t.remoteLineIds = ids
      t.auditTrail.push({
        id: newAuditId(),
        atUtc: t.updatedAtUtc,
        kind: 'stock_posted',
        userId: actor.userId,
        username: actor.username,
        note: `Posted movements: ${ids.join(', ')}`,
        fromStatus: 'in_transit',
        toStatus: 'completed',
      })
      t.auditTrail.push({
        id: newAuditId(),
        atUtc: t.updatedAtUtc,
        kind: 'completed',
        userId: actor.userId,
        username: actor.username,
        fromStatus: 'in_transit',
        toStatus: 'completed',
      })
      return mockDelay(t, 60)
    }

    throw new InventoryApiError('Transfer must be approved or in transit to complete.', 'INVALID_STATE')
  }
  const { data } = await http.post<TransferDetail>(`/api/Transfers/workflow/${transferId}/complete`, actor)
  return data
}
