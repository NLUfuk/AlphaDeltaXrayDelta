import { useState } from 'react'
import { Link } from 'react-router-dom'
import { useAuth } from '../lib/auth'
import { statusCategory } from '../lib/messages'
import { useChangeStatus, useKanban, useModeration } from '../lib/tickets'
import { Icon } from '../ui/primitives'
import { TicketCard } from '../ui/TicketCard'

// Kanban board (spec §17.8). Card drag = status change → same server rules (§12). Native HTML5 DnD, no
// library. On mobile the columns stack vertically (Tailwind `max-md`), the required list fallback.
export default function Kanban() {
  const { user } = useAuth()
  const companyId = user?.companies[0]?.companyId
  const { data: columns, isLoading, error } = useKanban(companyId)
  const { data: pending } = useModeration(companyId)
  const changeStatus = useChangeStatus(companyId)
  const [drag, setDrag] = useState<{ id: string; fromStatusId: string } | null>(null)
  const [overId, setOverId] = useState<string | null>(null)

  if (!companyId) return <p className="text-muted">Bu kullanıcı bir şirkete bağlı değil (kanban için şirket gerekli).</p>
  if (isLoading) return <p className="text-muted">Yükleniyor…</p>
  if (error) return <p className="text-red-600">Pano yüklenemedi.</p>

  function clearDrag() {
    setDrag(null)
    setOverId(null)
  }

  function drop(statusId: string) {
    // Only a real column change hits the server; dropping a card back in its own column is a no-op.
    if (drag && drag.fromStatusId !== statusId)
      changeStatus.mutate({ id: drag.id, targetStatusId: statusId })
    clearDrag()
  }

  return (
    <div className="space-y-4">
      <header className="flex flex-wrap items-center justify-between gap-3">
        <h1 className="text-lg font-semibold text-ink">Pano</h1>
        <div className="flex items-center gap-2 text-sm">
          {pending && pending.length > 0 && (
            <Link to="/moderation" className="flex items-center gap-1.5 rounded-full bg-amber-50 px-3 py-1 font-medium text-amber-700 ring-1 ring-amber-200">
              <Icon name="inbox-arrow-down-outline" />{pending.length} onay bekliyor
            </Link>
          )}
          <Link to="/admin/columns" className="flex items-center gap-1.5 rounded-md border border-line px-3 py-1.5 text-muted hover:text-ink">
            <Icon name="view-column-outline" />Sütunları yönet
          </Link>
        </div>
      </header>

      <div className="flex gap-4 overflow-x-auto pb-2 max-md:flex-col">
        {columns?.map((col) => {
          const cat = statusCategory(col.category)
          const active = overId === col.statusId
          return (
            <div
              key={col.statusId}
              onDragOver={(e) => { e.preventDefault(); setOverId(col.statusId) }}
              onDragLeave={() => setOverId((cur) => (cur === col.statusId ? null : cur))}
              onDrop={() => drop(col.statusId)}
              className={`flex w-72 shrink-0 flex-col rounded-xl border bg-canvas transition-colors max-md:w-full ${
                active ? 'border-primary ring-2 ring-primary/20' : 'border-line'
              }`}
            >
              <div className="flex items-center justify-between rounded-t-xl border-b border-line bg-surface px-3 py-2.5">
                <span className="flex items-center gap-2 text-sm font-semibold text-ink">
                  <span className="h-2.5 w-2.5 rounded-full" style={{ backgroundColor: col.color || cat.color }} />
                  {col.statusName}
                </span>
                <span className="rounded-full bg-canvas px-2 text-xs text-muted">{col.tickets.length}</span>
              </div>
              <div className="space-y-2 p-2">
                {col.tickets.map((t) => (
                  <TicketCard
                    key={t.id}
                    ticket={t}
                    dragging={drag?.id === t.id}
                    onDragStart={() => setDrag({ id: t.id, fromStatusId: col.statusId })}
                    onDragEnd={clearDrag}
                  />
                ))}
                {col.tickets.length === 0 && <p className="px-2 py-6 text-center text-xs text-muted">Boş</p>}
              </div>
            </div>
          )
        })}
      </div>
    </div>
  )
}
