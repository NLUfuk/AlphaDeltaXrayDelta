import { useState } from 'react'
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

function NavLinks({ items, onNavigate }: { items: NavItem[]; onNavigate: () => void }) {
  return (
    <>
      {items.map((it) => (
        <NavLink
          key={it.to}
          to={it.to}
          end={it.end}
          onClick={onNavigate}
          className={({ isActive }) =>
            `flex items-center gap-3 border-l-[3px] px-5 py-2.5 text-sm transition-colors ${
              isActive
                ? 'border-primary bg-primary/8 font-semibold text-primary'
                : 'border-transparent text-[#484848] hover:bg-canvas hover:text-ink'
            }`
          }
        >
          <Icon name={it.icon} className="text-lg" />
          {it.label}
        </NavLink>
      ))}
    </>
  )
}

/** Protected layout (spec §17.8): StarAdmin-style fixed light sidebar + white top navbar. */
export default function Shell() {
  const { user, loading, logout, impersonating, stopImpersonation } = useAuth()
  const [open, setOpen] = useState(false) // mobile off-canvas sidebar
  if (loading) return <div className="p-8 text-muted">Yükleniyor…</div>
  if (!user) return <Navigate to="/login" replace />

  const isStaff = user.isSuperAdmin || user.companies.length > 0
  const close = () => setOpen(false)

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

      <div className="flex">
        {/* Sidebar */}
        <aside
          className={`fixed inset-y-0 left-0 z-30 flex w-60 flex-col bg-surface shadow-[var(--shadow-sidebar)] transition-transform lg:sticky lg:top-0 lg:h-screen lg:translate-x-0 ${
            open ? 'translate-x-0' : '-translate-x-full'
          }`}
        >
          <div className="flex h-16 shrink-0 items-center gap-2.5 px-5">
            <span className="grid h-9 w-9 place-items-center rounded-lg bg-primary text-sm font-bold text-white">K</span>
            <span className="text-lg font-extrabold tracking-tight text-ink">CRM Kanban</span>
          </div>
          <nav className="flex-1 overflow-y-auto pb-6">
            {isStaff ? (
              <>
                <p className="px-5 pb-1 pt-4 text-[11px] font-bold uppercase tracking-widest text-slate-400">Menü</p>
                <NavLinks items={NAV} onNavigate={close} />
                {user.isSuperAdmin && (
                  <>
                    <p className="px-5 pb-1 pt-5 text-[11px] font-bold uppercase tracking-widest text-slate-400">Yönetim</p>
                    <NavLinks items={SUPER_NAV} onNavigate={close} />
                  </>
                )}
              </>
            ) : (
              <div className="pt-4">
                <NavLinks items={CUSTOMER_NAV} onNavigate={close} />
              </div>
            )}
          </nav>
        </aside>

        {/* Backdrop for mobile */}
        {open && <div className="fixed inset-0 z-20 bg-black/30 lg:hidden" onClick={close} />}

        {/* Main column */}
        <div className="flex min-w-0 flex-1 flex-col">
          <header className="sticky top-0 z-10 flex h-16 items-center justify-between border-b border-line bg-surface/90 px-5 backdrop-blur">
            <button
              className="grid h-9 w-9 place-items-center rounded-lg text-muted hover:bg-canvas lg:hidden"
              onClick={() => setOpen(true)}
              aria-label="Menü"
            >
              <Icon name="menu" className="text-xl" />
            </button>
            <div className="flex flex-1 items-center justify-end gap-3 text-sm text-muted">
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
