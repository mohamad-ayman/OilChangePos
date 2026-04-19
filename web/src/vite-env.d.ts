/// <reference types="vite/client" />

interface ImportMetaEnv {
  readonly VITE_API_BASE_URL?: string
  /** Set to `true` to use mock login in `auth.api.ts` (no backend). */
  readonly VITE_USE_AUTH_MOCK?: string
  /** Set to `true` for mock inventory API in `inventory.api.ts`. */
  readonly VITE_INVENTORY_MOCK?: string
}

interface ImportMeta {
  readonly env: ImportMetaEnv
}
