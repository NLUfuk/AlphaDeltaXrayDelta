import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from './api'

// Mirrors NotificationFeedService's DTOs. `eventType` is the raw TicketEventType — the wording lives in
// the message catalogue (lib/messages.ts), like every other enum the API sends.
export type NotificationItem = {
  eventId: string
  ticketId: string
  ticketNumber: string
  ticketTitle: string
  eventType: number
  oldValue: string | null
  newValue: string | null
  createdAt: string
  isUnread: boolean
}

export type NotificationFeed = { unreadCount: number; items: NotificationItem[] }

/** The caller's own notifications. No company parameter: the server decides what reaches you from your
 *  role on each ticket (opener / assignee / company admin), exactly as the notification e-mails do. */
export function useNotifications(take = 20) {
  return useQuery({
    queryKey: ['notifications', take],
    queryFn: async () => (await api.get<NotificationFeed>('/notifications', { params: { take } })).data,
  })
}

/** Marks everything up to now as read. Invalidated by prefix, so the badge and any open list agree. */
export function useMarkNotificationsSeen() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: async () => {
      await api.post('/notifications/seen')
    },
    onSuccess: () => qc.invalidateQueries({ queryKey: ['notifications'] }),
  })
}
