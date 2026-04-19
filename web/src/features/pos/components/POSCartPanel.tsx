import { memo } from 'react'
import type { CartLine } from '@/features/pos/engine/posEngine'
import { lineSubtotal } from '@/features/pos/engine/posEngine'
import { t } from '@/i18n'

type StockMap = ReadonlyMap<number, number>

type POSCartPanelProps = {
  lines: CartLine[]
  stockByProduct: StockMap
  onQty: (uid: string, qty: number) => void
  onRemove: (uid: string) => void
}

const LineRow = memo(function LineRow({
  line,
  onHand,
  onQty,
  onRemove,
}: {
  line: CartLine
  onHand: number
  onQty: (uid: string, q: number) => void
  onRemove: (uid: string) => void
}) {
  const over = line.quantity > onHand
  return (
    <tr className={over ? 'bg-rose-50' : 'border-b border-slate-200'}>
      <td className="max-w-[10rem] truncate px-2 py-1 text-xs text-slate-900">{line.name}</td>
      <td className="px-1 py-1">
        <input
          type="number"
          min={0.001}
          step={0.001}
          value={line.quantity}
          onChange={(e) => onQty(line.uid, Number(e.target.value))}
          className="w-[4.25rem] rounded border border-slate-300 bg-slate-100 px-1 py-0.5 text-end font-mono text-xs text-slate-900"
        />
      </td>
      <td className="px-2 py-1 text-end font-mono text-xs tabular-nums text-slate-600">
        {onHand.toLocaleString(undefined, { maximumFractionDigits: 3 })}
      </td>
      <td className="px-2 py-1 text-end font-mono text-xs tabular-nums text-slate-800">{lineSubtotal(line).toFixed(2)}</td>
      <td className="px-1 py-1 text-end">
        <button type="button" onClick={() => onRemove(line.uid)} className="text-[10px] text-rose-400 hover:underline">
          {t('pos.remove')}
        </button>
      </td>
    </tr>
  )
})

export const POSCartPanel = memo(function POSCartPanel({ lines, stockByProduct, onQty, onRemove }: POSCartPanelProps) {
  return (
    <div className="flex min-h-0 flex-1 flex-col border border-slate-200 bg-white">
      <div className="border-b border-slate-200 bg-slate-100 px-2 py-2 text-[10px] font-semibold uppercase tracking-wide text-slate-500">
        {t('pos.cart')}
      </div>
      <div className="min-h-0 flex-1 overflow-y-auto overscroll-contain">
        <table className="w-full border-collapse text-start">
          <thead className="sticky top-0 z-10 border-b border-slate-200 bg-white text-[10px] uppercase text-slate-500">
            <tr>
              <th className="px-2 py-1">{t('pos.item')}</th>
              <th className="px-1 py-1">{t('pos.qty')}</th>
              <th className="px-2 py-1 text-end">{t('pos.oh')}</th>
              <th className="px-2 py-1 text-end">{t('pos.line')}</th>
              <th className="w-8" />
            </tr>
          </thead>
          <tbody>
            {lines.length === 0 ? (
              <tr>
                <td colSpan={5} className="px-2 py-8 text-center text-xs text-slate-500">
                  {t('pos.scanHint')}
                </td>
              </tr>
            ) : (
              lines.map((line) => (
                <LineRow
                  key={line.uid}
                  line={line}
                  onHand={stockByProduct.get(line.productId) ?? 0}
                  onQty={onQty}
                  onRemove={onRemove}
                />
              ))
            )}
          </tbody>
        </table>
      </div>
    </div>
  )
})
