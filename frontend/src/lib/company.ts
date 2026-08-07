import { useSyncExternalStore } from 'react'
import { useCompanies } from './admin'
import { useAuth } from './auth'

// One admin can own several companies, so the company-scoped screens (kanban, onay kutusu, pano) need
// an "active" one instead of silently pinning to the first membership. Kept in localStorage — it is a
// view preference, not authorization: every request is still scoped server-side by the token's claims.
const KEY = 'crm.company'
const listeners = new Set<() => void>()

/** Super-admin-only pick: "every company at once". Report screens read it as "no company filter"
 * (the global totals they used to show unconditionally); per-company screens — the board, the
 * moderation queue, the column and form editors — ask for a real company instead, because there is
 * no such thing as one board across four companies. */
export const ALL_COMPANIES = 'all'

function subscribe(fn: () => void) {
  listeners.add(fn)
  return () => void listeners.delete(fn)
}

export function setActiveCompany(companyId: string) {
  localStorage.setItem(KEY, companyId)
  listeners.forEach((fn) => fn())
}

/**
 * Companies this user may switch between. `GET /companies` is the authority: memberships for staff,
 * every company for a super admin (CompanyService.ListAsync).
 *
 * The token's membership claims are used as an instant first answer so staff screens do not flash an
 * empty state while the request is in flight. They cannot be the only source: a super admin holds no
 * memberships, so deriving the active company from claims alone left them with no board at all
 * ("Bu kullanıcı bir şirkete bağlı değil") — the one role that is supposed to see everything.
 */
export function useSelectableCompanies(): { id: string; name: string }[] {
  return useCompanies().data?.map((c) => ({ id: c.id, name: c.name })) ?? []
}

/** The company the staff screens work on: the stored pick if it is still selectable (a deleted
 * company must not linger), otherwise the first one available.
 *
 * Ids only, so the token's membership claims can stand in while `GET /companies` is in flight —
 * that keeps staff screens from flashing an empty state. Names are not in the claims (`/me` returns
 * ids and roles), which is why the switcher above reads the fetched list instead. */
export function useActiveCompany(): string | undefined {
  const { user } = useAuth()
  const fetched = useSelectableCompanies()
  const stored = useSyncExternalStore(subscribe, () => localStorage.getItem(KEY))
  // Only a super admin can hold the "all companies" pick. Guarding on the role matters because the
  // value survives in localStorage: an impersonated session, or an account that lost the role, would
  // otherwise keep asking for a scope the server will not serve.
  if (stored === ALL_COMPANIES && user?.isSuperAdmin) return ALL_COMPANIES
  const claimed = user?.companies.map((c) => c.companyId) ?? []
  const ids = fetched.length ? fetched.map((c) => c.id) : claimed
  if (stored && ids.includes(stored)) return stored
  // No stored pick yet: only fall back to the claims when there is nothing to choose between. The
  // claims come out in membership order, which for a two-company admin put the SECOND company first
  // — the screens then loaded that one while the navbar switcher (which waits for the named, sorted
  // list) said the other. Guessing from an arbitrary order is worse than one render of "loading".
  return fetched.length || claimed.length === 1 ? ids[0] : undefined
}

/** The company filter the report endpoint wants: a company id, or null for "every company".
 *
 * Pano and Kazanç used to pin a super admin to null unconditionally, so the switcher changed the
 * navbar and nothing else — the one role that can reach every company could not scope a report to
 * one of them. Now they follow the pick like every other screen, and the global view is an explicit
 * choice in the switcher rather than a role's silent default. */
export function useReportCompany(): string | null {
  const active = useActiveCompany()
  return !active || active === ALL_COMPANIES ? null : active
}
