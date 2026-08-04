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
  ticket_staff_update: 'Personel güncelleme bildirimi',
  account_invite: 'Hesap daveti (müşteri)',
  account_verify: 'Hesap doğrulama (link)',
  account_code: 'Doğrulama kodu (müşteri girişi)',
  staff_invite: 'Personel daveti',
  password_reset: 'Parola sıfırlama',
}

// Which templates belong together in the editor's list.
export const TEMPLATE_GROUPS: { title: string; match: (key: string) => boolean }[] = [
  { title: 'Talep bildirimleri', match: (k) => k.startsWith('ticket_') },
  { title: 'Hesap e-postaları', match: (k) => !k.startsWith('ticket_') },
]

// Tokens actually present in each mail's payload. A {{token}} that is not in this list renders
// literally in the sent email, so the editor must not advertise it (backend: NotificationService /
// InviteEmail build these payloads).
const TICKET_TOKENS = ['ticketNumber', 'title', 'newValue', 'oldValue', 'link']
// The staff notice names the change itself ({{change}}: "durum güncellendi: İşlemde").
export const TEMPLATE_TOKENS: Record<string, string[]> = {
  ticket_staff_update: ['ticketNumber', 'title', 'change', 'newValue', 'link'],
  account_invite: ['name', 'companyName', 'link'],
  account_verify: ['name', 'link'],
  account_code: ['name', 'companyName', 'code', 'minutes'],
  staff_invite: ['name', 'companyName', 'role', 'link'],
  password_reset: ['name', 'link'],
}
export const tokensFor = (key: string): string[] => TEMPLATE_TOKENS[key] ?? TICKET_TOKENS

// Sample values so the preview shows a realistic mail instead of raw {{tokens}}.
const SAMPLE: Record<string, string> = {
  ticketNumber: 'TEKSTIL-42',
  title: 'Teklif talebi: 5.000 m pamuklu poplin',
  newValue: 'İşlemde',
  oldValue: 'Yeni',
  link: 'https://example.com/tickets/ornek',
  name: 'Ayşe Demir',
  companyName: 'Anadolu Tekstil',
  role: 'Personel',
  change: 'durum güncellendi: İşlemde',
  code: '482913',
  minutes: '15',
}

/** Fills {{tokens}} with sample values for the preview; unknown ones stay literal, exactly like the sender. */
export function renderPreview(text: string): string {
  return text.replace(/{{\s*(\w+)\s*}}/g, (whole, key: string) => SAMPLE[key] ?? whole)
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
