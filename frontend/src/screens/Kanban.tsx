import { useState } from 'react'
import { useAuth } from '../lib/auth'
import { statusCategory } from '../lib/messages'
import { useChangeStatus, useKanban } from '../lib/tickets'
import { TicketCard } from '../ui/TicketCard'

// Kanban board (spec §17.8). Card drag = status change → same server rules (§12). Native HTML5 DnD, no
// library. On mobile the columns stack vertically (Tailwind `max-md`), which is the required list fallback.
export default function Kanban() {
  const { user } = useAuth()
  const companyId = user?.companies[0]?.companyId
  const { data: columns, isLoading, error } = useKanban(companyId)
  const changeStatus = useChangeStatus(companyId)
  const [dragId, setDragId] = useState<string | null>(null)

  if (!companyId) return <p className="text-slate-500">Bu kullanıcı bir şirkete bağlı değil (kanban için şirket gerekli).</p>
  if (isLoading) return <p className="text-slate-500">Yükleniyor…</p>
  if (error) return <p className="text-red-600">Pano yüklenemedi.</p>

  function drop(statusId: string) {
    if (dragId) changeStatus.mutate({ id: dragId, targetStatusId: statusId })
    setDragId(null)
  }

  return (
    <div className="flex gap-4 overflow-x-auto max-md:flex-col">
      {columns?.map((col) => {
        const cat = statusCategory(col.category)
        return (
          <div
            key={col.statusId}
            onDragOver={(e) => e.preventDefault()}
            onDrop={() => drop(col.statusId)}
            className="w-72 shrink-0 rounded-lg bg-slate-100 p-3 max-md:w-full"
          >
            <div className="mb-2 flex items-center justify-between">
              <span className="text-sm font-semibold text-slate-700" style={{ color: cat.color }}>
                {col.statusName}
              </span>
              <span className="text-xs text-slate-400">{col.tickets.length}</span>
            </div>
            <div className="space-y-2">
              {col.tickets.map((t) => (
                <TicketCard key={t.id} ticket={t} onDragStart={() => setDragId(t.id)} />
              ))}
            </div>
          </div>
        )
      })}
    </div>
  )
}
