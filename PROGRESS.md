# CRM + Kanban — Progress

| Alan | Değer |
|---|---|
| Son güncelleme | 2026-07-30 |
| Aktif faz | Faz 4 tamam — sıradaki: Faz 5 (Bildirim / EmailQueue) |
| Genel durum | Faz 0-4 tamam, build+test yeşil (56 test: 16 domain + 40 application), `TicketPipeline`+`Attachments` migration'ları gerçek DB'ye uygulandı |
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

> **Faz 3 tamamlandı — Mustafa'ya demo noktası (spec §18.21).** Faz 5-8 spec §17'de.

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

## Bir sonraki oturum — açık uçlar / Mustafa'ya sorulacaklar (spec §18.21-24)

- Teslim/demo tarihi (Faz 3 sonu demo öneriliyor).
- ~~S3 sağlayıcı~~ → S3-uyumlu SDK, dev MinIO varsayımı seçildi (kod AWS/MinIO'da aynı; prod endpoint/credential env'den). Gerçek bucket sağlanınca e2e (teknik borç #11).
- ~~CAPTCHA provider~~ → seam + dev'de kapalı seçildi; provider (Turnstile/reCAPTCHA) sonra (teknik borç #12).
- **SMTP sağlayıcı (Faz 5'i doğrudan etkiler)** + deploy ortamı.
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

## Bilinen sorunlar / teknik borç

| # | Açıklama | Öncelik |
|---|---|---|
| 1 | `react-router-dom` 7.18.2'de kalan tek yüksek açık (GHSA-qwww-vcr4-c8h2) **RSC mode'a özgü**; biz SPA data router kullanıyoruz → uygulanamaz, kabul edildi. React Router açık listesi sürümler arası gidip geliyor; upstream stabilize olunca yükselt. | Düşük |
| 2 | Repo kök dizini adı `Yeni klasör` — ASCII dışı boşluklu; bazı CI/araçta yol sorunu çıkarabilir, yeniden adlandırılabilir. | Orta |
| 3 | JWT signing key + süper admin kimlik dev'de user-secrets'ta (`Jwt:SigningKey`, `SuperAdmin:*`). **Prod'da env ile verilecek** — repoya girmez. Dev signing key kısa/örnek; prod'da güçlü key şart. | Yüksek |
| 4 | Branch koruma / PR akışı tanımlı değil (`main` doğrudan push'a açık). | Düşük |
| 5 | Süper admin'in **sıfırdan admin + ilk şirket** oluşturma akışı henüz yok. Mevcut davet akışı şirket-scope'lu (var olan şirkete personel/2.admin). Spec §9 "admin hesabını süper admin açar, admin şirketini kendi açar" → ayrı company-management endpoint'i (Faz 3 civarı). | Orta |
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

## Ortam gereksinimleri

| Gereksinim | Durum |
|---|---|
| .NET 10 SDK (10.0.302) | ✅ kurulu |
| Node 20 + npm | ✅ kurulu |
| SQL Server 2022 (localhost, Windows auth) | ✅ çalışıyor; `CrmKanban` DB migration ile oluşacak |
| JWT signing key (secret) | ❌ Faz 2'de user-secrets: `Jwt:SigningKey` |
| İlk süper admin (env) | ❌ Faz 1/2 seed: `SUPERADMIN_EMAIL`, `SUPERADMIN_PASSWORD` |
| GitHub push yetkisi | Gerekli |
