import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from './api'
import type { User } from './auth'

// Portal (anonymous + logged-in customer) endpoints, separate from the staff-scoped ticket hooks.

export function useRegister() {
  return useMutation({
    mutationFn: (v: { email: string; firstName: string; lastName: string }) => api.post('/auth/register', v),
  })
}

// Self-service password reset (spec §1.12). Uniform response — always show "check your email".
export function useForgotPassword() {
  return useMutation({
    mutationFn: (v: { email: string }) => api.post('/auth/forgot-password', v),
  })
}

/**
 * Mints a per-customer sign-in link. The returned token is bound to this email + company and is
 * consumed by the first ticket, so the link is safe to hand over but useless to anyone else.
 */
export function useCreateCustomerInvite(companyId: string) {
  return useMutation({
    mutationFn: async (v: { email: string; firstName?: string; lastName?: string }) =>
      (await api.post<{ url: string; email: string; expiresAt: string }>(
        `/companies/${companyId}/customer-invites`, v)).data,
  })
}

// ---- company sign-in page (/c/{slug}): sign up with an emailed code, then send the first request ----

export type PublicField = { id: string; label: string; type: number; required: boolean; options: string[] }
export type FormConfig = {
  companyName: string
  kvkkText: string
  brandName: string
  primaryColor: string
  logoUrl: string | null
  fields: PublicField[]
}

export function useFormConfig(slug: string) {
  return useQuery({
    queryKey: ['form-config', slug],
    queryFn: async () => (await api.get<FormConfig>(`/public/form/${slug}`)).data,
  })
}

export function useCustomerRegister(slug: string) {
  return useMutation({
    mutationFn: (v: { email: string; firstName: string; lastName: string; password: string }) =>
      api.post(`/public/form/${slug}/register`, v),
  })
}

/** Types the emailed 6-digit code back; the response is a normal session (see auth.adoptSession). */
export function useVerifyCode(slug: string) {
  return useMutation({
    mutationFn: async (v: { email: string; code: string }) =>
      (await api.post<{ accessToken: string; refreshToken: string; user: User }>(`/public/form/${slug}/verify`, v)).data,
  })
}

/**
 * A signed-in customer's request through the company link — the first one creates the relationship.
 * `inviteToken` is the `?davet=` value from a staff-issued link, if the customer arrived with one:
 * it is what lets their first request skip the moderation queue. Absent = the request waits for
 * approval, which is the correct default for whoever simply found the page.
 */
export function useCustomerFormSubmit(slug: string) {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: async (v: {
      title: string
      body: string
      customFields: Record<string, string>
      inviteToken?: string | null
    }) => (await api.post<{ ticketNumber: string }>(`/public/form/${slug}/ticket`, v)).data,
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['my-tickets'] })
      qc.invalidateQueries({ queryKey: ['my-companies'] })
    },
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
