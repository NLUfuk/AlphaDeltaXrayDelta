import { Link } from 'react-router-dom'
import { formatDateTime, ticketEvent } from '../lib/messages'
import { useMarkNotificationsSeen, useNotifications, type NotificationItem } from '../lib/notifications'
import { Button, Icon, LoadError, Loading } from './primitives'

// The notification list, once. The home screen shows it as a panel and the navbar bell shows it in a
// popover; both mount THIS, so there is no second copy of the row markup or of the "mark read" call to
// keep in step. Both also share the query key, so opening one updates the other without a refetch.

/** One line: what happened, on which ticket, when. Clicking goes to the ticket — a notification that
 *  does not take you to the thing it is about is just noise. */
function Row({ item, onNavigate }: { item: NotificationItem; onNavigate?: () => void }) {
  const ev = ticketEvent(item.eventType, item.newValue)
  return (
    <Link
      to={`/tickets/${item.ticketId}`}
      onClick={onNavigate}
      className={`flex items-start gap-3 rounded-lg p-2 transition hover:bg-canvas ${item.isUnread ? 'bg-primary/5' : ''}`}
    >
      <Icon name={ev.icon} className={`mt-0.5 text-lg ${item.isUnread ? 'text-primary' : 'text-muted'}`} />
      <div className="min-w-0 flex-1">
        <p className="text-sm text-ink">
          <span className={item.isUnread ? 'font-semibold' : ''}>{ev.text}</span>
          <span className="text-muted"> — {item.ticketNumber}</span>
        </p>
        <p className="truncate text-xs text-muted">{item.ticketTitle}</p>
      </div>
      <span className="shrink-0 text-xs text-muted">{formatDateTime(item.createdAt)}</span>
    </Link>
  )
}

export function NotificationList({ take = 20, onNavigate }: { take?: number; onNavigate?: () => void }) {
  const { data, isLoading, error } = useNotifications(take)
  if (isLoading) return <Loading />
  if (error) return <LoadError error={error} what="Bildirimler" />

  const items = data?.items ?? []
  if (items.length === 0) return <p className="p-2 text-sm text-muted">Yeni bir hareket yok.</p>
  return (
    <div className="space-y-1">
      {items.map((n) => <Row key={n.eventId} item={n} onNavigate={onNavigate} />)}
    </div>
  )
}

/** "Mark everything read". Rendered only when there is something to mark, so the control never sits
 *  there doing nothing. Reading is always a deliberate click — merely opening a screen must not clear
 *  the badge, or it stops meaning anything for whoever lands there first. */
export function MarkAllSeen({ unread }: { unread: number }) {
  const markSeen = useMarkNotificationsSeen()
  if (unread === 0) return null
  return (
    <Button variant="secondary" onClick={() => markSeen.mutate()} disabled={markSeen.isPending}>
      <Icon name="check-all" className="mr-1" />Tümünü okundu işaretle
    </Button>
  )
}
