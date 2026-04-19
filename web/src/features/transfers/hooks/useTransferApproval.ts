import { useMutation, useQueryClient } from '@tanstack/react-query'
import { useAuthStore } from '@/shared/store/auth.store'
import {
  approveTransfer,
  completeTransfer,
  rejectTransfer,
  type TransferActor,
  type TransferDetail,
} from '@/shared/api/inventory.api'
import { roleMayApproveOrReject, roleMayDispatchAndReceive } from '@/features/transfers/workflow/transferWorkflow'
import { transferKeys } from '@/features/transfers/services/transferQueryKeys'
import { inventoryKeys } from '@/features/inventory/services/inventoryQueryKeys'

export function useTransferApproval() {
  const user = useAuthStore((s) => s.user)
  const qc = useQueryClient()

  const mayApproveReject = user ? roleMayApproveOrReject(user.role) : false
  const mayOperateShipment = user ? roleMayDispatchAndReceive(user.role) : false

  function actor(note?: string): TransferActor {
    if (!user) throw new Error('Not authenticated')
    return { userId: user.id, username: user.username, note }
  }

  const invalidate = async (id: string) => {
    await qc.invalidateQueries({ queryKey: transferKeys.root })
    await qc.invalidateQueries({ queryKey: transferKeys.detail(id) })
    await qc.invalidateQueries({ queryKey: inventoryKeys.root })
  }

  const approve = useMutation({
    mutationFn: async ({ transferId, note }: { transferId: string; note?: string }) =>
      approveTransfer(transferId, actor(note)),
    onSuccess: (t: TransferDetail) => void invalidate(t.id),
  })

  const reject = useMutation({
    mutationFn: async ({ transferId, reason }: { transferId: string; reason: string }) =>
      rejectTransfer(transferId, actor(reason)),
    onSuccess: (t: TransferDetail) => void invalidate(t.id),
  })

  const complete = useMutation({
    mutationFn: async ({ transferId, note }: { transferId: string; note?: string }) =>
      completeTransfer(transferId, actor(note)),
    onSuccess: (t: TransferDetail) => void invalidate(t.id),
  })

  return {
    user,
    mayApproveReject,
    mayOperateShipment,
    /** Cashier cannot approve — enforced here for UX; backend must still authorize. */
    isCashier: user?.role === 'cashier',
    approve,
    reject,
    complete,
  }
}
