import { useCreateTransferWizard } from '@/features/transfers/hooks/useCreateTransferWizard'
import { catalogDisplayName } from '@/shared/utils/catalogLine'
import { t } from '@/i18n'

export function CreateTransferWizard() {
  const w = useCreateTransferWizard()

  return (
    <div className="border-b border-slate-200 px-3 py-4 sm:px-4">
      <div className="border-b border-slate-200 pb-3">
        <h1 className="text-base font-semibold text-slate-900">{t('xfer.wizard.title')}</h1>
        <p className="text-xs text-slate-500">{t('xfer.wizard.subtitle')}</p>
        <div className="mt-3 flex gap-1 text-[10px] font-semibold uppercase tracking-wide text-slate-500">
          {([1, 2, 3, 4] as const).map((s) => (
            <span
              key={s}
              className={[
                'rounded border px-2 py-1',
                w.step === s ? 'border-sky-500 bg-sky-50 text-sky-900' : 'border-slate-200 bg-white text-slate-600',
              ].join(' ')}
            >
              {s}.{' '}
              {s === 1
                ? t('xfer.wizard.step.route')
                : s === 2
                  ? t('xfer.wizard.step.lines')
                  : s === 3
                    ? t('xfer.wizard.step.validate')
                    : t('xfer.wizard.step.confirm')}
            </span>
          ))}
        </div>
      </div>

      {w.step === 1 ? (
        <div className="mt-4 grid gap-4 sm:grid-cols-2">
          <label className="block text-xs text-slate-600">
            {t('xfer.fromWh')}
            <select
              value={w.fromWarehouseId === '' ? '' : String(w.fromWarehouseId)}
              onChange={(e) => w.setFromWarehouseId(e.target.value ? Number(e.target.value) : '')}
              className="mt-1 w-full rounded border border-slate-300 bg-slate-100 px-2 py-1.5 text-sm text-slate-900"
            >
              <option value="">{t('xfer.select')}</option>
              {w.mainWarehouses.map((wh) => (
                <option key={wh.id} value={String(wh.id)}>
                  {wh.name} {t('xfer.mainTag')}
                </option>
              ))}
              {w.branchWarehouses.map((wh) => (
                <option key={wh.id} value={String(wh.id)}>
                  {wh.name} {t('xfer.branchTag')}
                </option>
              ))}
            </select>
          </label>
          <label className="block text-xs text-slate-600">
            {t('xfer.toWh')}
            <select
              value={w.toWarehouseId === '' ? '' : String(w.toWarehouseId)}
              onChange={(e) => w.setToWarehouseId(e.target.value ? Number(e.target.value) : '')}
              className="mt-1 w-full rounded border border-slate-300 bg-slate-100 px-2 py-1.5 text-sm text-slate-900"
            >
              <option value="">{t('xfer.select')}</option>
              {w.warehouses.map((wh) => (
                <option key={wh.id} value={String(wh.id)}>
                  {wh.name}
                </option>
              ))}
            </select>
          </label>
          <label className="block text-xs text-slate-600 sm:col-span-2">
            {t('xfer.notes')}
            <input
              value={w.notes}
              onChange={(e) => w.setNotes(e.target.value)}
              className="mt-1 w-full rounded border border-slate-300 bg-slate-100 px-2 py-1.5 text-sm text-slate-900"
              placeholder={t('xfer.notesPh')}
            />
          </label>
          <div className="sm:col-span-2">
            <button
              type="button"
              disabled={!w.isStep1Valid}
              onClick={() => w.setStep(2)}
              className="rounded border border-slate-400 px-3 py-1.5 text-xs font-medium text-slate-900 hover:bg-slate-100 disabled:opacity-40"
            >
              {t('xfer.next')}
            </button>
          </div>
        </div>
      ) : null}

      {w.step === 2 ? (
        <div className="mt-4 space-y-3">
          <div className="flex flex-wrap gap-2">
            <input
              value={w.productSearch}
              onChange={(e) => w.setProductSearch(e.target.value)}
              placeholder={t('pos.searchProducts')}
              className="min-w-[12rem] flex-1 rounded border border-slate-300 bg-slate-100 px-2 py-1.5 text-xs text-slate-900"
            />
            <button
              type="button"
              onClick={() => w.setStep(1)}
              className="rounded border border-slate-300 px-2 py-1 text-xs text-slate-700 hover:bg-slate-100"
            >
              {t('xfer.back')}
            </button>
            <button
              type="button"
              disabled={!w.isStep2Valid}
              onClick={() => w.setStep(3)}
              className="rounded border border-slate-400 px-2 py-1 text-xs text-slate-900 hover:bg-slate-100 disabled:opacity-40"
            >
              {t('xfer.next')}
            </button>
          </div>
          <div className="grid gap-3 lg:grid-cols-2">
            <div className="max-h-56 overflow-y-auto border border-slate-200">
              {w.filteredProducts.map((p) => (
                <button
                  key={p.id}
                  type="button"
                  onClick={() =>
                    w.addLine(
                      p.id,
                      catalogDisplayName({
                        companyName: p.companyName,
                        name: p.name,
                        packageSize: p.packageSize,
                      }),
                    )
                  }
                  className="flex w-full items-center justify-between border-b border-slate-200 px-2 py-1.5 text-start text-xs hover:bg-slate-100"
                >
                  <span className="text-slate-800">
                    {catalogDisplayName({
                      companyName: p.companyName,
                      name: p.name,
                      packageSize: p.packageSize,
                    })}
                  </span>
                  <span className="font-mono text-[10px] text-slate-500">#{p.id}</span>
                </button>
              ))}
            </div>
            <div className="border border-slate-200">
              <div className="border-b border-slate-200 bg-slate-100 px-2 py-1 text-[10px] font-semibold uppercase text-slate-500">
                {t('xfer.lines')}
              </div>
              {w.lines.length === 0 ? (
                <p className="px-2 py-4 text-center text-xs text-slate-500">{t('xfer.addProducts')}</p>
              ) : (
                <ul className="divide-y divide-slate-200">
                  {w.lines.map((l) => (
                    <li key={l.productId} className="flex items-center gap-2 px-2 py-1.5 text-xs">
                      <span className="min-w-0 flex-1 truncate text-slate-800">{l.productName}</span>
                      <input
                        type="number"
                        min={1}
                        className="w-16 rounded border border-slate-300 bg-slate-100 px-1 py-0.5 text-end font-mono text-xs"
                        value={l.quantity}
                        onChange={(e) => w.setLineQty(l.productId, Number(e.target.value) || 0)}
                      />
                      <button
                        type="button"
                        onClick={() => w.removeLine(l.productId)}
                        className="text-rose-400 hover:underline"
                      >
                        {t('pos.remove')}
                      </button>
                    </li>
                  ))}
                </ul>
              )}
            </div>
          </div>
        </div>
      ) : null}

      {w.step === 3 ? (
        <div className="mt-4 space-y-3">
          <div className="flex gap-2">
            <button
              type="button"
              onClick={() => w.setStep(2)}
              className="rounded border border-slate-300 px-2 py-1 text-xs text-slate-700 hover:bg-slate-100"
            >
              {t('xfer.back')}
            </button>
            <button
              type="button"
              disabled={!w.isStep3Valid}
              onClick={() => w.setStep(4)}
              className="rounded border border-slate-400 px-2 py-1 text-xs text-slate-900 hover:bg-slate-100 disabled:opacity-40"
            >
              {t('xfer.next')}
            </button>
          </div>
          {w.ledgerLoading ? (
            <p className="text-xs text-slate-500">{t('xfer.validation.loading')}</p>
          ) : (
            <>
              {w.validation.ok === false ? (
                <div className="rounded border border-rose-200 bg-rose-50 px-3 py-2 text-xs text-rose-800">
                  {w.validation.message}
                </div>
              ) : (
                <div className="rounded border border-emerald-200 bg-emerald-50 px-3 py-2 text-xs text-emerald-900">
                  {t('xfer.wizard.validationPass')}
                </div>
              )}
              {w.lineIssues.length > 0 ? (
                <div className="border border-amber-200 bg-amber-50/80">
                  <div className="border-b border-amber-200 px-2 py-1 text-[10px] font-semibold uppercase text-amber-900">
                    {t('xfer.wizard.perLineChecks')}
                  </div>
                  <ul className="max-h-40 overflow-y-auto divide-y divide-amber-100">
                    {w.lineIssues.map((issue) => (
                      <li key={issue.productId} className="px-2 py-1.5 text-xs text-amber-900">
                        <span className="font-medium">{issue.label}</span>: {issue.message}
                      </li>
                    ))}
                  </ul>
                </div>
              ) : null}
            </>
          )}
        </div>
      ) : null}

      {w.step === 4 ? (
        <div className="mt-4 space-y-3">
          <div className="flex gap-2">
            <button
              type="button"
              onClick={() => w.setStep(3)}
              className="rounded border border-slate-300 px-2 py-1 text-xs text-slate-700 hover:bg-slate-100"
            >
              {t('xfer.back')}
            </button>
          </div>
          <div className="border border-slate-200 bg-white px-3 py-2 text-xs text-slate-700">
            <div className="grid grid-cols-2 gap-1">
              <span className="text-slate-500">{t('xfer.wizard.summaryFrom')}</span>
              <span className="text-end text-slate-800">{String(w.fromWarehouseId)}</span>
              <span className="text-slate-500">{t('xfer.wizard.summaryTo')}</span>
              <span className="text-end text-slate-800">{String(w.toWarehouseId)}</span>
              <span className="text-slate-500">{t('xfer.wizard.summaryLines')}</span>
              <span className="text-end text-slate-800">{w.lines.length}</span>
            </div>
            <ul className="mt-2 border-t border-slate-200 pt-2">
              {w.lines.map((l) => (
                <li key={l.productId} className="flex justify-between py-0.5">
                  <span className="truncate text-slate-600">{l.productName}</span>
                  <span className="font-mono tabular-nums text-slate-800">{l.quantity}</span>
                </li>
              ))}
            </ul>
          </div>
          {w.submitMutation.isError ? <p className="text-xs text-rose-700">{t('xfer.wizard.submitFail')}</p> : null}
          <button
            type="button"
            disabled={w.submitMutation.isPending}
            onClick={() => void w.submitTransfer()}
            className="rounded border border-sky-600 bg-sky-600 px-3 py-1.5 text-xs font-semibold text-white hover:bg-sky-700 disabled:opacity-40"
          >
            {w.submitMutation.isPending ? t('xfer.submitting') : t('xfer.submit')}
          </button>
        </div>
      ) : null}
    </div>
  )
}
