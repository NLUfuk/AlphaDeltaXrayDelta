import { Link } from 'react-router-dom'
import { priority as priorityOf } from '../lib/messages'
import type { TicketListItem } from '../lib/tickets'
import { Icon } from './primitives'

// Composed component (spec §4.2): one card, reused by the board (and later the customer list).
// Odoo's opportunity card shapes it: title first, number underneath, priority as stars and the
// assignee as an avatar in the footer. Drag = status change. The card stays a real <Link> (keyboard
// focus, open-in-new-tab), but we set dataTransfer on dragstart — without it Firefox never fires drop.
/// Compact money for a card: 145.000 TL rather than ₺145.000,00 — the board needs a glanceable
/// magnitude, and the exact figure lives on the detail screen. Currency symbol is deliberately
/// omitted here; the system runs on one currency (Settings finance.currency).
function formatMoney(n: number) {
  if (n >= 1_000_000) return `${(n / 1_000_000).toLocaleString('tr-TR', { maximumFractionDigits: 1 })}M`
  if (n >= 1_000) return `${(n / 1_000).toLocaleString('tr-TR', { maximumFractionDigits: 0 })}B`
  return n.toLocaleString('tr-TR', { maximumFractionDigits: 0 })
}

export function TicketCard({
  ticket,
  assigneeName,
  onDragStart,
  onDragEnd,
  dragging,
}: {
  ticket: TicketListItem
  assigneeName?: string
  onDragStart?: () => void
  onDragEnd?: () => void
  dragging?: boolean
}) {
  const p = priorityOf(ticket.priority)
  const draggable = !!onDragStart
  return (
    <Link
      to={`/tickets/${ticket.id}`}
      draggable={draggable}
      onDragStart={(e) => {
        // Required for a drag to actually start in Firefox; also stops the anchor's default
        // "drag the URL" behavior from taking over the payload.
        e.dataTransfer.setData('text/plain', ticket.id)
        e.dataTransfer.effectAllowed = 'move'
        onDragStart?.()
      }}
      onDragEnd={onDragEnd}
      className={`block rounded-md border border-line bg-surface p-3 shadow-sm transition hover:shadow-card ${
        draggable ? 'cursor-grab active:cursor-grabbing' : 'cursor-pointer'
      } ${dragging ? 'opacity-40 ring-2 ring-primary/40' : ''}`}
    >
      <p className="text-sm font-semibold leading-snug text-ink">{ticket.title}</p>
      <div className="mt-0.5 flex items-baseline justify-between gap-2">
        <p className="text-xs text-muted">{ticket.number}</p>
        {/* Null means either "no permission" or "not priced" — both render as nothing, because a
            placeholder would invite reading an absent amount as zero. */}
        {ticket.value !== null && (
          <span className="shrink-0 text-xs font-medium tabular-nums text-emerald-600 dark:text-emerald-400">
            {formatMoney(ticket.value)}
          </span>
        )}
      </div>
      <div className="mt-2 flex items-center justify-between">
        <Stars value={ticket.priority} title={p.label} />
        {assigneeName ? <Avatar name={assigneeName} /> : <span className="text-xs text-muted">atanmadı</span>}
      </div>
    </Link>
  )
}

// Priority as Odoo's star row: Low renders empty, Urgent fills all three.
function Stars({ value, title }: { value: number; title: string }) {
  return (
    <span className="flex gap-0.5 text-sm" title={`Öncelik: ${title}`}>
      {[1, 2, 3].map((step) => (
        <Icon key={step} name={value >= step ? 'star' : 'star-outline'}
          className={value >= step ? 'text-amber-400' : 'text-muted/50'} />
      ))}
    </span>
  )
}

function Avatar({ name }: { name: string }) {
  const initials = name.split(' ').filter(Boolean).slice(0, 2).map((w) => w[0]).join('').toUpperCase()
  return (
    <span
      title={name}
      className="flex h-6 w-6 items-center justify-center rounded-full bg-primary/10 text-[10px] font-semibold text-primary"
    >
      {initials}
    </span>
  )
}
