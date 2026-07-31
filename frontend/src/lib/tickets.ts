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

export type Paged<T> = { items: T[]; total: number; page: number; pageSize: number }

// The logged-in customer's own tickets (backend scopes GET /tickets by OpenedById for non-staff).
export function useMyTickets() {
  return useQuery({
    queryKey: ['my-tickets'],
    queryFn: async () => (await api.get<Paged<TicketListItem>>('/tickets')).data,
  })
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

export function useStatuses(companyId?: string) {
  return useQuery({
    queryKey: ['statuses', companyId ?? null],
    queryFn: async () =>
      (await api.get<Status[]>('/tickets/statuses', { params: companyId ? { companyId } : undefined })).data,
  })
}

// ---- kanban column management (admin, spec §12/§18.9) ----
export type StatusColumn = Status & { editable: boolean }

export function useColumns(companyId: string | undefined) {
  return useQuery({
    queryKey: ['columns', companyId],
    enabled: !!companyId,
    queryFn: async () => (await api.get<StatusColumn[]>(`/companies/${companyId}/statuses`)).data,
  })
}

function useColumnMutation<V>(companyId: string | undefined, fn: (v: V) => Promise<unknown>) {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: fn,
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['columns', companyId] })
      qc.invalidateQueries({ queryKey: ['kanban', companyId] })
      qc.invalidateQueries({ queryKey: ['statuses'] })
    },
  })
}

export function useCreateColumn(companyId: string | undefined) {
  return useColumnMutation<{ name: string; category: number; color: string; position: number }>(companyId, (v) =>
    api.post(`/companies/${companyId}/statuses`, v))
}
export function useUpdateColumn(companyId: string | undefined) {
  return useColumnMutation<{ id: string; name?: string; color?: string }>(companyId, ({ id, ...body }) =>
    api.put(`/companies/${companyId}/statuses/${id}`, body))
}
export function useReorderColumns(companyId: string | undefined) {
  return useColumnMutation<string[]>(companyId, (orderedStatusIds) =>
    api.post(`/companies/${companyId}/statuses/reorder`, { orderedStatusIds }))
}
export function useDeleteColumn(companyId: string | undefined) {
  return useColumnMutation<string>(companyId, (id) => api.delete(`/companies/${companyId}/statuses/${id}`))
}

// ---- moderation queue (zero-trust intake, spec §10) ----
export function useModeration(companyId: string | undefined) {
  return useQuery({
    queryKey: ['moderation', companyId],
    enabled: !!companyId,
    queryFn: async () => (await api.get<TicketListItem[]>(`/tickets/moderation/${companyId}`)).data,
  })
}

function useModerationMutation(companyId: string | undefined, action: 'approve' | 'reject') {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (ticketId: string) => api.post(`/tickets/${ticketId}/${action}`),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['moderation', companyId] })
      qc.invalidateQueries({ queryKey: ['kanban', companyId] })
    },
  })
}
export const useApproveTicket = (companyId: string | undefined) => useModerationMutation(companyId, 'approve')
export const useRejectTicket = (companyId: string | undefined) => useModerationMutation(companyId, 'reject')

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
