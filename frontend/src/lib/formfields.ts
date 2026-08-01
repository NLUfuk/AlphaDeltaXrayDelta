import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from './api'

// Configurable public-form fields (spec §4.6). Type: 0 Text, 1 TextArea, 2 Number, 3 Select.
export type FormField = {
  id: string
  label: string
  type: number
  required: boolean
  sortOrder: number
  options: string | null
  isActive: boolean
}

export const FIELD_TYPES = [
  { value: 0, label: 'Metin' },
  { value: 1, label: 'Uzun metin' },
  { value: 2, label: 'Sayı' },
  { value: 3, label: 'Seçim listesi' },
] as const

export function useFormFields(companyId: string | undefined) {
  return useQuery({
    queryKey: ['form-fields', companyId],
    enabled: !!companyId,
    queryFn: async () => (await api.get<FormField[]>(`/companies/${companyId}/form-fields`)).data,
  })
}

function useFieldMutation<V>(companyId: string | undefined, fn: (v: V) => Promise<unknown>) {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: fn,
    onSuccess: () => qc.invalidateQueries({ queryKey: ['form-fields', companyId] }),
  })
}

export function useCreateField(companyId: string | undefined) {
  return useFieldMutation<{ label: string; type: number; required: boolean; options: string | null }>(companyId, (v) =>
    api.post(`/companies/${companyId}/form-fields`, v))
}
export function useUpdateField(companyId: string | undefined) {
  return useFieldMutation<FormField>(companyId, (f) =>
    api.put(`/companies/${companyId}/form-fields/${f.id}`, {
      label: f.label, type: f.type, required: f.required, sortOrder: f.sortOrder, options: f.options, isActive: f.isActive,
    }))
}
export function useDeleteField(companyId: string | undefined) {
  return useFieldMutation<string>(companyId, (id) => api.delete(`/companies/${companyId}/form-fields/${id}`))
}
