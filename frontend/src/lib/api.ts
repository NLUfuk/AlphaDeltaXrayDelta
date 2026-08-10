import axios, { AxiosError } from 'axios'

// ---- token storage (localStorage; single owner so auth.tsx and interceptors agree) ----
// ponytail: localStorage is XSS-readable; fine for v1, move to httpOnly cookie if the threat model tightens.
const ACCESS = 'crm.access'
const REFRESH = 'crm.refresh'
// While a super admin is impersonating, the real admin session is snapshotted here so they can return.
const ORIG_ACCESS = 'crm.orig.access'
const ORIG_REFRESH = 'crm.orig.refresh'

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
    localStorage.removeItem(ORIG_ACCESS)
    localStorage.removeItem(ORIG_REFRESH)
  },

  isImpersonating: () => !!localStorage.getItem(ORIG_ACCESS),
  // Snapshot the current (real admin) session once — a nested impersonate must not clobber the snapshot.
  beginImpersonation: () => {
    if (localStorage.getItem(ORIG_ACCESS)) return
    localStorage.setItem(ORIG_ACCESS, localStorage.getItem(ACCESS) ?? '')
    localStorage.setItem(ORIG_REFRESH, localStorage.getItem(REFRESH) ?? '')
  },
  // Restore the snapshotted admin session; returns false if there was none.
  endImpersonation: () => {
    const a = localStorage.getItem(ORIG_ACCESS)
    const r = localStorage.getItem(ORIG_REFRESH)
    localStorage.removeItem(ORIG_ACCESS)
    localStorage.removeItem(ORIG_REFRESH)
    if (!a || !r) return false
    localStorage.setItem(ACCESS, a)
    localStorage.setItem(REFRESH, r)
    return true
  },
}

// No global Content-Type: axios sets application/json for object bodies on its own, and — crucially —
// sets multipart/form-data with the right boundary for FormData uploads. A hardcoded json default here
// would override the multipart type and break file uploads (415).
export const api = axios.create({ baseURL: '/api' })

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
/** Per-field reasons a validation failure carries (`validation.failed`). Rendered by `errorText`. */
export type ApiFieldError = { field: string; error: string }

// Not every failure reaches us as our envelope. The rate limiter rejects with a bare 429, the JWT
// middleware with a bare 401, a proxy or a crashed worker with an HTML 502 — all bodiless, or at least
// code-less. Those used to fall into the same bucket as "the request never left the browser" and the
// user was told "Sunucuya ulaşılamadı" while the server was answering perfectly well. The status line
// is the only thing left to read at that point, so read it.
const BY_STATUS: Record<number, ApiError> = {
  400: { code: 'validation.failed', message: 'Gönderilen bilgiler geçersiz.' },
  401: { code: 'auth.required', message: 'Oturumunuz sona ermiş. Lütfen tekrar giriş yapın.' },
  403: { code: 'forbidden', message: 'Bu işlem için yetkiniz yok.' },
  404: { code: 'not_found', message: 'Aradığınız kayıt bulunamadı.' },
  409: { code: 'conflict', message: 'Bu işlem mevcut kayıtla çakışıyor.' },
  413: { code: 'attachment.too_large', message: 'Dosya boyutu izin verilen sınırın dışında.' },
  429: { code: 'rate.limited', message: 'Çok fazla deneme yaptınız. Bir dakika bekleyip tekrar deneyin.' },
}

export function toApiError(error: unknown): ApiError {
  if (!axios.isAxiosError(error)) return { code: 'network.error', message: 'Sunucuya ulaşılamadı.' }

  const response = error.response
  // No response at all — DNS, offline, CORS, connection refused. The only true "server unreachable".
  if (!response) return { code: 'network.error', message: 'Sunucuya ulaşılamadı.' }

  if (response.data && typeof response.data === 'object') {
    const d = response.data as Partial<ApiError>
    if (d.code) return { code: d.code, message: d.message ?? d.code, details: d.details }
  }

  return (
    BY_STATUS[response.status] ??
    (response.status >= 500
      ? { code: 'server.error', message: 'Sunucuda bir hata oluştu. Lütfen daha sonra tekrar deneyin.' }
      : { code: 'unknown.error', message: 'Beklenmeyen bir hata oluştu.' })
  )
}
