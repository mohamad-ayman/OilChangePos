import { useQuery } from '@tanstack/react-query'
import { http } from '@/shared/api/client'

export type HealthDto = { ok: boolean }

export const healthQueryKey = ['health'] as const

export async function fetchHealth(): Promise<HealthDto> {
  const { data } = await http.get<HealthDto>('/api/Health')
  return data
}

export function useHealthQuery() {
  return useQuery({
    queryKey: healthQueryKey,
    queryFn: fetchHealth,
  })
}
