# CRM + Kanban — Progress

| Alan | Değer |
|---|---|
| Son güncelleme | 2026-07-30 |
| Aktif faz | Yönetim/Onboarding backend tamam — sıradaki: yönetim UI'ı, sonra kanban/ticket bağlama |
| Genel durum | Faz 0-6 tamam; **onboarding+RBAC yönetim katmanı eklendi** (şirket/admin oluşturma, üye+kullanıcı+yetki listeleme, `AdminOnboarding` migration). 85 test yeşil. Tam zincir canlı doğrulandı: süper admin→admin oluştur→invite kabul→admin şirket açar→personel davet→yetki ata→public form→ticket→kanban→bildirim maili (açan+admin). Worker scope bug'ı tam düzeltildi |
| Remote | https://github.com/NLUfuk/AlphaDeltaXrayDelta.git |
| Ana branch | `main` |
| Spec | `crm-kanban-mimari.md` (Rev 2) — kod bununla senkron tutulur |

## Teknoloji (onaylandı — 2026-07-29)

| Konu | Karar | Not |
|---|---|---|
| Backend | .NET **10** (LTS), Clean Architecture | Spec .NET 8 öneriyordu; makinede 8 SDK yok, 10 güncel LTS → kullanıcı onayladı |
| DB | MSSQL — mevcut yerel SQL Server 2022 Express (`localhost`, Windows auth) | Docker yerine mevcut instance seçildi (canlıya deploy edilecek) |
| ORM | EF Core 10 | Merkezi paket yönetimi (CPM), `Directory.Packages.props` |
| Auth | JWT (~15dk) + refresh (DB hash, rotasyonlu) | Faz 2 |
| Frontend | React 19.2 + Vite 8 + TanStack Query v5 + Tailwind v4 + React Router | `frontend/` |
| Loglama | Serilog (console) | |
| Validation | FluentValidation + otomatik pipeline | |

## Fazlar

### Faz 0 — İskelet ✅
- [x] .NET 10 solution (`.slnx`), 4 katman: Domain / Application / Infrastructure / Api
- [x] xUnit test projeleri: Domain.Tests, Application.Tests (FluentAssertions, EF InMemory)
- [x] Merkezi paket yönetimi (`Directory.Packages.props`), `Directory.Build.props`, `global.json` (SDK pin 10.0.302)
- [x] Serilog + OpenAPI + `/health` endpoint + kompozisyon kökü (`AddApplication`)
- [x] `.editorconfig`, güvenlik uyarıları temizlendi (OpenApi 2.0.0 → 2.11.0)
- [x] React frontend iskeleti: Tailwind v4, TanStack Query, Router, axios (`/api` dev proxy)
- [x] GitHub Actions CI (`.github/workflows/ci.yml`): backend build+test, frontend lint+build
- [x] Build + test yeşil (0 uyarı, 0 hata)

### Faz 1 — Domain & DB ✅
- [x] Entities + enums (private setter + davranış metotları) — auth/tenant + ticket state machine çekirdeği
- [x] Ticket state machine (`TicketStateMachine`, `Ticket`) + 16 unit test (staff/customer geçiş, terminal, reopen penceresi, zaman damgaları)
- [x] `CrmDbContext` + global query filter (tenant izolasyonu + soft-delete, tek yerde) + audit interceptor (timestamp + soft-delete)
- [x] `InitialCreate` migration — gerçek SQL Server'a uygulandı
- [x] Idempotent seed (11 permission, 24 rol-permission, 6 statü, 17 geçiş, ilk süper admin) — iki kez çalıştırıldı, sayılar sabit
- [x] `MustChangePassword` alanı User'a eklendi (§9), seed'de süper admin=true

### Faz 2 — Auth + RBAC/Permission + Tenancy ✅ (test-first)
- [x] JWT (~15dk) + refresh (rotasyonlu, DB'de SHA256 hash) + reuse tespiti (zincir revoke) — `JwtTokenService`, `AuthService`
- [x] PasswordHasher (ASP.NET Identity, `PasswordHasherAdapter`), change-password (tüm refresh'leri revoke eder)
- [x] `HttpCurrentUserService` (JWT claim'lerinden), `ICurrentUserService` per-request; company_id yalnız imzalı token'dan
- [x] İki katmanlı yetki: `PermissionResolver` (rol tabanı ± user Grant/Deny, deny kazanır, şirket-scope) + DATA katmanı tenant filtresi
- [x] `IPermissionService` — per-şirket yetki çözümü (stage a), SuperAdmin kısa devre
- [x] Yetki-atama elevation guard (`PermissionAssignmentGuard`): "sahip olmadığın yetkiyi/başka şirkette veremezsin" + audit log
- [x] Davet akışı: `InvitationService` (admin→personel/2.admin, şirket-scope'lu; accept=şifre belirle+aktive et), token hash'li tek-kullanım
- [x] Otomatik FluentValidation pipeline (`ValidationFilter`) + tek hata zarfı middleware (`{code,message,details}`)
- [x] Endpoint'ler: `/api/auth` (login/refresh/logout/me/change-password), `/api/invitations` (invite/accept), `/api/permissions/assign`
- [x] Çekirdek testler (35 toplam): PermissionResolver (6), elevation guard (4), tenant izolasyonu (4), AuthService rotasyon/reuse/change-pwd (5), state machine (16)
- [x] Uçtan uca doğrulama: süper admin login→JWT→/me→refresh rotasyonu→reuse 401, gerçek SQL Server + API

### Faz 3 — Ticket Pipeline ✅
- [x] Ticket CRUD: create (üyelik + arşiv guard, şirket başına artan numara `Company.AllocateTicketNumber`), edit, soft-delete (`TicketCommandService`)
- [x] Statü/geçiş: `ChangeStatus` (state machine + `StatusTransitions` + permission), `Reopen` (7 gün pencere), `SetPriority` (yalnız personel — §18.17)
- [x] Atama/çıkarma (`Assign`/`Unassign`, atanan şirket üyesi olmalı) — `TicketAuthorizationService` per-kayıt yetki (SuperAdmin/Staff/Customer aktörü)
- [x] Yorum + iç not (`CommentService`): iç not staff-only + `comment.internal`; müşteri iç notu ASLA göremez/yazamaz; düzenleme `CommentRevisions`'a + "düzenlendi" işareti; silme soft (§18.2)
- [x] `TicketEvents` her mutasyonda kaydediliyor (audit + Faz 5 mail kaynağı)
- [x] Sunucu tarafı arama/filtre/sayfalama (`TicketQueryService.ListAsync`, `PagedResult`), ticket detay (görünürlük-filtreli yorumlar), kanban (staff-only), müşteri düz listesi (OpenedById-scope)
- [x] Endpoint'ler: `/api/tickets` (list/get/create/edit/delete + assign/status/reopen/priority) + `/api/tickets/{id}/comments` + kanban
- [x] `TicketPipeline` migration (Comment/CommentRevision/TicketEvent + Company.NextTicketNumber/Prefix) — gerçek SQL Server'a uygulandı
- [x] Çekirdek testler (+10, toplam 45): iç-not sızmaması müşteri/staff (`CommentVisibilityTests`, §20 #1), statü/yorum yetkisi (`TicketAuthorizationTests`)

> **Faz 3 tamamlandı — demo noktası (spec §18.21).** Faz 5-8 spec §17'de.

### Faz 4 — Public Form + S3 ✅
- [x] Anonim public form (`PublicFormService`): `/api/public/form/{slug}` — slug→şirket, kimliksiz ticket açma; e-posta eşleşirse mevcut kullanıcıya bağlar, yoksa taslak müşteri + tek-kullanım davet token'ı (§9)
- [x] CAPTCHA seam (`ICaptchaValidator`/`CaptchaValidator`): config ile aç/kapa, dev'de kapalı, **açık+provider yoksa fail-closed** (§10/§13)
- [x] Rate limiting: native ASP.NET fixed-window (IP başına 5/dk) `public-form` policy — bağımlılık yok (§10)
- [x] KVKK onayı zorunlu — validator + servis (defense-in-depth) (§16)
- [x] S3-uyumlu depolama (`IFileStorage`/`S3FileStorage`, AWSSDK.S3): presigned PUT (yükleme) + kısa ömürlü presigned GET (indirme); bucket private; endpoint config'ten (AWS **veya** MinIO)
- [x] `Attachment` entity + migration; formda ve yorumda dosya; tip/boyut/adet **sunucu tarafı** doğrulama (presign + link'te iki kez)
- [x] İndirme yetkisi ticket aktörüne göre; **iç nota bağlı dosya müşteriye ASLA sızmaz** (§14/§20); ticket detayında attachment'lar presigned GET ile
- [x] Endpoint'ler: public form submit + upload-url; `/api/tickets/{id}/attachments/upload-url` + `/api/tickets/attachments/{id}/download`
- [x] Çekirdek testler (+11, toplam 56): public form (captcha fail-closed, KVKK, arşiv/slug, davet/link) + attachment (tip/boyut/adet + iç-not dosyası indirme reddi)
- [x] `TicketPipeline`+`Attachments` migration'ları gerçek SQL Server'a uygulandı

> **Faz 4 tamamlandı.** Faz 5-8 spec §17'de.

### Faz 5 — Bildirim / EmailQueue ✅
- [x] `TicketEvent` = outbox (`NotifiedAt`); worker olayları tam-bir-kez fan-out eder (mail çağrıları iş mantığına serpilmez, §14)
- [x] Olay×alıcı matrisi (`NotificationMatrix`, §14 v1 default): Created→açan+admin, StatusChanged/Reopened/Comment→açan+atanan, InternalNote→atanan+admin, Assigned→atanan; Priority/Category/Edit/Delete→kimse
- [x] Çekirdek kurallar kodda zorlanır: **iç not açana/müşteriye ASLA** (matris + hard-exclude, §20), **kimseye kendi işleminin maili yok** (actor çıkarılır), **Created makbuzu açana** (actor olsa bile)
- [x] Per-kullanıcı opt-out (`UserNotificationPref`, kritik-olmayan olaylar); Created kritik (opt-out'suz)
- [x] Debounce: tick içinde (alıcı, ticket, olay) tekilleştirme (ponytail ceiling: çapraz-tip özet mail sonra)
- [x] `EmailQueue` + arka plan `NotificationWorker` (BackgroundService, System-scope, tick başına fan-out+gönderim); retry + `DeadLetter` (max deneme)
- [x] `IEmailSender` seam: dev'de `DevLogEmailSender` (log), prod `SmtpEmailSender` (`System.Net.Mail`, bağımlılık yok) — config `Email:Provider` ile seçilir
- [x] `EmailTemplate` (DB, {{placeholder}} render) + 6 default şablon idempotent seed
- [x] `Notifications` migration (EmailQueue/EmailTemplates/UserNotificationPrefs + TicketEvent.NotifiedAt) gerçek DB'ye uygulandı
- [x] Çekirdek testler (+7, toplam 63): iç-not müşteriye gitmez, self-notify yok, Created makbuzu, opt-out, idempotent fan-out, dead-letter, başarılı render/gönderim

> **Faz 5 tamamlandı.** Faz 6-8 spec §17'de.

### Faz 6 — Ayarlar + Raporlar ✅
- [x] Jenerik `Setting` KV store (Key/Value/Type/Group/UpdatedById, §11/§13/§18.3) — yeni ayar = DB satırı, kod değil; `Settings` migration gerçek DB'ye uygulandı
- [x] `SettingsService`: `ListAsync`/`GetValueAsync`/`UpdateAsync` — global ayar = **SuperAdmin only** (§8) gate; `UpdateAsync` bilinmeyen key'de 404 (spam/typo guard); her değişiklik audit'li (`settings.update`)
- [x] v1 ayar seti idempotent seed (`DefaultSettings`, 15 satır: ticket/notification/file/form/brand/system/kvkk grupları) — deterministik Id (SHA256[..16]), re-seed duplikasyonsuz
- [x] Config split korundu: secret/infra (DB/S3/SMTP/JWT) hâlâ yalnız file/env; DB Settings yalnız iş parametreleri (§13)
- [x] `ReportService`: şirket raporu (`report.company` + tenant scope) + global rapor (SuperAdmin/`report.global`); metrikler statü **kategorisine** bakar, isme değil (§4.3)
- [x] Metrikler (§15): statü-kategori dağılımı, ort. ilk yanıt / çözüm süresi, personel yükü (open, terminal hariç), kategori kırılımı, açılış/kapanış trendi
- [x] Excel/CSV export (§15/§18.14): RFC 4180 CSV (bağımlılıksız, UTF-8 BOM, Excel'de açılır); ticket-seviyesi satırlar, aynı scope/auth
- [x] KVKK (§16): silme = **anonimleştirme** (`User.Anonymize` — kişisel alan maskesi, ticket/olay/istatistik korunur, audit zinciri bozulmaz); refresh token'lar revoke; SuperAdmin only; süper admin anonimleştirilemez
- [x] KVKK metni + branding artık Settings'ten okunuyor: public form config endpoint (`GET /api/public/form/{slug}`) — Settings read-path'i canlı (write-only depo değil)
- [x] Endpoint'ler: `/api/settings` (list/update, super admin) + `/api/reports/{company,global}` (+ `export.csv`) + `/api/kvkk/anonymize/{userId}`
- [x] Çekirdek testler (+15, toplam 78): rapor scope/izolasyon + izin (cross-company forbidden, global super-admin-only) + metrik doğruluğu; KVKK maskeleme+istatistik korunumu+token revoke+gate; Settings gate/404/açık okuma; CSV escape/satır sayısı

> **Faz 6 tamamlandı.** Faz 7-8 spec §17'de.

### Faz 7 — UI ⚠️ (başladı)
- [x] Foundation: axios client + Bearer header + 401'de tek-sefer refresh & retry + tek hata zarfı (`toApiError`, §4.3)
- [x] Mesaj kataloğu (`lib/messages.ts`, §4.3): hata kodu→mesaj, statü **kategorisi**→etiket/renk (isme değil)
- [x] Auth context (`lib/auth.tsx`): token localStorage, login/logout, `/me` ile hydrate; primitives (`ui/primitives.tsx` — Button/Input/Field/Alert/Badge, §4.2)
- [x] Login ekranı + protected Shell (router: `/login`, `/`); frontend build+lint yeşil (1 benign fast-refresh uyarısı)
- [x] Kanban panosu (`/tickets/kanban/{companyId}`): native HTML5 dnd → kart taşıma = statü değişimi (`/status`), mobilde kolonlar dikey yığılır = liste fallback (§17.8); TicketCard + statü/öncelik badge (§4.2)
- [x] Ticket detay (`/tickets/:id`): başlık/gövde + statü/öncelik + yorumlar (iç not sarı, "düzenlendi" işareti) + yorum ekleme (staff'a iç not seçeneği); react-query cache+invalidation (§4.2)
- [x] Müşteri public formu (`/form/:slug`): config'ten KVKK metni + branding, anonim submit, KVKK onay hard-gate; ticket no + kayıt bağlantısı bildirimi
- [x] Ayar ekranı (`/settings`, super admin): jenerik ayarlar gruplu, satır bazında inline değer düzenle/kaydet (PUT); backend gate 403 döndürürse hata gösterilir
- [x] Dashboard (`/reports`): şirket/global rapor tile'ları (toplam, ort. ilk yanıt/çözüm) + statü dağılımı + personel yükü + **CSV indir** (authed blob download); Shell'e nav (Pano/Raporlar/Ayarlar)
- [ ] Dosya yükleme UI (presigned PUT) — form/yorumda; backend hazır, UI dilimi kaldı (teknik borç)

**E2E ayağa kaldırma (2026-07-30):** API https://localhost:7084 (https launch profile) + `npm run dev` (5173, `/api`→7084 proxy). Doğrulanan: süper admin login→JWT→/me, settings list/update(204)/unknown-key(404), global report + CSV (BOM'lu). **Bulunan bug:** `NotificationWorker` scoped `DbContextOptions`'ı root provider'dan çözüyordu → dev'de scope validation ile host startup crash. Scope içinde çözülerek düzeltildi (`5ec26d2`) — Faz 5 dev'de hiç `dotnet run` edilmemiş olmalı. **Test engeli:** seed yalnız süper admin + statü/permission/settings kuruyor; şirket/admin/ticket yok ve şirket oluşturma akışı da yok (#5) → kanban/ticket/public-form UI'ı gerçek veriyle denenemiyor. Demo seed veya #5 akışı gerekli.

### Yönetim + Onboarding (gap-close, §8/§9/§18.8) ✅ backend
- [x] `User.CanCreateCompany` bayrağı: süper admin admin *hesabını* açar (bu bayrakla), admin kendi şirketini açar (`AdminOnboarding` migration)
- [x] `UserService`: süper admin admin oluşturur (invited-pending + invite token) + kullanıcı listeleme (RBAC UI hedefi); self-servis admin yok (§18.5)
- [x] `CompanyService`: şirket oluştur (admin=sahip+Admin membership; süper admin ownerAdminId ile), listele (membership'ten, taze JWT beklemeden), arşivle (§18.20), üye listele (atama/yetki picker'ı)
- [x] `PermissionQueryService`: yetki kataloğu (gruplu) + bir kullanıcının efektif yetkileri (kutuları işaretlemek için) — görüntüleme atama ile aynı gate
- [x] Endpoint'ler: `/api/users` (list + `admins`), `/api/companies` (list/create/{id}/archive/{id}/members), `/api/permissions` (GET katalog + `effective` + assign)
- [x] Çekirdek testler (+7, toplam 85): şirket oluşturma bayrak/sahiplik/slug-çakışma/üye-scope, admin oluşturma süper-admin-only/invited+bayrak/dup-email
- [x] **Yönetim UI'ı:** `/admin/users` (admin oluştur + kullanıcı listesi, invite token gösterimi), `/admin/companies` (şirket oluştur/listele + üye listesi + personel/2.admin davet), `/admin/permissions` (şirket→üye seç, yetki kataloğu checkbox + Ver/Reddet, efektif durum badge'i); Shell nav güncellendi

> **Not:** Bu katman tech debt #5'i kapatır. Public form + ticket + kanban + bildirim maili artık gerçek veriyle uçtan uca çalışıyor (ACME-1 canlı doğrulandı). Onboarding+RBAC hem backend hem UI tamam. Kalan bağlama işi: müşteri kendi ticket listesi/iptal-tamamla UI'ı, ticket atama UI'ı (member listesi hazır), dosya yükleme UI'ı.

## Bir sonraki oturum — açık uçlar (spec §18.21-24)

- Teslim/demo tarihi (Faz 3 sonu demo öneriliyor).
- ~~S3 sağlayıcı~~ → S3-uyumlu SDK, dev MinIO varsayımı seçildi (kod AWS/MinIO'da aynı; prod endpoint/credential env'den). Gerçek bucket sağlanınca e2e (teknik borç #11).
- ~~CAPTCHA provider~~ → seam + dev'de kapalı seçildi; provider (Turnstile/reCAPTCHA) sonra (teknik borç #12).
- ~~SMTP sağlayıcı~~ → seam + dev log-sender ile Faz 5 tamam; gerçek SMTP + SPF/DKIM/DMARC sağlayıcı gelince (teknik borç #16). Deploy ortamı hâlâ açık (Faz 8).
- Hazır mockup/tasarım var mı (Faz 7).
- Süper admin'in sıfırdan admin+ilk şirket oluşturma akışı (teknik borç #5).

## Karar / Varsayım Günlüğü (spec §18 + oturum kararları)

Spec §18'deki tüm kararlar "varsayıldı — onay bekliyor" statüsünde geçerli kabul edildi. Oturumda eklenenler:

- **[Faz 0] .NET 8 yerine .NET 10.** Spec §3/§18.6 stack'i "öneri, onaylanabilir" diyor. Makinede .NET 8 SDK yok (yalnız runtime). 10 güncel LTS → kullanıcı onayladı. Geri dönüşü ucuz (TFM değişikliği).
- **[Faz 0] Docker yerine mevcut SQL Server.** Kullanıcı "canlıya deploy edilecek proje" gerekçesiyle mevcut yerel instance'ı seçti. Dev connection: `Server=localhost;Database=CrmKanban;Trusted_Connection=True`. Prod'da env'den override.
- **[Faz 0] Merkezi paket yönetimi (CPM).** İkinci gerçek durum: 6 proje aynı EF/test sürümlerini paylaşıyor → sürüm sürükleneme riskini kapatan data-driven seam. Spec'in "abstraction ancak 2. gerçek durumda" kuralına uyar.
- **[Faz 0] `.slnx` çözüm formatı.** .NET 10 varsayılanı; tercih değil, araç kararı.
- **[Faz 1] Faz 3-6 tabloları (Comment/Attachment/Email*/Setting/TicketEvent/NotificationPref) şimdi kurulmadı.** Spec §11 hepsini Faz 1 şemasında listeliyor ama davranışları Faz 3-6'da. Davranışsız kabuk entity kurmak yerine, EF migration'ları additive olduğu için (tek doğrusal zincir) her faz kendi tablosunu ekleyecek. Spec'in "tek migration zinciri" ilkesi additive zincirle karşılanır; "tek migration" demek değil. Bu oturum Faz 0-2 olduğundan yalnız auth/tenant + state machine çekirdeği kuruldu.
- **[Faz 1] Roller ayrı tablo değil, `RoleType` enum.** Roller sabit 4'lü küme (spec §8); değişken olan rol-permission MATRİSİ (`RolePermission` satırları, §13 UI-editable). "Rol ekleme" kod değişikliği, "rolün yetkisini değiştirme" data. Ayrı `Roles` tablosu YAGNI.
- **[Faz 1] Statülerde sabit GUID.** Idempotency anahtarı Id; Name/Category süper admin'ce değişebildiği için (§4.3) isimle eşleştirme re-seed'de duplikasyon üretirdi. `TicketStatus` ctor'una opsiyonel `id` parametresi eklendi (yalnız seed kullanır).
- **[Faz 1] Tenant filtresi tek yerde (`CrmDbContext.OnModelCreating`).** Kapsam DATA katmanında garanti (spec §7, §20 "1 numaralı açık"); global query filter'lar denetlenebilir olsun diye dağıtılmadı. Login sırasında kendi membership'ini okuma chicken-egg'i `IgnoreQueryFilters` ile çözüldü (Faz 2).
- **[Faz 2] SuperAdmin = `User.IsSuperAdmin` flag, Membership değil.** SuperAdmin şirket-scope'lu değil; sabit rol flag olarak modellendi (Membership yalnız Admin/Personel). Tenant filtresini bypass eder, tüm yetkilere sahiptir.
- **[Faz 2] Yetki `ICurrentUserService`'te DEĞİL, `IPermissionService` ile per-şirket çözülür.** Çok-şirketli kullanıcıda düz permission seti belirsiz (yetki şirkete bağlı). JWT yalnız kimlik + company_ids + is_super_admin taşır; yetki her istekte kaydın şirketine göre çözülür (stage a). Query filter yalnız IsSuperAdmin+CompanyIds okur (stage b).
- **[Faz 2] `IAppDbContext` seam (DbContext=UoW, DbSet=repo).** Spec §4.1 "repository + UoW" per-entity repo yerine DbContext-as-UoW ile karşılandı; ikinci implementasyon yok, per-entity repo YAGNI (SCOPE DISCIPLINE). Application EF Core soyutlamalarına (`Microsoft.EntityFrameworkCore`, SqlServer değil) referans verir.
- **[Faz 2] Permission-based authorization declarative attribute (`[HasPermission]`) yerine imperative.** Bu fazın yetki-korumalı 2 endpoint'i (invite, permission.assign) şirketi request body'den alıyor; ambient company yok, declarative policy uymuyor. Kontrol servis içinde `IPermissionService` ile yapıldı. Declarative dynamic policy provider Faz 3'e (ticket endpoint'leri çoğalınca = 2. gerçek durum) bırakıldı.
- **[Faz 2] Refresh token reuse tespitinde `SaveChangesAsync` şart.** Test yakaladı: revoke'lar throw'dan önce kaydedilmezse zincir revoke persist olmuyordu — düzeltildi (gerçek güvenlik açığıydı).
- **[Faz 3] Declarative `[HasPermission]` policy provider AÇILMADI; per-kayıt authz servisi (`TicketAuthorizationService`) yapıldı.** Faz 2 log'u "ticket endpoint'leri çoğalınca declarative policy" demişti. Ama ticket yetkisi KAYDA bağlı (ticket'ın şirketi + atanan personel); ambient-company declarative policy bunu ifade edemez. Actor bir kez çözülüp (`ResolveAsync`) operasyon boyu tekrar kullanılıyor. Doğru seam ambient policy değil, per-kayıt aktör. Controller yalnız `[Authorize]`; yetki servis içinde.
- **[Faz 3] Faz 1 additive-migration kararı uygulandı.** Comment/CommentRevision/TicketEvent tabloları + Company'ye `NextTicketNumber`/`TicketNumberPrefix` tek additive migration (`TicketPipeline`) ile eklendi. Faz 1 şeması bunları atlamıştı (davranışsız kabuk kurulmadı); şimdi davranışlarıyla geldiler.
- **[Faz 3] Ticket detay tek erişim kapısı = `authz.ResolveAsync`.** GetDetail kaydı `IgnoreQueryFilters` ile yükleyip erişimi aktör çözümüne bırakıyor — bu tek kapı hem staff'ı tenant filtresiyle, hem müşteriyi (şirket üyesi değil) yalnız kendi ticket'ının açıcısı olarak geçiriyor. Müşteri listesi ayrıca `OpenedById` ile scope'lu (query filter müşteri için CompanyIds boş olduğundan tek başına yetmez).
- **[Faz 3] Yorum düzenle/sil `ticket.edit` ile korunuyor (admin gücü).** Müşteri kendi yorumunu düzenleyemez/silemez (§18.12); admin düzenlemesi izli (revision + işaret), silme soft (§18.2).
- **[Faz 3] `TicketListQuery.Sort` alanı silindi.** Hiçbir yerde tüketilmiyordu (ölü esneklik); liste sabit `CreatedAt desc`. Sıralama gerçek ihtiyaç olunca eklenir (2. gerçek durum).
- **[Faz 4] S3-uyumlu tek SDK (AWSSDK.S3), dev'de MinIO — kullanıcı onayladı.** Kod AWS/MinIO'da aynı; fark yalnız `S3:ServiceUrl`/`ForcePathStyle`/credential (config). Spec §3/§12'nin adlandırdığı varyasyon noktası → `IFileStorage` seam hak ediyor. Presign lokal HMAC (network yok) → sync arayüz.
- **[Faz 4] CAPTCHA seam + dev'de kapalı, fail-closed — kullanıcı onayladı.** `Captcha:Enabled=false` dev. Açık ama provider yoksa `false` döner (bot geçişini sessizce açmaz). Provider (Turnstile/reCAPTCHA) seçilince `CaptchaValidator`'a tek branch (§18.24). Rate limiting native ASP.NET ile tam yapıldı.
- **[Faz 4] Rate limiter native ASP.NET (IP/fixed-window 5dk).** Yeni bağımlılık yok (ponytail rung 4). ponytail: in-memory + per-instance; çok-instance prod'da distributed limiter'a geçilir (teknik borç).
- **[Faz 4] Anonim form tenant verisini `IgnoreQueryFilters` ile yazar.** Kimliksiz istekte SystemCurrentUser anonim → filtre okumaları boş. Şirket slug'la, statü global okunur (InvitationService pattern'i). Müşteri = Membership'siz User; ticket'a `OpenedById` ile bağlı (Faz 3 müşteri yolu ile tutarlı).
- **[Faz 4] Dosya tip/boyut/adet doğrulaması sunucuda, iki kez (presign + link).** Client asla güvenilmez. `BadRequestException` (400) eklendi. ponytail ceiling: presigned PUT S3'te gerçek boyutu **zorlayamaz** (client bildirilen boyutu doğrular); gerçek sınır için presigned POST content-length-range veya upload sonrası HEAD — teknik borç.
- **[Faz 4] İndirme yetkisi ticket aktörüne göre; iç-not dosyası müşteriye kapalı.** `AttachmentService.GetDownloadUrlAsync` ticket'ı çözer, müşteri ise iç nota bağlı dosyayı reddeder. Ticket detayında da attachment'lar görünür yorum kümesine göre süzülür (metin sızmama kuralının dosya karşılığı, §20).
- **[Faz 5] `TicketEvent` outbox olarak kullanıldı (`NotifiedAt`), ayrı outbox tablosu yok.** Spec §14 "tetikleyici TicketEvents'ten beslenir" diyor; olay zaten append-only kayıt. Worker `NotifiedAt==null` satırları işler, tam-bir-kez. Servisler yalnız olay yazar, mail bilmez → "mail iş mantığına serpilmez" garantisi yapısal.
- **[Faz 5] CommentAdded için tek matris girdisi iki §14 satırını karşılar.** "Public yorum (personel/admin)" ve "Müşteri yorum yazdı" farklı alıcılar; ama {Açan, Atanan} - actor kuralıyla ikisi de çıkar: müşteri yazarsa açan(actor) düşer→atanan; personel yazarsa açan+atanan(self düşer). Ekstra actor-role dalı gerekmedi.
- **[Faz 5] Bildirim mantığı Application'da (`NotificationService`), worker API composition root'ta.** Worker seeder gibi System-scope `CrmDbContext` kurup servisi elle new'ler (request-scoped `ICurrentUserService` HttpContext'siz background'da çözülemez). Hosting bağımlılığı Infrastructure'a girmedi (API'de zaten var — ponytail: platform özelliği).
- **[Faz 5] `IEmailSender` seam: SMTP `System.Net.Mail` ile (yeni bağımlılık yok).** Dev'de log sender; prod SMTP config'ten seçilir (`Email:Provider`). SMTP kimlik secret → env/user-secrets. Sağlayıcı seçimi bloklamadı (dev sender ile uçtan uca çalışıyor).
- **[Faz 5] Debounce = tick-içi tekilleştirme (ponytail ceiling).** Aynı (alıcı, ticket, olay) bir tick içinde tek mail. Gerçek zaman-pencereli çapraz-tip "tek özet mail" (§14) daha büyük değişiklik; mail gürültülü olursa yapılır.
- **[Faz 6] Settings tek additive `Setting` KV tablosu, jenerik (Key/Value/Type/Group).** Spec §18.3 "bütün ayarlar → jenerik altyapı + kapalı v1 listesi" doğrudan bu. Yeni ayar = DB satırı/seed, kod değil. Faz 1 additive-migration kararı sürüyor (`Settings` migration).
- **[Faz 6] SettingsService'te cache YOK (ponytail, SCOPE DISCIPLINE).** Spec §13 "cache'lenir, değişince invalidate" bir tasarım ipucu; ama Faz 6'da hot per-request okuyucu yok (KVKK metni form submit'te düşük frekans, unique Key index O(1)). Cache kanıtlanmamış eksende esneklik + invalidation/multi-instance hata yüzeyi getirir. Hot okuyucu çıkınca eklenir (teknik borç #20).
- **[Faz 6] Var olan tipli Options (Ticket/Notification/File) Settings'e TAŞINMADI.** §13 bunları "iş parametresi = DB" sayıyor ama config'te çalışıyorlar ve bazıları hot path (reopen window, page size command service'te). Toplu async-DB'ye çevirmek imza değişimi. Settings store artık var (seam); her tüketici dokunulunca tek tek geçirilir (2. gerçek durumda). Şimdi taşımak under/over-engineering değil, gereksiz geniş diff. (teknik borç #21)
- **[Faz 6] Global ayar = SuperAdmin only; `settings.manage` v1'de bağlanmadı.** v1 ayar setinin tamamı global (§13 listesi), §8 "global ayar = SuperAdmin". `settings.manage` permission key ileride per-şirket ayar yüzeyi için (§9 seam) duruyor; şimdi admin'e ayar yönetimi açılmadı.
- **[Faz 6] Ayar güncelleme yalnız var olan key'i değiştirir (create yok).** Jenerik-lik "ayar eklemek = DB satırı/seed" demek; API'den keyfi key yaratmak değil. Bilinmeyen key → 404 (spam/typo guard). v1 key'leri seed'den gelir.
- **[Faz 6] Rapor kaynağı = Tickets + statü kategorisi (TicketEvents değil).** §15 "kaynak TicketEvents" diyor ama v1 metrikleri (kategori dağılımı, ilk yanıt/çözüm süresi, personel yükü, trend) zaten ticket'taki zaman damgalarından (`FirstResponseAt`/`ResolvedAt`/`ClosedAt`, §12 ApplyStatus) ve mevcut statüden hesaplanabiliyor; olay tablosu tarama gereksiz. Olay-seviyesi metrik gerekirse (ör. statü-başına bekleme) TicketEvents eklenir.
- **[Faz 6] Rapor agregasyonu bellek içi (ponytail ceiling).** Projeksiyon satırları çekilip LINQ ile toplanıyor (tarih farkları SQL'de değil). v1 ölçeğinde kabul; ticket hacmi satır çekimini pahalılaştırırsa GROUP BY SQL'e itilir. Scope (tenant filtresi + izin) SQL tarafında garanti — sızıntı riski yok.
- **[Faz 6] Export = CSV (RFC 4180, bağımlılıksız); .xlsx ertelendi.** §18.14 "Excel/CSV". CSV native + Excel'de açılır (UTF-8 BOM). Gerçek .xlsx bağımlılık ister (ClosedXML/EPPlus) → istenene kadar yazılmaz (ponytail rung 4/5). (teknik borç #22)
- **[Faz 6] KVKK silme = anonimleştirme (§16), hard delete değil.** `User.Anonymize` domain davranışı: email Id'den türetilir (unique kısıt korunur), ad maskeленir, PasswordHash temizlenir, deaktive. Ticket/olay/audit korunur → istatistik ve kanıt zinciri bozulmaz. Refresh token'lar revoke. **SuperAdmin only** (kullanıcı çok-şirketli olabilir; tek admin kimliği global silmesin — teknik borç #23). Süper admin anonimleştirilemez.
- **[Faz 6] `kvkk.retention_days` saklanıyor ama otomatik purge YOK.** §16 "saklama süresi ayarlanabilir" — değer Settings'te; süreli anonimleştirme job'ı istenene kadar yazılmadı (ponytail). (teknik borç #24)
- **[Yönetim] `User.CanCreateCompany` bayrağı — "admin kim" sorusunun veri-güdümlü cevabı.** Taze admin'in henüz membership'i (dolayısıyla rolü) yok; §9 "hesabı süper admin açar, şirketini admin açar" için "bu kullanıcı şirket açabilir" durumunu bir yere yazmak gerek. Bayrak doğru seam (tek kolon migration); alternatif "süper admin ilk şirketi de açsın" §18.8'e ("şirketi admin kendi açar") aykırıydı.
- **[Yönetim] Şirket oluşturma permission-key'siz, rol/bayrak tabanlı.** `company.create` diye bir key yok; süper admin (bypass) veya `CanCreateCompany` olan kullanıcı açar. Sahiplik: admin kendi açtığının sahibi+Admin membership; süper admin `ownerAdminId` ile başkası adına açabilir. Slug global unique (form linki) → `IgnoreQueryFilters` ile kontrol.
- **[Yönetim] Şirket listesi JWT scope'undan değil, membership'ten okunuyor.** Admin yeni şirket açınca JWT'sindeki `company_ids` bayat kalır; `ListAsync` membership'i userId ile sorgular → yeni şirket refresh beklemeden görünür. (JWT tazeleme ayrı konu; tenant *filtresi* hâlâ imzalı token'dan.)
- **[Yönetim] Bildirim worker'ı: scope tick başına, DbContextOptions o scope ömründe kullanılıyor.** İlk düzeltme (`5ec26d2`) options'ı dispose edilmiş scope'tan alıyordu → her tick "disposed provider" hatası (host ayakta ama bildirim fan-out olmuyordu). Doğrusu: scope tüm tick'i sarar, context o scope içinde kullanılıp kapanır. Canlı doğrulandı (Created→açan+admin maili log sender'da).

## Bilinen sorunlar / teknik borç

| # | Açıklama | Öncelik |
|---|---|---|
| 1 | `react-router-dom` 7.18.2'de kalan tek yüksek açık (GHSA-qwww-vcr4-c8h2) **RSC mode'a özgü**; biz SPA data router kullanıyoruz → uygulanamaz, kabul edildi. React Router açık listesi sürümler arası gidip geliyor; upstream stabilize olunca yükselt. | Düşük |
| 2 | Repo kök dizini adı `Yeni klasör` — ASCII dışı boşluklu; bazı CI/araçta yol sorunu çıkarabilir, yeniden adlandırılabilir. | Orta |
| 3 | JWT signing key + süper admin kimlik dev'de user-secrets'ta (`Jwt:SigningKey`, `SuperAdmin:*`). **Prod'da env ile verilecek** — repoya girmez. Dev signing key kısa/örnek; prod'da güçlü key şart. | Yüksek |
| 4 | Branch koruma / PR akışı tanımlı değil (`main` doğrudan push'a açık). | Düşük |
| 5 | ~~Süper admin'in sıfırdan admin + ilk şirket oluşturma akışı yok~~ → **kapandı**: `User.CanCreateCompany` + `/api/users/admins` (admin oluştur) + `/api/companies` (admin şirket açar). Backend canlı doğrulandı; UI kaldı. | ✅ kapandı |
| 6 | Başlangıçta migration+seed API startup'ta çalışıyor; çok-instance prod'da tek seferlik migration adımına taşınmalı. | Orta |
| 7 | FluentValidation hata mesajları OS kültürüne göre (dev'de Türkçe) geliyor; i18n mesaj kataloğuyla (spec §4.3) birleştirilecek (Faz 7). | Düşük |
| 8 | Ticket araması `EF.Functions.Like('%term%')` — sargable değil, büyük tablolarda tarama. v1 ölçeğinde kabul; ölçek büyürse full-text index. | Düşük |
| 9 | `TicketEvents` yazılıyor ama tüketen mail worker'ı yok (Faz 5). Şimdilik yalnız audit/rapor kaynağı. | Düşük |
| 10 | ~~Attachment Faz 3'te yok~~ → Faz 4'te eklendi (S3 + presigned). | ✅ kapandı |
| 11 | Gerçek S3/MinIO bucket sağlanmadı; presign kod yolu fake'lerle test edildi, **gerçek yükleme/indirme baytları uçtan uca doğrulanmadı**. Bucket + credential (env/user-secrets `S3:AccessKey/SecretKey`) gelince e2e test. | Orta |
| 12 | CAPTCHA provider bağlı değil (açılırsa fail-closed). Provider seçilince `CaptchaValidator`'a tek branch. | Orta |
| 13 | Presigned PUT S3'te dosya boyutunu zorlayamaz (client bildirimi doğrulanıyor). Presigned POST content-length-range veya upload sonrası HEAD ile sıkılaştır. | Orta |
| 14 | Rate limiter in-memory + per-instance; çok-instance prod'da distributed (Redis vb.) limiter gerekir. | Düşük |
| 15 | Token hash'leme 3. kez kopyalandı (refresh/invite/public-form SHA256). Auth'a bir dahaki dokunuşta `TokenHasher` helper'ına çıkar. | Düşük |
| 16 | SMTP sağlayıcı seçilmedi; dev'de log sender. Gerçek SMTP + SPF/DKIM/DMARC + gönderim logu (§14) sağlayıcı gelince. | Orta |
| 17 | Bildirim debounce tick-içi tekilleştirme; gerçek zaman-pencereli çapraz-tip özet mail değil (§14). | Düşük |
| 18 | EmailQueue retry backoff'suz (her tick tekrar dener); üstel backoff + next-retry zamanı eklenebilir. | Düşük |
| 19 | Bildirim worker'ı migration+seed gibi tek-instance varsayar; çok-instance prod'da kuyruk çekişi için satır kilidi/`SKIP LOCKED` gerekir. | Orta |
| 20 | SettingsService cache'siz (unique-key okuma). Hot per-request okuyucu (ör. her istekte branding/limit) çıkarsa cache + değişince invalidate; çok-instance'ta invalidation stratejisi gerekir. | Düşük |
| 21 | Var olan tipli Options (Ticket/Notification/File) hâlâ config'ten; §13 gereği DB Settings'e taşınmalı. Seam hazır; her tüketici dokunulunca geçirilir. | Düşük |
| 22 | Export yalnız CSV; gerçek `.xlsx` yok (bağımlılık ister). İstenirse ClosedXML/EPPlus ile eklenir. | Düşük |
| 23 | KVKK anonimleştirme SuperAdmin only; per-şirket admin'in kendi müşterisinin talebini işlemesi yok (çok-şirketli kullanıcı kimlik çakışması). Kapsamlı çözüm: şirket-scope'lu maskeleme veya membership kaldırma. | Orta |
| 24 | `kvkk.retention_days` saklanıyor ama otomatik saklama-süresi purge/anonimleştirme job'ı yok. | Düşük |

## Ortam gereksinimleri

| Gereksinim | Durum |
|---|---|
| .NET 10 SDK (10.0.302) | ✅ kurulu |
| Node 20 + npm | ✅ kurulu |
| SQL Server 2022 (localhost, Windows auth) | ✅ çalışıyor; `CrmKanban` DB migration ile oluşacak |
| JWT signing key (secret) | ❌ Faz 2'de user-secrets: `Jwt:SigningKey` |
| İlk süper admin (env) | ❌ Faz 1/2 seed: `SUPERADMIN_EMAIL`, `SUPERADMIN_PASSWORD` |
| GitHub push yetkisi | Gerekli |
