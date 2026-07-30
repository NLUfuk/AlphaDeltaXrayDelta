import { Navigate, Outlet } from 'react-router-dom'
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
        <span className="font-semibold text-slate-800">CRM + Kanban</span>
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

/** Placeholder home until the kanban/dashboard screens land (next Faz 7 slices). */
export function Home() {
  const { user } = useAuth()
  return (
    <div className="text-slate-600">
      <p>Hoş geldin, {user?.name}.</p>
      <p className="text-sm text-slate-400">Ekranlar (kanban, ticket, ayarlar, dashboard) sıradaki dilimlerde.</p>
    </div>
  )
}
