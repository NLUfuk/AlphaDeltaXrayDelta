import { Link } from 'react-router-dom'
import { priority } from '../lib/messages'
import type { TicketListItem } from '../lib/tickets'
import { Badge } from './primitives'

// Composed component (spec §4.2): one card, reused by the board (and later the customer list).
// Drag = status change. The card stays a real <Link> (keyboard focus, open-in-new-tab), but we set
// dataTransfer on dragstart — without it Firefox never fires drop — and surface a "dragging" style.
export function TicketCard({
  ticket,
  onDragStart,
  onDragEnd,
  dragging,
}: {
  ticket: TicketListItem
  onDragStart?: () => void
  onDragEnd?: () => void
  dragging?: boolean
}) {
  const p = priority(ticket.priority)
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
      className={`block rounded-md border border-slate-200 bg-white p-3 shadow-sm transition hover:border-blue-300 ${
        draggable ? 'cursor-grab active:cursor-grabbing' : 'cursor-pointer'
      } ${dragging ? 'opacity-40 ring-2 ring-primary/40' : ''}`}
    >
      <div className="flex items-center justify-between">
        <span className="text-xs font-medium text-slate-400">{ticket.number}</span>
        <Badge label={p.label} color={p.color} />
      </div>
      <p className="mt-1 text-sm text-slate-800">{ticket.title}</p>
    </Link>
  )
}
