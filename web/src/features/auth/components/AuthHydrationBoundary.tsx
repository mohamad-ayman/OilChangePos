import { useEffect } from 'react'
import { Outlet } from 'react-router-dom'
import { useAuthStore } from '@/shared/store/auth.store'

/** Runs once on boot: rehydrate persisted auth + optional remote session validation. */
export function AuthHydrationBoundary() {
  useEffect(() => {
    void useAuthStore.getState().hydrate()
  }, [])

  return <Outlet />
}
