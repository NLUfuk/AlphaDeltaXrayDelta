import { useRef, useState } from 'react'
import { Link, Navigate } from 'react-router-dom'
import { useMembers } from '../lib/admin'
import { useAuth } from '../lib/auth'
import { ALL_COMPANIES, useActiveCompany } from '../lib/company'
import { errorText, PRIORITIES, priority as priorityOf, statusCategory } from '../lib/messages'
import { useChangeStatus, useCreateTicket, useKanban, useModeration, useStatuses, type KanbanFilters } from '../lib/tickets'
import { Alert, Button, Icon, Input, LoadError, Loading, Modal, PickCompany, Select, Textarea } from '../ui/primitives'
import { TicketCard } from '../ui/TicketCard'

// Kanban board (spec §17.8) — the opportunity pool. Card drag = status change → same server rules
// (§12). Native HTML5 DnD, no library.
//
// Layout: side-by-side columns that scroll horizontally. Faz 36 had replaced this with a wrapping
// vertical grid for one concrete reason — HTML5 drag does not auto-scroll its container, so a column
// past the right edge was physically undroppable — and the grid solved that by making the board grow
// downwards instead, which reviewers read (correctly) as the wrong shape for a pipeline. The columns
// are back and the original problem is fixed at its source: `edgeScroll` below pans the board while a
// dragged card hovers near an edge.
//
// The screen deliberately carries less than it used to. The customer link block moved to Şirketler
// (it is company configuration, not daily board work), "Sütunları yönet" was a duplicate of the
// sidebar's Sütunlar entry and is gone, the per-column inline create form was replaced by one dialog,
// and the assignee/priority filters sit behind a toggle so the default board is a board.
export default function Kanban() {
  const { user } = useAuth()
  const companyId = useActiveCompany()
  const [filters, setFilters] = useState<KanbanFilters>({})
  const [showFilters, setShowFilters] = useState(false)
  const { data: columns, isLoading, error } = useKanban(companyId, filters)
  const { data: members } = useMembers(companyId)
  const { data: pending } = useModeration(companyId)
  const { data: statuses } = useStatuses(companyId)
  const changeStatus = useChangeStatus(companyId)
  const create = useCreateTicket(companyId)
  const [drag, setDrag] = useState<{ id: string; fromStatusId: string } | null>(null)
  const [overId, setOverId] = useState<string | null>(null)
  /** Which column the create dialog will drop the new ticket into; null = dialog closed. */
  const [composeIn, setComposeIn] = useState<string | null>(null)
  const [createError, setCreateError] = useState<string | null>(null)

  const board = useRef<HTMLDivElement>(null)
  const scrollDir = useRef(0)
  const raf = useRef<number | null>(null)

  const memberName = (id: string | null) => members?.find((m) => m.userId === id)?.name

  // The board is staff-only server-side; since it got its own address, a customer could type it and
  // land on a staff message about companies. Send them where their work actually is.
  if (user && !user.isSuperAdmin && user.companies.length === 0) return <Navigate to="/" replace />
  if (companyId === ALL_COMPANIES) return <PickCompany what="Pano" />
  if (!companyId) return <p className="text-muted">Bu kullanıcı bir şirkete bağlı değil (kanban için şirket gerekli).</p>
  if (error) return <LoadError error={error} what="Pano" />

  // One rAF loop reading a direction ref, rather than a timer per edge: dragover fires at most every
  // ~350ms and stops firing entirely when the pointer is held still, so driving the pan from the event
  // itself would stutter and then stall exactly when the user is waiting to reach the last column.
  function pump() {
    const el = board.current
    if (el && scrollDir.current !== 0) el.scrollLeft += scrollDir.current * 14
    raf.current = scrollDir.current === 0 ? null : requestAnimationFrame(pump)
  }

  function edgeScroll(clientX: number | null) {
    const el = board.current
    if (!el || clientX === null) {
      scrollDir.current = 0
      return
    }
    const { left, right } = el.getBoundingClientRect()
    const EDGE = 90 // px from either end that counts as "asking to pan"
    scrollDir.current = clientX < left + EDGE ? -1 : clientX > right - EDGE ? 1 : 0
    if (scrollDir.current !== 0 && raf.current === null) raf.current = requestAnimationFrame(pump)
  }

  function clearDrag() {
    setDrag(null)
    setOverId(null)
    edgeScroll(null)
  }

  // A drag is the board's version of the status dropdown, so it obeys the same transition graph the
  // server enforces: a card in a terminal column (Tamamlandı/İptal) has no outgoing edge and cannot be
  // dropped anywhere — dragging it back to "Yeni" used to fire a request that always 4xx'd, silently.
  function canDrop(statusId: string): boolean {
    if (!drag || drag.fromStatusId === statusId) return false
    const from = statuses?.find((s) => s.id === drag.fromStatusId)
    // Statuses still loading: let the server be the judge rather than blocking a legal move.
    return from ? from.allowedTargetStatusIds.includes(statusId) : true
  }

  function drop(statusId: string) {
    if (canDrop(statusId))
      changeStatus.mutate({ id: drag!.id, targetStatusId: statusId })
    clearDrag()
  }

  function openCompose(statusId: string) {
    setCreateError(null)
    setComposeIn(statusId)
  }

  async function submitCompose(values: { title: string; body: string; priority?: number }) {
    if (!composeIn) return
    setCreateError(null)
    try {
      // The backend always starts a ticket in the pool column; only a card added elsewhere is moved.
      const initial = columns?.[0]?.statusId
      await create.mutateAsync({ ...values, targetStatusId: composeIn === initial ? undefined : composeIn })
      setComposeIn(null)
    } catch (err) {
      setCreateError(errorText(err))
    }
  }

  const firstColumn = columns?.[0]?.statusId
  const filtered = !!filters.assignedToId || filters.priority !== undefined
  const emptyBoard = columns && columns.length > 0 && columns.every((c) => c.tickets.length === 0)
    && !filters.search && !filtered

  return (
    <div className="space-y-4">
      <header className="flex flex-wrap items-center justify-between gap-3">
        <div className="flex items-center gap-3">
          <h1 className="text-lg font-semibold text-ink">Fırsat havuzu</h1>
          <Button
            onClick={() => firstColumn && openCompose(firstColumn)}
            disabled={!columns?.length}
            className="gap-1.5"
          >
            <Icon name="plus" />Yeni talep
          </Button>
        </div>
        {/* Kept despite the simplification pass: a count of requests waiting on a human is a
            notification, not a feature. The sidebar's "Onay kutusu" entry carries no number. */}
        {pending && pending.length > 0 && (
          <Link to="/moderation" className="flex items-center gap-1.5 rounded-full bg-amber-50 px-3 py-1 text-sm font-medium text-amber-700 ring-1 ring-amber-200">
            <Icon name="inbox-arrow-down-outline" />{pending.length} onay bekliyor
          </Link>
        )}
      </header>

      <div className="flex flex-wrap items-center gap-2">
        <div className="w-64">
          <Input
            placeholder="Ara (no / başlık)…"
            value={filters.search ?? ''}
            onChange={(e) => setFilters((f) => ({ ...f, search: e.target.value }))}
          />
        </div>
        <button
          onClick={() => setShowFilters((s) => !s)}
          aria-expanded={showFilters}
          className={`flex items-center gap-1.5 rounded-md border px-3 py-2 text-sm transition-colors ${
            filtered ? 'border-primary bg-primary/5 text-primary' : 'border-line text-muted hover:text-ink'
          }`}
        >
          <Icon name="filter-variant" />Filtrele{filtered && ' (açık)'}
        </button>
        {showFilters && (
          <>
            <Select
              value={filters.assignedToId ?? ''}
              onChange={(e) => setFilters((f) => ({ ...f, assignedToId: e.target.value || undefined }))}
            >
              <option value="">Atanan: herkes</option>
              {members?.map((m) => <option key={m.userId} value={m.userId}>{m.name}</option>)}
            </Select>
            <Select
              value={filters.priority ?? ''}
              onChange={(e) => setFilters((f) => ({ ...f, priority: e.target.value === '' ? undefined : Number(e.target.value) }))}
            >
              <option value="">Öncelik: tümü</option>
              {PRIORITIES.map((p, i) => <option key={i} value={i}>{p.label}</option>)}
            </Select>
          </>
        )}
        {(filters.search || filtered) && (
          <button onClick={() => setFilters({})} className="text-sm text-muted hover:text-ink">Temizle</button>
        )}
      </div>

      {isLoading && <Loading />}
      {/* A rejected move (permission, or a graph the client read before an admin changed it) left the
          card snapping back with no explanation. */}
      {changeStatus.isError && <Alert>{errorText(changeStatus.error)}</Alert>}
      {emptyBoard && <EmptyBoard onNew={() => firstColumn && openCompose(firstColumn)} />}

      <div
        ref={board}
        // The pan is driven from the board, not from each column: the pointer sitting past the last
        // visible column is over the container, not over any column, which is precisely the case that
        // used to be unreachable.
        onDragOver={(e) => edgeScroll(e.clientX)}
        onDragLeave={() => edgeScroll(null)}
        className="flex gap-3 overflow-x-auto pb-2"
      >
        {columns?.map((col) => {
          const cat = statusCategory(col.category)
          const color = col.color || cat.color
          const active = overId === col.statusId
          return (
            <section
              key={col.statusId}
              // No preventDefault on an illegal target: the browser then shows the "no drop" cursor
              // and the column never lights up, so the rule is visible before the release.
              onDragOver={(e) => { if (canDrop(col.statusId)) { e.preventDefault(); setOverId(col.statusId) } }}
              onDragLeave={() => setOverId((cur) => (cur === col.statusId ? null : cur))}
              onDrop={() => drop(col.statusId)}
              className={`flex w-72 shrink-0 flex-col overflow-hidden rounded-xl border bg-canvas transition-colors ${
                active ? 'border-primary' : 'border-line'
              }`}
            >
              <div className="h-1 shrink-0" style={{ backgroundColor: color }} />
              <div className="shrink-0 border-b border-line bg-surface px-3 py-2.5">
                <div className="flex items-center justify-between gap-2">
                  <span className="truncate text-sm font-semibold text-ink">{col.statusName}</span>
                  <span className="flex shrink-0 items-center gap-2">
                    <span className="rounded-full bg-canvas px-2 text-xs text-muted">{col.tickets.length}</span>
                    <button
                      onClick={() => openCompose(col.statusId)}
                      title="Bu sütuna yeni talep"
                      className="text-muted hover:text-primary"
                    >
                      <Icon name="plus" />
                    </button>
                  </span>
                </div>
                <PriorityBar tickets={col.tickets} />
              </div>

              {/* The drop signal belongs on the card area, not the whole box. Ringing the entire
                  <section> lit up its header and its existing cards along with it, which testers read
                  as "the board is dragging all of them". Inset ring = no layout shift. */}
              <div
                className={`min-h-24 flex-1 space-y-2 overflow-y-auto p-2 ${
                  active ? 'bg-primary/5 ring-2 ring-inset ring-primary/40' : ''
                }`}
              >
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
                {col.tickets.length === 0 && <p className="px-2 py-6 text-center text-xs text-muted">Boş</p>}
              </div>
            </section>
          )
        })}
      </div>

      <Modal open={composeIn !== null} onClose={() => setComposeIn(null)} title="Yeni talep">
        {createError && <div className="mb-3"><Alert>{createError}</Alert></div>}
        <ComposeForm busy={create.isPending} onCancel={() => setComposeIn(null)} onCreate={submitCompose} />
      </Modal>
    </div>
  )
}

/// What to do with an empty board. The columns are already drawn above it, so this is the missing
/// half: where tickets come FROM. Two real actions, no tour and no dismiss state to persist — the
/// card disappears the moment the first ticket exists, which is exactly when it stops being useful.
function EmptyBoard({ onNew }: { onNew: () => void }) {
  return (
    <div className="rounded-xl border border-dashed border-line bg-surface p-5">
      <h2 className="flex items-center gap-2 text-sm font-semibold text-ink">
        <Icon name="rocket-launch-outline" className="text-primary" />Pano boş — buradan başlayın
      </h2>
      <ol className="mt-3 list-decimal space-y-1.5 pl-5 text-sm text-muted">
        <li><b>Yeni talep</b> ile kendiniz talep açın (ya da bir sütunun <b>+</b> düğmesiyle doğrudan o aşamaya).</li>
        <li><b>Şirketler</b> sayfasındaki müşteri bağlantısını müşterinize gönderin; açtığı talepler bu havuza düşer.</li>
      </ol>
      <p className="mt-3 text-xs text-muted">Kartı bir sütundan diğerine sürüklemek talebin statüsünü değiştirir.</p>
      <Button onClick={onNew} className="mt-3 gap-1.5"><Icon name="plus" />İlk talebi aç</Button>
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

/// The create form, now the dialog's only content instead of something rendered inside a column.
function ComposeForm({
  busy,
  onCreate,
  onCancel,
}: {
  busy: boolean
  onCreate: (values: { title: string; body: string; priority?: number }) => void
  onCancel: () => void
}) {
  // Priority starts EMPTY, not at Normal: an empty box sends no priority at all, which is what lets the
  // server apply the `ticket.default_priority` setting. Hardcoding 1 here meant staff-opened tickets
  // always said "Normal" no matter what the super admin had configured (borç #60).
  const [values, setValues] = useState<{ title: string; body: string; priority: number | '' }>(
    { title: '', body: '', priority: '' })
  return (
    <form
      onSubmit={(e) => {
        e.preventDefault()
        onCreate({ ...values, priority: values.priority === '' ? undefined : values.priority })
      }}
      className="space-y-3"
    >
      <Input autoFocus required placeholder="Başlık" value={values.title}
        onChange={(e) => setValues({ ...values, title: e.target.value })} />
      <Textarea
        required rows={4} placeholder="Kısa açıklama"
        value={values.body} onChange={(e) => setValues({ ...values, body: e.target.value })}
      />
      <Select
        className="w-full"
        value={values.priority}
        onChange={(e) => setValues({ ...values, priority: e.target.value === '' ? '' : Number(e.target.value) })}
      >
        <option value="">Öncelik: varsayılan</option>
        {PRIORITIES.map((p, i) => <option key={i} value={i}>{p.label}</option>)}
      </Select>
      <div className="flex justify-end gap-2 pt-1">
        <Button type="button" variant="secondary" onClick={onCancel}>Vazgeç</Button>
        <Button type="submit" disabled={busy}>{busy ? 'Ekleniyor…' : 'Talebi aç'}</Button>
      </div>
    </form>
  )
}
