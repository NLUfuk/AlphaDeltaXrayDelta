import { useState } from 'react'
import { Link } from 'react-router-dom'
import { useCompanies, useMembers } from '../lib/admin'
import { useActiveCompany } from '../lib/company'
import { errorText, PRIORITIES, priority as priorityOf, statusCategory } from '../lib/messages'
import { useChangeStatus, useCreateTicket, useKanban, useModeration, type KanbanColumn, type KanbanFilters } from '../lib/tickets'
import { Alert, Button, Icon, Input, Loading } from '../ui/primitives'
import { CustomerLink } from '../ui/CustomerLink'
import { TicketCard } from '../ui/TicketCard'

// Kanban board (spec §17.8) — the opportunity pool. Card drag = status change → same server rules
// (§12). Native HTML5 DnD, no library. On mobile the columns stack vertically (Tailwind `max-md`),
// the required list fallback. Filters (search / assignee / priority) map onto the backend
// TicketListQuery the endpoint already binds. Layout follows Odoo's o_kanban_group: colored column
// head with a count, a priority progress bar, and an inline quick-create per column.
export default function Kanban() {
  const companyId = useActiveCompany()
  const [filters, setFilters] = useState<KanbanFilters>({})
  const { data: columns, isLoading, error } = useKanban(companyId, filters)
  const { data: members } = useMembers(companyId)
  const { data: companies } = useCompanies()
  const { data: pending } = useModeration(companyId)
  const changeStatus = useChangeStatus(companyId)
  const create = useCreateTicket(companyId)
  const [drag, setDrag] = useState<{ id: string; fromStatusId: string } | null>(null)
  const [overId, setOverId] = useState<string | null>(null)
  const [composeIn, setComposeIn] = useState<string | null>(null)
  const [createError, setCreateError] = useState<string | null>(null)

  const company = companies?.find((c) => c.id === companyId)
  const memberName = (id: string | null) => members?.find((m) => m.userId === id)?.name

  if (!companyId) return <p className="text-muted">Bu kullanıcı bir şirkete bağlı değil (kanban için şirket gerekli).</p>
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

  async function quickCreate(column: KanbanColumn, values: { title: string; body: string; priority: number }) {
    setCreateError(null)
    try {
      // The backend always starts a ticket in the pool column; only a card added elsewhere is moved.
      const initial = columns?.[0]?.statusId
      await create.mutateAsync({
        ...values,
        targetStatusId: column.statusId === initial ? undefined : column.statusId,
      })
      setComposeIn(null)
    } catch (err) {
      setCreateError(errorText(err))
    }
  }

  return (
    <div className="space-y-4">
      <header className="flex flex-wrap items-center justify-between gap-3">
        <div className="flex items-center gap-3">
          <h1 className="text-lg font-semibold text-ink">Fırsat havuzu</h1>
          <Button onClick={() => setComposeIn(columns?.[0]?.statusId ?? null)} className="gap-1.5">
            <Icon name="plus" />Yeni
          </Button>
        </div>
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

      {company && <CustomerLink slug={company.slug} companyName={company.name} />}

      <div className="flex flex-wrap items-center gap-2">
        <div className="w-56">
          <Input
            placeholder="Ara (no / başlık)…"
            value={filters.search ?? ''}
            onChange={(e) => setFilters((f) => ({ ...f, search: e.target.value }))}
          />
        </div>
        <select
          className="rounded-md border border-line bg-surface px-2 py-2 text-sm text-ink"
          value={filters.assignedToId ?? ''}
          onChange={(e) => setFilters((f) => ({ ...f, assignedToId: e.target.value || undefined }))}
        >
          <option value="">Atanan: herkes</option>
          {members?.map((m) => <option key={m.userId} value={m.userId}>{m.name}</option>)}
        </select>
        <select
          className="rounded-md border border-line bg-surface px-2 py-2 text-sm text-ink"
          value={filters.priority ?? ''}
          onChange={(e) => setFilters((f) => ({ ...f, priority: e.target.value === '' ? undefined : Number(e.target.value) }))}
        >
          <option value="">Öncelik: tümü</option>
          {PRIORITIES.map((p, i) => <option key={i} value={i}>{p.label}</option>)}
        </select>
        {(filters.search || filters.assignedToId || filters.priority !== undefined) && (
          <button onClick={() => setFilters({})} className="text-sm text-muted hover:text-ink">Temizle</button>
        )}
      </div>

      {isLoading && <Loading />}
      {createError && <Alert>{createError}</Alert>}

      <div className="flex gap-4 overflow-x-auto pb-2 max-md:flex-col">
        {columns?.map((col) => {
          const cat = statusCategory(col.category)
          const color = col.color || cat.color
          const active = overId === col.statusId
          return (
            <div
              key={col.statusId}
              onDragOver={(e) => { e.preventDefault(); setOverId(col.statusId) }}
              onDragLeave={() => setOverId((cur) => (cur === col.statusId ? null : cur))}
              onDrop={() => drop(col.statusId)}
              className={`flex w-72 shrink-0 flex-col overflow-hidden rounded-xl border bg-canvas transition-colors max-md:w-full ${
                active ? 'border-primary ring-2 ring-primary/20' : 'border-line'
              }`}
            >
              <div className="h-1" style={{ backgroundColor: color }} />
              <div className="border-b border-line bg-surface px-3 py-2.5">
                <div className="flex items-center justify-between">
                  <span className="text-sm font-semibold text-ink">{col.statusName}</span>
                  <span className="flex items-center gap-2">
                    <span className="rounded-full bg-canvas px-2 text-xs text-muted">{col.tickets.length}</span>
                    <button
                      onClick={() => { setCreateError(null); setComposeIn(composeIn === col.statusId ? null : col.statusId) }}
                      title="Bu sütuna yeni talep"
                      className="text-muted hover:text-primary"
                    >
                      <Icon name="plus" />
                    </button>
                  </span>
                </div>
                <PriorityBar tickets={col.tickets} />
              </div>

              <div className="space-y-2 p-2">
                {composeIn === col.statusId && (
                  <QuickCreate
                    busy={create.isPending}
                    onCancel={() => setComposeIn(null)}
                    onCreate={(values) => quickCreate(col, values)}
                  />
                )}
                {col.tickets.map((t) => (
                  <TicketCard
                    key={t.id}
                    ticket={t}
                    assigneeName={memberName(t.assignedToId)}
                    dragging={drag?.id === t.id}
                    onDragStart={() => setDrag({ id: t.id, fromStatusId: col.statusId })}
                    onDragEnd={clearDrag}
                  />
                ))}
                {col.tickets.length === 0 && composeIn !== col.statusId && (
                  <p className="px-2 py-6 text-center text-xs text-muted">Boş</p>
                )}
              </div>
            </div>
          )
        })}
      </div>
    </div>
  )
}

/// Odoo's column counter bar, by priority: shows at a glance how loaded a column is with urgent work.
function PriorityBar({ tickets }: { tickets: { priority: number }[] }) {
  if (tickets.length === 0) return <div className="mt-2 h-1 rounded-full bg-canvas" />
  return (
    <div className="mt-2 flex h-1 overflow-hidden rounded-full bg-canvas">
      {PRIORITIES.map((_, level) => {
        const count = tickets.filter((t) => t.priority === level).length
        if (count === 0) return null
        return (
          <span
            key={level}
            title={`${priorityOf(level).label}: ${count}`}
            style={{ width: `${(count / tickets.length) * 100}%`, backgroundColor: priorityOf(level).color }}
          />
        )
      })}
    </div>
  )
}

/// Inline quick create inside a column (Odoo's "+"): title, a short body and priority.
function QuickCreate({
  busy,
  onCreate,
  onCancel,
}: {
  busy: boolean
  onCreate: (values: { title: string; body: string; priority: number }) => void
  onCancel: () => void
}) {
  const [values, setValues] = useState({ title: '', body: '', priority: 1 })
  return (
    <form
      onSubmit={(e) => { e.preventDefault(); onCreate(values) }}
      className="space-y-2 rounded-md border border-primary/40 bg-surface p-2 shadow-card"
    >
      <Input autoFocus required placeholder="Başlık" value={values.title}
        onChange={(e) => setValues({ ...values, title: e.target.value })} />
      <textarea
        required rows={2} placeholder="Kısa açıklama"
        className="w-full rounded-md border border-line bg-surface px-3 py-2 text-sm text-ink"
        value={values.body} onChange={(e) => setValues({ ...values, body: e.target.value })}
      />
      <div className="flex items-center gap-2">
        <select
          className="flex-1 rounded-md border border-line bg-surface px-2 py-1.5 text-sm text-ink"
          value={values.priority} onChange={(e) => setValues({ ...values, priority: Number(e.target.value) })}
        >
          {PRIORITIES.map((p, i) => <option key={i} value={i}>{p.label}</option>)}
        </select>
        <Button type="submit" disabled={busy} className="px-3 py-1.5">{busy ? '…' : 'Ekle'}</Button>
        <button type="button" onClick={onCancel} className="text-sm text-muted hover:text-ink">Vazgeç</button>
      </div>
    </form>
  )
}
