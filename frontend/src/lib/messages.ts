// Single message catalog (spec §4.3): status/error text lives here, never inline in components.
// Code reads the semantic category, never the display name (which super admin can rename, §4.3/§12).

// Backend error code → user message. Unknown codes fall back to the server message.
const ERRORS: Record<string, string> = {
  'auth.required': 'Oturum açmanız gerekiyor.',
  'auth.invalid_credentials': 'E-posta veya parola hatalı.',
  'validation.failed': 'Girdiğiniz bilgileri kontrol edin.',
  'network.error': 'Sunucuya ulaşılamadı.',
  'settings.forbidden': 'Bu işlem için yetkiniz yok.',
  'report.forbidden': 'Bu rapora erişiminiz yok.',
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
