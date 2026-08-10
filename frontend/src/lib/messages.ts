// Single message catalog (spec §4.3): status/error text lives here, never inline in components.
// Code reads the semantic category, never the display name (which super admin can rename, §4.3/§12).

import { toApiError, type ApiFieldError } from './api'

// Timestamps arrive as UTC (ISO with Z). Always render them in Istanbul time, regardless of the
// viewer's machine timezone — a Turkish CRM should read one clock for everyone.
export function formatDateTime(iso: string): string {
  return new Date(iso).toLocaleString('tr-TR', { timeZone: 'Europe/Istanbul', dateStyle: 'medium', timeStyle: 'short' })
}

/**
 * Backend error code → user message. The server's own `message` is English (it is a developer/log
 * string, not UI copy), so an unmapped code used to put English in front of a Turkish user. Every code
 * the API can emit is listed here — keep it that way: `npm run check:errors` fails the build when the
 * backend gains a code this map does not know.
 */
const ERRORS: Record<string, string> = {
  // --- transport / envelope (client-side codes from toApiError) ---
  'network.error': 'Sunucuya ulaşılamadı. İnternet bağlantınızı kontrol edip tekrar deneyin.',
  'server.error': 'Sunucuda bir hata oluştu. Lütfen daha sonra tekrar deneyin.',
  'unknown.error': 'Beklenmeyen bir hata oluştu.',
  'rate.limited': 'Çok fazla deneme yaptınız. Bir dakika bekleyip tekrar deneyin.',
  'forbidden': 'Bu işlem için yetkiniz yok.',
  'not_found': 'Aradığınız kayıt bulunamadı.',
  'conflict': 'Bu işlem mevcut kayıtla çakışıyor.',
  // Only the fallback: a real validation failure is rendered from `details` (see errorText).
  'validation.failed': 'Girdiğiniz bilgileri kontrol edin.',

  // --- auth ---
  'auth.required': 'Oturum açmanız gerekiyor.',
  'auth.invalid_credentials': 'E-posta veya parola hatalı.',
  'auth.wrong_password': 'Mevcut parolanız hatalı.',
  'auth.invalid_refresh': 'Oturumunuz sona ermiş. Lütfen tekrar giriş yapın.',
  'auth.invalid_code': 'Kod hatalı veya süresi dolmuş.',
  'auth.code_locked': 'Çok fazla hatalı deneme. Yeni bir kod isteyin.',
  'auth.superadmin_delete': 'Süper admin hesabı kendini silemez.',
  'auth.impersonate_forbidden': 'Kullanıcı kimliğine bürünme yalnızca süper admin yetkisidir.',
  'auth.impersonate_inactive': 'Pasif bir hesabın kimliğine bürünülemez.',
  'auth.impersonate_superadmin': 'Başka bir süper adminin kimliğine bürünülemez.',
  'captcha.failed': 'Doğrulama başarısız. Lütfen tekrar deneyin.',

  // --- invitation / account activation ---
  'invite.invalid': 'Bu bağlantı geçersiz veya süresi dolmuş. Yeni bir bağlantı isteyin.',
  'invite.already_member': 'Bu kullanıcı zaten şirkete kayıtlı.',
  'invite.forbidden': 'Bu şirkete kullanıcı davet etme yetkiniz yok.',
  'invite.invalid_role': 'Yalnızca Yönetici veya Personel rolü davet edilebilir.',

  // --- user ---
  'user.not_found': 'Kullanıcı bulunamadı.',
  'user.email_taken': 'Bu e-posta adresiyle bir kullanıcı zaten var.',
  'user.forbidden': 'Bu işlem yalnızca süper admin yetkisidir.',
  'membership.not_found': 'Bu kullanıcı şirketin üyesi değil.',

  // --- company ---
  'company.not_found': 'Bu bağlantıya ait firma bulunamadı.',
  'company.form_closed': 'Bu firma şu anda yeni talep almıyor.',
  'company.not_related': 'Bu firmaya yalnızca kendi bağlantısı üzerinden yazabilirsiniz.',
  'company.slug_taken': 'Bu bağlantı adresi başka bir şirkette kullanılıyor.',
  'company.create_forbidden': 'Şirket açma yetkiniz yok.',
  'company.delete_forbidden': 'Yalnızca şirketin sahibi olan yönetici veya süper admin silebilir.',
  'company.delete_name_mismatch': 'Şirket adını birebir yazmanız gerekiyor.',
  'company.archive_forbidden': 'Yalnızca şirketin sahibi olan yönetici veya süper admin arşivleyebilir.',
  'company.archived': 'Bu şirket arşivlenmiş; üzerinde işlem yapılamaz.',
  'company.members_forbidden': 'Bu şirketin üyesi değilsiniz.',
  'company.member_remove_forbidden': 'Üye çıkarma yetkisi yalnızca şirket sahibi yönetici veya süper adminde.',
  'company.owner_immutable': 'Şirket sahibi çıkarılamaz.',

  // --- permissions / settings ---
  'permission.denied': 'Bu işlem için yetkiniz yok.',
  'permission.unknown': 'Tanımsız yetki anahtarı.',
  'permission.view_forbidden': 'Bu şirketin yetkilerini görüntüleyemezsiniz.',
  'permission.catalog_forbidden': 'Yetki yönetimi için yetkiniz yok.',
  'permission.assign.forbidden_target': 'Bu kullanıcının yetkilerini değiştiremezsiniz.',
  'permission.assign.forbidden': 'Bu şirkette yetki yönetemezsiniz.',
  'permission.assign.out_of_scope': 'Yalnızca kendi şirketinizde yetki atayabilirsiniz.',
  'permission.assign.not_held': 'Kendinizde olmayan bir yetkiyi başkasına veremezsiniz.',
  'settings.forbidden': 'Bu ayarları yalnızca süper admin yönetebilir.',
  'setting.unknown': 'Tanımsız ayar anahtarı.',
  'template.forbidden': 'E-posta şablonlarını yalnızca süper admin yönetebilir.',
  'template.unknown': 'Tanımsız e-posta şablonu.',
  'report.forbidden': 'Bu rapora erişiminiz yok.',
  'kanban.forbidden': 'Kanban panosu yalnızca personele açıktır.',
  'moderation.forbidden': 'Onay kuyruğu yalnızca personele açıktır.',
  'kvkk.forbidden': 'Anonimleştirme yalnızca süper admin yetkisidir.',
  'kvkk.superadmin': 'Bir süper admin anonimleştirilemez.',
  'kvkk.consent_required': 'Devam etmek için KVKK aydınlatma metnini onaylamalısınız.',

  // --- status columns ---
  'status.manage_forbidden': 'Sütunları yönetme yetkiniz yok.',
  'status.in_use': 'Bu sütunda talep var; önce taşıyın veya kapatın.',
  'status.last_open': 'En az bir açılış sütunu kalmalı.',
  'status.name_required': 'Sütun adı gerekli.',
  'status.not_found': 'Sütun bulunamadı.',
  'status.no_initial': 'Şirkette açılış sütunu tanımlı değil; önce bir sütun ekleyin.',
  'status.reorder_mismatch': 'Sıralama her sütunu tam bir kez içermeli.',

  // --- form fields ---
  'formfield.forbidden': 'Form alanlarını yalnızca şirket yöneticisi veya süper admin yönetebilir.',
  'formfield.not_found': 'Form alanı bulunamadı.',
  'formfield.required': 'Zorunlu form alanlarını doldurun.',
  'formfield.invalid_option': 'Seçilen değer bu alanın seçenekleri arasında değil.',

  // --- attachments & comments ---
  'attachment.type_not_allowed': 'Yalnız PDF, TXT, DOC ve DOCX dosyaları kabul edilir.',
  'attachment.type_mismatch': 'Dosya türü uzantısıyla uyuşmuyor.',
  'attachment.content_mismatch': 'Dosya içeriği geçerli bir belge değil.',
  'attachment.too_large': 'Dosya boyutu izin verilen sınırın dışında.',
  'attachment.too_many': 'Çok fazla dosya eklediniz.',
  'attachment.empty': 'Boş dosya yüklenemez.',
  'attachment.not_found': 'Dosya bulunamadı.',
  'attachment.internal_forbidden': 'Bu dosya bir iç nota ait; erişiminiz yok.',
  'comment.not_found': 'Yorum bulunamadı.',
  'comment.internal_forbidden': 'İç not yalnızca personel tarafından yazılabilir.',

  // --- tickets ---
  'ticket.not_found': 'Talep bulunamadı.',
  'ticket.forbidden': 'Bu talebe erişiminiz yok.',
  'ticket.view_forbidden': 'Bu şirketin taleplerini görüntüleyemezsiniz.',
  'ticket.create_forbidden': 'Bu şirkette talep oluşturamazsınız.',
  'ticket.permission_denied': 'Bu işlem için yetkiniz yok.',
  'ticket.assignee_not_member': 'Atanan kişi bu şirketin üyesi değil.',
  'ticket.priority_forbidden': 'Öncelik yalnızca personel tarafından belirlenebilir.',
  'ticket.value_forbidden': 'Tutar yalnızca personel tarafından belirlenebilir.',
  'ticket.value.negative': 'Tutar negatif olamaz.',
  'ticket.approve.not_pending': 'Yalnızca onay bekleyen bir talep onaylanabilir.',
  'ticket.reject.not_pending': 'Yalnızca onay bekleyen bir talep reddedilebilir.',
  'ticket.status.terminal': 'Bu talep kapandı; statüsü değiştirilemez. Gerekiyorsa yeniden açın.',
  'ticket.status.customer_forbidden': 'Talebinizi yalnızca iptal edebilir veya tamamlandı olarak işaretleyebilirsiniz.',
  'ticket.status.transition_invalid': 'Bu statü geçişine izin verilmiyor.',
  'ticket.status.forbidden': 'Bu statü geçişi için yetkiniz yok.',
  'ticket.status.not_assignee': 'Yalnızca size atanmış bir talebin statüsünü değiştirebilirsiniz.',
  'ticket.status.unchanged': 'Talep zaten bu statüde.',
  'ticket.status.stale': 'Talebin statüsü değişmiş; sayfayı yenileyip tekrar deneyin.',
  'ticket.reopen.not_closed': 'Yalnızca kapanmış bir talep yeniden açılabilir.',
  'ticket.reopen.target_terminal': 'Talep açık bir statüye alınmalı.',
  'ticket.reopen.window_expired': 'Yeniden açma süresi doldu; yeni bir talep oluşturun.',
}

export function errorMessage(code: string, serverMessage?: string): string {
  return ERRORS[code] ?? serverMessage ?? 'Beklenmeyen bir hata oluştu.'
}

/**
 * The password rules, mirrored from the backend's single definition (PasswordRules.StrongPassword in
 * src/CrmKanban.Application/Auth/AuthValidators.cs). Change one, change the other.
 *
 * The server stays the authority — this only exists so the four screens that set a password can say
 * what is required up front, instead of letting the user find out via a 400. `PASSWORD_HINT` goes
 * under the input; `passwordProblem` returns the first broken rule, or null when the password is fine.
 */
export const PASSWORD_HINT =
  'En az 8 karakter; en az bir büyük harf, bir küçük harf, bir rakam ve bir özel karakter (! @ # $ % & * ? _ -) içermeli.'

// Spelled out rather than /[^A-Za-z0-9]/, which would count Turkish letters (ş, ğ, ı, ö, ç, ü) as
// "special" and accept a password the hint says needs a symbol. Mirrors PasswordRules.SpecialCharacters.
const SPECIAL = /[!@#$%^&*()\-_=+[\]{};:'",.<>/?\\|`~]/

export function passwordProblem(password: string): string | null {
  if (password.length < 8) return 'Parola en az 8 karakter olmalı.'
  if (!/[A-Z]/.test(password)) return 'Parola en az bir büyük harf içermeli.'
  if (!/[a-z]/.test(password)) return 'Parola en az bir küçük harf içermeli.'
  if (!/[0-9]/.test(password)) return 'Parola en az bir rakam içermeli.'
  if (!SPECIAL.test(password)) return 'Parola en az bir özel karakter içermeli (örn. ! @ # $ % & * ? _ -).'
  return null
}

/**
 * Anything thrown by an API call → the one display string. Screens do `catch (e) { setError(errorText(e)) }`
 * instead of repeating the envelope-unwrap + catalog-lookup pair in every component (spec §4.3: error text
 * is shared, not written into the component that happens to hit the error).
 *
 * A `validation.failed` renders its per-field reasons instead of the catalog's generic sentence. The
 * server already said WHICH rule broke ("Parola en az bir büyük harf içermeli."); showing
 * "Girdiğiniz bilgileri kontrol edin." was throwing that away and leaving the user to guess — which is
 * exactly what made a rejected invite password look like a broken server.
 */
export function errorText(err: unknown): string {
  const { code, message, details } = toApiError(err)
  if (code === 'validation.failed') {
    const reasons = fieldErrors(details)
    if (reasons.length > 0) return reasons.join(' ')
  }
  return errorMessage(code, message)
}

/** The envelope's `details` is `unknown` (it differs per code) — narrow it before trusting it. */
function fieldErrors(details: unknown): string[] {
  if (!Array.isArray(details)) return []
  return details
    .filter((d): d is ApiFieldError => !!d && typeof (d as ApiFieldError).error === 'string')
    .map((d) => d.error)
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
