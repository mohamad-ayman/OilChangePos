import axios, { type AxiosError } from 'axios'
import { getApiBaseUrl } from '@/shared/config/env'

let readAccessToken: () => string | null = () => null

/** Register how the client reads the Bearer token (typically from auth store). */
export function registerAccessTokenReader(reader: () => string | null): void {
  readAccessToken = reader
}

let onUnauthorized: (() => void) | null = null

/** Global 401 handler (e.g. clear session + redirect to login). */
export function setUnauthorizedHandler(handler: (() => void) | null): void {
  onUnauthorized = handler
}

function isAuthLoginRequest(url: string | undefined): boolean {
  if (!url) return false
  return url.replace(/\\/g, '/').includes('/api/Auth/login')
}

/** Typed Axios instance; `baseURL` matches `getApiBaseUrl()` (empty in dev → Vite `/api` proxy). */
export const http = axios.create({
  baseURL: getApiBaseUrl(),
  headers: {
    'Content-Type': 'application/json',
  },
  validateStatus: (status) => status >= 200 && status < 300,
})

http.interceptors.request.use((config) => {
  if (isAuthLoginRequest(config.url)) {
    return config
  }
  const token = readAccessToken()
  if (token) {
    config.headers.Authorization = `Bearer ${token}`
  }
  return config
})

http.interceptors.response.use(
  (response) => response,
  (error: AxiosError) => {
    const status = error.response?.status
    if (status === 401 && !isAuthLoginRequest(error.config?.url)) {
      onUnauthorized?.()
    }
    return Promise.reject(error)
  },
)
