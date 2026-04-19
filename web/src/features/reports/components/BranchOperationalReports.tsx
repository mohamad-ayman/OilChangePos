import type { ReactNode } from 'react'
import { useMemo, useState } from 'react'
import { useQueries, useQuery } from '@tanstack/react-query'
import { branchReportKeys } from '@/features/reports/services/branchReportQueryKeys'
import {
  getBranchExpenses,
  getBranchIncomingRegister,
  getBranchProfitRollup,
  getBranchSalesLineRegister,
  getBranchSellerSummaries,
  getBranchTransferLedger,
} from '@/shared/api/branchReports.api'
import { getLowStockItems } from '@/shared/api/inventory.api'
import { t } from '@/i18n'

function toYmd(d: Date): string {
  const y = d.getFullYear()
  const m = String(d.getMonth() + 1).padStart(2, '0')
  const day = String(d.getDate()).padStart(2, '0')
  return `${y}-${m}-${day}`
}

function defaultRange(): { from: string; to: string } {
  const to = new Date()
  const from = new Date()
  from.setDate(from.getDate() - 29)
  return { from: toYmd(from), to: toYmd(to) }
}

function money(n: number) {
  return n.toLocaleString(undefined, { maximumFractionDigits: 2 })
}

function fmtDateTime(iso: string) {
  try {
    return new Date(iso).toLocaleString()
  } catch {
    return iso
  }
}

function downloadCsv(filename: string, headers: string[], rows: (string | number)[][]) {
  const esc = (c: string | number) => {
    const s = String(c)
    if (s.includes('"') || s.includes(',') || s.includes('\n') || s.includes('\r')) return `"${s.replace(/"/g, '""')}"`
    return s
  }
  const lines = [headers.map(esc).join(','), ...rows.map((r) => r.map(esc).join(','))]
  const blob = new Blob([`\uFEFF${lines.join('\n')}`], { type: 'text/csv;charset=utf-8;' })
  const url = URL.createObjectURL(blob)
  const a = document.createElement('a')
  a.href = url
  a.download = filename
  a.click()
  URL.revokeObjectURL(url)
}

type TabId = 'sales_lines' | 'incoming' | 'transfers' | 'expenses' | 'sellers' | 'low_stock'

const tabs: { id: TabId; labelKey: string }[] = [
  { id: 'sales_lines', labelKey: 'rep.branch.tab.salesRegister' },
  { id: 'incoming', labelKey: 'rep.branch.tab.incoming' },
  { id: 'transfers', labelKey: 'rep.branch.tab.transfers' },
  { id: 'expenses', labelKey: 'rep.branch.tab.expenses' },
  { id: 'sellers', labelKey: 'rep.branch.tab.sellers' },
  { id: 'low_stock', labelKey: 'rep.branch.tab.lowStock' },
]

type BranchOperationalReportsProps = {
  warehouseId: number
  /** Renders between the title block and the date range toolbar (e.g. admin warehouse selector). */
  topBar?: ReactNode
  className?: string
}

export function BranchOperationalReports({ warehouseId, topBar, className }: BranchOperationalReportsProps) {
  const initial = useMemo(() => defaultRange(), [])
  const [from, setFrom] = useState(initial.from)
  const [to, setTo] = useState(initial.to)
  const [applied, setApplied] = useState(initial)
  const [tab, setTab] = useState<TabId>('sales_lines')

  const rangeOk = applied.from <= applied.to

  const profitRollupQ = useQuery({
    queryKey: branchReportKeys.profitRollup(warehouseId, applied.from, applied.to),
    queryFn: () => getBranchProfitRollup(applied.from, applied.to, warehouseId),
    enabled: rangeOk && warehouseId > 0,
    staleTime: 60_000,
  })

  const [salesQ, incomingQ, transfersQ, expensesQ, sellersQ, lowQ] = useQueries({
    queries: [
      {
        queryKey: branchReportKeys.salesLines(warehouseId, applied.from, applied.to),
        queryFn: () => getBranchSalesLineRegister(applied.from, applied.to, warehouseId),
        enabled: rangeOk && warehouseId > 0 && tab === 'sales_lines',
        staleTime: 60_000,
      },
      {
        queryKey: branchReportKeys.incoming(warehouseId, applied.from, applied.to),
        queryFn: () => getBranchIncomingRegister(applied.from, applied.to, warehouseId),
        enabled: rangeOk && warehouseId > 0 && tab === 'incoming',
        staleTime: 60_000,
      },
      {
        queryKey: branchReportKeys.transfers(warehouseId, applied.from, applied.to),
        queryFn: () => getBranchTransferLedger(applied.from, applied.to, warehouseId),
        enabled: rangeOk && warehouseId > 0 && tab === 'transfers',
        staleTime: 60_000,
      },
      {
        queryKey: branchReportKeys.expenses(warehouseId, applied.from, applied.to),
        queryFn: () => getBranchExpenses(applied.from, applied.to, warehouseId),
        enabled: rangeOk && warehouseId > 0 && tab === 'expenses',
        staleTime: 60_000,
      },
      {
        queryKey: branchReportKeys.sellers(warehouseId, applied.from, applied.to),
        queryFn: () => getBranchSellerSummaries(applied.from, applied.to, warehouseId),
        enabled: rangeOk && warehouseId > 0 && tab === 'sellers',
        staleTime: 60_000,
      },
      {
        queryKey: branchReportKeys.lowStock(warehouseId),
        queryFn: () => getLowStockItems(warehouseId),
        enabled: warehouseId > 0 && tab === 'low_stock',
        staleTime: 60_000,
      },
    ],
  })

  const activeQuery =
    tab === 'sales_lines'
      ? salesQ
      : tab === 'incoming'
        ? incomingQ
        : tab === 'transfers'
          ? transfersQ
          : tab === 'expenses'
            ? expensesQ
            : tab === 'sellers'
              ? sellersQ
              : lowQ

  const loadErr = activeQuery.isError ? activeQuery.error : undefined

  function applyRange() {
    setApplied({ from, to })
  }

  function resetRange() {
    const d = defaultRange()
    setFrom(d.from)
    setTo(d.to)
    setApplied(d)
  }

  function exportActive() {
    const suffix = `${applied.from}_${applied.to}`
    switch (tab) {
      case 'sales_lines':
        downloadCsv(
          `branch-sales-lines-${suffix}.csv`,
          ['invoiceDateUtc', 'invoiceNumber', 'customer', 'seller', 'product', 'qty', 'unitPrice', 'lineTotal', 'subtotal', 'discount', 'total'],
          (salesQ.data ?? []).map((r) => [
            r.invoiceDateUtc,
            r.invoiceNumber,
            r.customerDisplay,
            r.sellerUsername,
            r.productName,
            r.quantity,
            r.unitPrice,
            r.lineTotal,
            r.invoiceSubtotal,
            r.invoiceDiscount,
            r.invoiceTotal,
          ]),
        )
        break
      case 'incoming':
        downloadCsv(
          `branch-incoming-${suffix}.csv`,
          ['entryDateUtc', 'entryType', 'product', 'quantity', 'amount', 'source', 'notes', 'by'],
          (incomingQ.data ?? []).map((r) => [
            r.entryDateUtc,
            r.entryType,
            r.productName,
            r.quantity,
            r.amountValue,
            r.sourceDetail,
            r.notes ?? '',
            r.createdByDisplay,
          ]),
        )
        break
      case 'transfers':
        downloadCsv(
          `branch-transfers-${suffix}.csv`,
          ['movementUtc', 'product', 'quantity', 'from', 'to', 'notes'],
          (transfersQ.data ?? []).map((r) => [
            r.movementUtc,
            r.productName,
            r.quantity,
            r.fromWarehouseName,
            r.toWarehouseName,
            r.notes ?? '',
          ]),
        )
        break
      case 'expenses':
        downloadCsv(
          `branch-expenses-${suffix}.csv`,
          ['dateUtc', 'amount', 'category', 'description', 'warehouse', 'by'],
          (expensesQ.data ?? []).map((r) => [
            r.expenseDateUtc,
            r.amount,
            r.category,
            r.description,
            r.warehouseName ?? '',
            r.createdByUsername ?? '',
          ]),
        )
        break
      case 'sellers':
        downloadCsv(
          `branch-sellers-${suffix}.csv`,
          ['seller', 'invoices', 'lines', 'gross', 'discount', 'net'],
          (sellersQ.data ?? []).map((r) => [
            r.sellerUsername,
            r.invoiceCount,
            r.lineItemCount,
            r.invoicesGrossSubtotal,
            r.invoicesDiscountTotal,
            r.invoicesNetTotal,
          ]),
        )
        break
      case 'low_stock':
        downloadCsv(`branch-low-stock-${warehouseId}.csv`, ['productId', 'product', 'onHand', 'threshold'], (lowQ.data ?? []).map((r) => [r.productId, r.productName, r.currentStock, r.threshold]))
        break
      default:
        break
    }
  }

  return (
    <section
      className={[
        'overflow-hidden rounded-2xl border border-slate-200/90 bg-white shadow-md shadow-slate-900/5 ring-1 ring-slate-900/[0.04]',
        className ?? '',
      ]
        .filter(Boolean)
        .join(' ')}
    >
      <header className="border-b border-slate-200 bg-gradient-to-br from-slate-50 via-white to-sky-50/40 px-4 py-4 sm:px-5">
        <h2 className="text-base font-bold tracking-tight text-slate-900 sm:text-lg">{t('rep.branch.operationalTitle')}</h2>
        <p className="mt-1 max-w-3xl text-xs leading-relaxed text-slate-600 sm:text-sm">{t('rep.branch.operationalSubtitle')}</p>
      </header>

      {topBar ? <div className="border-b border-slate-200 bg-slate-50/80 px-4 py-3 sm:px-5">{topBar}</div> : null}

      <div className="flex flex-col gap-3 border-b border-slate-200 px-4 py-3 sm:flex-row sm:flex-wrap sm:items-end sm:px-5">
        <div className="flex flex-col gap-1">
          <label className="text-[11px] font-medium text-slate-600" htmlFor="br-from">
            {t('rep.branch.dateFrom')}
          </label>
          <input
            id="br-from"
            type="date"
            value={from}
            onChange={(e) => setFrom(e.target.value)}
            className="h-10 min-w-[10.5rem] rounded-lg border border-slate-300 bg-white px-2.5 text-sm text-slate-900 shadow-sm"
          />
        </div>
        <div className="flex flex-col gap-1">
          <label className="text-[11px] font-medium text-slate-600" htmlFor="br-to">
            {t('rep.branch.dateTo')}
          </label>
          <input
            id="br-to"
            type="date"
            value={to}
            onChange={(e) => setTo(e.target.value)}
            className="h-10 min-w-[10.5rem] rounded-lg border border-slate-300 bg-white px-2.5 text-sm text-slate-900 shadow-sm"
          />
        </div>
        <button
          type="button"
          disabled={from > to}
          onClick={applyRange}
          className="h-10 rounded-lg border border-sky-600 bg-sky-600 px-4 text-sm font-semibold text-white shadow-sm hover:bg-sky-700 disabled:cursor-not-allowed disabled:opacity-50"
        >
          {t('rep.branch.applyRange')}
        </button>
        <button
          type="button"
          onClick={resetRange}
          className="h-10 rounded-lg border border-slate-300 bg-white px-4 text-sm font-medium text-slate-800 shadow-sm hover:bg-slate-50"
        >
          {t('common.reset')}
        </button>
        <button
          type="button"
          onClick={exportActive}
          disabled={activeQuery.isPending || activeQuery.isError}
          className="h-10 rounded-lg border border-slate-400 bg-white px-4 text-sm font-medium text-slate-800 shadow-sm hover:bg-slate-50 disabled:opacity-50"
        >
          {t('rep.branch.exportCsv')}
        </button>
      </div>

      {from > to ? <p className="px-4 pb-2 text-sm text-rose-700 sm:px-5">{t('rep.branch.rangeInvalid')}</p> : null}
      {loadErr ? (
        <p className="px-4 pb-2 text-sm text-rose-700 sm:px-5">
          {t('common.error')}: {loadErr instanceof Error ? loadErr.message : String(loadErr)}
        </p>
      ) : null}

      {rangeOk && warehouseId > 0 ? (
        <div className="border-b border-slate-200 bg-slate-50/90 px-4 py-3 sm:px-5">
          <h3 className="text-xs font-bold uppercase tracking-wide text-slate-700">{t('rep.branch.plTitle')}</h3>
          <p className="mt-1 text-[11px] leading-relaxed text-slate-600">{t('rep.branch.plSubtitle')}</p>
          {profitRollupQ.isPending ? (
            <p className="mt-2 text-xs text-slate-500">{t('common.loading')}</p>
          ) : profitRollupQ.isError ? (
            <p className="mt-2 text-xs text-rose-700">
              {t('common.error')}: {profitRollupQ.error instanceof Error ? profitRollupQ.error.message : String(profitRollupQ.error)}
            </p>
          ) : profitRollupQ.data ? (
            <>
              <dl className="mt-3 grid gap-2 sm:grid-cols-2 lg:grid-cols-5">
                <div className="rounded-lg border border-slate-200/80 bg-white px-3 py-2 shadow-sm">
                  <dt className="text-[10px] font-semibold uppercase tracking-wide text-slate-500">{t('rep.branch.plRevenue')}</dt>
                  <dd className="mt-0.5 font-mono text-sm font-semibold text-slate-900">{money(profitRollupQ.data.totalRevenue)}</dd>
                </div>
                <div className="rounded-lg border border-slate-200/80 bg-white px-3 py-2 shadow-sm">
                  <dt className="text-[10px] font-semibold uppercase tracking-wide text-slate-500">{t('rep.branch.plCogs')}</dt>
                  <dd className="mt-0.5 font-mono text-sm font-semibold text-slate-900">{money(profitRollupQ.data.totalEstimatedCogs)}</dd>
                </div>
                <div className="rounded-lg border border-slate-200/80 bg-white px-3 py-2 shadow-sm">
                  <dt className="text-[10px] font-semibold uppercase tracking-wide text-slate-500">{t('rep.branch.plGross')}</dt>
                  <dd className="mt-0.5 font-mono text-sm font-semibold text-slate-900">{money(profitRollupQ.data.totalEstimatedGrossProfit)}</dd>
                </div>
                <div className="rounded-lg border border-slate-200/80 bg-white px-3 py-2 shadow-sm">
                  <dt className="text-[10px] font-semibold uppercase tracking-wide text-slate-500">{t('rep.branch.plExpenses')}</dt>
                  <dd className="mt-0.5 font-mono text-sm font-semibold text-slate-900">{money(profitRollupQ.data.totalOperatingExpenses)}</dd>
                </div>
                <div className="rounded-lg border border-sky-200 bg-sky-50 px-3 py-2 shadow-sm ring-1 ring-sky-100">
                  <dt className="text-[10px] font-semibold uppercase tracking-wide text-sky-900">{t('rep.branch.plNet')}</dt>
                  <dd className="mt-0.5 font-mono text-sm font-bold text-sky-950">{money(profitRollupQ.data.netProfitAfterExpenses)}</dd>
                </div>
              </dl>
              {profitRollupQ.data.containsEstimatedCost ? (
                <p className="mt-2 text-[11px] text-amber-800">{t('rep.branch.plEstimatedCogs')}</p>
              ) : null}
            </>
          ) : null}
        </div>
      ) : null}

      <div className="-mx-px flex gap-1 overflow-x-auto border-b border-slate-200 px-3 py-2.5 sm:px-4">
        {tabs.map((x) => (
          <button
            key={x.id}
            type="button"
            onClick={() => setTab(x.id)}
            className={[
              'shrink-0 whitespace-nowrap rounded-lg px-3 py-2 text-sm font-medium transition-colors',
              tab === x.id
                ? 'bg-sky-600 text-white shadow-sm ring-1 ring-sky-700/30'
                : 'bg-slate-100 text-slate-700 hover:bg-slate-200',
            ].join(' ')}
          >
            {t(x.labelKey)}
          </button>
        ))}
      </div>

      <div className="min-h-[min(52vh,28rem)] max-h-[min(70vh,40rem)] overflow-auto p-3 sm:p-4">
        {activeQuery.isPending ? (
          <p className="px-2 py-10 text-center text-sm text-slate-500">{t('common.loading')}</p>
        ) : null}

        {tab === 'sales_lines' && !salesQ.isPending ? (
          <table className="w-full min-w-[56rem] border-collapse text-start text-xs">
            <thead className="sticky top-0 z-10 bg-slate-100 text-[11px] font-semibold uppercase tracking-wide text-slate-600 shadow-sm">
              <tr>
                <th className="border-b px-2 py-1.5">{t('rep.branch.col.when')}</th>
                <th className="border-b px-2 py-1.5">{t('rep.branch.col.invoice')}</th>
                <th className="border-b px-2 py-1.5">{t('rep.branch.col.customer')}</th>
                <th className="border-b px-2 py-1.5">{t('rep.branch.col.seller')}</th>
                <th className="border-b px-2 py-1.5">{t('rep.branch.col.product')}</th>
                <th className="border-b px-2 py-1.5 text-end">{t('rep.branch.col.qty')}</th>
                <th className="border-b px-2 py-1.5 text-end">{t('rep.branch.col.unit')}</th>
                <th className="border-b px-2 py-1.5 text-end">{t('rep.branch.col.line')}</th>
              </tr>
            </thead>
            <tbody>
              {(salesQ.data ?? []).length === 0 ? (
                <tr>
                  <td colSpan={8} className="px-2 py-6 text-center text-slate-500">
                    {t('rep.branch.empty')}
                  </td>
                </tr>
              ) : (
                (salesQ.data ?? []).map((r, i) => (
                  <tr key={`${r.invoiceNumber}-${i}`} className="border-b border-slate-100 odd:bg-slate-50/80">
                    <td className="whitespace-nowrap px-2 py-1 text-slate-600">{fmtDateTime(r.invoiceDateUtc)}</td>
                    <td className="px-2 py-1 font-mono text-slate-800">{r.invoiceNumber}</td>
                    <td className="max-w-[10rem] truncate px-2 py-1">{r.customerDisplay}</td>
                    <td className="px-2 py-1">{r.sellerUsername}</td>
                    <td className="max-w-[12rem] truncate px-2 py-1 font-medium text-slate-900">{r.productName}</td>
                    <td className="px-2 py-1 text-end tabular-nums">{r.quantity}</td>
                    <td className="px-2 py-1 text-end tabular-nums">{money(r.unitPrice)}</td>
                    <td className="px-2 py-1 text-end tabular-nums font-medium">{money(r.lineTotal)}</td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        ) : null}

        {tab === 'incoming' && !incomingQ.isPending ? (
          <table className="w-full min-w-[44rem] border-collapse text-start text-xs">
            <thead className="sticky top-0 z-10 bg-slate-100 text-[11px] font-semibold uppercase tracking-wide text-slate-600 shadow-sm">
              <tr>
                <th className="border-b px-2 py-1.5">{t('rep.branch.col.when')}</th>
                <th className="border-b px-2 py-1.5">{t('rep.branch.col.type')}</th>
                <th className="border-b px-2 py-1.5">{t('rep.branch.col.product')}</th>
                <th className="border-b px-2 py-1.5 text-end">{t('rep.branch.col.qty')}</th>
                <th className="border-b px-2 py-1.5 text-end">{t('rep.branch.col.amount')}</th>
                <th className="border-b px-2 py-1.5">{t('rep.branch.col.source')}</th>
              </tr>
            </thead>
            <tbody>
              {(incomingQ.data ?? []).length === 0 ? (
                <tr>
                  <td colSpan={6} className="px-2 py-6 text-center text-slate-500">
                    {t('rep.branch.empty')}
                  </td>
                </tr>
              ) : (
                (incomingQ.data ?? []).map((r, i) => (
                  <tr key={`${r.entryDateUtc}-${i}`} className="border-b border-slate-100 odd:bg-slate-50/80">
                    <td className="whitespace-nowrap px-2 py-1 text-slate-600">{fmtDateTime(r.entryDateUtc)}</td>
                    <td className="px-2 py-1">{r.entryType}</td>
                    <td className="max-w-[14rem] truncate px-2 py-1 font-medium">{r.productName}</td>
                    <td className="px-2 py-1 text-end tabular-nums">{r.quantity}</td>
                    <td className="px-2 py-1 text-end tabular-nums">{money(r.amountValue)}</td>
                    <td className="max-w-[16rem] truncate px-2 py-1 text-slate-600">{r.sourceDetail}</td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        ) : null}

        {tab === 'transfers' && !transfersQ.isPending ? (
          <table className="w-full min-w-[40rem] border-collapse text-start text-xs">
            <thead className="sticky top-0 z-10 bg-slate-100 text-[11px] font-semibold uppercase tracking-wide text-slate-600 shadow-sm">
              <tr>
                <th className="border-b px-2 py-1.5">{t('rep.branch.col.when')}</th>
                <th className="border-b px-2 py-1.5">{t('rep.branch.col.product')}</th>
                <th className="border-b px-2 py-1.5 text-end">{t('rep.branch.col.qty')}</th>
                <th className="border-b px-2 py-1.5">{t('rep.branch.col.fromWh')}</th>
                <th className="border-b px-2 py-1.5">{t('rep.branch.col.toWh')}</th>
              </tr>
            </thead>
            <tbody>
              {(transfersQ.data ?? []).length === 0 ? (
                <tr>
                  <td colSpan={5} className="px-2 py-6 text-center text-slate-500">
                    {t('rep.branch.empty')}
                  </td>
                </tr>
              ) : (
                (transfersQ.data ?? []).map((r, i) => (
                  <tr key={`${r.movementUtc}-${i}`} className="border-b border-slate-100 odd:bg-slate-50/80">
                    <td className="whitespace-nowrap px-2 py-1 text-slate-600">{fmtDateTime(r.movementUtc)}</td>
                    <td className="max-w-[14rem] truncate px-2 py-1 font-medium">{r.productName}</td>
                    <td className="px-2 py-1 text-end tabular-nums">{r.quantity}</td>
                    <td className="px-2 py-1">{r.fromWarehouseName}</td>
                    <td className="px-2 py-1">{r.toWarehouseName}</td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        ) : null}

        {tab === 'expenses' && !expensesQ.isPending ? (
          <table className="w-full min-w-[40rem] border-collapse text-start text-xs">
            <thead className="sticky top-0 z-10 bg-slate-100 text-[11px] font-semibold uppercase tracking-wide text-slate-600 shadow-sm">
              <tr>
                <th className="border-b px-2 py-1.5">{t('rep.branch.col.when')}</th>
                <th className="border-b px-2 py-1.5 text-end">{t('rep.branch.col.amount')}</th>
                <th className="border-b px-2 py-1.5">{t('rep.branch.col.category')}</th>
                <th className="border-b px-2 py-1.5">{t('rep.branch.col.description')}</th>
                <th className="border-b px-2 py-1.5">{t('rep.branch.col.by')}</th>
              </tr>
            </thead>
            <tbody>
              {(expensesQ.data ?? []).length === 0 ? (
                <tr>
                  <td colSpan={5} className="px-2 py-6 text-center text-slate-500">
                    {t('rep.branch.empty')}
                  </td>
                </tr>
              ) : (
                (expensesQ.data ?? []).map((r) => (
                  <tr key={r.id} className="border-b border-slate-100 odd:bg-slate-50/80">
                    <td className="whitespace-nowrap px-2 py-1 text-slate-600">{fmtDateTime(r.expenseDateUtc)}</td>
                    <td className="px-2 py-1 text-end tabular-nums font-medium">{money(r.amount)}</td>
                    <td className="px-2 py-1">{r.category}</td>
                    <td className="max-w-[18rem] truncate px-2 py-1">{r.description}</td>
                    <td className="px-2 py-1 text-slate-600">{r.createdByUsername ?? '—'}</td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        ) : null}

        {tab === 'sellers' && !sellersQ.isPending ? (
          <table className="w-full min-w-[36rem] border-collapse text-start text-xs">
            <thead className="sticky top-0 z-10 bg-slate-100 text-[11px] font-semibold uppercase tracking-wide text-slate-600 shadow-sm">
              <tr>
                <th className="border-b px-2 py-1.5">{t('rep.branch.col.seller')}</th>
                <th className="border-b px-2 py-1.5 text-end">{t('rep.branch.col.invoices')}</th>
                <th className="border-b px-2 py-1.5 text-end">{t('rep.branch.col.lines')}</th>
                <th className="border-b px-2 py-1.5 text-end">{t('rep.branch.col.net')}</th>
              </tr>
            </thead>
            <tbody>
              {(sellersQ.data ?? []).length === 0 ? (
                <tr>
                  <td colSpan={4} className="px-2 py-6 text-center text-slate-500">
                    {t('rep.branch.empty')}
                  </td>
                </tr>
              ) : (
                (sellersQ.data ?? []).map((r) => (
                  <tr key={r.sellerUsername} className="border-b border-slate-100 odd:bg-slate-50/80">
                    <td className="px-2 py-1 font-medium">{r.sellerUsername}</td>
                    <td className="px-2 py-1 text-end tabular-nums">{r.invoiceCount}</td>
                    <td className="px-2 py-1 text-end tabular-nums">{r.lineItemCount}</td>
                    <td className="px-2 py-1 text-end tabular-nums font-medium">{money(r.invoicesNetTotal)}</td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        ) : null}

        {tab === 'low_stock' && !lowQ.isPending ? (
          <table className="w-full min-w-[28rem] border-collapse text-start text-xs">
            <thead className="sticky top-0 z-10 bg-slate-100 text-[11px] font-semibold uppercase tracking-wide text-slate-600 shadow-sm">
              <tr>
                <th className="border-b px-2 py-1.5">{t('rep.branch.col.product')}</th>
                <th className="border-b px-2 py-1.5 text-end">{t('rep.branch.col.onHand')}</th>
                <th className="border-b px-2 py-1.5 text-end">{t('rep.branch.col.threshold')}</th>
              </tr>
            </thead>
            <tbody>
              {(lowQ.data ?? []).length === 0 ? (
                <tr>
                  <td colSpan={3} className="px-2 py-6 text-center text-slate-500">
                    {t('rep.branch.lowStockEmpty')}
                  </td>
                </tr>
              ) : (
                (lowQ.data ?? []).map((r) => (
                  <tr key={r.productId} className="border-b border-slate-100 odd:bg-slate-50/80">
                    <td className="px-2 py-1 font-medium">{r.productName}</td>
                    <td className="px-2 py-1 text-end tabular-nums text-rose-800">{r.currentStock}</td>
                    <td className="px-2 py-1 text-end tabular-nums text-slate-600">{r.threshold}</td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        ) : null}
      </div>
    </section>
  )
}
