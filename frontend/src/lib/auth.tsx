import { createContext, useContext, useEffect, useState, type ReactNode } from 'react'
import { useQueryClient } from '@tanstack/react-query'
import { api, refreshAccessToken, tokens } from './api'

export type User = {
  id: string
  email: string
  name: string
  isSuperAdmin: boolean
  mustChangePassword: boolean
  companies: { companyId: string; role: number }[]
}

type AuthContext = {
  user: User | null
  loading: boolean
  impersonating: boolean
  login: (email: string, password: string) => Promise<void>
  /** Adopts a session minted elsewhere (customer code verification returns the same body). The refresh
   *  token is not in it — the server already set that cookie on the same response. */
  adoptSession: (result: { accessToken: string; user: User }) => void
  /** Re-mints the token after a membership change (company opened/deleted). */
  refreshSession: () => Promise<void>
  logout: () => Promise<void>
  impersonate: (userId: string) => Promise<void>
  stopImpersonation: () => Promise<void>
}

const Ctx = createContext<AuthContext | null>(null)

/** The server's non-secret "you are impersonating" marker (Api/Auth/SessionCookie.cs). The tokens
 *  themselves are httpOnly and unreadable here, so this is how the strip survives a page reload. */
function markedAsImpersonating() {
  return document.cookie.split('; ').some((c) => c.startsWith('crm.imp='))
}

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<User | null>(null)
  const [loading, setLoading] = useState(true)
  const [impersonating, setImpersonating] = useState(markedAsImpersonating)
  const queryClient = useQueryClient()

  // Every cached query belongs to whoever the token belonged to when it was fetched. Anything that
  // swaps the identity (login, customer-code adoption, impersonate in/out, logout) has to drop that
  // cache, or the next screen paints the previous person's data until a refetch lands. Seen live on
  // 2026-08-13: after leaving an impersonated session the admin's amount card stayed hidden until F5;
  // on a shared machine the same path flashes user A's ticket list at user B. One wrapper instead of
  // five call sites, so a future identity switch cannot forget it. Clearing unconditionally is fine:
  // these are rare events and the mounted screens refetch immediately.
  function setIdentity(next: User | null) {
    queryClient.clear()
    setUser(next)
  }

  // Boot: the access token is gone (it only ever lived in memory), so ask the refresh cookie for a new
  // one. This IS the "am I logged in?" check now — no cookie, or an expired one, answers 401 and the
  // app starts logged out. The user object comes back on the same response, so no extra /me round trip.
  useEffect(() => {
    refreshAccessToken<User>()
      .then((s) => setUser(s.user))
      .catch(() => tokens.clear())
      .finally(() => setLoading(false))
  }, [])

  async function login(email: string, password: string) {
    const { data } = await api.post('/auth/login', { email, password })
    tokens.set(data.accessToken)
    setIdentity(data.user)
  }

  function adoptSession(result: { accessToken: string; user: User }) {
    tokens.set(result.accessToken)
    setIdentity(result.user)
  }

  // Tenant scope lives in the access token's company_id claims, so a company opened (or deleted) after
  // login stays invisible to every scoped endpoint until the token is re-minted. Refresh re-reads the
  // memberships, so calling this right after the mutation makes the new company usable immediately
  // instead of after the ~15 min token lifetime.
  async function refreshSession() {
    const session = await refreshAccessToken<User>().catch(() => null)
    if (session) setUser(session.user)
  }

  async function logout() {
    // The server revokes the token and clears the cookies; nothing to clear here but the access token
    // in memory. Best-effort: a failed call must still end the session on this device.
    await api.post('/auth/logout').catch(() => {})
    tokens.clear()
    setImpersonating(false)
    setIdentity(null)
  }

  // Super admin steps into another user's session (backend gates SuperAdmin-only, blocks super-admin
  // targets, and audit-logs the real actor). The snapshot of the real admin's session is taken and
  // rolled back server-side now — the browser has no refresh token to save or restore.
  async function impersonate(userId: string) {
    const { data } = await api.post('/auth/impersonate', { userId })
    tokens.set(data.accessToken)
    setIdentity(data.user)
    setImpersonating(true)
  }

  async function stopImpersonation() {
    if (!impersonating) return
    const { data } = await api.post('/auth/stop-impersonation')
    tokens.set(data.accessToken)
    setImpersonating(false)
    setIdentity(data.user)
  }

  return (
    <Ctx.Provider value={{ user, loading, impersonating, login, adoptSession, refreshSession, logout, impersonate, stopImpersonation }}>
      {children}
    </Ctx.Provider>
  )
}

export function useAuth() {
  const c = useContext(Ctx)
  if (!c) throw new Error('useAuth must be used within AuthProvider')
  return c
}
