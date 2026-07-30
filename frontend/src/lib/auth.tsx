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
  login: (email: string, password: string) => Promise<void>
  logout: () => Promise<void>
}

const Ctx = createContext<AuthContext | null>(null)

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<User | null>(null)
  const [loading, setLoading] = useState(true)

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
    setUser(null)
  }

  return <Ctx.Provider value={{ user, loading, login, logout }}>{children}</Ctx.Provider>
}

export function useAuth() {
  const c = useContext(Ctx)
  if (!c) throw new Error('useAuth must be used within AuthProvider')
  return c
}
