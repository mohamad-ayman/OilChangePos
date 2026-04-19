import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useMemo, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import {
  stockItemsToLedger,
  validateTransferDocument,
  buildTransferDocument,
  getAvailableStock,
} from '@/features/inventory/domain/inventory.engine'
import { inventoryKeys } from '@/features/inventory/services/inventoryQueryKeys'
import { transferKeys } from '@/features/transfers/services/transferQueryKeys'
import {
  createTransfer,
  getInventorySnapshot,
  getProducts,
  getWarehouses,
  type CreateTransferWorkflowInput,
} from '@/shared/api/inventory.api'
import { catalogDisplayName } from '@/shared/utils/catalogLine'
import { useAuthStore } from '@/shared/store/auth.store'
import { WarehouseType, type Warehouse } from '@/entities/warehouse'

export type WizardLine = { productId: number; quantity: number; productName: string }

export type WizardStep = 1 | 2 | 3 | 4

export function useCreateTransferWizard() {
  const user = useAuthStore((s) => s.user)
  const navigate = useNavigate()
  const qc = useQueryClient()

  const [step, setStep] = useState<WizardStep>(1)
  const [fromWarehouseId, setFromWarehouseId] = useState<number | ''>('')
  const [toWarehouseId, setToWarehouseId] = useState<number | ''>('')
  const [notes, setNotes] = useState('')
  const [lines, setLines] = useState<WizardLine[]>([])
  const [productSearch, setProductSearch] = useState('')

  const warehousesQuery = useQuery({
    queryKey: inventoryKeys.warehouses(),
    queryFn: getWarehouses,
    staleTime: 300_000,
  })

  const productsQuery = useQuery({
    queryKey: inventoryKeys.products(),
    queryFn: getProducts,
    staleTime: 60_000,
  })

  const snapshotQuery = useQuery({
    queryKey: inventoryKeys.snapshot(typeof fromWarehouseId === 'number' ? fromWarehouseId : -1),
    queryFn: () => getInventorySnapshot(fromWarehouseId as number),
    enabled: typeof fromWarehouseId === 'number',
    staleTime: 15_000,
  })

  const ledger = useMemo(() => {
    const wid = fromWarehouseId
    if (typeof wid !== 'number') return null
    const snap = snapshotQuery.data ?? []
    const items = snap.map((r) => ({
      productId: r.productId,
      warehouseId: wid,
      quantityOnHand: r.currentStock,
    }))
    return stockItemsToLedger(items)
  }, [fromWarehouseId, snapshotQuery.data])

  const validationDoc = useMemo(() => {
    if (!user || typeof fromWarehouseId !== 'number' || typeof toWarehouseId !== 'number') return null
    return buildTransferDocument({
      fromWarehouseId,
      toWarehouseId,
      userId: user.id,
      notes: notes.trim() || 'Transfer',
      lines: lines.map((l) => ({ productId: l.productId, quantity: l.quantity })),
    })
  }, [user, fromWarehouseId, toWarehouseId, notes, lines])

  const validation = useMemo(() => {
    if (!ledger || !validationDoc) return { ok: false as const, message: 'Select warehouses and lines.' }
    return validateTransferDocument(ledger, validationDoc)
  }, [ledger, validationDoc])

  const lineIssues = useMemo(() => {
    if (!ledger || typeof fromWarehouseId !== 'number') return []
    const issues: { productId: number; label: string; message: string }[] = []
    for (const line of lines) {
      if (line.quantity <= 0) {
        issues.push({
          productId: line.productId,
          label: line.productName,
          message: 'Quantity must be greater than zero.',
        })
        continue
      }
      const available = getAvailableStock(ledger, line.productId, fromWarehouseId)
      if (line.quantity > available) {
        issues.push({
          productId: line.productId,
          label: line.productName,
          message: `Available at source: ${available} — requested: ${line.quantity}.`,
        })
      }
    }
    return issues
  }, [ledger, fromWarehouseId, lines])

  const mainWarehouses: Warehouse[] = useMemo(
    () => (warehousesQuery.data ?? []).filter((w) => w.type === WarehouseType.Main),
    [warehousesQuery.data],
  )

  const branchWarehouses: Warehouse[] = useMemo(
    () => (warehousesQuery.data ?? []).filter((w) => w.type === WarehouseType.Branch),
    [warehousesQuery.data],
  )

  const filteredProducts = useMemo(() => {
    const q = productSearch.trim().toLowerCase()
    const all = productsQuery.data ?? []
    if (!q) return all.slice(0, 40)
    return all
      .filter((p) => {
        const label = catalogDisplayName({
          companyName: p.companyName,
          name: p.name,
          packageSize: p.packageSize,
        }).toLowerCase()
        return (
          label.includes(q) ||
          p.name.toLowerCase().includes(q) ||
          (p.companyName || '').toLowerCase().includes(q) ||
          (p.packageSize || '').toLowerCase().includes(q) ||
          p.productCategory.toLowerCase().includes(q) ||
          String(p.id).includes(q)
        )
      })
      .slice(0, 40)
  }, [productsQuery.data, productSearch])

  const submitMutation = useMutation({
    mutationFn: async (input: CreateTransferWorkflowInput) => createTransfer(input),
    onSuccess: async (res) => {
      await qc.invalidateQueries({ queryKey: transferKeys.root })
      navigate(`/app/transfers/workflow/${res.id}`)
    },
  })

  function addLine(productId: number, productName: string) {
    setLines((prev) => {
      const i = prev.findIndex((x) => x.productId === productId)
      if (i >= 0) {
        const next = [...prev]
        next[i] = { ...next[i], quantity: next[i].quantity + 1 }
        return next
      }
      return [...prev, { productId, productName, quantity: 1 }]
    })
  }

  function setLineQty(productId: number, quantity: number) {
    setLines((prev) => prev.map((l) => (l.productId === productId ? { ...l, quantity } : l)))
  }

  function removeLine(productId: number) {
    setLines((prev) => prev.filter((l) => l.productId !== productId))
  }

  async function submitTransfer() {
    if (!user || typeof fromWarehouseId !== 'number' || typeof toWarehouseId !== 'number') return
    if (validation.ok !== true) return
    await submitMutation.mutateAsync({
      fromWarehouseId,
      toWarehouseId,
      userId: user.id,
      username: user.username,
      notes: notes.trim() || 'Transfer',
      lines: lines.map((l) => ({ productId: l.productId, quantity: l.quantity })),
      branchSalePriceForDestination: null,
    })
  }

  return {
    step,
    setStep,
    fromWarehouseId,
    setFromWarehouseId,
    toWarehouseId,
    setToWarehouseId,
    notes,
    setNotes,
    lines,
    addLine,
    setLineQty,
    removeLine,
    productSearch,
    setProductSearch,
    filteredProducts,
    mainWarehouses,
    branchWarehouses,
    warehouses: warehousesQuery.data ?? [],
    validation,
    validationDoc,
    ledgerLoading: snapshotQuery.isPending,
    submitMutation,
    submitTransfer,
    isStep1Valid: typeof fromWarehouseId === 'number' && typeof toWarehouseId === 'number' && fromWarehouseId !== toWarehouseId,
    isStep2Valid: lines.length > 0 && lines.every((l) => l.quantity > 0),
    isStep3Valid: validation.ok === true && lineIssues.length === 0,
    lineIssues,
  }
}
