import { createContext, useContext, useEffect, useState, type ReactNode } from 'react'
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
  logout: () => Promise<void>
  impersonate: (userId: string) => Promise<void>
  stopImpersonation: () => Promise<void>
}

const Ctx = createContext<AuthContext | null>(null)

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<User | null>(null)
  const [loading, setLoading] = useState(true)
  const [impersonating, setImpersonating] = useState(tokens.isImpersonating())

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
    setUser(data.user)
  }

  async function logout() {
    const rt = tokens.refresh()
    if (rt) await api.post('/auth/logout', { refreshToken: rt }).catch(() => {})
    tokens.clear()
    setImpersonating(false)
    setUser(null)
  }

  // Super admin steps into another user's session (backend gates SuperAdmin-only, blocks super-admin
  // targets, and audit-logs the real actor). The real admin session is snapshotted so they can return.
  async function impersonate(userId: string) {
    tokens.beginImpersonation()
    try {
      const { data } = await api.post('/auth/impersonate', { userId })
      tokens.set(data.accessToken, data.refreshToken)
      setUser(data.user)
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
    setUser(data)
  }

  return (
    <Ctx.Provider value={{ user, loading, impersonating, login, logout, impersonate, stopImpersonation }}>
      {children}
    </Ctx.Provider>
  )
}

export function useAuth() {
  const c = useContext(Ctx)
  if (!c) throw new Error('useAuth must be used within AuthProvider')
  return c
}
