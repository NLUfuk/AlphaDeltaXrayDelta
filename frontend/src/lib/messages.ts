// Single message catalog (spec §4.3): status/error text lives here, never inline in components.
// Code reads the semantic category, never the display name (which super admin can rename, §4.3/§12).

import { toApiError } from './api'

// Timestamps arrive as UTC (ISO with Z). Always render them in Istanbul time, regardless of the
// viewer's machine timezone — a Turkish CRM should read one clock for everyone.
export function formatDateTime(iso: string): string {
  return new Date(iso).toLocaleString('tr-TR', { timeZone: 'Europe/Istanbul', dateStyle: 'medium', timeStyle: 'short' })
}

// Backend error code → user message. Unknown codes fall back to the server message.
const ERRORS: Record<string, string> = {
  'auth.required': 'Oturum açmanız gerekiyor.',
  'auth.invalid_credentials': 'E-posta veya parola hatalı.',
  'invite.invalid': 'Bu bağlantı geçersiz veya süresi dolmuş. Yeni bir bağlantı isteyin.',
  'auth.invalid_code': 'Kod hatalı veya süresi dolmuş.',
  'auth.code_locked': 'Çok fazla hatalı deneme. Yeni bir kod isteyin.',
  'company.not_found': 'Bu bağlantıya ait firma bulunamadı.',
  'company.form_closed': 'Bu firma şu anda yeni talep almıyor.',
  'company.not_related': 'Bu firmaya yalnızca kendi bağlantısı üzerinden yazabilirsiniz.',
  'company.slug_taken': 'Bu slug başka bir şirkette kullanılıyor.',
  'company.create_forbidden': 'Şirket açma yetkiniz yok.',
  'company.delete_forbidden': 'Yalnızca şirketin sahibi olan yönetici veya süper admin silebilir.',
  'company.delete_name_mismatch': 'Şirket adını birebir yazmanız gerekiyor.',
  'invite.already_member': 'Bu kullanıcı zaten şirkete kayıtlı.',
  'validation.failed': 'Girdiğiniz bilgileri kontrol edin.',
  'network.error': 'Sunucuya ulaşılamadı.',
  'settings.forbidden': 'Bu işlem için yetkiniz yok.',
  'report.forbidden': 'Bu rapora erişiminiz yok.',
  'status.manage_forbidden': 'Sütunları yönetme yetkiniz yok.',
  'status.in_use': 'Bu sütunda talep var; önce taşıyın veya kapatın.',
  'status.last_open': 'En az bir açılış sütunu kalmalı.',
  'status.name_required': 'Sütun adı gerekli.',
  'status.reorder_mismatch': 'Sıralama her sütunu tam bir kez içermeli.',
  'attachment.type_not_allowed': 'Yalnız PDF, TXT, DOC ve DOCX dosyaları kabul edilir.',
  'attachment.type_mismatch': 'Dosya türü uzantısıyla uyuşmuyor.',
  'attachment.content_mismatch': 'Dosya içeriği geçerli bir belge değil.',
  'attachment.too_large': 'Dosya boyutu izin verilen sınırın dışında.',
  'attachment.too_many': 'Çok fazla dosya eklediniz.',
  'attachment.empty': 'Boş dosya yüklenemez.',
}

export function errorMessage(code: string, serverMessage?: string): string {
  return ERRORS[code] ?? serverMessage ?? 'Beklenmeyen bir hata oluştu.'
}

/**
 * Anything thrown by an API call → the one display string. Screens do `catch (e) { setError(errorText(e)) }`
 * instead of repeating the envelope-unwrap + catalog-lookup pair in every component (spec §4.3: error text
 * is shared, not written into the component that happens to hit the error).
 */
export function errorText(err: unknown): string {
  const { code, message } = toApiError(err)
  return errorMessage(code, message)
}

/**
 * Wording for a failed READ (a `useQuery` that errored), as opposed to a failed action.
 *
 * Screens used to hardcode a sentence each — "Rapor yüklenemedi (yetki gerekebilir)", "Kullanıcılar
 * yüklenemedi (süper admin gerekli)". Two things were wrong with that: the text lived in the
 * component, and the parenthetical was a guess. The server already answers precisely
 * (`report.forbidden`, `settings.forbidden`, `auth.required`, …), so a known code wins and `what` is
 * only the fallback subject for codes we have no sentence for.
 */
export function loadErrorText(err: unknown, what: string): string {
  const { code, message } = toApiError(err)
  if (code in ERRORS) return ERRORS[code]
  // `undefined` shows up when a query resolves to nothing without throwing — no code, no server text.
  return message ?? `${what} yüklenemedi.`
}

// StatusCategory enum (backend Enums.cs) → label + color. Indexed by the numeric enum value.
export const STATUS_CATEGORIES = [
  { key: 'open', label: 'Açık', color: '#2563eb' },
  { key: 'pending', label: 'Beklemede', color: '#d97706' },
  { key: 'answered', label: 'Yanıtlandı', color: '#7c3aed' },
  { key: 'waiting', label: 'Müşteri Bekleniyor', color: '#0891b2' },
  { key: 'closed', label: 'Kapandı', color: '#16a34a' },
  { key: 'cancelled', label: 'İptal', color: '#6b7280' },
] as const

export function statusCategory(value: number) {
  return STATUS_CATEGORIES[value] ?? { key: 'unknown', label: '—', color: '#6b7280' }
}

// Priority enum (Low/Normal/High/Urgent) → label + color, indexed by numeric value.
export const PRIORITIES = [
  { label: 'Düşük', color: '#6b7280' },
  { label: 'Normal', color: '#2563eb' },
  { label: 'Yüksek', color: '#d97706' },
  { label: 'Acil', color: '#dc2626' },
] as const

export function priority(value: number) {
  return PRIORITIES[value] ?? { label: '—', color: '#6b7280' }
}
