import type { AppAuthRole } from '@/shared/api/auth.api'
import type { TransferAuditEntry, TransferWorkflowStatus } from '@/shared/api/inventory.api'
import { t } from '@/i18n'

export type TransferApprovalAction = 'approve' | 'reject' | 'advance_shipment' | 'finalize_receipt'

export function roleMayApproveOrReject(role: AppAuthRole): boolean {
  return role === 'admin' || role === 'manager'
}

export function roleMayDispatchAndReceive(role: AppAuthRole): boolean {
  return role === 'admin' || role === 'manager'
}

/** Allowed ERP actions for the current workflow row (UI gating only; server must enforce). */
export function allowedTransferActions(
  status: TransferWorkflowStatus,
  role: AppAuthRole,
): TransferApprovalAction[] {
  const out: TransferApprovalAction[] = []
  if (status === 'pending_approval' && roleMayApproveOrReject(role)) {
    out.push('approve', 'reject')
  }
  if (status === 'approved' && roleMayDispatchAndReceive(role)) {
    out.push('advance_shipment')
  }
  if (status === 'in_transit' && roleMayDispatchAndReceive(role)) {
    out.push('finalize_receipt')
  }
  return out
}

export function transferStatusLabel(s: TransferWorkflowStatus): string {
  switch (s) {
    case 'draft':
      return t('xfer.st.draft')
    case 'pending_approval':
      return t('xfer.st.pending_approval')
    case 'approved':
      return t('xfer.st.approved')
    case 'in_transit':
      return t('xfer.st.in_transit')
    case 'completed':
      return t('xfer.st.completed')
    case 'rejected':
      return t('xfer.st.rejected')
    default: {
      const _e: never = s
      return _e
    }
  }
}

export function sortAuditDescending(a: TransferAuditEntry, b: TransferAuditEntry): number {
  return b.atUtc.localeCompare(a.atUtc)
}
