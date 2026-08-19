import { useState } from 'react'
import { NavLink, Navigate, Outlet } from 'react-router-dom'
import { useAuth } from '../lib/auth'
import { ALL_COMPANIES, setActiveCompany, useActiveCompany, useSelectableCompanies } from '../lib/company'
import { useNotifications } from '../lib/notifications'
import { isDark, toggleTheme } from '../lib/theme'
import { Logo, LogoMark } from '../ui/Logo'
import { Button, Icon, Loading, Select } from '../ui/primitives'

type NavItem = { to: string; label: string; icon: string; end?: boolean }
const NAV: NavItem[] = [
  { to: '/', label: 'Ana sayfa', icon: 'home-outline', end: true },
  { to: '/pano', label: 'Pano', icon: 'view-dashboard-outline' },
  { to: '/moderation', label: 'Onay kutusu', icon: 'inbox-arrow-down-outline' },
  { to: '/admin/columns', label: 'Sütunlar', icon: 'view-column-outline' },
  { to: '/admin/form-fields', label: 'Form alanları', icon: 'form-select' },
  { to: '/revenue', label: 'Kazanç', icon: 'cash-multiple' },
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
  { to: '/', label: 'Ana sayfa', icon: 'home-outline', end: true },
  { to: '/taleplerim', label: 'Taleplerim', icon: 'ticket-outline' },
]

function NavLinks({ items, onNavigate, collapsed }: { items: NavItem[]; onNavigate: () => void; collapsed: boolean }) {
  return (
    <>
      {items.map((it) => (
        <NavLink
          key={it.to}
          to={it.to}
          end={it.end}
          onClick={onNavigate}
          title={collapsed ? it.label : undefined}
          className={({ isActive }) =>
            `flex items-center gap-3 border-l-[3px] py-2.5 text-sm transition-colors ${
              collapsed ? 'justify-center px-0' : 'px-5'
            } ${
              isActive
                ? 'border-primary bg-primary/8 font-semibold text-primary'
                : 'border-transparent text-muted hover:bg-canvas hover:text-ink'
            }`
          }
        >
          <Icon name={it.icon} className="text-lg" />
          {!collapsed && it.label}
        </NavLink>
      ))}
    </>
  )
}

/** Company picker for anyone who can reach more than one company — the staff screens all read the
 * active one. Rendered only in that case, so single-company users (and customers) see no clutter.
 * Returns null rather than an empty <select> while the list is still loading. */
function CompanySwitcher() {
  const { user } = useAuth()
  const companies = useSelectableCompanies()
  const active = useActiveCompany()
  if (companies.length < 2) return null
  // The icon is not decoration: as a bare dropdown this read as a filter, and an admin with two
  // companies could not tell it was what decided which company every screen was showing.
  return (
    <label className="flex items-center gap-1.5" title="Aktif şirket — tüm ekranlar bu şirketi gösterir">
      <Icon name="domain" className="text-lg text-muted" />
      <Select
        value={active ?? ''}
        onChange={(e) => setActiveCompany(e.target.value)}
        aria-label="Aktif şirket"
        className="max-w-[12rem] py-1.5"
      >
        {/* Only the super admin gets the cross-company view, and only by asking for it: their
            reports used to be pinned to it, which left the switcher looking broken. */}
        {user?.isSuperAdmin && <option value={ALL_COMPANIES}>Tüm şirketler</option>}
        {companies.map((c) => <option key={c.id} value={c.id}>{c.name}</option>)}
      </Select>
    </label>
  )
}

/** Unread counter in the navbar. Shares the home screen's query key, so opening the app costs one
 * request for both and marking everything read updates the badge without a refetch of its own. The
 * bell links to the home screen rather than opening a dropdown: the full list is already there, and a
 * second copy of it would be a second place to keep correct. */
function NotificationBell() {
  const { data } = useNotifications()
  const unread = data?.unreadCount ?? 0
  return (
    <NavLink
      to="/"
      end
      className="relative grid h-9 w-9 place-items-center rounded-lg text-muted hover:bg-canvas"
      title={unread > 0 ? `${unread} yeni bildirim` : 'Bildirimler'}
      aria-label={unread > 0 ? `${unread} yeni bildirim` : 'Bildirimler'}
    >
      <Icon name={unread > 0 ? 'bell-ring-outline' : 'bell-outline'} className="text-xl" />
      {unread > 0 && (
        <span className="absolute -right-0.5 -top-0.5 grid min-w-4 place-items-center rounded-full bg-danger px-1 text-[10px] font-bold leading-4 text-white">
          {unread > 9 ? '9+' : unread}
        </span>
      )}
    </NavLink>
  )
}

/** Protected layout (spec §17.8): StarAdmin-style fixed sidebar + top navbar. The sidebar collapses to
 * an icon rail (desktop) and the whole app has a dark theme — both toggled from the navbar, both persisted. */
export default function Shell() {
  const { user, loading, logout, impersonating, stopImpersonation } = useAuth()
  const [open, setOpen] = useState(false) // mobile off-canvas sidebar
  const [collapsed, setCollapsed] = useState(() => localStorage.getItem('crm.sidebar') === 'collapsed')
  const [dark, setDark] = useState(isDark())
  if (loading) return <Loading className="p-8" />
  if (!user) return <Navigate to="/login" replace />

  const isStaff = user.isSuperAdmin || user.companies.length > 0
  const close = () => setOpen(false)
  const toggleCollapsed = () =>
    setCollapsed((c) => {
      const next = !c
      localStorage.setItem('crm.sidebar', next ? 'collapsed' : 'open')
      return next
    })

  return (
    <div className="min-h-screen">
      {/* Sticky, and above the sidebar's z-30: this strip is the only sign that the screen belongs to
          someone else and the only way back. It used to scroll away while the navbar stayed put, so on
          a long list an admin could act as a customer with nothing on screen saying so. */}
      {impersonating && (
        <div className="sticky top-0 z-40 flex items-center justify-center gap-3 bg-amber-500 px-4 py-1.5 text-sm text-white">
          <Icon name="account-eye-outline" />
          <span><b>{user.name}</b> kimliğiyle görüntülüyorsunuz.</span>
          <button onClick={() => stopImpersonation()} className="font-semibold underline underline-offset-2">
            Yönetici hesabına dön
          </button>
        </div>
      )}

      <div className="flex">
        {/* Sidebar */}
        <aside
          className={`fixed inset-y-0 left-0 z-30 flex w-60 flex-col bg-surface shadow-[var(--shadow-sidebar)] transition-all lg:sticky lg:top-0 lg:h-screen lg:translate-x-0 ${
            open ? 'translate-x-0' : '-translate-x-full'
          } ${collapsed ? 'lg:w-16' : 'lg:w-60'}`}
        >
          {/* Collapsed rail shows the mark alone — the wordmark would not fit and a truncated one
              reads as a glitch, not as branding. */}
          <div className={`flex h-16 shrink-0 items-center ${collapsed ? 'justify-center px-0' : 'px-5'}`}>
            {collapsed ? <LogoMark className="h-8 w-8 text-ink" /> : <Logo />}
          </div>
          <nav className="flex-1 overflow-y-auto pb-6">
            {isStaff ? (
              <>
                {!collapsed && <p className="px-5 pb-1 pt-4 text-[11px] font-bold uppercase tracking-widest text-muted">Menü</p>}
                <NavLinks items={NAV} onNavigate={close} collapsed={collapsed} />
                {user.isSuperAdmin && (
                  <>
                    {!collapsed && <p className="px-5 pb-1 pt-5 text-[11px] font-bold uppercase tracking-widest text-muted">Yönetim</p>}
                    <NavLinks items={SUPER_NAV} onNavigate={close} collapsed={collapsed} />
                  </>
                )}
              </>
            ) : (
              <div className="pt-4">
                <NavLinks items={CUSTOMER_NAV} onNavigate={close} collapsed={collapsed} />
              </div>
            )}
          </nav>
        </aside>

        {/* Backdrop for mobile */}
        {open && <div className="fixed inset-0 z-20 bg-black/30 lg:hidden" onClick={close} />}

        {/* Main column */}
        <div className="flex min-w-0 flex-1 flex-col">
          <header className="sticky top-0 z-10 flex h-16 items-center justify-between border-b border-line bg-surface/90 px-5 backdrop-blur">
            <div className="flex items-center gap-1">
              <button
                className="grid h-9 w-9 place-items-center rounded-lg text-muted hover:bg-canvas lg:hidden"
                onClick={() => setOpen(true)}
                aria-label="Menü"
              >
                <Icon name="menu" className="text-xl" />
              </button>
              <button
                className="hidden h-9 w-9 place-items-center rounded-lg text-muted hover:bg-canvas lg:grid"
                onClick={toggleCollapsed}
                aria-label={collapsed ? 'Menüyü genişlet' : 'Menüyü daralt'}
                title={collapsed ? 'Menüyü genişlet' : 'Menüyü daralt'}
              >
                <Icon name={collapsed ? 'chevron-double-right' : 'chevron-double-left'} className="text-xl" />
              </button>
            </div>
            <div className="flex items-center justify-end gap-3 text-sm text-muted">
              <CompanySwitcher />
              <NotificationBell />
              <button
                className="grid h-9 w-9 place-items-center rounded-lg text-muted hover:bg-canvas"
                onClick={() => setDark(toggleTheme())}
                aria-label={dark ? 'Açık temaya geç' : 'Koyu temaya geç'}
                title={dark ? 'Açık tema' : 'Koyu tema'}
              >
                <Icon name={dark ? 'weather-sunny' : 'weather-night'} className="text-xl" />
              </button>
              <NavLink to="/account" className="flex items-center gap-2 rounded-full py-1 pl-1 pr-2 transition-colors hover:bg-primary/5" title="Hesabım">
                <span className="grid h-8 w-8 place-items-center rounded-full bg-primary/10 text-xs font-semibold text-primary">
                  {user.name.slice(0, 1).toUpperCase()}
                </span>
                <span className="hidden text-ink sm:inline">{user.name}</span>
              </NavLink>
              <Button variant="secondary" onClick={logout}><Icon name="logout" className="mr-1" />Çıkış</Button>
            </div>
          </header>
          <main className="mx-auto w-full max-w-7xl flex-1 p-6">
            <Outlet />
          </main>
        </div>
      </div>
    </div>
  )
}
