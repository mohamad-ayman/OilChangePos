import { useState } from 'react'
import type { InvoiceDto } from '@/shared/api/pos.api'
import { t } from '@/i18n'

type POSCheckoutModalProps = {
  open: boolean
  busy: boolean
  grandTotal: number
  onClose: () => void
  onConfirm: (method: 'cash' | 'card') => void
  lastInvoice: InvoiceDto | null
}

export function POSCheckoutModal({
  open,
  busy,
  grandTotal,
  onClose,
  onConfirm,
  lastInvoice,
}: POSCheckoutModalProps) {
  const [method, setMethod] = useState<'cash' | 'card'>('cash')

  if (!open) return null

  return (
    <>
      <button type="button" className="fixed inset-0 z-40 bg-slate-900/35" aria-label={t('common.close')} onClick={onClose} />
      <div
        className="fixed left-1/2 top-1/2 z-50 w-full max-w-md -translate-x-1/2 -translate-y-1/2 border border-slate-300 bg-white p-4 shadow-2xl"
        role="dialog"
        aria-modal="true"
      >
        {!lastInvoice ? (
          <>
            <h2 className="text-sm font-semibold text-slate-900">{t('pos.payment')}</h2>
            <p className="mt-1 font-mono text-lg text-sky-800">{grandTotal.toFixed(2)}</p>
            <div className="mt-3 flex gap-2">
              <label className="flex cursor-pointer items-center gap-2 text-xs text-slate-700">
                <input type="radio" checked={method === 'cash'} onChange={() => setMethod('cash')} />
                {t('pos.cash')}
              </label>
              <label className="flex cursor-pointer items-center gap-2 text-xs text-slate-700">
                <input type="radio" checked={method === 'card'} onChange={() => setMethod('card')} />
                {t('pos.card')}
              </label>
            </div>
            <div className="mt-4 flex justify-end gap-2">
              <button
                type="button"
                onClick={onClose}
                className="rounded border border-slate-400 px-3 py-1.5 text-xs text-slate-700 hover:bg-slate-100"
              >
                {t('pos.cancel')}
              </button>
              <button
                type="button"
                disabled={busy || grandTotal <= 0}
                onClick={() => onConfirm(method)}
                className="rounded border border-emerald-600 bg-emerald-600 px-3 py-1.5 text-xs font-semibold text-white hover:bg-emerald-700 disabled:opacity-40"
              >
                {busy ? t('pos.posting') : t('pos.completeSale')}
              </button>
            </div>
          </>
        ) : (
          <div className="text-xs text-slate-700">
            <h2 className="text-sm font-semibold text-slate-900">{t('pos.receipt')}</h2>
            <p className="mt-2 font-mono text-slate-600">#{lastInvoice.receiptNo}</p>
            <p className="mt-1 text-slate-500">{lastInvoice.createdAtUtc.slice(0, 19).replace('T', ' ')} UTC</p>
            <ul className="mt-3 max-h-40 overflow-y-auto border border-slate-200">
              {lastInvoice.lines.map((l) => (
                <li key={l.productId} className="flex justify-between border-b border-slate-200 px-2 py-1">
                  <span className="truncate">{l.name}</span>
                  <span className="font-mono tabular-nums">
                    {l.quantity} × {l.unitPrice.toFixed(2)}
                  </span>
                </li>
              ))}
            </ul>
            <div className="mt-3 flex justify-between border-t border-slate-200 pt-2 font-mono text-sm text-slate-900">
              <span>{t('pos.total')}</span>
              <span>{lastInvoice.grandTotal.toFixed(2)}</span>
            </div>
            <p className="mt-2 text-[10px] uppercase text-slate-500">
              {lastInvoice.paymentMethod} — {t('pos.tender')}
            </p>
            <button
              type="button"
              onClick={onClose}
              className="mt-4 w-full rounded border border-slate-400 py-2 text-xs font-semibold text-slate-900 hover:bg-slate-100"
            >
              {t('common.close')}
            </button>
          </div>
        )}
      </div>
    </>
  )
}
