import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from './api'

export type UserCompany = { companyId: string; companyName: string; role: number }
export type UserRow = { id: string; email: string; name: string; isSuperAdmin: boolean; canCreateCompany: boolean; isActive: boolean; companies: UserCompany[] }
export type Company = { id: string; name: string; slug: string; ownerAdminId: string; isActive: boolean; isArchived: boolean; ticketNumberPrefix: string }
export type Member = { userId: string; email: string; name: string; role: number }
export type PermissionInfo = { key: string; group: string; label: string; groupLabel: string }
export type Effective = { userId: string; companyId: string; permissions: string[] }
export type InviteResult = { userId: string; rawToken: string; expiresAt: string }

// ---- users / admins (super admin) ----
export function useUsers() {
  return useQuery({ queryKey: ['users'], queryFn: async () => (await api.get<UserRow[]>('/users')).data })
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
