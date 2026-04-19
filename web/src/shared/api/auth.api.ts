import axios from 'axios'
import { http } from '@/shared/api/client'

/** Canonical app roles (UI + route guards). Backend returns Admin | Manager | Cashier (legacy "Branch" → manager). */
export type AppAuthRole = 'admin' | 'manager' | 'cashier'

export type LoginCredentials = {
  username: string
  password: string
}

export type AuthUser = {
  id: number
  username: string
  role: AppAuthRole
  isActive: boolean
  homeBranchWarehouseId: number | null
}

export type AuthSession = {
  user: AuthUser
  /** Bearer token when API issues JWT; otherwise a client-issued opaque session id until backend is ready. */
  accessToken: string
}

export class AuthApiError extends Error {
  readonly code: 'invalid_credentials' | 'network' | 'unknown'

  constructor(message: string, code: 'invalid_credentials' | 'network' | 'unknown') {
    super(message)
    this.name = 'AuthApiError'
    this.code = code
  }
}

type LoginResponseDto = {
  id: number
  username: string
  role: string
  isActive: boolean
  homeBranchWarehouseId?: number | null
  accessToken?: string
}

function mapApiRoleToAppRole(apiRole: string): AppAuthRole {
  const r = apiRole.trim().toLowerCase()
  if (r === 'admin') return 'admin'
  if (r === 'manager') return 'manager'
  if (r === 'cashier') return 'cashier'
  if (r === 'branch') return 'manager'
  return 'cashier'
}

function mapLoginDto(dto: LoginResponseDto): AuthUser {
  return {
    id: dto.id,
    username: dto.username,
    role: mapApiRoleToAppRole(dto.role),
    isActive: dto.isActive,
    homeBranchWarehouseId: dto.homeBranchWarehouseId ?? null,
  }
}

function createOpaqueSessionToken(): string {
  if (typeof crypto !== 'undefined' && 'randomUUID' in crypto) {
    return `opaque_${crypto.randomUUID()}`
  }
  return `opaque_${Date.now()}_${Math.random().toString(36).slice(2)}`
}

const useAuthMock = import.meta.env.VITE_USE_AUTH_MOCK === 'true'

// ---------------------------------------------------------------------------
// TEMP MOCK IMPLEMENTATION — Replace when backend auth endpoints are unavailable
// Same exports as real adapter; toggle with VITE_USE_AUTH_MOCK=true
// ---------------------------------------------------------------------------
async function loginMock(credentials: LoginCredentials): Promise<AuthSession> {
  await new Promise((r) => setTimeout(r, 250))
  const u = credentials.username.trim().toLowerCase()
  const ok =
    (u === 'admin' && credentials.password === 'admin123') ||
    (u === 'cashier' && credentials.password === 'admin123')
  if (!ok) {
    throw new AuthApiError('Invalid username or password.', 'invalid_credentials')
  }
  const user: AuthUser =
    u === 'admin'
      ? {
          id: 1,
          username: credentials.username.trim(),
          role: 'admin',
          isActive: true,
          homeBranchWarehouseId: null,
        }
      : {
          id: 2,
          username: credentials.username.trim(),
          role: 'cashier',
          isActive: true,
          homeBranchWarehouseId: 1,
        }
  return { user, accessToken: `mock_jwt_${createOpaqueSessionToken()}` }
}

async function loginReal(credentials: LoginCredentials): Promise<AuthSession> {
  try {
    const { data } = await http.post<LoginResponseDto>('/api/Auth/login', {
      username: credentials.username.trim(),
      password: credentials.password,
    })
    const user = mapLoginDto(data)
    if (!user.isActive) {
      throw new AuthApiError('Account is disabled.', 'invalid_credentials')
    }
    const token =
      typeof data.accessToken === 'string' && data.accessToken.length > 0
        ? data.accessToken
        : createOpaqueSessionToken()
    return { user, accessToken: token }
  } catch (e) {
    if (axios.isAxiosError(e)) {
      if (e.response?.status === 401) {
        const msg =
          typeof e.response.data === 'object' &&
          e.response.data !== null &&
          'error' in e.response.data &&
          typeof (e.response.data as { error?: string }).error === 'string'
            ? (e.response.data as { error: string }).error
            : 'Invalid username or password.'
        throw new AuthApiError(msg, 'invalid_credentials')
      }
      if (e.code === 'ERR_NETWORK' || !e.response) {
        throw new AuthApiError('Unable to reach the server.', 'network')
      }
    }
    throw new AuthApiError('Login failed.', 'unknown')
  }
}

/**
 * Login — uses real `POST /api/Auth/login` unless `VITE_USE_AUTH_MOCK=true`.
 */
export async function login(credentials: LoginCredentials): Promise<AuthSession> {
  if (useAuthMock) {
    return loginMock(credentials)
  }
  return loginReal(credentials)
}

/** Reserved for `POST /api/Auth/logout` when the backend adds it. No-op today. */
export async function logout(): Promise<void> {
  if (useAuthMock) return
  // await http.post('/api/Auth/logout').catch(() => {})
}

/**
 * Validate / fetch current user with Bearer token.
 * When `GET /api/Auth/me` (or similar) exists, implement here. Until then returns `null` and the client keeps persisted user.
 */
export async function getCurrentUser(_accessToken: string): Promise<AuthUser | null> {
  if (useAuthMock) {
    return null
  }
  void _accessToken
  return null
}
