import type { InventoryGridRow, InventorySortColumn, InventorySortDir } from '@/features/inventory/hooks/useInventory'
import { t } from '@/i18n'

type InventoryTableProps = {
  rows: InventoryGridRow[]
  sortColumn: InventorySortColumn
  sortDir: InventorySortDir
  onSort: (column: InventorySortColumn) => void
  onRowClick: (row: InventoryGridRow) => void
  loading?: boolean
}

function SortGlyph({ active, dir }: { active: boolean; dir: InventorySortDir }) {
  if (!active) return <span className="text-slate-600">↕</span>
  return <span className="text-sky-700">{dir === 'asc' ? '↑' : '↓'}</span>
}

function StatusBadge({ status }: { status: InventoryGridRow['status'] }) {
  if (status === 'out') {
    return (
      <span className="inline-flex rounded border border-rose-300 bg-rose-50 px-1.5 py-0.5 text-[10px] font-semibold uppercase tracking-wide text-rose-800">
        {t('inv.status.out')}
      </span>
    )
  }
  if (status === 'low') {
    return (
      <span className="inline-flex rounded border border-amber-300 bg-amber-50 px-1.5 py-0.5 text-[10px] font-semibold uppercase tracking-wide text-amber-900">
        {t('inv.status.low')}
      </span>
    )
  }
  return (
    <span className="inline-flex rounded border border-emerald-300 bg-emerald-50 px-1.5 py-0.5 text-[10px] font-semibold uppercase tracking-wide text-emerald-900">
      {t('inv.status.ok')}
    </span>
  )
}

function Th({
  label,
  column,
  active,
  dir,
  onSort,
  align = 'start',
}: {
  label: string
  column: InventorySortColumn
  active: boolean
  dir: InventorySortDir
  onSort: (c: InventorySortColumn) => void
  align?: 'start' | 'end'
}) {
  return (
    <th
      scope="col"
      className={[
        'border-b border-slate-200 bg-slate-100 px-2 py-2 font-semibold text-slate-600',
        align === 'end' ? 'text-end' : 'text-start',
      ].join(' ')}
    >
      <button
        type="button"
        onClick={() => onSort(column)}
        className="inline-flex items-center gap-1 text-[11px] uppercase tracking-wide hover:text-slate-800"
      >
        {label}
        <SortGlyph active={active} dir={dir} />
      </button>
    </th>
  )
}

export function InventoryTable({
  rows,
  sortColumn,
  sortDir,
  onSort,
  onRowClick,
  loading,
}: InventoryTableProps) {
  return (
    <div className="overflow-x-auto border border-slate-200 bg-white">
      <table className="min-w-full border-collapse text-start text-xs text-slate-800">
        <thead>
          <tr>
            <Th label={t('inv.col.product')} column="name" active={sortColumn === 'name'} dir={sortDir} onSort={onSort} />
            <Th label={t('inv.col.sku')} column="sku" active={sortColumn === 'sku'} dir={sortDir} onSort={onSort} />
            <Th label={t('inv.col.category')} column="category" active={sortColumn === 'category'} dir={sortDir} onSort={onSort} />
            <Th label={t('inv.col.cost')} column="cost" active={sortColumn === 'cost'} dir={sortDir} onSort={onSort} align="end" />
            <Th label={t('inv.col.sale')} column="sale" active={sortColumn === 'sale'} dir={sortDir} onSort={onSort} align="end" />
            <Th label={t('inv.col.totalQty')} column="total" active={sortColumn === 'total'} dir={sortDir} onSort={onSort} align="end" />
            <th
              scope="col"
              className="border-b border-slate-200 bg-slate-100 px-2 py-2 text-start text-[11px] font-semibold uppercase tracking-wide text-slate-600"
            >
              {t('inv.col.warehouses')}
            </th>
            <Th label={t('inv.col.status')} column="status" active={sortColumn === 'status'} dir={sortDir} onSort={onSort} />
          </tr>
        </thead>
        <tbody>
          {loading ? (
            <tr>
              <td colSpan={8} className="px-3 py-8 text-center text-slate-500">
                {t('inv.loading')}
              </td>
            </tr>
          ) : rows.length === 0 ? (
            <tr>
              <td colSpan={8} className="px-3 py-8 text-center text-slate-500">
                {t('inv.empty')}
              </td>
            </tr>
          ) : (
            rows.map((r) => (
              <tr
                key={r.productId}
                className="cursor-pointer border-b border-slate-200 odd:bg-slate-50 hover:bg-slate-100/60"
                onClick={() => onRowClick(r)}
              >
                <td className="max-w-[14rem] truncate px-2 py-1.5 font-medium text-slate-900">{r.name}</td>
                <td className="whitespace-nowrap px-2 py-1.5 font-mono text-slate-600">{r.sku}</td>
                <td className="whitespace-nowrap px-2 py-1.5 text-slate-600">{r.category}</td>
                <td className="whitespace-nowrap px-2 py-1.5 text-end tabular-nums text-slate-700">
                  {r.costUnitApprox != null ? r.costUnitApprox.toLocaleString() : '—'}
                </td>
                <td className="whitespace-nowrap px-2 py-1.5 text-end tabular-nums text-slate-700">
                  {r.saleUnitPrice.toLocaleString()}
                </td>
                <td className="whitespace-nowrap px-2 py-1.5 text-end tabular-nums text-slate-900">{r.totalQty.toLocaleString()}</td>
                <td className="max-w-[18rem] truncate px-2 py-1.5 text-slate-500" title={r.breakdown}>
                  {r.breakdown}
                </td>
                <td className="whitespace-nowrap px-2 py-1.5">
                  <StatusBadge status={r.status} />
                </td>
              </tr>
            ))
          )}
        </tbody>
      </table>
    </div>
  )
}
