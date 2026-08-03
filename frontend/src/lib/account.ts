import { useMutation } from '@tanstack/react-query'
import { api } from './api'

// Self-service account actions for the logged-in user (spec §1.12, KVKK §16).
export function useChangePassword() {
  return useMutation({
    mutationFn: (v: { currentPassword: string; newPassword: string }) => api.post('/auth/change-password', v),
  })
}

export function useDeleteAccount() {
  return useMutation({
    mutationFn: (v: { password: string }) => api.post('/auth/delete-account', v),
  })
}
