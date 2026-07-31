// Single message catalog (spec §4.3): status/error text lives here, never inline in components.
// Code reads the semantic category, never the display name (which super admin can rename, §4.3/§12).

// Backend error code → user message. Unknown codes fall back to the server message.
const ERRORS: Record<string, string> = {
  'auth.required': 'Oturum açmanız gerekiyor.',
  'auth.invalid_credentials': 'E-posta veya parola hatalı.',
  'invite.invalid': 'Bu bağlantı geçersiz veya süresi dolmuş. Yeni bir bağlantı isteyin.',
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
