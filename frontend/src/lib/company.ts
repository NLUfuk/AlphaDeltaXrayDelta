import { useSyncExternalStore } from 'react'
import { useAuth } from './auth'

// One admin can own several companies, so the company-scoped screens (kanban, onay kutusu, pano) need
// an "active" one instead of silently pinning to the first membership. Kept in localStorage — it is a
// view preference, not authorization: every request is still scoped server-side by the token's claims.
const KEY = 'crm.company'
const listeners = new Set<() => void>()

function subscribe(fn: () => void) {
  listeners.add(fn)
  return () => void listeners.delete(fn)
}

export function setActiveCompany(companyId: string) {
  localStorage.setItem(KEY, companyId)
  listeners.forEach((fn) => fn())
}

/** The company the staff screens work on: the stored pick if the user is still a member of it
 * (a deleted company must not linger), otherwise their first membership. */
export function useActiveCompany(): string | undefined {
  const { user } = useAuth()
  const stored = useSyncExternalStore(subscribe, () => localStorage.getItem(KEY))
  const ids = user?.companies.map((c) => c.companyId) ?? []
  return stored && ids.includes(stored) ? stored : ids[0]
}
