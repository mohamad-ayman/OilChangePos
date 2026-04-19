import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useMemo, useState } from 'react'
import { WarehouseType, type Warehouse } from '@/entities/warehouse'
import { getWarehouses } from '@/shared/api/inventory.api'
import { getExpensesReport, recordExpense, type ExpenseReportRow, type RecordExpenseBody } from '@/shared/api/expenses.api'
import { useAuthStore } from '@/shared/store/auth.store'
import { t } from '@/i18n'

const expenseListRootKey = ['expenses', 'list'] as const

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

function parseApiError(err: unknown): string {
  if (err && typeof err === 'object' && 'response' in err) {
    const r = err as { response?: { data?: unknown } }
    const d = r.response?.data
    if (typeof d === 'string' && d.trim()) return d
    if (d && typeof d === 'object') {
      if ('detail' in d && typeof (d as { detail: string }).detail === 'string' && (d as { detail: string }).detail.trim()) {
        return (d as { detail: string }).detail
      }
      if ('message' in d && typeof (d as { message: string }).message === 'string') {
        return (d as { message: string }).message
      }
    }
  }
  if (err instanceof Error) return err.message
  return String(err)
}

export function ExpensesPage() {
  const user = useAuthStore((s) => s.user)
  const qc = useQueryClient()
  const isAdmin = user?.role === 'admin'
  const homeId = user?.homeBranchWarehouseId ?? null

  const initial = useMemo(() => defaultRange(), [])
  const [from, setFrom] = useState(initial.from)
  const [to, setTo] = useState(initial.to)
  const [applied, setApplied] = useState(initial)

  /** Admin: which rows to load — all sites, or one warehouse (includes null-warehouse rows for that filter per API). */
  const [listWarehouseId, setListWarehouseId] = useState<number | 'all'>(isAdmin ? 'all' : homeId ?? 'all')

  const [amount, setAmount] = useState('')
  const [category, setCategory] = useState('')
  const [description, setDescription] = useState('')
  const [expenseDate, setExpenseDate] = useState(toYmd(new Date()))
  const [formWarehouseId, setFormWarehouseId] = useState<number | 'company'>(
    isAdmin ? 'company' : homeId != null ? homeId : 'company',
  )

  const warehousesQ = useQuery({
    queryKey: ['warehouses', 'expenses-page'],
    queryFn: getWarehouses,
    staleTime: 300_000,
  })

  const warehouses = warehousesQ.data ?? []
  const whById = useMemo(() => new Map(warehouses.map((w: Warehouse) => [w.id, w])), [warehouses])

  const rangeOk = applied.from <= applied.to
  const canRecord = isAdmin || (user?.role === 'manager' || user?.role === 'cashier') ? homeId != null : false

  const listParams = useMemo(() => {
    if (!rangeOk) return null
    if (isAdmin) {
      if (listWarehouseId === 'all') return { from: applied.from, to: applied.to } as const
      return { from: applied.from, to: applied.to, warehouseId: listWarehouseId as number } as const
    }
    if (homeId == null) return null
    return { from: applied.from, to: applied.to, warehouseId: homeId } as const
  }, [applied.from, applied.to, rangeOk, isAdmin, listWarehouseId, homeId])

  const expensesQ = useQuery({
    queryKey: [...expenseListRootKey, listParams?.from, listParams?.to, listParams && 'warehouseId' in listParams ? listParams.warehouseId : 'all'],
    queryFn: () => {
      if (!listParams) return Promise.resolve([] as ExpenseReportRow[])
      if ('warehouseId' in listParams) return getExpensesReport(listParams.from, listParams.to, listParams.warehouseId)
      return getExpensesReport(listParams.from, listParams.to)
    },
    enabled: listParams != null,
    staleTime: 30_000,
  })

  const recordMut = useMutation({
    mutationFn: (body: RecordExpenseBody) => recordExpense(body),
    onSuccess: async () => {
      await qc.invalidateQueries({ queryKey: expenseListRootKey })
      setAmount('')
      setCategory('')
      setDescription('')
    },
  })

  function applyRange() {
    setApplied({ from, to })
  }

  function resetRange() {
    const d = defaultRange()
    setFrom(d.from)
    setTo(d.to)
    setApplied(d)
  }

  function submit(e: React.FormEvent) {
    e.preventDefault()
    const amt = Number(amount.replace(/,/g, '.'))
    if (!Number.isFinite(amt) || amt <= 0) return

    let wid: number | null
    if (isAdmin) {
      wid = formWarehouseId === 'company' ? null : formWarehouseId
    } else {
      if (homeId == null) return
      wid = homeId
    }

    recordMut.mutate({
      amount: amt,
      category: category.trim(),
      description: description.trim(),
      expenseDateLocal: expenseDate,
      warehouseId: wid,
    })
  }

  if (!user) return null

  return (
    <div className="mx-auto max-w-5xl space-y-6 px-3 py-4 sm:px-4 sm:py-6">
      <header className="rounded-2xl border border-slate-200/90 bg-white/90 p-5 shadow-sm shadow-slate-900/[0.04] ring-1 ring-slate-900/[0.02] sm:p-6">
        <h1 className="text-lg font-bold tracking-tight text-slate-900 sm:text-xl">{t('expenses.page.title')}</h1>
        <p className="mt-1 text-sm text-slate-600">{t('expenses.page.subtitle')}</p>
      </header>

      {!canRecord && (user.role === 'manager' || user.role === 'cashier') ? (
        <p className="rounded-xl border border-amber-200 bg-amber-50 px-4 py-3 text-sm text-amber-950">{t('expenses.page.noBranch')}</p>
      ) : null}

      <section className="rounded-2xl border border-slate-200 bg-white p-4 shadow-sm sm:p-6">
        <h2 className="text-base font-bold text-slate-900">{t('expenses.form.title')}</h2>
        <form onSubmit={submit} className="mt-4 grid gap-4 sm:grid-cols-2">
          <div className="sm:col-span-2">
            <label className="block text-xs font-semibold text-slate-600" htmlFor="ex-date">
              {t('expenses.form.date')}
            </label>
            <input
              id="ex-date"
              type="date"
              required
              value={expenseDate}
              onChange={(e) => setExpenseDate(e.target.value)}
              className="mt-1 h-10 w-full max-w-xs rounded-lg border border-slate-300 px-2 text-sm"
            />
          </div>

          {isAdmin ? (
            <div className="sm:col-span-2">
              <label className="block text-xs font-semibold text-slate-600" htmlFor="ex-wh">
                {t('expenses.form.warehouse')}
              </label>
              <select
                id="ex-wh"
                value={formWarehouseId === 'company' ? '' : String(formWarehouseId)}
                onChange={(e) => {
                  const v = e.target.value
                  setFormWarehouseId(v === '' ? 'company' : Number(v))
                }}
                className="mt-1 h-10 w-full max-w-md rounded-lg border border-slate-300 bg-white px-2 text-sm"
              >
                <option value="">{t('expenses.form.warehouseCompany')}</option>
                {warehouses
                  .filter((w) => w.isActive)
                  .map((w) => (
                    <option key={w.id} value={w.id}>
                      {w.name}
                      {w.type === WarehouseType.Main
                        ? ` (${t('expenses.form.siteMain')})`
                        : ` (${t('expenses.form.siteBranch')})`}
                    </option>
                  ))}
              </select>
              <p className="mt-1 text-[11px] text-slate-500">{t('expenses.form.warehouseHint')}</p>
            </div>
          ) : homeId != null ? (
            <div className="sm:col-span-2 rounded-lg border border-slate-200 bg-slate-50 px-3 py-2 text-sm text-slate-800">
              <span className="font-semibold">{t('expenses.form.warehouse')}: </span>
              {whById.get(homeId)?.name ?? `#${homeId}`}
            </div>
          ) : null}

          <div>
            <label className="block text-xs font-semibold text-slate-600" htmlFor="ex-amt">
              {t('expenses.form.amount')}
            </label>
            <input
              id="ex-amt"
              type="text"
              inputMode="decimal"
              required
              value={amount}
              onChange={(e) => setAmount(e.target.value)}
              className="mt-1 h-10 w-full rounded-lg border border-slate-300 px-2 text-sm"
              placeholder="0.00"
            />
          </div>
          <div>
            <label className="block text-xs font-semibold text-slate-600" htmlFor="ex-cat">
              {t('expenses.form.category')}
            </label>
            <input
              id="ex-cat"
              type="text"
              required
              maxLength={80}
              value={category}
              onChange={(e) => setCategory(e.target.value)}
              className="mt-1 h-10 w-full rounded-lg border border-slate-300 px-2 text-sm"
            />
          </div>
          <div className="sm:col-span-2">
            <label className="block text-xs font-semibold text-slate-600" htmlFor="ex-desc">
              {t('expenses.form.description')}
            </label>
            <textarea
              id="ex-desc"
              rows={2}
              maxLength={500}
              value={description}
              onChange={(e) => setDescription(e.target.value)}
              className="mt-1 w-full rounded-lg border border-slate-300 px-2 py-2 text-sm"
            />
          </div>

          {recordMut.isError ? (
            <p className="sm:col-span-2 text-sm text-rose-700">{parseApiError(recordMut.error)}</p>
          ) : null}

          <div className="sm:col-span-2">
            <button
              type="submit"
              disabled={!canRecord || recordMut.isPending}
              className="h-10 rounded-lg bg-sky-600 px-5 text-sm font-semibold text-white shadow-sm hover:bg-sky-700 disabled:cursor-not-allowed disabled:opacity-50"
            >
              {recordMut.isPending ? t('common.loading') : t('expenses.form.submit')}
            </button>
          </div>
        </form>
      </section>

      <section className="rounded-2xl border border-slate-200 bg-white p-4 shadow-sm sm:p-6">
        <h2 className="text-base font-bold text-slate-900">{t('expenses.list.title')}</h2>
        <div className="mt-4 flex flex-col gap-3 sm:flex-row sm:flex-wrap sm:items-end">
          <div>
            <label className="text-xs font-medium text-slate-600" htmlFor="lst-from">
              {t('rep.branch.dateFrom')}
            </label>
            <input
              id="lst-from"
              type="date"
              value={from}
              onChange={(e) => setFrom(e.target.value)}
              className="mt-1 block h-10 rounded-lg border border-slate-300 px-2 text-sm"
            />
          </div>
          <div>
            <label className="text-xs font-medium text-slate-600" htmlFor="lst-to">
              {t('rep.branch.dateTo')}
            </label>
            <input
              id="lst-to"
              type="date"
              value={to}
              onChange={(e) => setTo(e.target.value)}
              className="mt-1 block h-10 rounded-lg border border-slate-300 px-2 text-sm"
            />
          </div>
          <button
            type="button"
            disabled={from > to}
            onClick={applyRange}
            className="h-10 rounded-lg border border-sky-600 bg-sky-600 px-4 text-sm font-semibold text-white hover:bg-sky-700 disabled:opacity-50"
          >
            {t('rep.branch.applyRange')}
          </button>
          <button type="button" onClick={resetRange} className="h-10 rounded-lg border border-slate-300 bg-white px-4 text-sm">
            {t('common.reset')}
          </button>
          {isAdmin ? (
            <div className="min-w-[12rem]">
              <label className="text-xs font-medium text-slate-600" htmlFor="lst-wh">
                {t('expenses.list.filterWarehouse')}
              </label>
              <select
                id="lst-wh"
                value={listWarehouseId === 'all' ? 'all' : String(listWarehouseId)}
                onChange={(e) => {
                  const v = e.target.value
                  setListWarehouseId(v === 'all' ? 'all' : Number(v))
                }}
                className="mt-1 block h-10 w-full rounded-lg border border-slate-300 bg-white px-2 text-sm"
              >
                <option value="all">{t('expenses.list.allWarehouses')}</option>
                {warehouses
                  .filter((w) => w.isActive)
                  .map((w) => (
                    <option key={w.id} value={w.id}>
                      {w.name}
                    </option>
                  ))}
              </select>
            </div>
          ) : null}
        </div>

        {from > to ? <p className="mt-2 text-sm text-rose-700">{t('rep.branch.rangeInvalid')}</p> : null}

        <div className="mt-4 overflow-x-auto">
          {expensesQ.isPending ? (
            <p className="text-sm text-slate-500">{t('common.loading')}</p>
          ) : expensesQ.isError ? (
            <p className="text-sm text-rose-700">{parseApiError(expensesQ.error)}</p>
          ) : (
            <table className="w-full min-w-[40rem] border-collapse text-start text-xs">
              <thead className="border-b border-slate-200 bg-slate-50 text-[11px] font-semibold uppercase text-slate-600">
                <tr>
                  <th className="px-2 py-2">{t('rep.branch.col.when')}</th>
                  <th className="px-2 py-2">{t('rep.branch.col.amount')}</th>
                  <th className="px-2 py-2">{t('rep.branch.col.category')}</th>
                  <th className="px-2 py-2">{t('rep.branch.col.description')}</th>
                  {isAdmin ? <th className="px-2 py-2">{t('expenses.list.colWarehouse')}</th> : null}
                  <th className="px-2 py-2">{t('rep.branch.col.by')}</th>
                </tr>
              </thead>
              <tbody>
                {(expensesQ.data ?? []).length === 0 ? (
                  <tr>
                    <td colSpan={isAdmin ? 6 : 5} className="px-2 py-6 text-center text-slate-500">
                      {t('rep.branch.empty')}
                    </td>
                  </tr>
                ) : (
                  (expensesQ.data ?? []).map((r: ExpenseReportRow) => (
                    <tr key={r.id} className="border-b border-slate-100">
                      <td className="whitespace-nowrap px-2 py-2 text-slate-600">{fmtDateTime(r.expenseDateUtc)}</td>
                      <td className="px-2 py-2 font-mono font-medium">{money(r.amount)}</td>
                      <td className="px-2 py-2">{r.category}</td>
                      <td className="max-w-[14rem] truncate px-2 py-2 text-slate-700">{r.description}</td>
                      {isAdmin ? <td className="px-2 py-2 text-slate-600">{r.warehouseName ?? '—'}</td> : null}
                      <td className="px-2 py-2 text-slate-600">{r.createdByUsername ?? '—'}</td>
                    </tr>
                  ))
                )}
              </tbody>
            </table>
          )}
        </div>
      </section>
    </div>
  )
}
