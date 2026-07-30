import { Link } from 'react-router-dom'
import { priority } from '../lib/messages'
import type { TicketListItem } from '../lib/tickets'
import { Badge } from './primitives'

// Composed component (spec §4.2): one card, reused by the board (and later the customer list).
export function TicketCard({ ticket, onDragStart }: { ticket: TicketListItem; onDragStart?: () => void }) {
  const p = priority(ticket.priority)
  return (
    <Link
      to={`/tickets/${ticket.id}`}
      draggable={!!onDragStart}
      onDragStart={onDragStart}
      className="block cursor-pointer rounded-md border border-slate-200 bg-white p-3 shadow-sm hover:border-blue-300"
    >
      <div className="flex items-center justify-between">
        <span className="text-xs font-medium text-slate-400">{ticket.number}</span>
        <Badge label={p.label} color={p.color} />
      </div>
      <p className="mt-1 text-sm text-slate-800">{ticket.title}</p>
    </Link>
  )
}
