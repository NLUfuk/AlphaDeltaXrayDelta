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

/**
 * `options` turns a free-text box into the control the value actually deserves:
 *  - a list the server ENUMERATES (ticket.default_priority) is a `<select>`: typing "acil" into it
 *    used to be accepted, stored, and then silently read back as Normal by `GetEnumAsync`;
 *  - a list that is only a suggestion (timezone, currency) is a `<datalist>` — the common answers are
 *    one click away, but an operator who needs `America/New_York` is not locked out by our catalog.
 * Everything else is driven by the row's own `type` (see CONTROLS in Settings.tsx), which the store has
 * carried since Faz 1 — no new column, no per-key switch.
 */
export type SettingMeta = { label: string; help: string; options?: readonly string[]; strict?: boolean }

export const SETTING_LABELS: Record<string, SettingMeta> = {
  'ticket.reopen_window_days': { label: 'Yeniden açma süresi (gün)', help: 'Kapanan talep kaç gün içinde yeniden açılabilir.' },
  'ticket.default_priority': {
    label: 'Varsayılan öncelik',
    help: 'Personel/müşteri öncelik seçmeden açtığında talebin başlayacağı öncelik.',
    // Values are the backend Priority enum names; the labels next to them are what the board shows.
    options: ['Low', 'Normal', 'High', 'Urgent'], strict: true,
  },
  'file.max_size_mb': { label: 'Maks. dosya boyutu (MB)', help: 'Yüklenebilecek tek dosyanın üst sınırı.' },
  'file.max_per_comment': { label: 'Yorum başına dosya', help: 'Bir yoruma/forma eklenebilecek en fazla dosya adedi.' },
  'file.allowed_types': { label: 'İzinli dosya tipleri', help: 'MIME tipleri, JSON dizi olarak. Public form ayrıca kendi dar listesini uygular.' },
  'form.kvkk_text': { label: 'KVKK metni', help: 'Formda gösterilen aydınlatma metni.' },
  'brand.system_name': { label: 'Sistem adı', help: 'Menüde, giriş ekranında, public formda ve PDF raporlarda görünen marka adı.' },
  'brand.primary_color': { label: 'Ana renk', help: 'Müşteriye açık sayfaların (public form, müşteri giriş sayfası) vurgu rengi, #rrggbb.' },
  'brand.logo_url': { label: 'Logo URL', help: 'Müşteriye açık sayfalarda görünen logo. Boş bırakılırsa logo gösterilmez.' },
  'system.timezone': {
    label: 'Zaman dilimi', help: 'Raporların tarihleri bu dilime göre yazılır.',
    options: ['Europe/Istanbul', 'Europe/London', 'Europe/Berlin', 'UTC'],
  },
  'finance.currency': {
    label: 'Para birimi', help: 'Tutarların gösterildiği para birimi kodu.',
    options: ['TRY', 'USD', 'EUR', 'GBP'],
  },
}

/** Turkish names for the enum values above — the board says "Acil", the settings box should too. */
export const OPTION_LABELS: Record<string, string> = {
  Low: 'Düşük', Normal: 'Normal', High: 'Yüksek', Urgent: 'Acil',
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
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['settings'] })
      // The brand triple is cached for ten minutes (it changes about once a year), so without this a
      // renamed system would keep the old name in the sidebar and the tab title until a reload —
      // exactly the "I changed it and nothing happened" the settings screen just stopped doing.
      qc.invalidateQueries({ queryKey: ['brand'] })
    },
  })
}
