import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from './api'

export type UserCompany = { companyId: string; companyName: string; role: number }
// `companies` = staff memberships (role-bearing); `customerOf` = company names a customer works with,
// derived from their tickets (customers have no membership).
export type UserRow = {
  id: string
  email: string
  name: string
  isSuperAdmin: boolean
  canCreateCompany: boolean
  isActive: boolean
  companies: UserCompany[]
  customerOf: string[]
}
export type Company = { id: string; name: string; slug: string; ownerAdminId: string; isActive: boolean; isArchived: boolean; ticketNumberPrefix: string }
export type Member = { userId: string; email: string; name: string; role: number }
export type PermissionInfo = {
  key: string; group: string; label: string; groupLabel: string
  description: string
  /** True when only a super admin can satisfy the key — a per-company switch would do nothing. */
  globalOnly: boolean
}
/** `roleBaseline` is what the member's ROLE alone grants and `overridden` lists the keys carrying an
 * explicit user-level row — together they let the switch say whether "on" comes from the role or from
 * a grant, and whether there is an override to clear. */
export type Effective = {
  userId: string; companyId: string
  permissions: string[]; roleBaseline: string[]; overridden: string[]
}
export type InviteResult = { userId: string; rawToken: string; expiresAt: string }

// ---- users / admins (super admin) ----
// Search runs on the server: the list is capped at 100 rows, so filtering client-side would hide matches.
export function useUsers(search?: string) {
  const term = search?.trim() || undefined
  return useQuery({
    queryKey: ['users', term ?? ''],
    queryFn: async () => (await api.get<UserRow[]>('/users', { params: term ? { search: term } : undefined })).data,
    placeholderData: (prev) => prev, // keep the table on screen while a new search resolves
  })
}

/** KVKK erasure (spec §16): masks personal fields and deactivates; ticket history stays. Super admin only. */
export function useAnonymizeUser() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (userId: string) => api.post(`/kvkk/anonymize/${userId}`),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['users'] }),
  })
}
export function useCreateAdmin() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: async (v: { email: string; firstName: string; lastName: string }) =>
      (await api.post<InviteResult>('/users/admins', v)).data,
    onSuccess: () => qc.invalidateQueries({ queryKey: ['users'] }),
  })
}

// ---- companies ----
export function useCompanies() {
  return useQuery({ queryKey: ['companies'], queryFn: async () => (await api.get<Company[]>('/companies')).data })
}
export function useCreateCompany() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: async (v: { name: string; slug: string; ownerAdminId?: string }) =>
      (await api.post<Company>('/companies', v)).data,
    onSuccess: () => qc.invalidateQueries({ queryKey: ['companies'] }),
  })
}
/** Deletes a company. `confirmName` must equal the company name — the server re-checks it, so the
 * typed confirmation is a real gate, not just UI decoration. */
export function useDeleteCompany() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (v: { companyId: string; confirmName: string }) =>
      api.delete(`/companies/${v.companyId}`, { data: { confirmName: v.confirmName } }),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['companies'] }),
  })
}
export function useMembers(companyId: string | undefined) {
  return useQuery({
    queryKey: ['members', companyId],
    enabled: !!companyId,
    queryFn: async () => (await api.get<Member[]>(`/companies/${companyId}/members`)).data,
  })
}
export function useRemoveMember() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (v: { companyId: string; userId: string }) =>
      api.delete(`/companies/${v.companyId}/members/${v.userId}`),
    onSuccess: (_d, v) => qc.invalidateQueries({ queryKey: ['members', v.companyId] }),
  })
}
export function useInvite() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: async (v: { email: string; firstName: string; lastName: string; companyId: string; role: number }) =>
      (await api.post<InviteResult>('/invitations', v)).data,
    onSuccess: (_d, v) => qc.invalidateQueries({ queryKey: ['members', v.companyId] }),
  })
}

// ---- permissions ----
export function usePermissionCatalog() {
  return useQuery({ queryKey: ['perm-catalog'], queryFn: async () => (await api.get<PermissionInfo[]>('/permissions')).data })
}
export function useEffective(userId: string | undefined, companyId: string | undefined) {
  return useQuery({
    queryKey: ['effective', userId, companyId],
    enabled: !!userId && !!companyId,
    queryFn: async () => (await api.get<Effective>('/permissions/effective', { params: { userId, companyId } })).data,
  })
}
export function useAssignPermission() {
  const qc = useQueryClient()
  return useMutation({
    // type: 0 = Grant, 1 = Deny (backend UserPermissionType)
    mutationFn: (v: { userId: string; companyId: string; permissionKey: string; type: number }) =>
      api.post('/permissions/assign', v),
    onSuccess: (_d, v) => qc.invalidateQueries({ queryKey: ['effective', v.userId, v.companyId] }),
  })
}
/** Drops the explicit override so the key follows the role again — the third state Grant/Deny can't express. */
export function useClearPermission() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (v: { userId: string; companyId: string; permissionKey: string }) =>
      api.delete('/permissions/assign', { params: v }),
    onSuccess: (_d, v) => qc.invalidateQueries({ queryKey: ['effective', v.userId, v.companyId] }),
  })
}
