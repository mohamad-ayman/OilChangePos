import { isAxiosError } from 'axios'

/** Turns axios JSON `{ error, type }` or ProblemDetails into a string safe for React text nodes. */
export function formatApiError(e: unknown): string {
  if (isAxiosError(e)) {
    const d = e.response?.data
    if (typeof d === 'string') return d
    if (d && typeof d === 'object') {
      const o = d as Record<string, unknown>
      if (typeof o.error === 'string') {
        const extra =
          typeof o.detail === 'string' && o.detail.trim().length > 0
            ? `\n${o.detail.trim()}`
            : ''
        return o.error + extra
      }
      if (typeof o.title === 'string') return o.title
      if (typeof o.detail === 'string') return o.detail
      if (typeof o.message === 'string') return o.message
      try {
        return JSON.stringify(d)
      } catch {
        return e.message || 'Request failed'
      }
    }
    return e.message || 'Request failed'
  }
  if (e instanceof Error) return e.message
  return String(e)
}
