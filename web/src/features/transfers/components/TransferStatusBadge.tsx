import type { TransferWorkflowStatus } from '@/shared/api/inventory.api'
import { transferStatusLabel } from '@/features/transfers/workflow/transferWorkflow'

const palette: Record<TransferWorkflowStatus, string> = {
  draft: 'border-slate-300 bg-slate-100 text-slate-800',
  pending_approval: 'border-amber-300 bg-amber-50 text-amber-900',
  approved: 'border-sky-300 bg-sky-50 text-sky-900',
  in_transit: 'border-violet-300 bg-violet-50 text-violet-900',
  completed: 'border-emerald-300 bg-emerald-50 text-emerald-900',
  rejected: 'border-rose-300 bg-rose-50 text-rose-900',
}

export function TransferStatusBadge({ status }: { status: TransferWorkflowStatus }) {
  return (
    <span
      className={[
        'inline-flex rounded border px-1.5 py-0.5 text-[10px] font-semibold uppercase tracking-wide',
        palette[status],
      ].join(' ')}
    >
      {transferStatusLabel(status)}
    </span>
  )
}
