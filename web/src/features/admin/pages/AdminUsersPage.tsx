import { useMemo, useState } from 'react'
import { Navigate } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { isAxiosError } from 'axios'
import {
  createAdminUser,
  createWarehouseBranch,
  fetchAdminUsers,
  fetchBranchesForAdmin,
  resetAdminUserPassword,
  updateAdminUser,
  updateWarehouseBranch,
} from '@/features/admin/api/admin.api'
import { adminBranchKeys, adminUserKeys } from '@/features/admin/services/adminQueryKeys'
import { useAuthStore } from '@/shared/store/auth.store'
import { t } from '@/i18n'

type ApiRole = 'Admin' | 'Manager' | 'Cashier'

function toApiRole(r: ApiRole): string {
  return r
}

function roleLabel(role: string): string {
  const k = `role.${role.toLowerCase()}`
  const v = t(k)
  return v === k ? role : v
}

function errMsg(e: unknown): string {
  if (isAxiosError(e)) {
    const d = e.response?.data
    if (d && typeof d === 'object' && 'error' in d && typeof (d as { error: unknown }).error === 'string') {
      return (d as { error: string }).error
    }
    return e.message
  }
  return e instanceof Error ? e.message : t('admin.errorGeneric')
}

export function AdminUsersPage() {
  const user = useAuthStore((s) => s.user)
  const qc = useQueryClient()

  const [createOpen, setCreateOpen] = useState(false)
  const [cu, setCu] = useState({ username: '', password: '', role: 'Cashier' as ApiRole, branchId: '' as string })
  const [editId, setEditId] = useState<number | null>(null)
  const [edit, setEdit] = useState({ role: 'Cashier' as ApiRole, isActive: true, branchId: '' as string })
  const [pwdId, setPwdId] = useState<number | null>(null)
  const [pwd, setPwd] = useState('')
  const [newBranchName, setNewBranchName] = useState('')
  const [branchDrafts, setBranchDrafts] = useState<Record<number, { name: string; isActive: boolean }>>({})
  const [flash, setFlash] = useState<string | null>(null)

  const usersQ = useQuery({ queryKey: adminUserKeys.list(), queryFn: fetchAdminUsers })
  const branchesQ = useQuery({ queryKey: adminBranchKeys.list(), queryFn: fetchBranchesForAdmin })

  const branchOptions = useMemo(
    () => (branchesQ.data ?? []).filter((b) => b.type === 2).filter((b) => b.isActive),
    [branchesQ.data],
  )

  const createMut = useMutation({
    mutationFn: async () => {
      const bid = cu.role === 'Admin' ? null : cu.branchId ? Number(cu.branchId) : null
      if (cu.role !== 'Admin' && bid == null) throw new Error(t('admin.mustSelectBranch'))
      return createAdminUser({
        username: cu.username.trim(),
        password: cu.password,
        role: toApiRole(cu.role),
        homeBranchWarehouseId: bid,
      })
    },
    onSuccess: async () => {
      setFlash(t('admin.saved'))
      setCreateOpen(false)
      setCu({ username: '', password: '', role: 'Cashier', branchId: '' })
      await qc.invalidateQueries({ queryKey: adminUserKeys.all })
    },
    onError: (e) => setFlash(errMsg(e)),
  })

  const updateMut = useMutation({
    mutationFn: async () => {
      if (editId == null) return
      const bid = edit.role === 'Admin' ? null : edit.branchId ? Number(edit.branchId) : null
      if (edit.role !== 'Admin' && bid == null) throw new Error(t('admin.mustSelectBranch'))
      await updateAdminUser(editId, {
        role: toApiRole(edit.role),
        isActive: edit.isActive,
        homeBranchWarehouseId: bid,
      })
    },
    onSuccess: async () => {
      setFlash(t('admin.saved'))
      setEditId(null)
      await qc.invalidateQueries({ queryKey: adminUserKeys.all })
    },
    onError: (e) => setFlash(errMsg(e)),
  })

  const pwdMut = useMutation({
    mutationFn: async () => {
      if (pwdId == null) return
      await resetAdminUserPassword(pwdId, pwd)
    },
    onSuccess: async () => {
      setFlash(t('admin.passwordUpdated'))
      setPwdId(null)
      setPwd('')
    },
    onError: (e) => setFlash(errMsg(e)),
  })

  const createBranchMut = useMutation({
    mutationFn: () => createWarehouseBranch(newBranchName.trim()),
    onSuccess: async () => {
      setFlash(t('admin.saved'))
      setNewBranchName('')
      await qc.invalidateQueries({ queryKey: adminBranchKeys.all })
      await qc.invalidateQueries({ queryKey: ['warehouses'] })
    },
    onError: (e) => setFlash(errMsg(e)),
  })

  const saveBranchMut = useMutation({
    mutationFn: ({ id, name, isActive }: { id: number; name: string; isActive: boolean }) =>
      updateWarehouseBranch(id, { name: name.trim(), isActive }),
    onSuccess: async () => {
      setFlash(t('admin.saved'))
      await qc.invalidateQueries({ queryKey: adminBranchKeys.all })
      await qc.invalidateQueries({ queryKey: ['warehouses'] })
    },
    onError: (e) => setFlash(errMsg(e)),
  })

  if (!user || user.role !== 'admin') {
    return <Navigate to="/app" replace />
  }

  return (
    <div className="mx-auto max-w-6xl space-y-10 rounded-2xl border border-slate-200/90 bg-white p-6 shadow-sm shadow-slate-900/[0.04] ring-1 ring-slate-900/[0.02] sm:p-8">
      <header className="space-y-2 border-b border-slate-200/80 pb-6">
        <h1 className="text-2xl font-bold text-slate-900">{t('admin.title')}</h1>
        <p className="text-sm text-slate-600">{t('admin.hintServerAuthority')}</p>
        {flash ? (
          <p className="rounded-md bg-amber-50 px-3 py-2 text-sm text-amber-900" role="status">
            {flash}
            <button
              type="button"
              className="ms-3 text-amber-800 underline"
              onClick={() => setFlash(null)}
            >
              {t('common.close')}
            </button>
          </p>
        ) : null}
      </header>

      <section className="space-y-4">
        <div className="flex flex-wrap items-center justify-between gap-3">
          <h2 className="text-lg font-semibold text-slate-800">{t('admin.usersHeading')}</h2>
          <button
            type="button"
            onClick={() => setCreateOpen((v) => !v)}
            className="rounded-lg bg-sky-600 px-4 py-2 text-sm font-semibold text-white shadow-sm hover:bg-sky-700"
          >
            {t('admin.addUser')}
          </button>
        </div>

        {createOpen ? (
          <div className="rounded-xl border border-slate-200 bg-slate-50 p-4 shadow-sm">
            <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
              <label className="block text-sm">
                <span className="font-medium text-slate-700">{t('admin.username')}</span>
                <input
                  className="mt-1 w-full rounded-md border border-slate-300 px-2 py-1.5 text-sm"
                  value={cu.username}
                  onChange={(e) => setCu((s) => ({ ...s, username: e.target.value }))}
                />
              </label>
              <label className="block text-sm">
                <span className="font-medium text-slate-700">{t('admin.password')}</span>
                <input
                  type="password"
                  className="mt-1 w-full rounded-md border border-slate-300 px-2 py-1.5 text-sm"
                  value={cu.password}
                  onChange={(e) => setCu((s) => ({ ...s, password: e.target.value }))}
                />
              </label>
              <label className="block text-sm">
                <span className="font-medium text-slate-700">{t('admin.role')}</span>
                <select
                  className="mt-1 w-full rounded-md border border-slate-300 px-2 py-1.5 text-sm"
                  value={cu.role}
                  onChange={(e) => setCu((s) => ({ ...s, role: e.target.value as ApiRole }))}
                >
                  <option value="Admin">Admin</option>
                  <option value="Manager">Manager</option>
                  <option value="Cashier">Cashier</option>
                </select>
              </label>
              {cu.role !== 'Admin' ? (
                <label className="block text-sm">
                  <span className="font-medium text-slate-700">{t('admin.branch')}</span>
                  <select
                    className="mt-1 w-full rounded-md border border-slate-300 px-2 py-1.5 text-sm"
                    value={cu.branchId}
                    onChange={(e) => setCu((s) => ({ ...s, branchId: e.target.value }))}
                  >
                    <option value="">{t('admin.selectBranch')}</option>
                    {branchOptions.map((b) => (
                      <option key={b.id} value={String(b.id)}>
                        {b.name}
                      </option>
                    ))}
                  </select>
                </label>
              ) : null}
            </div>
            <div className="mt-3 flex gap-2">
              <button
                type="button"
                disabled={createMut.isPending}
                onClick={() => createMut.mutate()}
                className="rounded-lg bg-slate-900 px-4 py-2 text-sm font-semibold text-white hover:bg-slate-800 disabled:opacity-50"
              >
                {t('admin.createUserSubmit')}
              </button>
            </div>
          </div>
        ) : null}

        <div className="overflow-x-auto rounded-xl border border-slate-200 bg-white shadow-sm">
          <table className="min-w-full text-left text-sm">
            <thead className="border-b border-slate-200 bg-slate-50 text-xs uppercase tracking-wide text-slate-600">
              <tr>
                <th className="px-3 py-2">{t('admin.colId')}</th>
                <th className="px-3 py-2">{t('admin.username')}</th>
                <th className="px-3 py-2">{t('admin.role')}</th>
                <th className="px-3 py-2">{t('admin.active')}</th>
                <th className="px-3 py-2">{t('admin.branch')}</th>
                <th className="px-3 py-2">{t('admin.actions')}</th>
              </tr>
            </thead>
            <tbody>
              {usersQ.isLoading ? (
                <tr>
                  <td colSpan={6} className="px-3 py-6 text-center text-slate-500">
                    {t('common.loading')}
                  </td>
                </tr>
              ) : null}
              {(usersQ.data ?? []).map((row) => (
                <tr key={row.id} className="border-b border-slate-100">
                  <td className="px-3 py-2 font-mono text-xs text-slate-600">{row.id}</td>
                  <td className="px-3 py-2 font-medium text-slate-900">{row.username}</td>
                  <td className="px-3 py-2">{roleLabel(row.role)}</td>
                  <td className="px-3 py-2">{row.isActive ? t('admin.yes') : t('admin.no')}</td>
                  <td className="px-3 py-2 text-slate-700">{row.homeBranchName ?? '—'}</td>
                  <td className="px-3 py-2 space-x-2">
                    <button
                      type="button"
                      className="text-sky-700 underline"
                      onClick={() => {
                        setEditId(row.id)
                        setEdit({
                          role: row.role as ApiRole,
                          isActive: row.isActive,
                          branchId: row.homeBranchWarehouseId != null ? String(row.homeBranchWarehouseId) : '',
                        })
                      }}
                    >
                      {t('admin.edit')}
                    </button>
                    <button
                      type="button"
                      className="text-sky-700 underline"
                      onClick={() => {
                        setPwdId(row.id)
                        setPwd('')
                      }}
                    >
                      {t('admin.resetPassword')}
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>

        {editId != null ? (
          <div className="rounded-xl border border-sky-200 bg-sky-50 p-4">
            <h3 className="mb-3 text-sm font-semibold text-sky-900">{t('admin.editUserTitle')}</h3>
            <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
              <label className="block text-sm">
                <span className="font-medium text-slate-700">{t('admin.role')}</span>
                <select
                  className="mt-1 w-full rounded-md border border-slate-300 px-2 py-1.5 text-sm"
                  value={edit.role}
                  onChange={(e) => setEdit((s) => ({ ...s, role: e.target.value as ApiRole }))}
                >
                  <option value="Admin">Admin</option>
                  <option value="Manager">Manager</option>
                  <option value="Cashier">Cashier</option>
                </select>
              </label>
              <label className="flex items-center gap-2 pt-6 text-sm">
                <input
                  type="checkbox"
                  checked={edit.isActive}
                  onChange={(e) => setEdit((s) => ({ ...s, isActive: e.target.checked }))}
                />
                <span>{t('admin.active')}</span>
              </label>
              {edit.role !== 'Admin' ? (
                <label className="block text-sm">
                  <span className="font-medium text-slate-700">{t('admin.branch')}</span>
                  <select
                    className="mt-1 w-full rounded-md border border-slate-300 px-2 py-1.5 text-sm"
                    value={edit.branchId}
                    onChange={(e) => setEdit((s) => ({ ...s, branchId: e.target.value }))}
                  >
                    <option value="">{t('admin.selectBranch')}</option>
                    {branchOptions.map((b) => (
                      <option key={b.id} value={String(b.id)}>
                        {b.name}
                      </option>
                    ))}
                  </select>
                </label>
              ) : null}
            </div>
            <div className="mt-3 flex gap-2">
              <button
                type="button"
                disabled={updateMut.isPending}
                onClick={() => updateMut.mutate()}
                className="rounded-lg bg-sky-600 px-4 py-2 text-sm font-semibold text-white hover:bg-sky-700 disabled:opacity-50"
              >
                {t('admin.save')}
              </button>
              <button type="button" className="text-sm text-slate-600 underline" onClick={() => setEditId(null)}>
                {t('common.close')}
              </button>
            </div>
          </div>
        ) : null}

        {pwdId != null ? (
          <div className="rounded-xl border border-amber-200 bg-amber-50 p-4">
            <h3 className="mb-2 text-sm font-semibold text-amber-900">{t('admin.resetPasswordTitle')}</h3>
            <label className="block max-w-sm text-sm">
              <span className="font-medium text-slate-700">{t('admin.newPassword')}</span>
              <input
                type="password"
                className="mt-1 w-full rounded-md border border-slate-300 px-2 py-1.5 text-sm"
                value={pwd}
                onChange={(e) => setPwd(e.target.value)}
              />
            </label>
            <div className="mt-3 flex gap-2">
              <button
                type="button"
                disabled={pwdMut.isPending}
                onClick={() => pwdMut.mutate()}
                className="rounded-lg bg-amber-700 px-4 py-2 text-sm font-semibold text-white hover:bg-amber-800 disabled:opacity-50"
              >
                {t('admin.savePassword')}
              </button>
              <button type="button" className="text-sm text-slate-600 underline" onClick={() => setPwdId(null)}>
                {t('common.close')}
              </button>
            </div>
          </div>
        ) : null}
      </section>

      <section className="space-y-4">
        <h2 className="text-lg font-semibold text-slate-800">{t('admin.branchesHeading')}</h2>
        <p className="text-sm text-slate-600">{t('admin.branchesHint')}</p>

        <div className="flex flex-wrap items-end gap-2 rounded-xl border border-slate-200 bg-white p-4 shadow-sm">
          <label className="block min-w-[12rem] text-sm">
            <span className="font-medium text-slate-700">{t('admin.newBranchName')}</span>
            <input
              className="mt-1 w-full rounded-md border border-slate-300 px-2 py-1.5 text-sm"
              value={newBranchName}
              onChange={(e) => setNewBranchName(e.target.value)}
            />
          </label>
          <button
            type="button"
            disabled={createBranchMut.isPending || !newBranchName.trim()}
            onClick={() => createBranchMut.mutate()}
            className="rounded-lg bg-slate-900 px-4 py-2 text-sm font-semibold text-white hover:bg-slate-800 disabled:opacity-50"
          >
            {t('admin.createBranch')}
          </button>
        </div>

        <div className="overflow-x-auto rounded-xl border border-slate-200 bg-white shadow-sm">
          <table className="min-w-full text-left text-sm">
            <thead className="border-b border-slate-200 bg-slate-50 text-xs uppercase tracking-wide text-slate-600">
              <tr>
                <th className="px-3 py-2">{t('admin.colId')}</th>
                <th className="px-3 py-2">{t('admin.branchName')}</th>
                <th className="px-3 py-2">{t('admin.active')}</th>
                <th className="px-3 py-2">{t('admin.actions')}</th>
              </tr>
            </thead>
            <tbody>
              {(branchesQ.data ?? [])
                .filter((b) => b.type === 2)
                .map((b) => {
                  const draft = branchDrafts[b.id] ?? { name: b.name, isActive: b.isActive }
                  return (
                    <tr key={b.id} className="border-b border-slate-100">
                      <td className="px-3 py-2 font-mono text-xs">{b.id}</td>
                      <td className="px-3 py-2">
                        <input
                          className="w-full max-w-xs rounded-md border border-slate-300 px-2 py-1 text-sm"
                          value={draft.name}
                          onChange={(e) =>
                            setBranchDrafts((m) => ({
                              ...m,
                              [b.id]: { ...draft, name: e.target.value },
                            }))
                          }
                        />
                      </td>
                      <td className="px-3 py-2">
                        <input
                          type="checkbox"
                          checked={draft.isActive}
                          onChange={(e) =>
                            setBranchDrafts((m) => ({
                              ...m,
                              [b.id]: { ...draft, isActive: e.target.checked },
                            }))
                          }
                        />
                      </td>
                      <td className="px-3 py-2">
                        <button
                          type="button"
                          disabled={saveBranchMut.isPending}
                          onClick={() =>
                            saveBranchMut.mutate({ id: b.id, name: draft.name, isActive: draft.isActive })
                          }
                          className="text-sky-700 underline"
                        >
                          {t('admin.save')}
                        </button>
                      </td>
                    </tr>
                  )
                })}
            </tbody>
          </table>
        </div>
      </section>
    </div>
  )
}
