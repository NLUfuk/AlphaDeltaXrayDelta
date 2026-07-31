import { Link } from 'react-router-dom'
import { priority, statusCategory } from '../lib/messages'
import { useMyTickets } from '../lib/tickets'
import { Badge } from '../ui/primitives'

// A customer's own ticket list (spec §17.4). Customers aren't company members, so they get this flat
// list (OpenedById-scoped server-side) instead of the staff kanban. Each row opens the detail, where
// they can comment, cancel, or mark complete.
export default function CustomerTickets() {
  const { data, isLoading, error } = useMyTickets()
  if (isLoading) return <p className="text-muted">Yükleniyor…</p>
  if (error) return <p className="text-red-600">Talepler yüklenemedi.</p>
  const items = data?.items ?? []

  return (
    <div className="mx-auto max-w-3xl space-y-4">
      <h1 className="text-lg font-semibold text-ink">Taleplerim</h1>
      {items.length === 0 ? (
        <p className="text-sm text-muted">Henüz bir talebiniz yok.</p>
      ) : (
        <div className="space-y-2">
          {items.map((t) => {
            const cat = statusCategory(t.category)
            const p = priority(t.priority)
            return (
              <Link
                key={t.id}
                to={`/tickets/${t.id}`}
                className="block rounded-lg border border-line bg-white p-4 shadow-sm transition hover:border-primary"
              >
                <div className="flex items-center justify-between">
                  <span className="text-xs font-medium text-slate-400">{t.number}</span>
                  <div className="flex gap-2">
                    <Badge label={cat.label} color={t.statusColor || cat.color} />
                    <Badge label={p.label} color={p.color} />
                  </div>
                </div>
                <p className="mt-1 text-sm text-ink">{t.title}</p>
                <p className="mt-1 text-xs text-muted">{new Date(t.createdAt).toLocaleDateString('tr-TR')}</p>
              </Link>
            )
          })}
        </div>
      )}
    </div>
  )
}
