import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from './api'

export type Setting = { key: string; value: string; type: string; group: string; updatedAt: string | null }

// Friendly group + per-key labels/help (spec §4.3 catalog — text lives here, not inline).
export const GROUP_LABELS: Record<string, string> = {
  ticket: 'Ticket', notification: 'Bildirim', file: 'Dosya', form: 'Form',
  brand: 'Marka', system: 'Sistem', kvkk: 'KVKK',
}

export const SETTING_LABELS: Record<string, { label: string; help: string }> = {
  'ticket.reopen_window_days': { label: 'Yeniden açma süresi (gün)', help: 'Kapanan ticket kaç gün içinde yeniden açılabilir.' },
  'ticket.default_priority': { label: 'Varsayılan öncelik', help: 'Yeni ticket’ların başlangıç önceliği.' },
  'notification.debounce_seconds': { label: 'Bildirim debounce (sn)', help: 'Aynı olayın tekrar mailini engelleme penceresi.' },
  'file.max_size_mb': { label: 'Maks. dosya boyutu (MB)', help: 'Yüklenebilecek tek dosya üst sınırı.' },
  'file.max_per_comment': { label: 'Yorum başına dosya', help: 'Bir yorumda/en fazla dosya adedi.' },
  'file.allowed_types': { label: 'İzinli dosya tipleri', help: 'MIME tipleri (JSON dizi).' },
  'form.captcha_enabled': { label: 'CAPTCHA', help: 'Public formda bot korumasını aç/kapa (true/false).' },
  'form.rate_limit_per_minute': { label: 'Dakikada istek limiti', help: 'Public form IP başına hız limiti.' },
  'form.kvkk_text': { label: 'KVKK metni', help: 'Formda gösterilen aydınlatma metni.' },
  'brand.system_name': { label: 'Sistem adı', help: 'Üst barda ve formda görünen marka adı.' },
  'brand.primary_color': { label: 'Ana renk', help: 'Marka birincil rengi (hex).' },
  'brand.logo_url': { label: 'Logo URL', help: 'Marka logosu bağlantısı.' },
  'system.timezone': { label: 'Zaman dilimi', help: 'Sistem varsayılan zaman dilimi.' },
  'system.language': { label: 'Dil', help: 'Varsayılan arayüz dili.' },
  'kvkk.retention_days': { label: 'Saklama süresi (gün)', help: 'KVKK veri saklama süresi.' },
}

export function useSettings() {
  return useQuery({
    queryKey: ['settings'],
    queryFn: async () => (await api.get<Setting[]>('/settings')).data,
  })
}

export function useUpdateSetting() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (v: { key: string; value: string }) => api.put(`/settings/${v.key}`, { value: v.value }),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['settings'] }),
  })
}
