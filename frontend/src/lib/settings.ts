import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from './api'

export type Setting = { key: string; value: string; type: string; group: string; updatedAt: string | null }

// Friendly group + per-key labels/help (spec §4.3 catalog — text lives here, not inline).
//
// Every key below drives real server behaviour. Keys whose feature does not exist were removed from the
// seed catalog rather than left on the screen — a control that changes nothing costs the whole screen
// its credibility. When the feature ships, the row and its label come back together.
export const GROUP_LABELS: Record<string, string> = {
  ticket: 'Ticket', file: 'Dosya', form: 'Form',
  brand: 'Marka', system: 'Sistem', finance: 'Finans',
}

export const SETTING_LABELS: Record<string, { label: string; help: string }> = {
  'ticket.reopen_window_days': { label: 'Yeniden açma süresi (gün)', help: 'Kapanan talep kaç gün içinde yeniden açılabilir.' },
  'ticket.default_priority': { label: 'Varsayılan öncelik', help: 'Öncelik seçilmeden açılan talebin önceliği: Low, Normal, High veya Urgent.' },
  'file.max_size_mb': { label: 'Maks. dosya boyutu (MB)', help: 'Yüklenebilecek tek dosyanın üst sınırı.' },
  'file.max_per_comment': { label: 'Yorum başına dosya', help: 'Bir yoruma/forma eklenebilecek en fazla dosya adedi.' },
  'file.allowed_types': { label: 'İzinli dosya tipleri', help: 'MIME tipleri, JSON dizi olarak. Public form ayrıca kendi dar listesini uygular.' },
  'form.kvkk_text': { label: 'KVKK metni', help: 'Formda gösterilen aydınlatma metni.' },
  'brand.system_name': { label: 'Sistem adı', help: 'Public formda görünen marka adı.' },
  'brand.primary_color': { label: 'Ana renk', help: 'Marka birincil rengi, #rrggbb.' },
  'brand.logo_url': { label: 'Logo URL', help: 'Marka logosu bağlantısı. Boş bırakılırsa logo gösterilmez.' },
  'system.timezone': { label: 'Zaman dilimi', help: 'Raporların tarihleri bu dilime göre yazılır.' },
  'finance.currency': { label: 'Para birimi', help: 'Tutarların gösterildiği para birimi kodu, ör. TRY.' },
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
