import { NavLink, Navigate, Outlet } from 'react-router-dom'
import { useAuth } from '../lib/auth'
import { Button, Icon } from '../ui/primitives'

type NavItem = { to: string; label: string; icon: string; end?: boolean }
const NAV: NavItem[] = [
  { to: '/', label: 'Pano', icon: 'view-dashboard-outline', end: true },
  { to: '/reports', label: 'Raporlar', icon: 'chart-line' },
  { to: '/admin/companies', label: 'Şirketler', icon: 'domain' },
  { to: '/admin/permissions', label: 'Yetkiler', icon: 'shield-key-outline' },
]
const SUPER_NAV: NavItem[] = [
  { to: '/admin/users', label: 'Kullanıcılar', icon: 'account-multiple-outline' },
  { to: '/settings', label: 'Ayarlar', icon: 'cog-outline' },
]

/** Protected layout (spec §17.8), Odoo-style app bar: brand + module tabs, active tab highlighted. */
export default function Shell() {
  const { user, loading, logout } = useAuth()
  if (loading) return <div className="p-8 text-slate-500">Yükleniyor…</div>
  if (!user) return <Navigate to="/login" replace />

  const items = user.isSuperAdmin ? [...NAV, ...SUPER_NAV] : NAV

  return (
    <div className="min-h-screen">
      <header className="flex items-center justify-between border-b border-line bg-white px-5 shadow-sm">
        <nav className="flex items-center gap-1">
          <span className="mr-3 font-semibold text-primary">CRM·Kanban</span>
          {items.map((it) => (
            <NavLink
              key={it.to}
              to={it.to}
              end={it.end}
              className={({ isActive }) =>
                `flex items-center gap-1.5 border-b-2 px-3 py-3 text-sm transition-colors ${
                  isActive ? 'border-primary font-medium text-primary' : 'border-transparent text-slate-600 hover:text-primary'
                }`
              }
            >
              <Icon name={it.icon} className="text-base" />
              {it.label}
            </NavLink>
          ))}
        </nav>
        <div className="flex items-center gap-3 text-sm text-slate-600">
          <span className="hidden sm:inline">{user.name}</span>
          <span className="flex h-8 w-8 items-center justify-center rounded-full bg-primary/10 text-xs font-semibold text-primary">
            {user.name.slice(0, 1).toUpperCase()}
          </span>
          <Button variant="secondary" onClick={logout}><Icon name="logout" className="mr-1" />Çıkış</Button>
        </div>
      </header>
      <main className="p-6">
        <Outlet />
      </main>
    </div>
  )
}
