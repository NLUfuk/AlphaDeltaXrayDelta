import { NavLink, Navigate, Outlet } from 'react-router-dom'
import { useAuth } from '../lib/auth'
import { Button, Icon } from '../ui/primitives'

type NavItem = { to: string; label: string; icon: string; end?: boolean }
const NAV: NavItem[] = [
  { to: '/', label: 'Pano', icon: 'view-dashboard-outline', end: true },
  { to: '/moderation', label: 'Onay kutusu', icon: 'inbox-arrow-down-outline' },
  { to: '/admin/columns', label: 'Sütunlar', icon: 'view-column-outline' },
  { to: '/admin/form-fields', label: 'Form alanları', icon: 'form-select' },
  { to: '/reports', label: 'Raporlar', icon: 'chart-line' },
  { to: '/admin/companies', label: 'Şirketler', icon: 'domain' },
  { to: '/admin/permissions', label: 'Yetkiler', icon: 'shield-key-outline' },
]
const SUPER_NAV: NavItem[] = [
  { to: '/admin/users', label: 'Kullanıcılar', icon: 'account-multiple-outline' },
  { to: '/admin/templates', label: 'Şablonlar', icon: 'email-edit-outline' },
  { to: '/settings', label: 'Ayarlar', icon: 'cog-outline' },
]
// A customer (no company membership) only has their own tickets — the staff tabs would 403 anyway.
const CUSTOMER_NAV: NavItem[] = [
  { to: '/', label: 'Taleplerim', icon: 'ticket-outline', end: true },
]

/** Protected layout (spec §17.8): a minimal sticky app bar — wordmark + module tabs + account. */
export default function Shell() {
  const { user, loading, logout, impersonating, stopImpersonation } = useAuth()
  if (loading) return <div className="p-8 text-muted">Yükleniyor…</div>
  if (!user) return <Navigate to="/login" replace />

  const isStaff = user.isSuperAdmin || user.companies.length > 0
  const items = user.isSuperAdmin ? [...NAV, ...SUPER_NAV] : isStaff ? NAV : CUSTOMER_NAV

  return (
    <div className="min-h-screen">
      {impersonating && (
        <div className="flex items-center justify-center gap-3 bg-amber-500 px-4 py-1.5 text-sm text-white">
          <Icon name="account-eye-outline" />
          <span><b>{user.name}</b> kimliğiyle görüntülüyorsunuz.</span>
          <button onClick={() => stopImpersonation()} className="font-semibold underline underline-offset-2">
            Yönetici hesabına dön
          </button>
        </div>
      )}
      <header className="sticky top-0 z-10 border-b border-line bg-surface/90 backdrop-blur">
        <div className="mx-auto flex max-w-7xl items-center justify-between px-5">
          <nav className="flex items-center gap-1 overflow-x-auto">
            <span className="mr-4 flex items-center gap-1.5 font-semibold text-ink">
              <span className="grid h-6 w-6 place-items-center rounded-md bg-primary text-xs text-white">K</span>
              Kanban
            </span>
            {items.map((it) => (
              <NavLink
                key={it.to}
                to={it.to}
                end={it.end}
                className={({ isActive }) =>
                  `flex items-center gap-1.5 whitespace-nowrap border-b-2 px-3 py-3 text-sm transition-colors ${
                    isActive ? 'border-primary font-medium text-primary' : 'border-transparent text-muted hover:text-ink'
                  }`
                }
              >
                <Icon name={it.icon} className="text-base" />
                {it.label}
              </NavLink>
            ))}
          </nav>
          <div className="flex items-center gap-3 text-sm text-muted">
            <NavLink to="/account" className="flex items-center gap-2 rounded-full py-1 pl-1 pr-2 transition-colors hover:bg-primary/5" title="Hesabım">
              <span className="grid h-8 w-8 place-items-center rounded-full bg-primary/10 text-xs font-semibold text-primary">
                {user.name.slice(0, 1).toUpperCase()}
              </span>
              <span className="hidden text-ink sm:inline">{user.name}</span>
            </NavLink>
            <Button variant="secondary" onClick={logout}><Icon name="logout" className="mr-1" />Çıkış</Button>
          </div>
        </div>
      </header>
      <main className="mx-auto max-w-7xl p-6">
        <Outlet />
      </main>
    </div>
  )
}
