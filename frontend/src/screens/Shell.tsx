import { Link, Navigate, Outlet } from 'react-router-dom'
import { useAuth } from '../lib/auth'
import { Button } from '../ui/primitives'

/** Protected layout (spec §17.8). Redirects to /login when unauthenticated; screens render in the outlet. */
export default function Shell() {
  const { user, loading, logout } = useAuth()
  if (loading) return <div className="p-8 text-slate-500">Yükleniyor…</div>
  if (!user) return <Navigate to="/login" replace />

  return (
    <div className="min-h-screen bg-slate-50">
      <header className="flex items-center justify-between border-b bg-white px-6 py-3">
        <nav className="flex items-center gap-4 text-sm">
          <span className="font-semibold text-slate-800">CRM + Kanban</span>
          <Link to="/" className="text-slate-600 hover:text-blue-600">Pano</Link>
          <Link to="/reports" className="text-slate-600 hover:text-blue-600">Raporlar</Link>
          {user.isSuperAdmin && <Link to="/settings" className="text-slate-600 hover:text-blue-600">Ayarlar</Link>}
        </nav>
        <div className="flex items-center gap-3 text-sm text-slate-600">
          <span>{user.name}</span>
          <Button className="bg-slate-200 text-slate-700 hover:bg-slate-300" onClick={logout}>
            Çıkış
          </Button>
        </div>
      </header>
      <main className="p-6">
        <Outlet />
      </main>
    </div>
  )
}
