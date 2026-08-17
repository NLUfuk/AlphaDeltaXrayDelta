import axios, { AxiosError } from 'axios'

// ---- access token: memory only ----
// Both tokens used to live in localStorage. That meant a 14-day refresh token, readable by any script
// that reached this page and by anyone who opened DevTools, and it survived closing the browser. Now
// the refresh token is an httpOnly cookie the server sets (see Api/Auth/SessionCookie.cs) — this file
// never sees it — and the access token lives in this variable, so a tab reload drops it and the app
// asks /auth/refresh for a new one. Worst case for an injected script is the minutes left on one
// access token; it cannot mint another after the tab closes.
let accessToken: string | null = null

export const tokens = {
  access: () => accessToken,
  set: (access: string) => {
    accessToken = access
  },
  clear: () => {
    accessToken = null
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

/** The session body every auth endpoint now returns. No refreshToken field: it went out as a cookie. */
export type Session<TUser = unknown> = { accessToken: string; accessTokenExpiresAt: string; user: TUser }

/** Asks the server for a new access token. Sends no token itself — the refresh token rides along as an
 *  httpOnly cookie the browser attaches on its own. Single-flight: concurrent 401s share one call, so
 *  the rotation is not raced from within this tab. Rejects (401) when there is no usable cookie, which
 *  is also how app startup finds out there is no session. */
export function refreshAccessToken<TUser = unknown>(): Promise<Session<TUser>> {
  refreshing ??= api
    .post<Session<TUser>>('/auth/refresh')
    .then((res) => {
      tokens.set(res.data.accessToken)
      return res.data
    })
    .finally(() => (refreshing = null))
  return refreshing as Promise<Session<TUser>>
}

// Refresh once on 401, then retry the original request. A failed refresh clears the token; the router's
// protected layout redirects to /login on the next render.
let refreshing: Promise<Session> | null = null

api.interceptors.response.use(
  (r) => r,
  async (error: AxiosError) => {
    const original = error.config
    const url = original?.url ?? ''
    const isAuthCall = url.includes('/auth/login') || url.includes('/auth/refresh')

    if (error.response?.status === 401 && original && !isAuthCall && !(original as { _retried?: boolean })._retried) {
      try {
        const session = await refreshAccessToken()
        ;(original as { _retried?: boolean })._retried = true
        original.headers!.Authorization = `Bearer ${session.accessToken}`
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

const NETWORK_ERROR: ApiError = { code: 'network.error', message: 'Sunucuya ulaşılamadı.' }

export function toApiError(error: unknown): ApiError {
  if (axios.isAxiosError(error)) {
    const response = error.response
    // No response at all — DNS, offline, CORS, connection refused. The only true "server unreachable".
    if (!response) return NETWORK_ERROR

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

  // Already an envelope. THIS is what made a healthy server look unreachable: the response interceptor
  // below rejects with `toApiError(...)`, so by the time a screen calls `errorText(err)` the value is an
  // ApiError, never an AxiosError. Converting it a second time failed `isAxiosError` and fell straight
  // through to "Sunucuya ulaşılamadı" — discarding a perfectly good `{code:"invite.invalid"}` the server
  // had actually sent. Idempotence is the fix: converting an already-converted error returns it unchanged.
  if (error !== null && typeof error === 'object' && typeof (error as ApiError).code === 'string')
    return error as ApiError

  return NETWORK_ERROR
}
