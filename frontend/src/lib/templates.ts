import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from './api'

export type EmailTemplate = { key: string; subject: string; body: string; isActive: boolean; updatedAt: string | null }

// Friendly labels for the seeded template keys (text lives here, spec §4.3).
export const TEMPLATE_LABELS: Record<string, string> = {
  ticket_created: 'Talep oluşturuldu',
  ticket_status_changed: 'Durum değişti',
  ticket_reopened: 'Yeniden açıldı',
  ticket_comment_added: 'Yeni yorum',
  ticket_internal_note_added: 'İç not',
  ticket_assigned: 'Atandı',
  ticket_approved: 'Talep onaylandı',
  ticket_rejected: 'Talep reddedildi',
  ticket_attachment_added: 'Dosya eklendi',
  ticket_edited: 'Talep güncellendi',
  account_invite: 'Hesap daveti (müşteri)',
  account_verify: 'Hesap doğrulama',
  staff_invite: 'Personel daveti',
  password_reset: 'Parola sıfırlama',
}

export function useEmailTemplates() {
  return useQuery({
    queryKey: ['email-templates'],
    queryFn: async () => (await api.get<EmailTemplate[]>('/email-templates')).data,
  })
}

export function useUpdateTemplate() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (v: { key: string; subject: string; body: string }) =>
      api.put(`/email-templates/${v.key}`, { subject: v.subject, body: v.body }),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['email-templates'] }),
  })
}
