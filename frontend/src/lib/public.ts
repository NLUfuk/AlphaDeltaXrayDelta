import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from './api'

// Portal (anonymous + logged-in customer) endpoints, separate from the staff-scoped ticket hooks.
export type PublicCompany = { id: string; name: string; slug: string }

export function usePublicCompanies() {
  return useQuery({
    queryKey: ['public-companies'],
    queryFn: async () => (await api.get<PublicCompany[]>('/public/companies')).data,
  })
}

export function useRegister() {
  return useMutation({
    mutationFn: (v: { email: string; firstName: string; lastName: string }) => api.post('/auth/register', v),
  })
}

// A logged-in customer opens a request to a company they picked.
export function useCreateCustomerTicket() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (v: { companyId: string; title: string; body: string }) => api.post('/tickets/customer', v),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['my-tickets'] }),
  })
}
