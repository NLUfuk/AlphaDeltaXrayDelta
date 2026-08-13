import { createContext, useContext, useEffect, useState, type ReactNode } from 'react'
import { useQueryClient } from '@tanstack/react-query'
import { api, tokens } from './api'

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
  /** Adopts a session minted elsewhere (customer code verification returns the same AuthResult). */
  adoptSession: (result: { accessToken: string; refreshToken: string; user: User }) => void
  /** Re-mints the token after a membership change (company opened/deleted). */
  refreshSession: () => Promise<void>
  logout: () => Promise<void>
  impersonate: (userId: string) => Promise<void>
  stopImpersonation: () => Promise<void>
}

const Ctx = createContext<AuthContext | null>(null)

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<User | null>(null)
  const [loading, setLoading] = useState(true)
  const [impersonating, setImpersonating] = useState(tokens.isImpersonating())
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

  // Hydrate from an existing token on load (survives refresh); a failed /me clears the session.
  useEffect(() => {
    if (!tokens.access()) return setLoading(false)
    api
      .get<User>('/auth/me')
      .then((r) => setUser(r.data))
      .catch(() => tokens.clear())
      .finally(() => setLoading(false))
  }, [])

  async function login(email: string, password: string) {
    const { data } = await api.post('/auth/login', { email, password })
    tokens.set(data.accessToken, data.refreshToken)
    setIdentity(data.user)
  }

  function adoptSession(result: { accessToken: string; refreshToken: string; user: User }) {
    tokens.set(result.accessToken, result.refreshToken)
    setIdentity(result.user)
  }

  // Tenant scope lives in the access token's company_id claims, so a company opened (or deleted) after
  // login stays invisible to every scoped endpoint until the token is re-minted. Refresh re-reads the
  // memberships, so calling this right after the mutation makes the new company usable immediately
  // instead of after the ~15 min token lifetime.
  async function refreshSession() {
    const rt = tokens.refresh()
    if (!rt) return
    const { data } = await api.post('/auth/refresh', { refreshToken: rt })
    tokens.set(data.accessToken, data.refreshToken)
    setUser(data.user)
  }

  async function logout() {
    const rt = tokens.refresh()
    if (rt) await api.post('/auth/logout', { refreshToken: rt }).catch(() => {})
    tokens.clear()
    setImpersonating(false)
    setIdentity(null)
  }

  // Super admin steps into another user's session (backend gates SuperAdmin-only, blocks super-admin
  // targets, and audit-logs the real actor). The real admin session is snapshotted so they can return.
  async function impersonate(userId: string) {
    tokens.beginImpersonation()
    try {
      const { data } = await api.post('/auth/impersonate', { userId })
      tokens.set(data.accessToken, data.refreshToken)
      setIdentity(data.user)
      setImpersonating(true)
    } catch (e) {
      tokens.endImpersonation() // roll back the snapshot on failure
      throw e
    }
  }

  async function stopImpersonation() {
    if (!tokens.endImpersonation()) return
    setImpersonating(false)
    const { data } = await api.get<User>('/auth/me')
    setIdentity(data)
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
