import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from './api'

// Mirrors the backend ticket DTOs (spec §11); enums arrive as their numeric values.
export type TicketListItem = {
  id: string
  number: string
  title: string
  statusId: string
  statusName: string
  category: number
  statusColor: string
  priority: number
  assignedToId: string | null
  categoryId: string | null
  createdAt: string
}

export type KanbanColumn = {
  statusId: string
  statusName: string
  category: number
  color: string
  order: number
  tickets: TicketListItem[]
}

export type Comment = {
  id: string
  authorId: string
  body: string
  isInternal: boolean
  isEdited: boolean
  createdAt: string
  editedAt: string | null
}

export type TicketDetail = {
  id: string
  number: string
  companyId: string
  title: string
  body: string
  statusId: string
  statusName: string
  category: number
  priority: number
  openedById: string
  assignedToId: string | null
  categoryId: string | null
  createdAt: string
  comments: Comment[]
  attachments: { id: string; fileName: string }[]
}

export function useKanban(companyId: string | undefined) {
  return useQuery({
    queryKey: ['kanban', companyId],
    enabled: !!companyId,
    queryFn: async () => (await api.get<KanbanColumn[]>(`/tickets/kanban/${companyId}`)).data,
  })
}

export function useTicket(id: string) {
  return useQuery({
    queryKey: ['ticket', id],
    queryFn: async () => (await api.get<TicketDetail>(`/tickets/${id}`)).data,
  })
}

export function useChangeStatus(companyId: string | undefined) {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (v: { id: string; targetStatusId: string }) =>
      api.post(`/tickets/${v.id}/status`, { targetStatusId: v.targetStatusId }),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['kanban', companyId] }),
  })
}

export type Status = { id: string; name: string; category: number; color: string; order: number; isTerminal: boolean }

export function useStatuses() {
  return useQuery({ queryKey: ['statuses'], queryFn: async () => (await api.get<Status[]>('/tickets/statuses')).data })
}

// Detail-side mutations: invalidate both the ticket detail and the company's kanban.
function useTicketMutation<V>(ticketId: string, companyId: string | undefined, fn: (v: V) => Promise<unknown>) {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: fn,
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['ticket', ticketId] })
      if (companyId) qc.invalidateQueries({ queryKey: ['kanban', companyId] })
    },
  })
}

export function useChangeTicketStatus(ticketId: string, companyId: string | undefined) {
  return useTicketMutation<string>(ticketId, companyId, (targetStatusId) =>
    api.post(`/tickets/${ticketId}/status`, { targetStatusId }))
}
export function useAssignTicket(ticketId: string, companyId: string | undefined) {
  return useTicketMutation<string | null>(ticketId, companyId, (assigneeUserId) =>
    api.post(`/tickets/${ticketId}/assign`, { assigneeUserId }))
}
export function useSetTicketPriority(ticketId: string, companyId: string | undefined) {
  return useTicketMutation<number>(ticketId, companyId, (priority) =>
    api.post(`/tickets/${ticketId}/priority`, { priority }))
}

export function useAddComment(ticketId: string) {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (v: { body: string; isInternal: boolean }) =>
      api.post(`/tickets/${ticketId}/comments`, { body: v.body, isInternal: v.isInternal }),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['ticket', ticketId] }),
  })
}
