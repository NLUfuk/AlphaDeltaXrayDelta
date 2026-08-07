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
  createdAt: string
  /** Reportable amount. Null when the caller lacks ticket.value (withheld server-side, not hidden here). */
  value: number | null
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
  authorName: string
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
  createdAt: string
  comments: Comment[]
  attachments: Attachment[]
  customFields: CustomFieldValue[]
  estimatedValue: number | null
  actualValue: number | null
  /** Distinguishes "not allowed to know" from "nobody priced it yet" — the field is hidden entirely
   *  when false, rather than rendered as a misleading empty box. */
  canSeeValue: boolean
}

export type CustomFieldValue = { label: string; value: string }

export type Attachment = { id: string; fileName: string; contentType: string; size: number; url: string }

export type Paged<T> = { items: T[]; total: number; page: number; pageSize: number }

// The logged-in customer's own tickets (backend scopes GET /tickets by OpenedById for non-staff).
export function useMyTickets() {
  return useQuery({
    queryKey: ['my-tickets'],
    queryFn: async () => (await api.get<Paged<TicketListItem>>('/tickets')).data,
  })
}

// Companies the customer already works with (has a ticket at) — the only ones they can message from the
// portal. First contact with a new company is that company's public form, not the portal.
export type MyCompany = { id: string; name: string }
export function useMyCompanies() {
  return useQuery({
    queryKey: ['my-companies'],
    queryFn: async () => (await api.get<MyCompany[]>('/tickets/my-companies')).data,
  })
}

// Board filters map straight onto the backend TicketListQuery (search/assignee/priority). Empty fields
// are dropped so the query key stays stable when nothing is set.
export type KanbanFilters = { search?: string; assignedToId?: string; priority?: number }

export function useKanban(companyId: string | undefined, filters: KanbanFilters = {}) {
  const params: Record<string, string | number> = {}
  if (filters.search?.trim()) params.search = filters.search.trim()
  if (filters.assignedToId) params.assignedToId = filters.assignedToId
  if (filters.priority !== undefined) params.priority = filters.priority
  return useQuery({
    queryKey: ['kanban', companyId, params],
    enabled: !!companyId,
    queryFn: async () => (await api.get<KanbanColumn[]>(`/tickets/kanban/${companyId}`, { params })).data,
  })
}

export function useTicket(id: string) {
  return useQuery({
    queryKey: ['ticket', id],
    queryFn: async () => (await api.get<TicketDetail>(`/tickets/${id}`)).data,
  })
}

// Staff opens a ticket straight on the board (Odoo's "Yeni" / per-column quick create). The backend
// always starts it in the initial (pool) status, so a card added to another column is moved right
// after creation — the same status endpoint the drag-and-drop uses.
export function useCreateTicket(companyId: string | undefined) {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: async (v: { title: string; body: string; priority: number; targetStatusId?: string }) => {
      const { data } = await api.post<{ id: string }>('/tickets', {
        companyId, title: v.title, body: v.body, priority: v.priority,
      })
      if (v.targetStatusId) await api.post(`/tickets/${data.id}/status`, { targetStatusId: v.targetStatusId })
      return data
    },
    onSuccess: () => qc.invalidateQueries({ queryKey: ['kanban', companyId] }),
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

/** `trust` (approve only) also marks the customer trusted for this company, so their later tickets
 *  skip the queue instead of needing this click every time. */
function useModerationMutation(companyId: string | undefined, action: 'approve' | 'reject') {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: ({ ticketId, trust }: { ticketId: string; trust?: boolean }) =>
      api.post(`/tickets/${ticketId}/${action}${trust ? '?trust=true' : ''}`),
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

/**
 * Deletes a ticket. Soft delete server-side (the audit interceptor sets DeletedAt), so the record,
 * its events and its comments survive for the audit trail — it only leaves every list and board.
 * Requires ticket.delete, which the seeded matrix grants to Admin/SuperAdmin.
 */
/** Sets the opportunity's money figures. Requires ticket.value; the server rejects otherwise. */
export function useSetTicketValue(ticketId: string, companyId: string | undefined) {
  return useTicketMutation<{ estimatedValue: number | null; actualValue: number | null }>(
    ticketId, companyId, (v) => api.post(`/tickets/${ticketId}/value`, v))
}

export function useDeleteTicket(ticketId: string, companyId: string | undefined) {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: () => api.delete(`/tickets/${ticketId}`),
    onSuccess: () => {
      qc.removeQueries({ queryKey: ['ticket', ticketId] })
      if (companyId) qc.invalidateQueries({ queryKey: ['kanban', companyId] })
      qc.invalidateQueries({ queryKey: ['tickets'] })
      qc.invalidateQueries({ queryKey: ['moderation', companyId] })
    },
  })
}

// Uploads a file through the API (bytes proxy through the backend to private storage). FormData lets
// axios set the multipart boundary itself — don't force a Content-Type here.
export function useUploadAttachment(ticketId: string) {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (file: File) => {
      const form = new FormData()
      form.append('file', file)
      return api.post(`/tickets/${ticketId}/attachments`, form)
    },
    onSuccess: () => qc.invalidateQueries({ queryKey: ['ticket', ticketId] }),
  })
}

/** Image attachments are fetched as blobs (an <img src> can't carry the Bearer header) and shown inline. */
export function useAttachmentBlobUrl(id: string, enabled: boolean) {
  const { data } = useQuery({
    queryKey: ['attachment-blob', id],
    enabled,
    staleTime: Infinity,
    queryFn: async () => {
      const res = await api.get(`/tickets/attachments/${id}/download`, { responseType: 'blob' })
      return URL.createObjectURL(res.data as Blob)
    },
  })
  // ponytail: object URLs live until the tab is closed — a handful of thumbnails per ticket. Revoke on
  // unmount if a ticket ever carries enough images for it to matter.
  return data
}

export const isImage = (contentType: string) => contentType.startsWith('image/')

/** Downloads an attachment through the authed client (a plain <a> can't send the Bearer header). */
export async function downloadAttachment(id: string, fileName: string) {
  const res = await api.get(`/tickets/attachments/${id}/download`, { responseType: 'blob' })
  const url = URL.createObjectURL(res.data as Blob)
  const a = document.createElement('a')
  a.href = url
  a.download = fileName
  a.click()
  URL.revokeObjectURL(url)
}

export function useAddComment(ticketId: string) {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (v: { body: string; isInternal: boolean }) =>
      api.post(`/tickets/${ticketId}/comments`, { body: v.body, isInternal: v.isInternal }),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['ticket', ticketId] }),
  })
}
