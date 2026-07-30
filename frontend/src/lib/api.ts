import axios, { AxiosError } from 'axios'

// ---- token storage (localStorage; single owner so auth.tsx and interceptors agree) ----
// ponytail: localStorage is XSS-readable; fine for v1, move to httpOnly cookie if the threat model tightens.
const ACCESS = 'crm.access'
const REFRESH = 'crm.refresh'

export const tokens = {
  access: () => localStorage.getItem(ACCESS),
  refresh: () => localStorage.getItem(REFRESH),
  set: (access: string, refresh: string) => {
    localStorage.setItem(ACCESS, access)
    localStorage.setItem(REFRESH, refresh)
  },
  clear: () => {
    localStorage.removeItem(ACCESS)
    localStorage.removeItem(REFRESH)
  },
}

export const api = axios.create({
  baseURL: '/api',
  headers: { 'Content-Type': 'application/json' },
})

api.interceptors.request.use((config) => {
  const t = tokens.access()
  if (t) config.headers.Authorization = `Bearer ${t}`
  return config
})

// Refresh once on 401, then retry the original request. A failed refresh clears tokens; the router's
// protected layout redirects to /login on the next render.
let refreshing: Promise<string> | null = null

api.interceptors.response.use(
  (r) => r,
  async (error: AxiosError) => {
    const original = error.config
    const url = original?.url ?? ''
    const isAuthCall = url.includes('/auth/login') || url.includes('/auth/refresh')

    if (error.response?.status === 401 && original && !isAuthCall && !(original as { _retried?: boolean })._retried) {
      const rt = tokens.refresh()
      if (!rt) return Promise.reject(toApiError(error))
      try {
        refreshing ??= api
          .post('/auth/refresh', { refreshToken: rt })
          .then((res) => {
            tokens.set(res.data.accessToken, res.data.refreshToken)
            return res.data.accessToken as string
          })
          .finally(() => (refreshing = null))
        const access = await refreshing
        ;(original as { _retried?: boolean })._retried = true
        original.headers!.Authorization = `Bearer ${access}`
        return api(original)
      } catch {
        tokens.clear()
      }
    }
    return Promise.reject(toApiError(error))
  },
)

/** The shared error envelope { code, message, details } (spec §4.3). One place turns any failure into it. */
export type ApiError = { code: string; message: string; details?: unknown }

export function toApiError(error: unknown): ApiError {
  if (axios.isAxiosError(error) && error.response?.data && typeof error.response.data === 'object') {
    const d = error.response.data as Partial<ApiError>
    if (d.code) return { code: d.code, message: d.message ?? d.code, details: d.details }
  }
  return { code: 'network.error', message: 'Sunucuya ulaşılamadı.' }
}
