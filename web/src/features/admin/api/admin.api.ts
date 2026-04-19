import { http } from '@/shared/api/client'

export type AdminUserRow = {
  id: number
  username: string
  role: string
  isActive: boolean
  homeBranchWarehouseId: number | null
  homeBranchName: string | null
}

export type WarehouseAdminRow = {
  id: number
  name: string
  type: number
  isActive: boolean
}

export async function fetchAdminUsers(): Promise<AdminUserRow[]> {
  const { data } = await http.get<AdminUserRow[]>('/api/admin/users')
  return data
}

export async function createAdminUser(payload: {
  username: string
  password: string
  role: string
  homeBranchWarehouseId: number | null
}): Promise<number> {
  const { data } = await http.post<number>('/api/admin/users', payload)
  return data
}

export async function updateAdminUser(
  userId: number,
  payload: { role: string; isActive: boolean; homeBranchWarehouseId: number | null },
): Promise<void> {
  await http.put(`/api/admin/users/${userId}`, payload)
}

export async function resetAdminUserPassword(userId: number, newPassword: string): Promise<void> {
  await http.post(`/api/admin/users/${userId}/password`, { newPassword })
}

export async function fetchBranchesForAdmin(): Promise<WarehouseAdminRow[]> {
  const { data } = await http.get<WarehouseAdminRow[]>('/api/Warehouses/branches-admin')
  return data
}

export async function createWarehouseBranch(name: string): Promise<number> {
  const { data } = await http.post<number>('/api/Warehouses/branches', { name })
  return data
}

export async function updateWarehouseBranch(
  branchId: number,
  payload: { name: string; isActive: boolean },
): Promise<void> {
  await http.put(`/api/Warehouses/branches/${branchId}`, payload)
}
