import { useMemo } from 'react'
import type { TransferAuditEntry, TransferDetail } from '@/shared/api/inventory.api'
import { sortAuditDescending, transferStatusLabel } from '@/features/transfers/workflow/transferWorkflow'
import { t } from '@/i18n'

function kindLabel(k: TransferAuditEntry['kind']): string {
  switch (k) {
    case 'created':
      return t('xfer.audit.created')
    case 'submitted':
      return t('xfer.audit.submitted')
    case 'approved':
      return t('xfer.audit.approved')
    case 'rejected':
      return t('xfer.audit.rejected')
    case 'dispatched':
      return t('xfer.audit.dispatched')
    case 'completed':
      return t('xfer.audit.completed')
    case 'stock_posted':
      return t('xfer.audit.stock_posted')
    default:
      return k
  }
}

export function TransferTimeline({ detail }: { detail: TransferDetail }) {
  const entries = useMemo(
    () => [...detail.auditTrail].sort(sortAuditDescending),
    [detail.auditTrail],
  )

  return (
    <div className="border border-slate-200 bg-white">
      <div className="border-b border-slate-200 bg-slate-100 px-3 py-2 text-[11px] font-semibold uppercase tracking-wide text-slate-600">
        {t('xfer.audit.title')}
      </div>
      <ol className="max-h-[22rem] divide-y divide-slate-200 overflow-y-auto">
        {entries.length === 0 ? (
          <li className="px-3 py-6 text-center text-xs text-slate-500">{t('xfer.audit.empty')}</li>
        ) : (
          entries.map((e) => (
            <li key={e.id} className="flex gap-3 px-3 py-2.5 text-xs">
              <div className="w-36 shrink-0 font-mono text-[10px] text-slate-500">
                {e.atUtc.slice(0, 19).replace('T', ' ')}
              </div>
              <div className="min-w-0 flex-1">
                <div className="font-semibold text-slate-800">{kindLabel(e.kind)}</div>
                <div className="mt-0.5 text-slate-500">
                  {e.fromStatus != null && e.toStatus != null ? (
                    <span>
                      {transferStatusLabel(e.fromStatus)} · {transferStatusLabel(e.toStatus)}
                    </span>
                  ) : e.toStatus != null ? (
                    <span>{transferStatusLabel(e.toStatus)}</span>
                  ) : null}
                </div>
                {e.username ? (
                  <div className="mt-0.5 text-slate-600">
                    {t('xfer.audit.user')} #{e.userId} ({e.username})
                  </div>
                ) : (
                  <div className="mt-0.5 text-slate-600">
                    {t('xfer.audit.user')} #{e.userId}
                  </div>
                )}
                {e.note ? <div className="mt-1 text-slate-500">{e.note}</div> : null}
              </div>
            </li>
          ))
        )}
      </ol>
    </div>
  )
}
