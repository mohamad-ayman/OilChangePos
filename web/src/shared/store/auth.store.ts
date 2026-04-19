import { create } from 'zustand'
import { createJSONStorage, persist } from 'zustand/middleware'
import { getCurrentUser, login as loginApi, logout as logoutApi, type AuthUser } from '@/shared/api/auth.api'
import { registerAccessTokenReader, setUnauthorizedHandler } from '@/shared/api/client'

export type AuthStoreState = {
  user: AuthUser | null
  token: string | null
  hasHydrated: boolean
  isAuthenticated: boolean
  login: (credentials: { username: string; password: string }) => Promise<void>
  logout: () => void
  hydrate: () => Promise<void>
}

const storageKey = 'oilchangepos-auth-v1'

export const useAuthStore = create<AuthStoreState>()(
  persist(
    (set, get) => ({
      user: null,
      token: null,
      hasHydrated: false,
      isAuthenticated: false,
      login: async (credentials) => {
        const session = await loginApi(credentials)
        set({
          user: session.user,
          token: session.accessToken,
          isAuthenticated: true,
        })
      },
      logout: () => {
        void logoutApi()
        set({ user: null, token: null, isAuthenticated: false })
      },
      hydrate: async () => {
        await useAuthStore.persist.rehydrate()
        const { token, user } = get()
        set({ isAuthenticated: Boolean(user && token?.length) })
        if (token && user) {
          try {
            const remote = await getCurrentUser(token)
            if (remote && remote.id !== user.id) {
              set({ user: remote })
            }
          } catch {
            // No remote validation yet — keep local session
          }
        }
      },
    }),
    {
      name: storageKey,
      storage: createJSONStorage(() => localStorage),
      partialize: (s) => ({ user: s.user, token: s.token }),
      onRehydrateStorage: () => (state, error) => {
        if (error) {
          console.error('[auth] rehydrate failed', error)
        }
        useAuthStore.setState({
          hasHydrated: true,
          isAuthenticated: Boolean(state?.user && state?.token),
        })
      },
    },
  ),
)

registerAccessTokenReader(() => useAuthStore.getState().token)

setUnauthorizedHandler(() => {
  useAuthStore.getState().logout()
  const path = window.location.pathname
  if (!path.startsWith('/login')) {
    window.location.assign('/login?reason=session')
  }
})
