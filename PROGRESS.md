# CRM + Kanban — Progress

| Alan | Değer |
|---|---|
| Son güncelleme | 2026-08-06 |
| Aktif faz | **Faz 0-37 tamam. 214 test yeşil** (16 domain + 190 application + 8 API/HTTP). **CANLI = `cb1f61c` (Faz 37), 2026-08-06 14:00.** **Faz 37 (2026-08-06):** süper admin hiçbir panoyu göremiyordu (aktif şirket üyelik claim'lerinden türetiliyordu, süper adminin üyeliği yok) → `GET /companies` yetkili kaynak yapıldı; **Faz 35+36+37 canlıya alındı** (CI'da deploy adımı yok, elle). Deploy'da Faz 34'ün boyut-farkı yönteminin `index.html`/`Api.exe`/`staticwebassets.endpoints.json`'ı kaçırdığı görüldü (aynı uzunluk, farklı içerik) → SHA256 farkına geçildi, 16 yerine 19 dosya. Canlı doğrulama: yeni SPA paketi yayında, `customer-invites` 401 (var), anonim form `pendingApproval:true` döndü → yeni kural + migration canlıda. **Faz 36 (2026-08-06):** (1) **talep silme** — backend (uç + `ticket.delete` + soft-delete) hazırdı, UI'da buton hiç yoktu; `useDeleteTicket` + onaylı "Sil" eklendi. (2) Pano kapsamı **zaten şirket bazlı** (tek aktif şirket + navbar seçici), değişiklik yapılmadı. (3) **Pano kutu ızgarasına geçti**: eski `overflow-x-auto` + `w-72` düzeninde 6 sütunun 4'ü sığıyordu ve HTML5 drag oto-kaydırma yapmadığı için ekran dışı sütuna kart bırakmak **imkânsızdı**; artık `auto-fill` grid ile hepsi sayfada, başlığa tıklayınca tam satıra genişliyor, kapalı kutular da bırakma hedefi. Açık iş: süper admin hiçbir panoyu göremiyor (üyeliği yok). **Faz 35 (2026-08-06):** giriş kuralı yeniden yazıldı — "müşteri linki oluştur" herkese **aynı** tokensiz URL'i (`/c/{slug}`) veriyordu, bu yüzden "personelin davet ettiği müşteri" ile "linki bulan yabancı" sunucu için ayırt edilemiyordu ve ikisi de doğrudan kanbana düşüyordu. Link artık kişiye özel davet token'ı taşıyor (`?davet=`, e-posta+şirkete bağlı, tek kullanımlık, 7 gün). Yeni kural: **otomatik onayın tek yolu geçerli bir personel daveti** — yabancı da, kayıtlı müşterinin sonraki talebi de onay kutusuna düşer; süreklilik için onay ekranında "Onayla + güven". `Invitation.CompanyId` eklendi (bir şirketin daveti diğerinde maymuncuk olurdu), `CustomerTrust` entity + migration. **Faz 34 (2026-08-06):** MonsterASP.NET canlı deploy — **canlıda HTTPS hiç yoktu** (sertifika verilmemiş, domain yalnız `http://` bağlı), yani `App:PublicBaseUrl=https://...` olduğu için maillerdeki tüm bağlantılar ölüydü; Let's Encrypt açıldı + zorunlu HTTPS yönlendirmesi (307 doğrulandı). Deploy: bayt karşılaştırmasıyla yalnız 2 dosya farklıydı (`Infrastructure.dll` + `appsettings.Production.json`), `app_offline.htm` ile kilit açılıp yüklendi. Canlı doğrulama: `https://crm-kanban.runasp.net` üzerinden ilgisiz üçüncü taraf bir adrese kayıt → mail ulaştı, `text/plain` parçası ve `reply-to` başlığı canlıda aktif. **Faz 33 (2026-08-06):** mail teslimatı — "sadece benim adresime gidiyor" şikâyeti ölçüldü: **alıcı kısıtı yok** (ham SMTP `RCPT TO` üç sağlayıcıya 250, uygulama akışından üçüncü taraf adrese gerçek teslim), sorun **spam eşiği** idi (SpamAssassin 4.1/5.0). İki fix: `SmtpEmailSender` artık `multipart/alternative` (HTML + türetilmiş düz metin) ve yeni `Email__ReplyTo` — Brevo'nun freemail `From`'u yeniden yazarken enjekte ettiği Reply-To'yu (`FREEMAIL_FORGED_REPLYTO` +2.5) engelliyor. **Ölçülen: 4.9/10 → 7.8/10, SpamAssassin 4.1 → 1.2.** `From`'u `<id>.brevosend.com`'a taşıma denendi ve **reddedildi** (relay 250 veriyor, mesaj sessizce hiç teslim edilmiyor — maili tamamen keserdi). `publish-fd/` gitignore'a alındı (dolu secret'lı `appsettings.Production.json` taşıyor). **Faz 32 (2026-08-05):** gereksinim notlarının (OneDrive/Belgeler txt'leri) koda karşı denetimi — SOLID/clean-arch maddeleri karşılanıyor; txt'nin açıkça şikâyet ettiği **UI tekrarı kapatıldı** (15 ekrandaki hata-mesajı kopyası → tek `errorText`, 10 ekrandaki "Yükleniyor…" → tek `Loading` primitive'i); **deploy sertleştirme:** teknik borç #35 (yanlış 500 log alarmı) ve #40 (yetki anahtarı ↔ uygulama yeri denetim testi, negatif doğrulamalı) kapandı, UTC serileştirici kültür bağımsız (`src/` 0 uyarı), `publish.ps1` paketi uçtan uca üretildi. **Faz 31 (2026-08-05):** yetkilerde üçüncü durum — Ver / Reddet / **rol varsayılanına dön** (`DELETE /permissions/assign`); bu sırada "silinmiş yetki satırları hâlâ uygulanıyor" bug'ı bulundu ve kapatıldı (Faz 28 ile aynı sınıf: `IgnoreQueryFilters` + eksik `DeletedAt` koşulu). **Faz 30 (2026-08-05):** yetki ekranı on/off switch'e geçti (StarAdmin `.form-switch` biçimi) + her yetkinin ne iş yaptığı yazıldı; kullanıcı satırındaki "kimliğine gir / hesabı sil" buton oldu; **dört yetki bug'ı** bulundu ve kapatıldı — `ticket.view` hiçbir yerde uygulanmıyordu, `permission.assign` yazma yolunda kontrol edilmiyordu, iki global yetki şirket bazında atanabilir görünüyordu, katalog her kullanıcıya açıktı. **Faz 29 (2026-08-05):** doküman denetimi — 6 md dosyasında 21 sapma bulundu ve düzeltildi; spec 16 faz sonra ilk kez koda göre güncellendi (presigned→API-proxy, .NET 10, şirket-özel statüler, eksik tablolar, §18.21/22 boşluğu). **Faz 28 (2026-08-05):** manuel test turunda **iki kiracı-izolasyonu açığı** bulundu ve kapatıldı — (1) `GET /api/tickets` + moderasyon kuyruğu her personele tüm firmaların taleplerini veriyordu (`IgnoreQueryFilters()` join'i sorgu genelinde filtreyi kapatıyor), (2) soft-delete edilmiş üyelik token/`/me` kapsamında kalıyordu, yani şirketten çıkarılan personel yeni girişte tam yetkiyle dönüyordu. +10 test, canlı doğrulandı; `CRM_Kanban_Gereksinim_Listesi.md` kanıtlarıyla işaretlendi. **Faz 27 (2026-08-04):** şirket silme — çift onay (UI paneli + sunucuda şirket adı doğrulaması), yalnız sahibi admin/süper admin, soft delete + arşiv + slug serbest bırakma; bir admin birden fazla şirket açabiliyor (navbar'da aktif şirket seçici + şirket açma/silme sonrası token tazeleme). **Faz 26 (2026-08-04):** müşteri görsel yükleme (png/jpg/webp, magic-byte doğrulamalı) + ticket detayda küçük resim; **dosyalar artık AWS S3'te** (`crm-kanban-fiskirmacoop`, eu-north-1, tek-bucket'lık IAM kullanıcısı). **Faz 25:** bildirim dili alıcıya göre + Brevo. **Faz 25 (2026-08-04):** talep bildirimleri artık hem müşteriye hem atanana gidiyor, şablon alıcıya göre seçiliyor (müşteri dili / personel dili `ticket_staff_update`); Brevo için `Email__FromName` + `.env.example`. **Faz 24 (2026-08-04):** `/admin/users` (sunucu-taraflı arama + tür/durum filtresi + müşteri-firma ilişkisi + KVKK hesap silme + davet bağlantısı) ve `/admin/templates` (gruplu liste + canlı önizleme + şablona özel yer tutucular). **Faz 23 (2026-08-04):** şirkete özel müşteri giriş sayfası `/c/{slug}` (kayıt → e-postaya 6 haneli kod → doğrula → ilk talep doğrudan fırsat havuzuna), "müşteri linki oluştur" (kopyala/WhatsApp/e-posta), personel için "Yeni" + sütun içi hızlı ekleme, Odoo (`o_kanban_group`/`o_opportunity_kanban`) görünümü. Öncesi: **Faz 0-19** **Faz 19 (2026-08-03):** süper admin görünmezliği (member list + effective-view + assign-target sızıntıları kapatıldı) + ham yetki anahtarları Türkçeleştirildi + **yetki Ver/Reddet toggle 500 bug'ı düzeltildi** (remove+add → yerinde güncelle). **Faz 16-18 (2026-08-03):** (16) süper admin impersonation UI; (17) müşteri portalı ilişki-scope'u (yalnız iş yaptığı firmalar) + müşteri süreç göstergesi; **deploy config:** SMTP→Resend (canlı doğrulandı), depolama→host diski `local` (+azure/s3 provider); (18) hesap sayfası (`/account`: bilgi + parola değiştir + KVKK hesap silme) + mail şablonları anlamsallaştırıldı (buton/footer + ticket deep-link). Kalan: operasyonel (TLS→MonsterASP, CAPTCHA provider, prod secret) — ONERILER P0 |
| Genel durum | Faz 0-8 + onboarding/RBAC + Faz 9 tamam. **Faz 9 (2026-07-31):** (1) müşteri self-registration e-posta doğrulama akışı uçtan uca bağlandı — public form yeni müşteride `account_invite` mailini kuyruğa atıyor, `/invite` ekranı token'la şifre belirleyip hesabı aktive ediyor (personel daveti de `staff_invite` maili gönderiyor); (2) müşteri yüzeyi: `Home` dispatcher (personel→kanban, müşteri→Taleplerim), `CustomerTickets` listesi, müşteri-sade nav; (3) **bug fix:** müşteri yazma yolu (`CommentService`/`TicketCommandService` ticket load) tenant filtresiyle yüklüyordu → müşterinin şirket scope'u yok → kendi ticket'ına yorum/iptal 404; `IgnoreQueryFilters + authz` deseniyle düzeltildi; (4) kanban drag-drop: `dataTransfer` set (Firefox), `onDragEnd` temizliği, kendi kolonuna no-op drop engeli, sürükleme görsel geri bildirimi; (5) CRM-tadında demo seed (teklif/talep + müşteri-personel yorum thread'leri) + `Seed:Demo` bayrağıyla Production'da da çalıştırılabilir. **96 test yeşil** (+3 müşteri yazma-yolu). Docker stack `up.ps1` ile ayağa kaldırıldı, e2e doğrulandı (login/public-form→mail→invite→müşteri yorum) |
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
- [x] Ticket detay aksiyonları bağlandı: staff için STATÜ/ATANAN/ÖNCELİK dropdown'ları (`/status`,`/assign`,`/priority`); müşteri için İptal/Tamamlandı butonları (terminal değilse); statü kataloğu endpoint'i (`GET /api/tickets/statuses`)
- [x] Dev-only demo seed (`DevSeeder`, yalnız Development, idempotent): demo şirket + admin(`admin@demo.local`/`Demo!2026Pass`) + personel + müşteriler + 6 statüye yayılmış 10 ticket → kanban/dashboard dolu görünüyor
- [x] Public form dosya yükleme UI — **zero-trust'a geçirildi** (backend'e byte akışı + magic-byte doğrulama, yalnız pdf/txt/doc/docx); form'da çoklu dosya ekleme + kaldırma
- [x] Personel/yorum tarafı dosya yükleme UI — **Faz 12'de tamamlandı** (API-proxy yükleme + indirme, ticket detay "Ekler" bölümü). Presigned PUT yerine backend-proxy seçildi (bkz. Faz 12).

### Faz 7b — Kanban sütun yönetimi (per-şirket) ✅ (2026-07-31)
- [x] Domain: `TicketStatus` mutatörleri (Rename/Recolor/MoveTo), `Ticket.MigrateStatus` (kimlik remap, state-machine'siz), `PermissionKeys.StatusManage` + Admin baseline
- [x] `StatusManagementService`: istenilen konuma ekleme (Order kaydırma), yeniden sıralama, ad/renk güncelleme, silme (kullanımda/son-açık guard'ı). İlk özelleştirmede global set **fork** + şirketin ticket'ları klona **migrasyon** (ticket orphan olmaz)
- [x] Yeni sütun otomatik transition zincirleme (spec §12 kuralı: non-terminal ↔ tüm sütunlar; terminal yalnız hedef) → staff kartı içine/dışına sürükleyebilir
- [x] `StatusSet.EffectiveAsync` tek predicate ("kendi seti varsa o, yoksa global") — kanban + statü dropdown + initial-status tek yerden okur
- [x] Endpoint'ler: `/api/companies/{companyId}/statuses` (list/create/update/reorder/delete); `/api/tickets/statuses?companyId=` company-aware
- [x] UI: `/admin/columns` — konum seçerek ekle, renk/ad inline düzenle, yukarı/aşağı sırala, sil; şirket seçici (çok-şirketli/süper admin)
- [x] Testler (+6): fork+migrasyon, konuma ekleme, transition zincirleme, reorder, kullanımda-silme reddi, yetki gate

### Faz 7c — Public form zero-trust intake + moderasyon ✅ (2026-07-31)
- [x] `Ticket.ApprovalState` (Approved/Pending/Rejected, default Approved) + `TicketApprovalState` migration (mevcut/staff ticket'ları Approved)
- [x] İlk-kez (bilinmeyen e-posta) müşteri → ticket **Pending**; bilinen müşteri direkt havuza. Kanban + staff listesi Pending/Rejected'ı **dışlar**; müşteri kendi ticket'ını her durumda görür
- [x] Moderasyon: `/api/tickets/moderation/{companyId}` (staff-only) + `/approve` + `/reject` (ticket.edit gate → Admin/SuperAdmin); UI `/moderation` + kanban'da "onay bekliyor" rozeti
- [x] Zero-trust upload: `IFileStorage.PutAsync` (S3'e server-side yükleme), `PublicFileValidator` (uzantı + content-type + **magic byte** eşleşmesi; pdf=%PDF, docx=PK zip, doc=OLE2, txt=NUL/kontrol-karakter sniff), boyut **sunucuda** ölçülür (client bildirimi güvenilmez)
- [x] Public upload endpoint `POST /api/public/form/{slug}/upload` (IFormFile, 11MB request limit) — presigned public path kaldırıldı; staff presigned path korundu
- [x] Testler (+12): PublicFileValidator (7: pdf/txt/doc/docx kabul, png/mismatch/NUL/oversize red), StorePublicUpload (2), moderasyon (4: kanban dışlama, approve, reject, non-pending red), public form Pending/Approved (2), + attachment build (public set)

### Faz 8 — Deploy (Docker Compose) ⚠️ (artefaktlar hazır, imaj build daemon'suz doğrulanmadı)
- [x] `src/CrmKanban.Api/Dockerfile` (multi-stage .NET 10 SDK→aspnet, non-root, http:8080)
- [x] `frontend/Dockerfile` (node build → nginx) + `frontend/nginx.conf` (SPA fallback + `/api`→api:8080 reverse proxy, same-origin)
- [x] `docker-compose.yml`: db (mssql 2022 Express, healthcheck) + minio (+ createbucket one-shot) + api (depends healthy) + web (nginx)
- [x] `.env.example` (tüm secret'lar), `appsettings.Production.json` (non-secret; Captcha kapalı — provider'sız fail-closed uyarısı), `.dockerignore`, README deploy bölümü
- [x] Doğrulanan: `dotnet publish -c Release` (Dockerfile'ın komutu) + frontend `npm run build` (Dockerfile'ın komutu) + `docker compose config`
- [ ] **`docker compose build/up` gerçek imaj build'i çalıştırılmadı** — bu ortamda Docker daemon (Desktop Linux engine) kapalı. İlk deploy'da imaj build + e2e stack testi yapılmalı (teknik borç #25)

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

### Faz 9 — Email doğrulama akışı + müşteri yüzeyi + drag-drop + demo seed ✅ (2026-07-31)
- [x] **Müşteri e-posta doğrulama / self-registration (uçtan uca):** public form yeni (bilinmeyen) müşteride, mevcut davet token'ını artık gerçekten mailliyor — `InviteEmail.Enqueue` ortak yardımcısı `EmailQueue`'ya `account_invite` satırı atar, notification worker gönderir. `/invite?token=...` ekranı (`AcceptInvite.tsx`) şifre belirler → `/api/invitations/accept` → hesap aktive (token sahipliği = e-posta doğrulaması). Personel daveti de aynı mekanizmayla `staff_invite` mailler.
- [x] **Güvenlik:** raw invite token artık API cevabında dönmüyor (`PublicFormResult.InviteToken`→`NewAccount:bool`); token yalnız e-postayla çıkıyor.
- [x] **Link base URL:** `AppOptions.PublicBaseUrl` (`App__PublicBaseUrl`, docker'da `http://localhost:8080`) — maildeki mutlak link.
- [x] **Müşteri yüzeyi:** `Home` dispatcher index'te (personel→`Kanban`, müşteri→`CustomerTickets`); `CustomerTickets` = müşterinin kendi talep listesi (`GET /tickets`, OpenedById-scope); `TicketDetail` zaten müşteri yorum + İptal/Tamamlandı destekliyordu; Shell nav müşteride yalnız "Taleplerim".
- [x] **Bug fix (core, test-first):** `CommentService.LoadTicketAsync`/`LoadCommentAsync` ve `TicketCommandService.LoadAsync` ticket'ı tenant query filter ile yüklüyordu; müşterinin şirket scope'u olmadığından kendi ticket'ına yorum/iptal/tamamla **404** dönüyordu. `GetDetailAsync` deseniyle `IgnoreQueryFilters()` + `DeletedAt==null` + `authz.ResolveAsync` (opener/in-company geçidi) olarak düzeltildi. Çapraz-tenant hâlâ 403 (izolasyon authz'da korunuyor). +3 test: müşteri kendi ticket'ına yorum/iptal yapabilir, yabancı yapamaz (403).
- [x] **Kanban drag-drop etkileşimi:** `TicketCard` — `onDragStart`'ta `dataTransfer.setData` + `effectAllowed='move'` (Firefox drop'u için şart), `onDragEnd` temizliği, sürüklenen kartta opacity/ring; `Kanban` — sürükleme kaynağı statüsü izleniyor, karta kendi kolonuna bırakılırsa sunucuya no-op status çağrısı gitmiyor.
- [x] **CRM demo seed:** `DevSeeder` zenginleştirildi — teklif/talep dilinde başlıklar + gövde + müşteri↔personel yorum thread'leri (iki şirket: tekstil/mermer). `Program.cs` artık `Seed:Demo=true` ile Production'da da demo seed'i çalıştırıyor (`docker-compose` `SEED_DEMO`, default false). Idempotent (slug varsa atlar).
- [x] **Doğrulama (canlı, 8080):** `up.ps1` ile stack ayakta; tekstil admin login→kanban seed görünür; public form (yeni müşteri)→`account_invite` maili log'da link'le→`/invite` şifre→müşteri login→"Taleplerim"→ticket yorum (200). 96 test yeşil, frontend build temiz.

**Karar/Varsayım (Faz 9):**
- *Kayıt modeli:* Kullanıcı onayıyla "şirket formu üzerinden" seçildi (mimari çok-kiracılı; müşteri bir şirkete talep açarak doğar). Bağımsız global `/register` yapılmadı — hangi şirkete bağlanacağı belirsiz olurdu; istenirse küçük bir ek (flag).
- *Neden mevcut davet akışını maillemek, yeni e-posta-doğrulama entity'si değil:* spec §9 zaten "klasik register + invite link" diyor; Invitation + AcceptInvite hazırdı, tek eksik mail gönderimiydi. Ayrı `EmailVerificationToken` tablosu ikinci bir tek-kullanım-token mekanizması olurdu (over-engineering). Token sahipliği zaten adresi doğruluyor.
- *Bug fix kapsamı:* Müşteri yüzeyi eklenince ortaya çıkan latent hata; istenen özellik (müşteri iletişimi) bu düzeltme olmadan çalışmıyordu → SCOPE DISCIPLINE "yanlış/eksik olan bitişik düzeltme" istisnası kapsamında yapıldı.

### Faz 10 — Müşteri portalı (self-service kayıt + şirket seçip mesaj) ✅ (2026-07-31)
- [x] **Self-service kayıt:** `POST /api/auth/register` (anonim, rate-limitli, nötr 204 — enumeration yok) → `AuthService.RegisterAsync` inactive hesap + `Invitation` token + `account_verify` maili. Şifre kayıt adımında DEĞİL, mevcut `/invite` linkinde belirlenir (en az kod, akışı yeniden kullanır). Frontend `Register.tsx` (`/register`) + Login'e "Kayıt ol" linki.
- [x] ~~**Public şirket listesi:** `GET /api/public/companies` (anonim) → aktif/arşivsiz şirketler `(id,name,slug)` — kayıt/mesaj için seçici besler.~~ → **Faz 17'de kaldırıldı** (uç nokta bugün 404). Müşteri portalı ilişki-scope'una geçince global firma listesi hem gereksiz hem yanlış oldu: müşteri yalnız **iş yaptığı** firmalara yazabiliyor, seçiciyi `GET /api/tickets/my-companies` besliyor.
- [x] **Müşteri şirket seçip mesaj:** `POST /api/tickets/customer` (yetkili) → `TicketCommandService.CreateAsCustomerAsync`: üyelik gerekmez, seçilen aktif şirkette ticket açar (OpenedById=müşteri, doğrulanmış hesap → `Approved`). Frontend `CustomerTickets` içinde "Yeni mesaj" bestecisi (firma dropdown + konu + mesaj).
- [x] **Gerçek SMTP plumbing:** `docker-compose`/`​.env.example`'a `EMAIL_HOST/PORT/USE_SSL/USERNAME/PASSWORD/FROM`. Gmail App Password ile gerçek gönderim (`SmtpEmailSender` hazır). Kullanıcı `.env`'de doldurup `EMAIL_PROVIDER=smtp` yapınca gerçek mail gider.
- [x] **Temizlik:** `TokenHasher` ortak helper'ı (refresh dışı 3 kopya token-hash birleşti) — Faz 9'daki ponytail borcu kapandı; `PublicFormService`/`InvitationService` bunu kullanıyor.
- [x] **Bug fix:** `ListOpenCompaniesAsync` `!c.IsArchived` (hesaplanan property) SQL'e çevrilemiyordu → **500** (InMemory testi yakalamadı, gerçek SQL Server yakaladı). `c.ArchivedAt == null` ile düzeltildi. Ders: computed property'leri EF Where'de kullanma.
- [x] **Doğrulama (canlı, 8080, e2e API + UI):** register→`account_verify` mail (log'da link)→`/invite` şifre→müşteri login (companies=0)→`/public/companies` (3 firma)→şirket seçip `POST /tickets/customer` (TEKSTIL-13)→"Taleplerim"de görünür. UI: `/register` ekranı + besteci dropdown (3 firma) render doğrulandı. **99 test yeşil** (+3: 2 register + 1 customer-ticket).

**Karar/Varsayım (Faz 10):**
- *Şifre kayıtta değil, doğrulama linkinde:* mevcut `/invite` set-password akışını aynen kullanır (yeni verify-only endpoint'i yok). Kullanıcı "kayıtta şifre" isterse: `RegisterRequest`'e şifre + `POST /api/auth/verify-email` (yalnız activate) — küçük ek.
- *Doğrulanmış müşteri ticket'ı `Approved`:* anonim formun zero-trust `Pending`'inin aksine — doğrulama güveni kurdu, portal akıcı kalsın.
- *Public şirket listesi açık enumerasyon:* public-talep CRM'i için kabul; rate-limitli.
- *`CreateAsCustomerAsync` herhangi bir yetkili kullanıcıya açık:* müşteri o şirketin müşterisi olur; üyelik istemez (staff `CreateAsync` kullanır). Amaçlanan portal davranışı.

### Faz 11 — MonsterASP.NET deploy hazırlığı ✅ (kod/artefakt hazır; upload kullanıcıda)
Plan `~/.claude/plans/concurrent-crunching-teacup.md`. Yapıldı:
- [x] `Program.cs`: tek-site hosting — `UseStaticFiles`+`UseDefaultFiles`+`MapFallbackToFile("index.html")` (SPA wwwroot'tan) + `UseForwardedHeaders` (IIS/nginx proxy arkasında şema/IP). Docker'da wwwroot boş → no-op (nginx sunar); `.dockerignore`'da wwwroot hariç.
- [x] `publish.ps1`: SPA build → API `wwwroot` → `dotnet publish -c Release -r win-x64 --self-contained true -o publish`. Self-contained → host'ta .NET 10 gerekmez. `web.config` (in-process ANCM) publish ile gelir. **Doğrulandı:** publish çıktısı `wwwroot/index.html` + `web.config` bundle'lıyor.
- [x] `DEPLOY-monsterasp.md`: adım adım — ücretsiz MSSQL bağlantı dizesi, env config (`Section__Key`), upload, R2/B2 (dosya için), Gmail SMTP, sorun giderme.
- [x] **nginx DNS fix:** `resolver 127.0.0.11` + değişkenli `proxy_pass` — api rebuild sonrası nginx'in stale-IP cache'inden gelen 502 kalıcı çözüldü (bu oturumda gözlendi).
- [ ] **Kullanıcı adımı:** MonsterASP.NET hesabı/DB oluştur → `./publish.ps1` → `./publish` içeriğini siteye yükle → env'leri (ConnectionString, Jwt, SuperAdmin, Email/Gmail, App__PublicBaseUrl) panelde ayarla → aç. (Ben hesaba erişemem.)
- [ ] **Açık:** dosya yükleme için S3 (R2/B2) — yoksa yalnız dosya akışı çalışmaz.

### Faz 12 — Personel/müşteri dosya eki (API-proxy) + canlı e2e doğrulama ✅ (2026-07-31)
Bu oturumda stack Docker'da gerçekten ayağa kaldırıldı (`up.ps1`, 8080) ve tarayıcıda uçtan uca gözlemlenerek eksik kapatıldı.
- [x] **Ticket detay "Ekler" bölümü (`TicketDetail.tsx`):** dosya seç → yükle, mevcut ekleri listele, tıklayıp indir. Faz 7'nin açık maddesi (personel/yorum dosya UI) kapandı.
- [x] **Yükleme/indirme artık API üzerinden proxy'leniyor (presigned DEĞİL):** `POST /api/tickets/{id}/attachments` (IFormFile, 11MB) baytı backend'e alır, `authz.ResolveAsync` ile yetki, boyutu **sunucuda ölçer** (client bildirimini güvenmez), `IFileStorage.PutAsync` ile depolar, satırı ticket-seviyesinde (`CommentId=null`) bağlar. İndirme `GET /api/tickets/attachments/{id}/download` artık presigned redirect yerine `IFileStorage.GetAsync` ile baytı **stream eder** (`File(...)`). `IFileStorage`'a `GetAsync` eklendi.
- [x] **Kök-neden düzeltmesi (latent, tarayıcıda yakalandı):** presigned URL'ler `S3:ServiceUrl=http://minio:9000` (docker-içi host) üzerinden imzalanıyordu → tarayıcı bu host'a erişemez → yükleme **ve** indirme hiçbir zaman tarayıcıdan çalışmamıştı (teknik borç #11'in neden hiç doğrulanmadığının sebebi buydu). Proxy yaklaşımı topolojiden bağımsız (tarayıcı same-origin `/api` → nginx → api → minio).
- [x] **Kök-neden düzeltmesi #2 (latent axios bug):** `api` axios instance'ı global `Content-Type: application/json` set ediyordu → FormData yüklemelerinde multipart/boundary'yi eziyor → **415**. Global default kaldırıldı (axios obje gövdesine json, FormData'ya multipart+boundary'yi kendi koyar). Tüm gelecekteki yüklemeleri de düzeltir.
- [x] **MIME fallback (sağlamlaştırma):** tarayıcı bazı dosyalar için boş/`octet-stream` content-type yollar (özellikle Office). `ResolveContentType` uzantıdan kanonik tipi türetir (AllowedContentTypes ile senkron) → geçerli dosya eksik header yüzünden sessizce reddedilmez.
- [x] **Testler (+3, toplam 101):** ticket yükleme gerçek boyutu ölçer + satırı bağlar; boş MIME uzantıdan türetilir; iç-not dosyası müşteriye kapalı testi yeni `OpenAttachmentAsync`'e taşındı. `TicketQueryService` artık `AttachmentService`'e bağımlı değil (`ToDto` static; DTO url'i API indirme yolu).
- [x] **Canlı e2e doğrulama (tarayıcı, 8080):** (a) upload→download bayt round-trip `roundTripMatch: true`, boyut sunucuda 39B ölçüldü; (b) UI dosya seçici ile `ui-upload.pdf` yüklendi, "Ekler" listesinde göründü; (c) login/kanban/ticket-detay/moderasyon (`2 onay bekliyor`)/public form (branding+KVKK) canlı gözlemlendi.

**Karar/Varsayım (Faz 12):**
- *Presigned yerine API-proxy (staff yolu da):* Presigned PUT/GET bu deploy topolojisinde tarayıcıdan çalışmıyor (imza minio:9000'e); ayrıca presigned PUT boyutu S3'te zorlayamıyordu (teknik borç #13). ONERILER #9 zaten "staff yolunu da backend-proxy'ye çek" diyordu. Public yol zaten proxy'liydi → aynı deseni staff'a genişletmek en az kod + en tutarlı + en güvenli (boyut sunucuda). SCOPE DISCIPLINE "istenen değişiklik bitişik düzeltme olmadan yanlış/çalışmaz" istisnası: "staff dosya UI'ı" presigned'ın çalışmadığı bir dünyada anlamsızdı.
- *Ticket-seviyesi ek (yorum-seviyesi değil):* MVP olarak dosya ticket'a bağlanır (`CommentId=null`), yoruma değil. Yorum-eki (`BuildAttachments` zaten destekliyor) ve iç-nota özel dosya UI'ı ileride; ticket-seviyesi "personel dosya ekleyebilsin" ihtiyacını karşılıyor.
- *Yükleme herkese (opener dahil) açık:* backend `authz.ResolveAsync` opener'ı da geçirir → müşteri kendi ticket'ına dosya ekleyebilir (public form ile tutarlı). İç-not dosyası indirmesi müşteriye hâlâ kapalı (korunmuş invariant).
- *`ToDto` static + DTO url = API yolu:* presigned url ölü olduğundan (frontend kullanmıyordu, host erişilemez) DTO url'i `/api/tickets/attachments/{id}/download`'a çevrildi; `ToDto` instance state kullanmıyor → static; `TicketQueryService`'in `AttachmentService` bağımlılığı düştü.

### Faz 13 — Forgot-password + moderasyon audit/bildirim ✅ (2026-08-01)
Checklist (`crm-kanban-checklist.md`) koda karşı doğrulandı: liste bayattı, çoğu `[ ]` madde aslında yapılmıştı (Faz 0-12). Gerçek kod eksikleri kapatıldı.
- [x] **Forgot-password (spec §1.12, test-first):** `POST /api/auth/forgot-password` (anonim, rate-limitli, nötr 204 — enumeration yok) → `AuthService.ForgotPasswordAsync` yalnız aktif+parolalı hesaba `password_reset` maili kuyruğa atar. Reset linki mevcut `/invite` set-password akışını yeniden kullanır. Frontend `ForgotPassword.tsx` + Login'e "Parolamı unuttum" linki.
- [x] **Güvenlik kök-neden düzeltmesi:** `InvitationService.AcceptInviteAsync` parola belirlendiğinde artık kullanıcının **tüm aktif refresh token'larını iptal ediyor** — taze hesapta no-op, reset'te çalınmış oturumu öldürür (ChangePassword ile aynı invariant). +1 test.
- [x] **Moderasyon audit + bildirim (tech debt #27):** `ApproveAsync`/`RejectAsync` artık `TicketEvent` yazıyor (`Approved`/`Rejected` yeni enum) → audit + bildirim üretir. `NotificationMatrix`: Approved→açan (bilgi), Rejected→açan (**kritik**, müşteri kibar red bildirimi alır). 2 yeni şablon.
- [x] Testler: +3 forgot-password (aktif→mail, bilinmeyen→sessiz, inaktif→sessiz) + 1 reset-oturum-iptali + moderasyon testleri Approved/Rejected event assertion'ı ile genişletildi.

### Faz 14 — Kanban filtre UI + dosya-eklendi bildirimi ✅ (2026-08-01)
- [x] **Kanban filtreleri (checklist §5):** `useKanban(companyId, filters)` — ara (no/başlık) / atanan / öncelik. Backend `TicketQueryService.ApplyFilters` + `Kanban` endpoint'i `[FromQuery] TicketListQuery`'yi zaten binliyordu; yalnız UI eksikti. Filtre çubuğu `Kanban.tsx`'e eklendi (react-query key filtreleri içerir; invalidation prefix-match ile hâlâ çalışır). Not: tarih/müşteri filtresi backend contract'ında yok — istenirse eklenir.
- [x] **Dosya-eklendi bildirimi (checklist §7):** `StoreTicketUploadAsync` artık `AttachmentAdded` (yeni enum) olayı yazıyor → açan+atanan bildirim alır (actor çıkarılır; müşteri kendi yüklemesine mail almaz). Matris girdisi + `ticket_attachment_added` şablonu. Ticket-seviyesi yükleme asla iç-not olmadığından açana bildirim güvenli. +1 test assertion.
- [x] **Checklist reconcile:** `crm-kanban-checklist.md` tamamen yeniden yazıldı — gerçek durumu yansıtıyor; kalan işler (A) operasyonel deploy, (B) kod eksikleri, (~) spec-çelişkisi olarak sınıflandı.

**Karar/Varsayım (Faz 13-14):**
- *Forgot-password ayrı entity değil, invite/accept yeniden kullanımı:* `/invite` set-password akışı zaten vardı; ayrı `PasswordResetToken` tablosu ikinci tek-kullanım-token mekanizması olurdu (over-engineering). Reset linki aynı sayfaya gider. Ponytail: token sahipliği = kimlik.
- *Reset'te oturum iptali paylaşılan `AcceptInviteAsync`'te:* kök-neden doğru yer — taze hesapta zararsız (token yok), reset'te güvenlik şart. Her iki çağıran da düzelir.
- *Moderasyon: Approved açana bildirim de gider:* submit makbuzu "alındı" der; approve "işleme alındı" anlamlı sinyal. Rejected kritik (opt-out'suz) — müşteri reddi mutlaka öğrenir.
- *Dosya bildirimi yalnız ticket-seviyesi upload path'inde:* `StoreTicketUploadAsync` (UI'nin kullandığı yol) CommentId=null → asla iç-not → açana güvenli. Yorum-seviyesi ek (iç-not olabilir) ayrı yoldan gelir, dokunulmadı.
- *Kanban filtreleri yalnız backend'in desteklediği eksende:* ara/atanan/öncelik (+statü/kategori mevcut). Tarih/müşteri filtresi backend'de yok → uydurma backend işi yapılmadı (SCOPE DISCIPLINE).
- *Checklist bir bilgi kaydı, kod değil:* bayat checklist'i gerçekle uyumlamak minimalizmin kestiği şey değil (SCOPE DISCIPLINE: knowledge record kesilmez).

### Faz 15 — Kalan kod eksiklerinin tamamı (kullanıcı onayıyla) ✅ (2026-08-01)
Kullanıcı checklist'teki tüm (B) kod eksiklerini + spec-çelişen edit-bildirimini "yap" dedi. Hepsi kapatıldı.
- [x] **Edit bildirimi (checklist §7, spec §14 override):** başlık/gövde düzenlemesinde açan+atanan mail alır. `NotificationMatrix`'e `Edited` girdisi + `ticket_edited` şablonu. Karar: kullanıcı checklist'i spec §14 "Edited→kimse" default'unun üzerine seçti.
- [x] **Şirketten üye çıkarma (§3):** `DELETE /api/companies/{id}/members/{userId}` + `CompanyService.RemoveMemberAsync` (company admin/super admin gate, sahip çıkarılamaz, soft-delete membership) + Companies UI'da "Çıkar". +2 test.
- [x] **Mail şablon düzenleme UI (§7/§9):** `EmailTemplateService` (list/update, super-admin) + `/api/email-templates` + `/admin/templates` ekranı (sol liste + konu/gövde editörü + placeholder ipuçları). Şablonlar zaten DB'deydi; UI eksikti.
- [x] **Controller/HTTP smoke testleri (§11, ONERILER P1#14):** yeni `CrmKanban.Api.Tests` projesi (`WebApplicationFactory` + InMemory DB). 5 test: /health, protected→401, forgot-password→204, bad login→401 zarf, süper admin login→/me. `Program.cs` iki küçük değişiklik: (a) non-relational provider'da `EnsureCreated` (InMemory), (b) host-capture sentinel'i (`StopTheHostException`/`HostAbortedException`) catch'te yutmama.
- [x] **Konfigüre edilebilir public form alanları (§4.6):** `FormField` entity (per-şirket: label/type/required/options/order/active) + `ConfigurableFormFields` migration (gerçek SQL Server'a uygulandı) + `Ticket.CustomFieldsJson` (denormalize {label,value}). `FormFieldService` (admin/super-admin gate) + `/api/companies/{id}/form-fields` CRUD. Public form config aktif alanları döner; submit zorunluları doğrular (400) ve değerleri ticket'a JSON yazar; ticket detay bunları gösterir. `/admin/form-fields` yönetim ekranı + public form dinamik render. +2 (FormFieldService) +2 (public submit) test.
- [x] **Canlı doğrulama (8080→5221):** migration uygulandı, health 200; form-field create/list/public-config e2e curl ile doğrulandı (test alanı sonra silindi).

**Karar/Varsayım (Faz 15):**
- *Form alanı değerleri denormalize JSON (`{label,value}`) ticket'ta, ayrı `TicketFieldValue` tablosu değil:* v1'de custom alan üzerinde sorgu/rapor YAGNI; JSON tek kolon, alan sonradan silinse/yeniden adlandırılsa bile yakalanan veri okunur kalır. Sorgulanabilirlik gerekirse tablo eklenir (2. gerçek durum).
- *Form alanı yetkisi permission-key'siz, company-admin/super-admin:* `form.manage` permission seed churn'ü yerine `RemoveMember` ile aynı company-admin gate kullanıldı (ponytail). Gerçek granularite gerekirse permission key eklenir. RBAC data-layer prensibi burada company-admin membership kontrolüyle korunuyor.
- *Custom field select doğrulaması sunucuda:* seçilen değer alanın options'ında olmalı (client bildirimi güvenilmez); required boşsa 400.
- *Controller testleri InMemory + env-var config:* WAF'ın `ConfigureAppConfiguration`'ı Program'ın build-time config okumasından geç kaldığı için Jwt/SuperAdmin process env-var ile veriliyor; DB SqlServer→InMemory swap'inde `IDbContextOptionsConfiguration<CrmDbContext>` da kaldırılmalı (EF 9+) yoksa iki provider çakışır.
- *Edit bildirimi spec'i geçersiz kıldı — bilinçli:* PROGRESS Faz 5 "Edited→kimse (§14)" diyordu; kullanıcı checklist §7 lehine karar verdi. Ürün sahibi kararı > spec default'u.

### Faz 16 — Süper admin impersonation UI ✅ (2026-08-01)
Backend zaten vardı (`AuthService.ImpersonateAsync` + `POST /api/auth/impersonate` + 3 test: super-admin-only, super-admin hedeflenemez, audit'li). Eksik olan UI eklendi.
- [x] **Auth context:** `impersonate(userId)` / `stopImpersonation()` / `impersonating` bayrağı. Gerçek admin oturumu (`crm.orig.*`) localStorage'a snapshot'lanır; dönüşte geri yüklenir (`tokens.beginImpersonation/endImpersonation`). Snapshot tek sefer (iç içe impersonate real admin'i ezmez). Hata olursa snapshot geri alınır.
- [x] **UI:** `/admin/users` tablosunda "Kimliğine gir" (yalnız aktif, süper-admin-olmayan, kendisi-olmayan satırlar); `Shell`'de sarı banner "X kimliğiyle görüntülüyorsunuz — Yönetici hesabına dön". Logout tüm token'ları (orig dahil) temizler.
- [x] **Canlı doğrulama (proxy 5173→7084):** süper admin login → impersonate(admin@mermer) → dönen oturum isSuperAdmin=false, `/me`=admin@mermer. Non-super gate + endpoint proxy üzerinden çalışıyor.

- [x] **Kullanıcı listesi şirkete göre gruplandı:** `UserDto`'ya `Companies` (companyId+name+role) eklendi (`UserService.ListAsync` membership+company join); `/admin/users` artık her şirket için ayrı tablo + "Şirkete bağlı olmayan (müşteri/süper admin)" grubu gösterir. Çok-şirketli kullanıcı her grupta görünür. Canlı doğrulandı (Anadolu Tekstil / Ege Mermer / şirketsiz).

**Karar (Faz 16):** *Dönüş için admin token'ları client'ta snapshot:* backend impersonation ayrı "impersonator" claim taşımıyor (basit — hesap verebilirlik audit log'da). Dönüş, admin'in kendi refresh token'ını saklayıp geri yükleyerek yapılır; impersonation sırasında admin token'ı hiç kullanılmadığından revoke olmaz. ponytail: localStorage XSS-okunur (mevcut token modeliyle aynı tavan); tehdit modeli sıkılaşırsa httpOnly cookie.

### Faz 17 — Müşteri portalı: ilişki-scope'lu firma + süreç göstergesi ✅ (2026-08-03)
Kullanıcı canlı test ederken müşteri deneyimindeki boşluğu bildirdi: müşteri portalde TÜM firmaları görüyordu; yalnız iş yaptığı (ilişkili) firmaları görmeli, ilk temas firmanın public formundan olmalı, ve staff kanban'ı değil "talebiniz alındı/işlemde/sonuçlandı" gibi müşteri-dostu bir süreç görmeli.
- [x] **İlişki-scope (güvenlik sınırı, veri katmanında zorlanıyor):** `TicketQueryService.ListMyCompaniesAsync` → müşterinin **kendi ticket'ı olan** firmalar (distinct). `GET /api/tickets/my-companies`. `CreateAsCustomerAsync` artık ilişkisiz firmaya talebi reddediyor (`company.not_related`, `ForbiddenException`) — sadece UI'da gizlemek değil, DATA katmanında (CLAUDE.md least-privilege). +3 test (ilişkili→ok, ilişkisiz→403, my-companies scope).
- [x] **Frontend:** "Yeni mesaj" bestecisi `useMyCompanies` (global liste değil); ilişki yoksa "önce firmanın form linkinden yaz" yönlendirmesi. Ölü `usePublicCompanies` hook kaldırıldı. "Panoya dön"→müşteride "Taleplerime dön".
- [x] **Müşteri süreç göstergesi (`TicketDetail`):** 6 statü-kategorisi 3 müşteri-dostu adıma indirgeniyor — Alındı (Open) → İşlemde (Pending/Answered/Waiting) → Sonuçlandı (Closed); Cancelled ayrı "iptal edildi" durumu. Yalnız müşteride (staff kanban/kontrolleri görür).
- [x] **Canlı doğrulama:** 116 backend testi + frontend build yeşil; stack rebuild, `/api/tickets/my-companies` 401 (yetkili), public form 200. Kullanıcı tarayıcıda uçtan uca test edecek (form→ilişki→portal).

**Karar/Varsayım (Faz 17):**
- *İlişki tanımı = "müşterinin o firmada ticket'ı var":* Müşteri üye değil; mimaride firmaya ticket açarak bağlanır. Ayrı `CustomerCompany` bağ tablosu YAGNI — ticket zaten ilişkinin kanıtı. İlk temas kanalı firmanın public formu (slug), portal değil (kullanıcı onayladı: "dış link → form"). Portaldeki global `/api/public/companies` endpoint'i + `ListOpenCompaniesAsync` + `PublicCompanyDto` + `PublicController` **kaldırıldı** (ilişki-scope sonrası ölü + enumerasyon yüzeyi; ponytail).
- *Scope hem read hem write'ta:* my-companies (read) + CreateAsCustomerAsync (write) ikisi de ilişkiye bağlı. Write reddi kritik — yoksa müşteri API'den rastgele tenant'a ticket açardı (izolasyon ihlali).
- *Süreç göstergesi kategori-bazlı, statü-adı değil:* statü adları süper-admin editable (§4.3); gösterge `StatusCategory`'e map eder. 6→3 sadeleştirme müşteri için; staff tam kanban'ı görür.

### Faz 18 — Hesap sayfası + mail şablonları anlamsallaştırma ✅ (2026-08-03)
- [x] **Hesap sayfası (`/account`):** sağ üstteki isim/avatar linki. Kendi bilgileri (ad, e-posta, hesap türü, bağlı firma sayısı) + **parola değiştir** (mevcut+yeni, tüm oturumları iptal eder) + **hesap sil**. Route Shell altında.
- [x] **Hesap silme = KVKK anonimleştirme (self-service):** `AuthService.DeleteOwnAccountAsync` — parola yeniden-onayı (yıkıcı işlem), `User.Anonymize` (maskele+deaktive, ticket geçmişi korunur), tüm token'lar revoke. **Süper admin kendini silemez** (`auth.superadmin_delete`, sistemi öksüz bırakmaz — KVKK anonymize ile aynı kural). `POST /api/auth/delete-account`. UI'da super admin'e silme bölümü gösterilmez. +3 test (anonimleştir+revoke, yanlış parola→401, super admin→409).
- [x] **Mail şablonları anlamsallaştırıldı:** tutarlı yapı (`Btn`/`Footer`/`Compose`/`Account` helper'ları — 15 şablon aynı iskeleti paylaşıyor), net Türkçe kopya, **ticket maillerine "talebi görüntüle" deep-link**. `NotificationService` payload'una `link` eklendi (`{baseUrl}/tickets/{id}`); base URL `AppOptions` ile worker'dan geçiriliyor. Yalnız payload'da dolu token kullanıldı (render bilinmeyen `{{token}}`'ı literal bırakır).
- [x] **Çalışan DB'ye uygulama:** seed eksik-ekler (UI-edit'leri korumak için mevcut satırı güncellemez); iyileştirilmiş şablonların canlıya inmesi için `EmailTemplates` satırları silinip API restart ile yeniden seed'lendi.

**Karar/Varsayım (Faz 18):**
- *Hesap silme = anonimleştirme, hard delete değil:* mevcut KVKK `User.Anonymize` deseni yeniden kullanıldı (ayrı silme yolu YAGNI); ticket/audit bütünlüğü korunur. Parola onayı zorunlu (yıkıcı + geri alınamaz). Süper admin istisnası KVKK ile tutarlı.
- *Mail şablon helper'ları (Btn/Footer/Compose):* 15 şablon aynı HTML iskeletini paylaşıyor → gerçek 2+ tekrar, DRY seam hak ediyor (SCOPE DISCIPLINE). Tam HTML e-posta framework'ü değil; sadece ortak buton+footer.
- *Ticket deep-link'i `{{link}}` payload'da:* ticket mailleri "bir şey oldu" deyip link vermiyordu (mantıksız); en anlamlı iyileştirme. Base URL `AppOptions.PublicBaseUrl` (account mailleriyle aynı kaynak).
- *Seed insert-if-missing korundu:* şablonlar UI-editable; her boot'ta üzerine yazmak kullanıcı düzenlemesini silerdi. Canlı DB'ye uygulamak için tek seferlik satır-silme+reseed (dev/deploy işlemi, kod değil).

### Faz 19 — Süper admin görünmezliği + Türkçe yetki etiketleri ✅ (2026-08-03)
- [x] **Süper admin sızıntısı kapatıldı (güvenlik, kullanıcı bildirdi):** normal admin/personel süper admini hiçbir yerde görememeli/enumere edememeli.
  - `CompanyService.ListMembersAsync`: member listesine `where !u.IsSuperAdmin` — süper admin bir şirkete üye olsa bile atama/yetki seçicilerinde çıkmaz.
  - `PermissionQueryService.GetEffectiveAsync`: hedef kullanıcı süper admin ise (çağıran değilse) `permission.view_forbidden` — süper adminin yetki seti asla ifşa edilmez. **Aynı hata kodu** kullanıldı ki "yetkin yok" ile "hedef süper admin" ayırt edilemesin (enumeration önlemi).
  - `PermissionAssignmentService.AssignAsync`: hedef süper admin ise (çağıran değilse) `permission.assign.forbidden_target` — süper admine grant/deny denenemez (zaten inert ama adreslenebilir bile olmamalı).
- [x] **Yetki isimleri Türkçeleştirildi:** `comment.internal`/`permission.assign`/`report.company` gibi ham anahtarlar artık UI'da Türkçe. `PermissionLabels` statik haritası (anahtar+grup), `PermissionInfo`'ya `Label`+`GroupLabel` alanı; frontend `Permissions.tsx` etiketi gösterir, ham anahtar `title=` tooltip'te kalır.
- [x] Test: `Super_admin_is_never_listed_as_a_member` (CompanyServiceTests, 7/7 yeşil). Build + frontend tsc temiz.
- [x] **BUG FIX — "Reddet çalışmıyor" (aslında replace patlıyor):** aynı yetkiye ikinci atama (Ver→Reddet toggle) **500** veriyordu. Sebep: `AssignAsync` "replace" için `Remove(existing)+Add` yapıyordu; audit interceptor `Remove`'u soft-delete'e çeviriyor (satır kalıyor), yeni `Add` aynı unique `(UserId,PermissionId,CompanyId)` index'ine çakışıyordu. Fix: **yerinde güncelle** (`UserPermission.SetType` + gerekirse `Restore()`); override config'tir, geçmiş değil (değişiklik zaten AuditLog'ta). Eski bug'ın soft-delete'lediği satırları da iyileştiriyor. Canlıda doğrulandı: grant→deny→grant→deny hepsi 204, effective doğru toggle. +1 test (`PermissionAssignmentServiceTests.Re_assigning_the_same_key_updates_in_place_and_does_not_throw`). 121/121 yeşil.

**Karar/Varsayım (Faz 19):**
- *Süper admin görünmezliği veri katmanında:* frontend zaten super admin'e silme UI'ı gizliyordu ama gerçek güvenlik backend'de (SCOPE DISCIPLINE §core: authz DATA layer'da). Üç sızıntı yolu da (member list, effective-view, assign-target) servis katmanında kapatıldı; tek bir "target is superadmin" reddi deseni.
- *Enumeration önlemi:* GetEffective'te aynı `permission.view_forbidden` kodu — hedefin süper admin olduğunu hata farkından anlamak mümkün olmasın.
- *Türkçe etiketler statik harita, DB değil:* yetki anahtarları sabit geliştirici-metni (UI-editable business config değil), `PermissionKeys.All` gibi. Statik `PermissionLabels` minimal doğru seam (migration/DB kolonu YAGNI). Bilinmeyen anahtar ham anahtara düşer → yeni permission boş render olmaz.

### Faz 20 — StarAdmin görsel kimliği (foundation) ✅ (2026-08-03)
- [x] **Tema token'ları (`index.css`):** StarAdmin paleti — primary `#1F3BB3`, aksan `#5E50F9`, canvas `#F4F5F7`, kart gölgesi, hairline border. Font **Manrope** (Google Fonts, `index.html`).
- [x] **Layout (`Shell.tsx`):** üst-tab → **sol sabit sidebar (240px, açık/beyaz)** + beyaz sticky navbar. Kategori başlıkları (Menü/Yönetim), aktif öğe mavi tint + sol aksan çizgisi. Mobilde off-canvas (hamburger + backdrop).
- [x] **Primitives:** Button (primary/danger gölgeli), Card (`shadow-card`) StarAdmin görünümüne çekildi.
- [x] Canlıda doğrulandı (localhost:8080, ekran görüntüsü): Pano + Yetkiler ekranları yeni kimlikte. Build temiz.

**Karar/Varsayım (Faz 20):**
- *StarAdmin'i kopyalamak yerine yeniden üretmek:* şablon Bootstrap 5 + jQuery + 60 vendor lib; app React 19 + Tailwind v4. Şablonu sürüklemek YAGNI + React'le çatışır. Sadece **görsel token'lar** (renk/font/layout) Tailwind'de yeniden üretildi. Görsel kimliğin %90'ı 3 dosyada (token+shell+primitives) → tüm ekranlar tek diff'le döndü.
- *Açık sidebar:* kullanıcı StarAdmin'in iki resmi varyantından açık olanı seçti.
- *Manrope Google Fonts ile:* self-host yerine `<link>` (mdi zaten bundle'da). İstenirse woff2 self-host'a çevrilir.
- *Kalan (polish, kritik değil):* bazı ekranlar hâlâ `text-slate-*` gibi ham renk kullanıyor (token yerine); çalışıyor ama ekran ekran tokenize edilebilir. Kanban/Dashboard kartları StarAdmin KPI-kart stiline daha da yaklaştırılabilir.

### Faz 21 — StarAdmin tokenizasyon (polish tamam) ✅ (2026-08-04)
Faz 20 foundation'ının bıraktığı "ham renk" borcu kapatıldı: 17 dosyadaki tüm `text-slate-*` / `bg-slate-*` / `border-slate-*` / `bg-white` / `hover:border-blue-300` kullanımları tema token'larına çevrildi (`text-ink`/`text-muted`/`bg-canvas`/`bg-surface`/`border-line`/`border-primary/40`). Artık ekranların hiçbirinde ham Tailwind rengi yok — palet tek yerden (`index.css`) yönetiliyor.
- [x] Kök-neden yaklaşım: shade→token haritası (slate-400/500/600→muted, 700/800→ink, bg-slate-50/100→canvas, border-slate-*→line, bg-white→surface) tek geçişte uygulandı; charts SVG hex serileri (dataviz kategorik renkler) ve semantik durum renkleri (red/amber = hata/uyarı) bilinçli korundu.
- [x] Auth kartları (Login/Register/Invite/Forgot) düz `shadow`→`shadow-card` (StarAdmin yumuşak gölge).
- [x] Dashboard KPI tile'ları + Kanban sütunları zaten StarAdmin stilindeydi (icon-tile + büyük sayı; renkli sütun başlığı + sayaç) — dokunulmadı, YAGNI.
- [x] Doğrulama: `tsc -b` + `vite build` temiz (0 hata); dev server (5173) + API (7084) canlı, health 200 / login 200.

**Karar (Faz 21):** *Ekran-ekran token swap, KPI kart yeniden-yazımı değil.* Görsel kimlik Faz 20'de kurulmuştu; kalan tek eksik ham renklerin token'a çevrilmesiydi. Dashboard/Kanban kartları zaten hedef stildeydi → yeniden yazmak gereksiz diff olurdu (SCOPE DISCIPLINE). Semantik renkler (hata/uyarı/dataviz) paletten ayrı tutuldu — bunlar marka değil anlam taşıyor.

### Faz 22 — Dark tema + sidebar collapse + yorum düzeltmeleri + zengin seed ✅ (2026-08-04)
- [x] **Dark tema:** `.dark` altında aynı token'lar yeniden maplendi (`index.css`) → tek sınıf değişmeden tüm app koyu temaya döner. Navbar'da güneş/ay toggle butonu; `lib/theme.ts` (localStorage + sistem tercihi, ilk boyamadan önce `main.tsx`'te init → FOUC yok).
- [x] **Sidebar collapse:** navbar'da chevron butonu → sidebar 240px ↔ 64px ikon-rayı (desktop), localStorage'da kalıcı; daraltılınca etiketler/başlıklar gizli, ikonlar `title` ile. Mobil off-canvas korundu.
- [x] **Yorum yazarı görünmüyordu (kök-neden):** `CommentDto`'da yazar adı yoktu. `AuthorName` eklendi (Users join, `FirstName+' '+LastName`); UI yorumda yazarı gösteriyor.
- [x] **Yorum saati İstanbul değildi (kök-neden = backend serileştirme):** `datetime2` kolonları `Kind=Unspecified` dönüyor → JSON'da `Z` yok → tarayıcı UTC anı yerel sanıyordu. `UtcDateTimeConverter` (+nullable) global eklendi → tüm tarihler `Z`'li UTC. Frontend `formatDateTime` her tarihi `Europe/Istanbul` olarak render eder (izleyen makinenin TZ'sinden bağımsız).
- [x] **Audit interceptor latent bug:** `CreatedAt` her Added'da koşulsuz `now`'a eziliyordu → seed'in trend için tarih yayması **no-op**'tu. `if (CreatedAt == default)` ile önceden set edilen tarih korunur (normal akışta domain saati ellemez → hep default → davranış aynı).
- [x] **Zengin demo seed (tech debt #29 kapandı):** iç notlar (staff-only, amber), gerçek ekler (byte'lar `IFileStorage.PutAsync` ile depolanır → indirme round-trip'i çalışır), özel public-form alanları (tekstil: Sipariş No/Ürün Kategorisi/Ek açıklama) + eşleşen `CustomFieldsJson` taşıyan ticket, gerçekçi/yayılmış zaman damgaları (ticket + yorum kronolojisi). Canlı doğrulandı: iç not spread'i, TEKSTIL-1 custom fields + ek indirme (57B, içerik eşleşti).
- [x] **Doğrulama:** 144 test yeşil (16+121+7), backend build + frontend `tsc -b` temiz. DB drop→migrate→enriched seed; superadmin `ChangeMe!2026Dev`, demo admin `admin@tekstil.local`/`Demo!2026Pass`.

**Karar (Faz 22):** *Timezone kök-nedeni backend'de düzeltildi, sadece UI'da maskelenmedi.* Kullanıcı "UI'da halletmek doğru mu" diye sordu — hayır: `Z`'siz UTC her tüketiciyi yanıltır (mail, export, API). Converter tek yerde tüm sistemi düzeltir; UI ayrıca İstanbul'a sabitler (Türk CRM'i tek saat okur). Seed'e byte'lı ek koymak "dangling row" yerine gerçek indirme testini mümkün kıldı.

### Faz 23 — Müşteri giriş sayfası (kod ile doğrulama) + fırsat havuzu UI (Odoo) ✅ (2026-08-04)
Kullanıcı canlı Odoo CRM'i (`/odoo/crm`, "Fırsat Havuzu") referans göstererek eksikleri kapatmayı istedi. Odoo'dan alınanlar: kanban sütun başlığı (renk şeridi + sayaç + sütun içi hızlı ekleme), `o_kanban_counter_progress` benzeri oran çubuğu, `o_opportunity_kanban` kart düzeni (başlık → no → yıldız öncelik + atanan avatarı), üstte "Yeni" butonu.
- [x] **Şirkete özel müşteri giriş sayfası `/c/{slug}`:** kayıt → e-postaya **6 haneli kod** → kodu girme → oturum → ilk talep (şirketin özel form alanlarıyla). Firma markası/KVKK metni mevcut public form config'inden okunur. Dönen müşteri için aynı sayfada "giriş yap".
- [x] **Kod akışı backend (test-first, çekirdek):** `POST /api/public/form/{slug}/register` (nötr 204, enumeration yok) + `POST .../verify` (AuthResult döner). Kod `Invitation` tablosunda `Kind=Code` olarak, **kullanıcı id'siyle tuzlanmış** hash ile saklanır; 15 dk ömür, tek kullanım, yeni kod eskisini iptal eder, **5 hatalı denemede kilitlenir**. `AcceptInviteAsync` artık yalnız `Kind=Link` kabul ediyor — kısa kodun link akışından brute-force edilmesi yapısal olarak imkânsız. Migration: `CustomerSignInCodes` (Invitations.Kind + Attempts; mevcut satırlar Link).
- [x] **İlk temas = ilişki:** `POST /api/public/form/{slug}/ticket` (**[Authorize]**) — doğrulanmış müşteri firmanın kendi linkinden talebini açar, ticket **doğrudan havuza** düşer (moderasyon yok; adres zaten kod ile kanıtlandı) ve Faz 17 ilişki-scope'u böylece kurulur (portal sonrasında devralır).
- [x] **Güvenlik düzeltmesi (build uyarısı ASP0026):** `PublicFormController`'da sınıf seviyesindeki `[AllowAnonymous]` aksiyon seviyesindeki `[Authorize]`'u eziyordu → müşteri-ticket ucu anonim kalacaktı. `[AllowAnonymous]` aksiyonlara indirildi.
- [x] **"Müşteri linki oluştur" (kopyala / WhatsApp / e-posta):** `ui/CustomerLink.tsx` — link `window.location.origin`'den üretilir (dev/docker/prod'da ayrı config yok). Kanban üstünde ve `/admin/companies` şirket kartında.
- [x] **Personel talebi açabiliyor:** kanban'da "Yeni" + her sütunda "+" (Odoo hızlı ekleme). Backend ticket'ı hep havuz sütununda açtığı için başka sütuna eklenen kart oluşturma sonrası mevcut status ucuyla taşınır.
- [x] **Odoo görünümü:** sütun renk şeridi + sayaç + öncelik oran çubuğu; kartta yıldız öncelik + atanan avatarı (baş harfler); başlık "Fırsat havuzu".
- [x] **Yeni mail şablonu `account_code`** (büyük punto kod + geçerlilik süresi). Seed insert-if-missing → restart ile canlıya indi.
- [x] **Testler (+10, toplam 154: 16 domain + 131 application + 7 API):** kod akışı 8 test (inaktif kalma + 6 hane, doğrulama+oturum, replay reddi, deneme kilidi, süre dolması, yeni kod eskisini iptal, **kodun link akışında geçmemesi**, arşivli firma) + müşteri-ticket 2 test (havuza direkt + anonim reddi).
- [x] **Canlı doğrulama (7084 + 5173, hem API hem tarayıcı):** kayıt→kod (log'dan)→doğrula→talep = TEKSTIL-11 (curl) ve TEKSTIL-12 (UI, özel alanlar dahil); `my-companies` boşken ilk talepten sonra "Anadolu Tekstil" döndü; personel panosunda ikisi de **Yeni** sütununda; sütun içi hızlı ekleme TEKSTIL-13'ü "İşlemde" sütununda oluşturdu; müşteri linki paneli (kopyala/WhatsApp/e-posta) doğrulandı.

**Karar/Varsayım (Faz 23):**
- *Kod (6 hane) mevcut `Invitation` tablosunda, ayrı entity değil:* ikinci bir tek-kullanım-token mekanizması over-engineering olurdu. Ama iki tür **asla** birbirinin yerine geçmemeli → `Kind` ayrımı + kod'un **kullanıcıya göre** aranması (token'a göre değil) + deneme sayacı. Kod tuzlanarak hash'lenir: 6 haneli bir SHA-256, çalınan DB satırından anında geri çevrilirdi.
- *Kod niye link değil:* kullanıcı isteği ("mailden onay kodunu logine girmesi lazım"). Link akışı (public form / personel daveti / parola sıfırlama) aynen duruyor — iki kanal bir arada.
- *Kayıtta parola alınıyor, kod yalnız adresi kanıtlıyor:* zaten aktif bir hesap kaydolmayı denerse parolası **değiştirilmez** (bu bir sıfırlama yolu değil); kod o durumda tek seferlik giriş gibi çalışır. Enumeration yok (her durumda 204).
- *Doğrulanmış müşterinin talebi moderasyona düşmüyor:* zero-trust `Pending` tutması anonim form içindi (kimlik yok). Kod doğrulaması güveni kuruyor → Odoo'daki gibi doğrudan fırsat havuzuna. Anonim form davranışı değişmedi.
- *İlişki hâlâ "ticket var mı" (ayrı `CustomerCompany` tablosu yok):* `/c/{slug}` ilk talebi ilişkiyi kuruyor; Faz 17 kararı bozulmadı. Yeni tablo ancak "talep açmadan ilişki" gerekirse gelir.
- *Link `window.location.origin`'den:* `App:PublicBaseUrl` mail içindir (sunucu tarafı); tarayıcıdaki panel zaten doğru origin'i biliyor → ikinci bir config kaynağı yaratılmadı.
- *Kartta yıldız (0-3) 4 öncelik seviyesini gösteriyor:* Odoo'nun yıldız satırı; Düşük=boş, Acil=3 dolu. Renk kodu badge yerine yıldız + oran çubuğunda taşınıyor.
- *Hızlı ekleme "oluştur + taşı" iki çağrı:* backend'in başlangıç statüsü kuralı korunuyor (state machine tek doğruluk kaynağı); sütuna özel oluşturma için API'ye yeni parametre eklenmedi.
- *Global `/register` ekranı duruyor ama artık ikincil:* firma linki olmadan kaydolan müşterinin ilişkisi olmaz → portalde yazacak firma göremez. Kaldırmak/yönlendirmek ayrı bir karar (kullanıcıya bırakıldı, teknik borç #30).
- *Gerçek mail:* prod/docker `.env` zaten Resend SMTP (`EMAIL_PROVIDER=smtp`, canlı doğrulanmış). Lokal `dotnet run` `Email:Provider=log` kullanıyor (kod konsola düşer). Lokalde gerçek gönderim için servis env ile çalıştırılmalı: `Email__Provider=smtp Email__Host=smtp.resend.com Email__Port=587 Email__UseSsl=true Email__Username=resend Email__Password=<re_...> Email__From=onboarding@resend.dev dotnet run`. Resend doğrulanmış domain gelene kadar yalnız hesap sahibinin adresine teslim eder.

### Faz 24 — Kullanıcı yönetimi ekranı + şablon editörü ✅ (2026-08-04)
- [x] **`/admin/users` yeniden düzenlendi:** sunucu taraflı arama (`GET /users?search=` — liste 100 satırla sınırlı, client-side filtre eşleşme gizlerdi), tür (Süper Admin/Admin/Personel/Müşteri) ve durum (Aktif/Davet bekliyor) filtreleri, gruplar **şirketler + Müşteriler + Şirketi olmayan yöneticiler** olarak ayrıldı (eskiden hepsi tek "şirkete bağlı olmayan" torbasındaydı), satırda avatar + tür rozeti + durum, tablo kendi `overflow-x-auto` kutusunda.
- [x] **Müşterinin ilişkili firmaları listede:** `UserDto.CustomerOf` — müşterilerin üyeliği yok, ilişki ticket'tan türetiliyor (Faz 17 tanımı). Sadece üyeliği olmayan kullanıcılar için tek ek sorgu. +1 test.
- [x] **KVKK "Hesabı sil" aksiyonu UI'a bağlandı:** `POST /api/kvkk/anonymize/{userId}` (süper admin) — backend vardı, ekran yoktu. Satır içi iki adımlı onay (tarayıcı `confirm()` yerine; hem daha okunur hem otomasyonu kilitlemez). Süper admin ve kullanıcının kendisi hedeflenemez (backend zaten reddediyor, UI de göstermiyor).
- [x] **Admin daveti sonucu insanlaşmıştı:** ham token yerine "davet e-postası gönderildi" + **hazır `/invite?token=...` bağlantısı + kopyala** (mail dönerse/telefonla onboarding'de gerekiyor).
- [x] **`/admin/templates` iyileştirildi:** şablon listesi **Talep bildirimleri / Hesap e-postaları** olarak gruplandı; **canlı önizleme** (örnek verilerle doldurulmuş konu + `sandbox=""` iframe'de gerçek HTML); **şablona özel yer tutucu çipleri** imleç konumuna ekliyor (eskiden tüm şablonlar için aynı jenerik liste vardı — yanlış token sessizce literal gidiyordu); eksik `account_code` etiketi eklendi; son güncelleme tarihi gösteriliyor.
- [x] **Doğrulama:** 155 test yeşil (16 + 132 + 7), frontend build temiz. Tarayıcıda: arama "mehmet" → 2 hesap, müşteri satırlarında "Anadolu Tekstil"/"Ege Mermer" ilişkisi; test hesabında KVKK silme → satır maskelenip listeden düştü; şablon önizlemesi doğrulama kodu mailini gerçek görünümüyle render etti.

**Karar/Varsayım (Faz 24):**
- *Arama sunucuda, filtreler client'ta:* arama backend'de zaten vardı ve liste `Take(100)` — client-side arama eşleşmeleri gizlerdi. Tür/durum filtresi ise gelen kümeyi daraltıyor, ek uç gerekmiyor.
- *`CustomerOf` DTO alanı, yeni tablo değil:* Faz 17'nin "ilişki = ticket" tanımı korunuyor; liste ekranı için tek `Distinct` sorgu yeterli. Yalnız üyeliksiz kullanıcılar için çalışır (personel zaten şirketiyle gruplu).
- *KVKK silme = mevcut anonimleştirme ucu:* yeni bir "kullanıcı sil" yolu açılmadı; ticket/audit bütünlüğü korunur (§16).
- *Yer tutucular şablon başına:* `NotificationService`/`InviteEmail` payload'larıyla birebir. Jenerik liste yanıltıcıydı — payload'da olmayan token e-postada `{{...}}` olarak görünür.
- *Önizleme `sandbox=""` iframe:* şablon gövdesi süper adminin yazdığı HTML; script çalıştırmasın ve sayfanın CSS'ini etkilemesin diye izole edildi.

### Faz 25 — Bildirim kapsamı + alıcıya göre mail dili + Brevo hazırlığı ✅ (2026-08-04)
Kullanıcı bildirimi: bir talepte değişiklik olduğunda hem (varsa) müşteriye hem atanan personele mail gitmeli; müşteri "talebiniz işleme alınmıştır", personel ise "X nolu kaydınızda güncelleme var" dilinde okumalı.
- [x] **Aynı olay, iki dil:** `MatrixEntry.StaffTemplateKey` eklendi; `NotificationService` alıcı ticket'ın şirketinde **üye mi** diye bakıp şablonu seçiyor (üye → personel dili, değil → müşteri dili). Tek yeni şablon `ticket_staff_update` ("{{ticketNumber}} — {{change}}"), 10 ayrı personel şablonu yerine (kullanıcının örneği de tek jenerik cümleydi). `{{change}}` Türkçe olay metni (`durum güncellendi: İşlemde`, `yeni yorum eklendi`, `öncelik değişti: Yüksek`…).
- [x] **Kapsam genişledi:** `Approved`/`Rejected` artık atanana da gidiyor; `PriorityChanged`/`CategoryChanged` (eskiden kimseye) atanana gidiyor. Statü/yorum/dosya/düzenleme/yeniden-açma zaten açan+atanan idi.
- [x] **Müşteri statü metni** kullanıcının örneğine göre yazıldı: "**{{ticketNumber}}** numaralı **{{title}}** başlıklı talebiniz **{{newValue}}** durumuna alınmıştır" (konu: "{{ticketNumber}} numaralı talebiniz: {{newValue}}").
- [x] **Korunan invaryantlar:** iç not müşteriye ASLA gitmiyor (matris + hard-exclude), kimse kendi işleminin mailini almıyor, `Created` makbuzu açana gidiyor. +4 test (toplam 159: 16 + 136 + 7).
- [x] **Brevo hazırlığı:** `SmtpEmailSender` zaten host/port/STARTTLS/kullanıcı/parola destekliyordu → kod değişikliği gerekmedi; tek eklenen **`Email__FromName`** (görünen gönderici adı). `.env.example` Brevo talimatlarıyla yeniden yazıldı (SMTP login ≠ hesap e-postası, `xsmtpsib-` SMTP key, 587/STARTTLS, doğrulanmış gönderici).
- [x] **Canlı doğrulama (docker 8080):** TEKSTIL-14 admin tarafından atandı + statü değişti → kuyrukta `ticket_assigned` (atanan personele), `ticket_staff_update` (atanan personele, statü değişimi) ve `ticket_status_changed` (talebi açan müşteriye, gerçek adrese **gönderildi**). Demo `.local` adresleri beklendiği gibi Failed.

**Mail sağlayıcı — Brevo (2026-08-04, canlı):** `.env` Brevo SMTP'ye geçirildi (`smtp-relay.brevo.com:587`, STARTTLS, SMTP login `b456b4001@smtp-brevo.com`, SMTP key `.env`'de — repoya girmez; panoyla dosyaya aktarıldı, sohbete/loga yazılmadı). Gönderici: Brevo'da **doğrulanmış** `fiskirmacoop <ufukf1998@gmail.com>`; `EMAIL_FROM_NAME=CRM Kanban`. Doğrulama: `forgot-password` → EmailQueue `Sent` + Brevo Logs **Sent → Delivered → First opening**. Resend'in aksine Brevo serbest alıcıya gönderiyor (sandbox kısıtı yok), yani müşteri akışı gerçek adreslerle çalışır.
- **Açık (teknik borç #33):** gönderici `gmail.com` olduğu için Brevo From'u `ufukf1998@11818676.brevosend.com` olarak yeniden yazıyor ve Google/Yahoo gönderici kuralları uyarısı duruyor. Kendi domainin Brevo > Domains'te DKIM/DMARC ile doğrulanıp `EMAIL_FROM=destek@<domain>` yapılmalı — spam'e düşmemek için üretim öncesi şart. *(Faz 33: yeniden yazmanın asıl zararı ölçüldü ve büyük kısmı `Email__ReplyTo` ile giderildi; domain doğrulaması hâlâ kalıcı çözüm.)*

**Karar/Varsayım (Faz 25):**
- *Personel için tek jenerik şablon, olay başına ayrı şablon değil:* kullanıcının tarifi ("xyz nolu işleminizde güncelleme olmuştur göz atınız") zaten tek cümle; 10 şablonun ikinci bir kopyası bakım yüküydü. Ne değiştiği `{{change}}` ile taşınıyor. Olay bazında özel personel metni gerekirse `StaffTemplateKey` alanına yeni anahtar yazmak yeterli (seam hazır).
- *"Personel mi" testi = ticket'ın şirketinde membership:* müşterinin üyeliği yok (Faz 17). Böylece **personelin açtığı** ticket'ta açan da personel dilinde mail alıyor — kullanıcının ikinci senaryosu.
- *Öncelik/kategori müşteriye gitmiyor:* iç triyaj bilgisi; müşteriye göndermek gürültü ve iç işleyiş sızıntısı. Statü/yorum/dosya/düzenleme müşteriye gidiyor.
- *`Unassigned` hâlâ kimseye:* alıcı "eski atanan" olurdu, olay onu `OldValue`'da taşıyor ama matriste karşılık gelen slot yok. Gerekirse slot eklenir (borç).
- *Brevo'ya geçiş kod değil config:* sağlayıcı `Email:Provider=smtp` + host/kullanıcı/parola ile değişiyor; Resend→Brevo geçişi yalnız `.env`. Sağlayıcıya özel kod yazılmadı (ponytail).

### Faz 26 — Müşteri görsel yükleme + AWS S3 ✅ (2026-08-04)
İstenen: "müşteriler açtıkları ticket'lara yorum yazabilecek, görsel yükleyebilecek, görseller S3 gibi bulutta saklanacak."
- [x] **Yorum:** müşteri kendi ticket'ına zaten yorum yazabiliyordu (Faz 9'daki tenant-filtre düzeltmesi) — dokunulmadı.
- [x] **Görsel yükleme açıldı:** zero-trust `PublicFileValidator`'a **png / jpg / jpeg / webp** eklendi (uzantı + MIME + **magic byte** üçlüsü: PNG imzası, JPEG SOI, RIFF….WEBP). Kimliği doğrulanmış yol (`FileOptions.AllowedContentTypes`) görselleri zaten kabul ediyordu; asıl kapalı olan anonim/ilk-temas yoluydu.
- [x] **SVG bilerek DIŞARIDA:** kullanıcıya "resim" görünür ama tarayıcıda script çalıştırır (XSS). Test bunu sabitliyor.
- [x] **UI:** ticket detayında görseller artık **küçük resim olarak gösteriliyor** (tıklayınca tam boyut); belgeler indirme linki olarak kalıyor. Görseller özel bucket'ta olduğu için `<img src>` doğrudan uca bakamıyor → blob olarak yetkili istemciyle çekilip object URL veriliyor. Dosya seçicilere `accept` listesi ve "hangi tipler" ipucu eklendi (public form + ticket detay).
- [x] **Testler (+4, toplam 164):** gerçek png/jpg/jpeg/webp kabul, `.png` adlı **PE çalıştırılabilir** reddi (`content_mismatch`), `.svg` reddi (`type_not_allowed`).
- [x] **Canlı doğrulama (docker 8080):** gerçek PNG yüklendi → depoya yazıldı → indirildi, **SHA-256 eşleşti** (165 bayt); ticket detayında küçük resim render edildi.
- [x] **Compose S3 parametreleştirildi:** `S3__ServiceUrl/Region/ForcePathStyle` artık env'den (`${S3_SERVICE_URL-http://minio:9000}` — `-` kullanıldı ki **bilerek boş** değer AWS'i işaret etsin). MinIO varsayılanı bozulmadı.
- [x] **AWS S3 kuruldu (kullanıcı hesabında, tarayıcıdan):** `crm-kanban-fiskirmacoop` bucket'ı — **eu-north-1 (Stockholm)**, tüm public erişim kapalı, SSE-S3 şifreleme, ACL'ler devre dışı. IAM kullanıcısı **`crm-kanban-app`** (konsol erişimi yok, yalnız programatik) + satır içi politika **`crm-kanban-bucket-access`**: yalnız bu bucket'ta `GetObject`/`PutObject`/`DeleteObject` (+ bucket'ın kendisinde `ListBucket`). Politika denetleyicisi: 0 hata / 0 uyarı / 0 güvenlik bulgusu.
- [x] **Anahtarlar `.env`'e panodan aktarıldı** (sohbete/loga hiç yazılmadı); `S3_SERVICE_URL=` boş → AWS bölge uç noktası, `S3_REGION=eu-north-1`, `S3_FORCE_PATH_STYLE=false`, `S3_BUCKET=crm-kanban-fiskirmacoop`.
- [x] **AWS üzerinde canlı doğrulama:** gerçek PNG yüklendi → S3'e yazıldı → indirildi, **165 bayt, SHA-256 eşleşti**, PNG imzası doğru.

**Karar/Varsayım (Faz 26):**
- *Görseller ticket-seviyesi ek olarak kalıyor* (Faz 12 kararı): yorum-eki ayrı bir yol; müşteri "talebime fotoğraf ekleyeyim" ihtiyacı ticket-seviyesiyle karşılanıyor.
- *Küçük resim blob ile:* bucket private, presigned URL bu topolojide tarayıcıdan erişilemez (Faz 12 kök-nedeni) → API-proxy indirme + object URL. ponytail: object URL sekme kapanana dek yaşıyor; ticket başına birkaç görselde sorun değil.
- *gif kabul edilmedi (authenticated listede var, public'te yok):* animasyonlu içerik moderasyon yüzeyi; gerekirse tek satır.
- *Depolama sağlayıcısı hâlâ config:* `IFileStorage` seam'i sayesinde AWS S3 / R2 / B2 / MinIO farkı yalnız env. R2 (10GB kalıcı ücretsiz, egress yok) AWS kredisi bitince alternatif.
- *Bölge Stockholm (eu-north-1):* kullanıcı o bölgede sekme açmıştı; Frankfurt'a göre biraz ucuz, TR'ye gecikme farkı ihmal edilebilir.
- *IAM kullanıcısı + statik anahtar (rol değil):* uygulama kullanıcının makinesinde docker'da çalışıyor, instance profile yok → statik anahtar tek seçenek. Politika tek bucket'a kilitli, konsol erişimi kapalı.
- *Compose'daki MinIO servisi duruyor:* lokal/offline geliştirme için hâlâ kullanışlı (env'i geri çevirmek yeterli). Kaldırmak istenirse tek blok siliniyor.
- *`docker compose config` ile `S3__ServiceUrl: ""` doğrulandı:* `${VAR-default}` (iki nokta yok) sayesinde bilerek boş bırakılan değer default'a düşmüyor — AWS'e geçişin çalışma şartı buydu.

### Faz 27 — Şirket silme (çift onay) + çoklu şirket ✅ (2026-08-04)
İstenen: "şirket silme özelliği olması lazım ve bunu bir iki onay ile alması lazım; bir admin birden fazla şirket açabilir."
- [x] **Silme (`DELETE /api/companies/{id}`, gövdede `confirmName`):** iki kapı — (1) UI'da "Sil" → onay paneli, (2) şirket adının **birebir yazılması**, sunucuda yeniden doğrulanıyor (`company.delete_name_mismatch`, 400). Yetki: **yalnız sahibi olan admin** veya süper admin (`company.delete_forbidden`); ikinci bir şirket admini üye yönetebilir ama tenant'ı silemez.
- [x] **Silme yumuşak (spec §18.20 korundu):** satır `DeletedAt` ile işaretleniyor, talepler/audit veritabanında duruyor, cascade yok. Aynı anda `Company.Retire()` şirketi **arşivliyor** (public form + müşteri girişi `IsArchived/IsActive`'e bakıyor → tek hamlede kapanıyor) ve **slug'ı serbest bırakıyor** (`acme` → `acme-deleted-1a2b3c4d`), çünkü slug'ın unique index'i global; aksi halde aynı slug bir daha hiç kullanılamazdı.
- [x] **Üyelikler de siliniyor** (soft): tenant herkesin oturum kapsamından düşüyor.
- [x] **Çoklu şirket:** backend'de zaten sınır yoktu (her şirket kendi Admin üyeliğini alıyor) — eksik olan **istemci tarafıydı**: ekranlar `user.companies[0]`'a sabitliydi. `lib/company.ts` (`useActiveCompany`/`setActiveCompany`, localStorage) + navbar'da **şirket seçici** (yalnız 1'den fazla şirketi olana görünür); Kanban / Onay kutusu / Pano artık aktif şirketi okuyor.
- [x] **Oturum tazeleme:** tenant kapsamı JWT'nin `company_id` claim'lerinden geliyor → yeni açılan (veya silinen) şirket token yenilenene kadar (~15 dk) görünmüyordu. `refreshSession()` şirket oluşturma/silme sonrası token'ı yeniden mintliyor.
- [x] **Testler (+5, toplam 169):** ad eşleşmezse silinmiyor; sahibi olmayan admin 403; silinen şirket listeden düşüyor + satır duruyor + arşivleniyor + slug tekrar kullanılabiliyor; bir admin iki şirket açıp ikisinin de Admin'i oluyor; HTTP seviyesinde gövdeli DELETE + yanlış ad → 400 zarfı → doğru ad → 204.
- [x] **Canlı doğrulama (docker stack, localhost:8080):** API — yanlış ad 400, boş ad `validation.failed`, doğru ad 204, silinen şirket listeden düştü, `/public/form/{slug}` 404'e döndü, aynı slug ile yeniden şirket açıldı; sahibi olmayan admin **ve şirketin ikinci admini** 403. UI (Chrome) — Ali adlı gerçek admin 3 şirket açtı, navbar şirket seçicisi anında güncellendi (token tazeleme çalışıyor) ve seçim sayfalar arası korundu; onay panelinde yanlış ad yazılıyken "Kalıcı olarak sil" pasif kaldı ve **sunucuya hiç istek gitmedi**, doğru adla 204 + kart ve seçici girdisi kalktı. DB: silinen satırlar `DeletedAt` + `ArchivedAt` dolu, slug `ali-dort-deleted-fb32d202` biçiminde. Test verileri temizlendi (Ali/Veli hesapları duruyor).

**Karar/Varsayım (Faz 27):**
- *Silme = soft + arşiv, hard delete değil.* Spec §18.20 "şirketler silinmez, arşivlenir" diyor; kullanıcı silme istedi. Uzlaşma: **kullanıcı için silme** (her listeden kalkar, link kapanır, üyelikler biter), **veri için arşiv** (talepler/audit/KVKK zinciri kırılmaz). Hard delete cascade ile ticket geçmişini ve audit'i yok ederdi — geri dönüşü olmayan tek yol bu olurdu.
- *Slug'ın yeniden adlandırılması* (`-deleted-<id8>`): `HasIndex(Slug).IsUnique()` filtresiz global bir index; soft-delete edilmiş satır slug'ı tutmaya devam ederdi ve aynı ad bir daha açılamazdı. Alternatif (filtreli unique index) migration gerektirirdi — tek satırlık domain metodu daha ucuz.
- *Silme yetkisi "şirket admini" değil "sahibi olan admin":* üye çıkarma rutin yönetim işi, tenant silmek değil. Süper admin her zaman yapabilir.
- *İkinci onay sunucuda da doğrulanıyor:* yalnız UI'da olsaydı düz bir `DELETE` isteği tek kapıyı geçerdi. Onay metni = şirket adı (trim + büyük/küçük harf duyarsız).
- *Aktif şirket localStorage'da:* görünüm tercihi, yetki değil — her istek yine token'ın claim'leriyle sunucuda kapsamlanıyor. Üyeliği kalmayan bir id saklıysa ilk üyeliğe düşülüyor (silinen şirket seçili kalmasın).
- *Columns/FormFields ekranlarının kendi şirket seçicisi duruyor:* zaten çalışıyorlar, kapsam dışı; ileride ortak seçiciye bağlanabilir (teknik borç).
- *Silinen şirketi geri getirme (undelete) yok:* istenmedi. DB'de `DeletedAt`/slug geri alınarak elle mümkün.

### Faz 28 — Manuel test turu: iki kiracı-izolasyonu açığı bulundu ve kapatıldı ✅ (2026-08-05)
İstenen: "projeyi ayağa kaldır, gerekli manuel testleri yapalım" + ardından `CRM_Kanban_Gereksinim_Listesi.md` kontrolü.
Stack `up.ps1` ile ayağa kalktı (8080). Manuel tur **çalışan sistemde iki güvenlik bug'ı ortaya çıkardı**; ikisi de 169 testin hiçbirini kırmıyordu.

- [x] **Bug 1 — çapraz-kiracı sızıntısı (kritik).** `GET /api/tickets` her personele **tüm firmaların** taleplerini döndürüyordu (mermer personeli 22 talebin hepsini gördü: MERMER + TEKSTIL + MAHMUT-INSAAT); `GET /tickets/moderation/{companyId}` de yabancı firmanın kuyruğunu veriyordu (mermer personeli TEKSTIL-10/11/15/17'yi okudu). **Kök neden:** `PaginateAsync`/`ModerationQueueAsync` statü meta verisi için `db.TicketStatuses.IgnoreQueryFilters()` ile join yapıyordu; EF'te `IgnoreQueryFilters()` **sorgu geneline** uygulanır → ticket'ların kiracı filtresi de kapanıyordu. Kanban ve ticket detayı etkilenmiyordu (birinde böyle bir join yok, diğerinde `authz` geçidi var — canlıda 0 kayıt ve 403 ile doğrulandı). **Düzeltme:** join kaldırıldı, statüler kendi sorgusuyla okunuyor (`LoadStatusesAsync`), kategori filtresi bellekte statü id kümesine çevriliyor. Kiracıyı neyin sınırladığı yine tek yerde: query filter.
- [x] **Bug 2 — iptal edilen üyelik yetki vermeye devam ediyordu.** `AuthService.GetMeAsync` + token üretimi `Memberships.IgnoreQueryFilters()` sorgusunu `DeletedAt == null` olmadan çalıştırıyordu. Üyelikler yalnızca **soft** silindiği için (üye çıkarma ve şirket silme, ikisi de) şirketten çıkarılan personel **yeni girişte tam personel kapsamıyla** dönüyordu (canlı: `uretim@tekstil.local` çıkarıldıktan sonra `/me` firmayı listeliyor, kanban 200); silinen şirketler de süper adminin oturumunda duruyordu (4 üyeliğin 3'ü silinmiş şirketlere aitti) — bu da Faz 27'nin `refreshSession()` mantığını boşa çıkarıyordu. **Düzeltme:** tek ortak sorgu `MembershipQueries.ActiveMemberships()`; üyeliği yetki olarak okuyan **8 çağrı noktası** buna geçirildi (token, `/me`, `PermissionService`, `TicketAuthorizationService`, ticket oluşturma + atama, şirket üye listesi/`IsAdminOf`, form alanları, bildirim fan-out).
- [x] **Yan etki düzeltmesi (zorunluydu):** `(UserId, CompanyId)` tekil indeksli ve soft-delete edilmiş satır o indeksi tutmaya devam ediyor → çıkarılan personel **yeniden davet edilemiyordu** (`invite.already_member`, ve `DeletedAt` filtresi eklenince ikinci satır unique-index ihlali verirdi). `InvitationService` artık eski satırı diriltiyor (`Restore()` + rol güncelleme), yeni satır eklemiyor. Bu, "üyelik iptali gerçekten iptal" düzeltmesinin çalışabilmesi için şart — SCOPE DISCIPLINE'ın "istenen değişiklik bitişik düzeltme olmadan yanlış" istisnası.
- [x] **Testler (+10, toplam 179):** `TicketTenantScopeTests` (5) — personel yabancı kiracıyı listeleyemez, yabancı `companyId` ile moderasyona ulaşamaz, kendi özel statü sütunu yine görünür, müşteri firmalar arası kendi taleplerini statü adlarıyla görür, kategori filtresi kiracı içinde kalır. `MembershipRevocationTests` (3) — iptal edilen üyelik token kapsamına/`/me`'ye girmez, o şirketin ticket'ında personel sayılmaz. `InvitationServiceTests` (+2) — yeniden davet satırı diriltir, aktif üyeyi yine reddeder.
- [x] **Canlı doğrulama (yeniden build + 8080):** mermer personeli `/tickets` → yalnız 9 MERMER; yabancı moderasyon → boş, kendi moderasyonu → MERMER-10; kategori filtresi → yalnız MERMER; süper admin 25 talebin hepsini görüyor. Üyelik döngüsü uçtan uca: çıkar → yeni giriş `/me` 0 firma + kanban **403** + liste 0 → yeniden davet → `/me` 1 firma + kanban 200 + 15 TEKSTIL talebi. Demo verisi eski hâline döndürüldü.
- [x] **Aynı turda doğrulanan (regresyon yok):** login (4 rol), public form → Pending → moderasyon → onay → kanban, `/c/{slug}` kod akışı (Brevo gerçekten mail gönderdi, kuyrukta `Sent`), müşteri kendi liste kapsamı, ticket oluştur/yorum/detay, **başlık-gövde düzenleme + yorum düzenleme/silme**, AWS S3 ek yükleme→indirme bayt round-trip, şirket + global rapor + CSV, global rapor firma adminine 403, Faz 27 silme akışının tamamı (yanlış ad 400 → doğru ad 204 → listeden düşme → form 404 → slug yeniden kullanılabilir).
- [x] **`CRM_Kanban_Gereksinim_Listesi.md` kontrol edildi ve kanıtlarıyla işaretlendi.** 25 maddenin 25'i karşılanıyor; 4'ü ⚠️ notlu (konfigürasyon dosya/DB ayrımı, bildirim matrisinde öncelik/kategori/silme kasıtlı hariç, render optimizasyonu ölçülmedi, MediatR kullanılmadı — gereksinimde "kullanılabilir"). Bulunan iki bug listedeki §4-3 ("admin yalnız kendi şirketinin ticket'larını görebilmeli") ve §2-5 ("yetki yönetimi arayüzden") maddelerini doğrudan çürütüyordu; artık ikisi de gerçekten sağlanıyor.

**Karar/Varsayım (Faz 28):**
- *Join yerine ikinci sorgu:* Alternatif, ticket sorgusuna kiracı koşulunu **elle** yazmaktı (`isSuper || scope.Contains(t.CompanyId)`). Reddedildi: izolasyonu uygulama koduna kopyalamak CLAUDE.md'nin "least-privilege DATA katmanında" kuralını tersine çevirir ve aynı hatayı her yeni sorguda tekrar davet ederdi. Statü tablosu küçük; iki sorgunun bedeli, filtrenin tek otorite kalmasının yanında önemsiz.
- *Neden `IgnoreQueryFilters` statüde gerekliydi:* müşterinin hiç şirket kapsamı yok, firmaya özel statü satırı ona filtrelenirdi ve join'de eşleşme bulunmayınca **kendi talepleri listeden düşerdi**. Yani orijinal niyet doğruydu, aracı yanlıştı — bu yüzden testlerden biri özellikle "firmanın kendi sütunu yine görünüyor" diyor, düzeltmenin bu ihtiyacı geri kırmadığını kilitlemek için.
- *Tek `ActiveMemberships()` seam'i (soyutlama eşiği):* CLAUDE.md soyutlama için "ikinci gerçek durum ya da spec'te adı geçen değişim ekseni" istiyor. Burada **sekiz** gerçek çağrı noktası var ve her biri aynı güvenlik koşulunu elle tekrarlamak zorundaydı; ikisi unuttuğu için açık doğdu. Tek yordam bu unutmayı imkânsız kılıyor.
- *`InvitationService` bilinçli olarak soft-delete'i GÖRÜYOR:* orası yetki okuması değil, **tekillik** kontrolü; silinmiş satırı görmezse unique index'e çarpar. Bu ayrım kodda yorumla işaretlendi — yoksa bir sonraki okuyan "tutarlılık" adına onu da `ActiveMemberships()`'e çevirir ve insert patlar.
- *Testler servis seviyesinde, DbContext seviyesinde değil:* mevcut `TenantIsolationTests` filtre predicate'ini doğruluyordu ve **yeşildi** — bug filtrenin kendisinde değil, servisin onu kapatmasındaydı. Bu yüzden yeni testler servisin gerçekten filtre altında koştuğunu kanıtlıyor.
- *Süper adminin kendi şirketi kalmadı:* `/me` artık 0 firma dönüyor çünkü üyeliklerinin hepsi silinmiş şirketlere aitti (biri, "mahmut insaat", bu oturumda 06:53'te `admin@example.com` ile silinmiş — benim çağrılarımdan biri değil, tarayıcıdan yapılmış olmalı). Süper admin zaten kapsamdan bağımsız her şeyi görüyor; veri kaybı yok.

### Faz 29 — Doküman denetimi: md dosyalarının kodla senkronu ✅ (2026-08-05)
İstenen: "bütün md dosyalarını kontrol et ve gerekli eksikleri belirle, faz oluştur ve çöz."
Denetlenen 6 dosya (vendor `staradminuifiles/**/LICENCE.md` hariç): `crm-kanban-mimari.md` (spec),
`PROGRESS.md`, `ONERILER.md`, `README.md`, `DEPLOY-monsterasp.md`, `CRM_Kanban_Gereksinim_Listesi.md`.
Bulunan 21 sapma aşağıda; hepsi **doküman** sapması, kod değişikliği gerektiren bir bulgu çıkmadı.

**A. `crm-kanban-mimari.md` (spec) — kod spec'ten ayrışmış, spec güncellenmemiş**
- [x] A1 §3: "**.NET 8** (LTS)" → gerçek .NET 10. Karar PROGRESS'te kayıtlıydı, spec'e hiç işlenmemiş.
- [x] A2 §12 + §18.13: "yükleme **presigned PUT** ile doğrudan tarayıcıdan, okuma presigned GET" → Faz 12'de presigned tamamen kaldırıldı, her iki yön de API-proxy. Spec'in anlattığı topoloji bu deploy'da **çalışmıyor** (imza docker-içi host'a atılıyordu) — en yanıltıcı sapma.
- [x] A3 §18.9: "şirket-özel statü override v1'de **kapalı** (seam var, özellik yok)" → Faz 7b'de açıldı, `/admin/columns` canlı.
- [x] A4 §11 şema listesi eksik: `Tickets.ApprovalState` (Faz 7c), `FormFields` tablosu (Faz 15), `Invitations.Kind/Attempts` (Faz 23), `Companies.NextTicketNumber/TicketNumberPrefix` (Faz 3).
- [x] A5 §14 matris: "Öncelik/kategori değişti → hiç kimse" → gerçekte **atanan personele** gidiyor (müşteriye hâlâ gitmiyor).
- [x] A6 §18 numaralandırması 20'den 23'e atlıyor; **§18.21 ve §18.22 yok** ama 4 yerden referans veriliyor (spec §17, spec §20 risk tablosu, PROGRESS ×2).
- [x] A7 Metin bozulmaları: §19'da mojibake ("S<?>zlü"), §4 başlığında ve §17 Faz 3 satırında boş parantez/boş bold (bir isim silinirken metin kırılmış).

**B. `ONERILER.md` — bayat (2026-07-31'de donmuş)**
- [x] B1 Başlık bağlamı "Faz 0-12 tamam, 101 test yeşil" → Faz 28, 179 test.
- [x] B2 P0 #1 (Docker e2e), #5 (S3 bayt e2e), #6 (SMTP) kapandı ama hâlâ "bloklayıcı" başlığı altında.
- [x] B3 #5 "depolama → **host diski (LocalFileStorage)** seçildi" → gerçekte **AWS S3** kullanılıyor (Faz 26, `crm-kanban-fiskirmacoop`).
- [x] B4 #6 "SMTP → **Resend**" → gerçekte **Brevo** (`.env`, `.env.example`, docker-compose hepsi Brevo).
- [x] B5 P1 #9 ve #13 zaten "kapandı" işaretli ama #8/#14 de kapanmış olduğu hâlde açık duruyor.
- [x] B6 "Bu değişiklikler commit edilmedi… `feat/kanban-columns-moderation-deploy` branch'i" → 16 faz öncesinin durumu.

**C. `README.md` — eksik (yanlış değil, ama projeyi ilk kez açan yanlış yola girer)**
- [x] C1 `up.ps1` hiç geçmiyor — asıl tek-komut yol o (secret üretimi + build + hazır olma beklemesi); README elle `.env` kopyalama + `docker compose up` anlatıyor.
- [x] C2 Config tablosu eksik: `App__PublicBaseUrl` (maildeki linkler bunsuz kırık), `Email__From/FromName/UseSsl/Username`, `S3__Region/ForcePathStyle`, `Seed__Demo`, `Files__Provider`.
- [x] C3 `publish.ps1` + MonsterASP yolu (`DEPLOY-monsterasp.md`) README'den link almıyor.
- [x] C4 Müşteri giriş sayfası `/c/{slug}` (Faz 23) ve moderasyon akışı README'nin "ilk adımlar"ında yok.

**D. `DEPLOY-monsterasp.md` — kendi içinde çelişiyor**
- [x] D1 §8 son madde "**dosya depolama S3'e bağlı; host disk kullanılmaz** (spec §12)" ↔ §6a "host diski, **seçilen varsayılan**, ücretsiz". İki madde birbirini çürütüyor; okuyan hangisine uyacağını bilemez.
- [x] D2 `Files__Provider` "varsayılan `local`" deniyor ama kodda varsayılan **`s3`** (`DependencyInjection.cs:50`) — env verilmezse S3 aranır ve dosya yolu patlar.
- [x] D3 §3/§7 Resend anlatıyor (host/user/API-key/troubleshooting) → Brevo'ya geçildi.

**E. `PROGRESS.md` (kendi kaydı)**
- [x] E1 Satır 183 `GET /api/public/companies`'i canlı gösteriyor; Faz 17'de kaldırılmış, bugün 404 döndüğü doğrulandı. (Faz 28'de borç #38'i yazarken "doküman düzeltildi" demiştim — satırın kendisi düzeltilmemişti, şimdi düzeltildi.)

**F. `CRM_Kanban_Gereksinim_Listesi.md`** — Faz 28'de kanıtlarıyla işaretlendi, sapma yok.

**Kapanış doğrulaması:** 21 maddenin 21'i düzeltildi. Kalan bayat ifadeler yalnız ONERILER.md'nin
**tarihli** güncelleme bloklarında (Faz 13-15, Faz 16-17) — bunlar bilerek bırakıldı; en üstteki
2026-08-05 bloğu "altındakiler tarihseldir" diyerek hangi satırların geçersiz olduğunu tek tek sayıyor.
Tarihli bir kaydı geriye dönük düzeltmek, o gün ne bilindiğinin izini siler.

**Karar/Varsayım (Faz 29):**
- *Spec düzeltiliyor, "PROGRESS'te kayıtlı" sayılmıyor:* spec'in kendi başlığı "kod her zaman bu dokümanla senkron tutulur; ayrıştığı yerler PROGRESS.md'de kaydedilir" diyor. Kayıt tutulmuştu ama spec 16 faz boyunca hiç güncellenmedi; artık spec'i okuyan yanlış sistemi anlıyor (özellikle A2 presigned). Ayrışmayı kaydetmek, ayrışmayı **kalıcılaştırmak** değil.
- *Eski kararlar silinmiyor, üstü çizilip gerekçesi yazılıyor:* CLAUDE.md "minimalizm koda uygulanır, bilgi kaydına değil". §18'deki "varsayıldı" satırları duruyor, yanına "→ Faz N'de şöyle değişti, çünkü…" ekleniyor. Neyin neden değiştiği, ne olduğu kadar değerli.
- *§18.21/§18.22 uydurulmadı:* numaralar dört yerden referans alınıyor ama içerik hiç yazılmamış. Boşluğa metin uydurmak kayda yalan eklerdi; bunun yerine referanslar gerçekten var olan maddelere yönlendirildi ve boşluk açıkça not edildi.
- *ONERILER.md arşive çevrilmedi, güncellendi:* kapanan maddeler siliniyor değil, "✅ kapandı (Faz N)" ile işaretleniyor — hangi önerinin ne zaman ve neyle kapandığı yol haritasının asıl değeri.

### Faz 30 — Yetki ekranı: on/off switch + dört yetki bug'ı + kullanıcı aksiyon butonları ✅ (2026-08-05)
İstenen: yetkiler switch olsun; kullanıcılar bölümündeki "kimliğine gir / hesabı sil" genel UI'a uygun buton olsun
(StarAdmin'den alınabilir); yetkilerdeki bug'lar test edilip çözülsün; her yetkinin ne iş yaptığı yazılsın.

**Bulunan 4 bug (hepsi canlı sistemde doğrulandı, 185 testin hiçbiri yakalamıyordu):**
- [x] **B1 — `ticket.view` hiçbir yerde uygulanmıyordu (kritik).** Yetki seed'liydi, UI'da listeleniyordu, atanabiliyordu; **0 kullanım**. `PermissionKeys.TicketView` kod tabanının hiçbir okuma yolunda kontrol edilmiyordu — üyelik, okuma izni sayılıyordu. Canlı kanıt: `uretim@tekstil.local`'a `ticket.view` **Reddet** verildi, kullanıcı 16 talebi, kanban'ı ve talep detayını görmeye devam etti. Yönetici anahtarı kapatıyor, hiçbir şey olmuyordu. **Düzeltme:** `TicketQueryService`'e `IPermissionService` enjekte edildi; `ListAsync` staff kolunu `ticket.view` tutulan şirketlere daraltıyor, `KanbanAsync`/`ModerationQueueAsync` şirket bazında reddediyor (`ticket.view_forbidden`), `GetDetailAsync` personelden yetkiyi istiyor. Müşteri yolu **kasten** muaf: müşteriler üye değil, hiç yetkileri yok — onları `ticket.view`'a bağlamak kendi taleplerinden kilitlerdi.
- [x] **B2 — `permission.assign` yazma yolunda hiç kontrol edilmiyordu (yetki yayılımı).** Okuma tarafı yetkiyi istiyordu (`GetEffectiveAsync` → 403), yazma tarafı istemiyordu. Guard yalnızca "dağıttığın anahtara sahip misin" ve "kendi şirketin mi" diye bakıyordu. Canlı kanıt: `permission.assign` **reddedilmiş** bir personel, şirket adminine `ticket.assign` yetkisini **204 ile verdi** — üstelik sonucu göremediği hâlde. **Okuma kapalı + yazma açık** en kötü ikili: açığı fark edecek kişiden gizliyor. **Düzeltme:** `PermissionAssignmentGuard`'a üçüncü kapı (`permission.assign.forbidden`).
- [x] **B3 — `report.global` ve `settings.manage` şirket bazında atanabiliyordu ama hiçbir etkisi yoktu.** İkisinin de uygulama yeri doğrudan "süper admin mi?" diye soruyor (`report.global` ve `settings.manage` anahtarları kodda **0 kullanım**). Şirket bazlı bir anahtar bu kontrolü asla karşılayamaz → yönetici anahtarı açıyor, hiçbir şey değişmiyordu. **Düzeltme:** katalogda `globalOnly` bayrağı; UI bu satırları kilit rozetiyle ve pasif switch'le gösteriyor. Yetki silinmedi — süper adminin rol tabanında gerçek.
- [x] **B4 — Yetki kataloğu (`GET /api/permissions`) her kimlikli kullanıcıya açıktı.** Müşteri hesabı 200 alıyordu; yetki modelinin haritası hiç işi olmayan kişilere veriliyordu. **Düzeltme:** süper admin **veya** en az bir şirketinde `permission.assign` tutan çağıran (`permission.catalog_forbidden`).

**UI:**
- [x] **`Switch` primitive'i** (`ui/primitives.tsx`): StarAdmin'in Bootstrap `.form-switch` biçimi projenin token'larıyla — pill track, sağa kayan knob, açıkken `primary`. Altında gerçek `<input type="checkbox" role="switch">`; klavye, odak halkası ve ekran okuyucu bedavaya geliyor, yeniden yazılmıyor.
- [x] **Yetki ekranı switch'e geçti** (`admin/Permissions.tsx`): Ver/Reddet buton çifti kalktı. Her satır: etiket + anahtar + **ne iş yaptığının bir cümlelik açıklaması** + durum rozeti (`rol varsayılanı` / `özel olarak verildi` / `rolünden alındı` / `yalnız süper admin`) + switch. Hata olursa satır üstünde tek hata zarfı.
- [x] **Yetki açıklamaları** `PermissionLabels.Descriptions`'a yazıldı — 12 anahtarın her biri için, **etiketten değil uygulama yerinden** türetilmiş cümle. B1/B3 tam olarak bu ayrımdan doğmuştu: anahtar kulağa bir şey yapıyormuş gibi geliyordu.
- [x] **Kullanıcılar ekranı aksiyonları buton oldu** (`admin/AdminUsers.tsx`): "Kimliğine gir" ve "Hesabı sil" artık `Button` primitive'i + mdi ikon + tooltip; onay adımı da düz link yerine `danger` "Onayla" + `secondary` "Vazgeç" butonu.
- [x] **Tarayıcıda görsel doğrulama:** switch'ler açık/kapalı doğru render oluyor, `report.global`/`settings.manage` kilitli ve pasif, kullanıcı satırındaki iki buton viewport'a sığıyor (sağ kenar 1342px < 1396px), sil onayı açılıp "Vazgeç" ile kapanıyor.

- [x] **Testler (+6, toplam 185):** `TicketViewPermissionTests` (5) — yetkisiz personel liste/kanban/moderasyon/detay göremez, yetkiliyken görür; müşteri hiçbir yetkisi olmadan kendi talebine ulaşır; süper admin üyeliksiz her şeyi okur. `PermissionAssignmentGuardTests` (+1) — `permission.assign` olmadan, sahip olunan bir yetki bile dağıtılamaz (mevcut 3 test de yeni kapıya göre güncellendi).
- [x] **Canlı doğrulama:** `ticket.view` Reddet → liste 0 / kanban 403 / moderasyon 403; Ver → 16 / 200 / 200. `permission.assign`'sız dağıtım → `permission.assign.forbidden`. Katalog: müşteri 403, admin 200 + 12 anahtar açıklamalarıyla. Test override'ları temizlendi, demo verisi bulunduğu hâle döndürüldü.

**Karar/Varsayım (Faz 30):**
- *Switch'te "kapalı" = açık **Deny**, satır silme değil.* Rol tabanı yetkiyi veriyorsa, override'ı silmek anahtarı anında geri açardı — yönetici kapattığı switch'in kendiliğinden kalktığını görürdü. Bunun bedeli: bir yetkiyi "rol varsayılanına geri döndürmek" (override'ı kaldırmak) UI'dan mümkün değil. Gerekirse üçüncü bir durum (Ver / Rol varsayılanı / Reddet) gerekir — üç durumlu bir kontrol switch değildir, o yüzden istenmeden eklenmedi.
- *`roleBaseline` API'ye eklendi:* switch'in "açık" olması rolden mi geliyor yoksa özel mi verilmiş, ayırt edilemiyordu. Bu ayrım olmadan "kapattım geri açıldı" ile "kapattım ama zaten çalışmıyor" aynı görünüyor — B1'in 30 faz boyunca fark edilmemesinin sebeplerinden biri de bu.
- *`report.global`/`settings.manage` katalogdan silinmedi, kilitlendi:* silmek "bu yetki yok" derdi; oysa var, yalnız süper adminde anlamlı. Kilitli satır + açıklama, boş bir liste kadar ucuz ama doğru.
- *B1 düzeltmesi `ListAsync`'te şirket başına yetki sorgusu yapıyor:* personelin şirket sayısı tipik olarak 1-2, döngü ucuz. Çok şirketli bir kullanıcıda N sorgu olur; ölçülmeden tek sorguya çevrilmesin (teknik borç #39).
- *Müşteri `ticket.view`'dan muaf:* müşteriler üye değil → hiç yetkileri yok. Onları da yetkiye bağlamak, "kendi talebini görmek" için yetki gerektirirdi; bu invaryant teste bağlandı.

### Faz 31 — Yetkilerde üçüncü durum: "rol varsayılanına dön" ✅ (2026-08-05)
İstenen: Faz 30'un sonunda önerilen üç durumlu kontrol onaylandı ("olur dediğin gibi yapalım, aslında gösterge eklemişsin").
Faz 30'un rozetleri zaten override'ın varlığını gösteriyordu; eksik olan onu **kaldırma** yoluydu.

- [x] **Üçüncü durum eklendi:** Ver (Grant) / Reddet (Deny) / **rol varsayılanı** (satır yok). `DELETE /api/permissions/assign?userId=&companyId=&permissionKey=` → `PermissionAssignmentService.ClearAsync`. Grant ve Deny yalnızca "hep açık" ve "hep kapalı" diyebiliyordu; geri dönüş yolu olmadan bir kez reddedilen yetki bir daha rol varsayılanına döndürülemiyordu — yalnız tersine sabitlenebiliyordu, ki bu farklı bir şey ve rol matrisi değiştikçe sürükleniyor.
- [x] **Temizleme, atama ile aynı kapıdan geçiyor** (`EnsureCanAssign`). Paranoya değil: bir **Deny'ı kaldırmak**, yetkiyi rol tabanı üzerinden geri verir — kapısız bir temizleme, silme kılığında bir grant olurdu. Test bunu ayrıca kilitliyor.
- [x] **UI:** switch korundu (istenen buydu), yanına yalnız **override varken** çıkan `rol varsayılanına dön` düğmesi eklendi. Rozet zaten ne zaman anlamlı olduğunu söylüyor; düğme onun eylem karşılığı. `globalOnly` satırlarda çıkmıyor (temizlenecek bir şey yok).
- [x] **`Overridden` alanı API'ye eklendi:** UI önce override'ı `etkin != rol` karşılaştırmasıyla tahmin ediyordu; oysa override rolle **aynı fikirde** olabilir (rolün zaten verdiği bir anahtara Grant). O durumda karşılaştırma "rol varsayılanı" der ve temizleme düğmesini gizlerdi. Türetilen bilgi, çıkarsanan bilgiden güvenli.

**Bu iş sırasında bulunan bug (Faz 28 ile aynı sınıf):**
- [x] **Silinmiş yetki satırları hâlâ uygulanıyordu.** `PermissionService.GetPermissionsAsync` `UserPermissions`/`RolePermissions`/`Permissions` satırlarını `IgnoreQueryFilters()` ile okuyor ama **`DeletedAt == null` yazmıyordu**. Sonuç: "rol varsayılanına dön" 204 dönüyor, satır soft-delete ediliyor, **hiçbir şey değişmiyordu** — silinmiş bir Deny yetkiyi kapalı tutmaya devam ediyordu, üstelik UI'da artık görünmediği için sebebi de görünmüyordu. Canlı yakalandı (adım 3: `override=False` ama `etkin=False`). **Düzeltme:** üç sorguya da açık soft-delete koşulu. *Bu tam olarak Faz 28'in dersi: filtreyi baypas etmek, garantilerinin hiçbirini devralmamak demek.*
- [x] **Testler (+6, toplam 191):** temizleme override'ı kaldırır (soft-delete, satır durur); **temizlenen override artık uygulanmaz** (yeni bug'ın regresyon testi); iki kez temizlemek hata değil; temizleme sonrası yeniden Ver satırı **diriltir** (tekil indeks ihlali yok); temizleme `permission.assign` ister; sahip olunmayan yetki temizlenemez.
- [x] **Canlı doğrulama (her iki yön):** rolde **olan** `ticket.view` → Reddet (liste 0) → rol varsayılanına dön → etkin açık, liste 16. Rolde **olmayan** `ticket.delete` → Ver (etkin açık) → rol varsayılanına dön → etkin kapalı. UI'da: rozet `rolünden alındı` + switch kapalı + düğme → tıkla → rozet `rol varsayılanı` + switch açık + düğme kayboldu. Demo verisi oturum başındaki hâline döndü.

**Karar/Varsayım (Faz 31):**
- *Segment kontrolü değil, switch + ayrı düğme:* üç durumlu bir segment (Ver/Rol/Reddet) switch'i tamamen değiştirirdi; kullanıcı switch istemişti. Switch iki **etkin** durumu (açık/kapalı) taşımaya devam ediyor, düğme ise "bu kararı ben verdim"i geri alıyor. Bu ayrım aynı zamanda doğru zihinsel model: etkin durum ile onun **nereden geldiği** ayrı sorular.
- *Temizleme yoksa 404 değil 204:* zaten rol varsayılanındaysa istenen sonuç zaten sağlanmış. Idempotent; UI'nin çift tıklamada hata göstermesine gerek yok.
- *Soft-delete korundu (hard delete değil):* yetki değişiklikleri audit konusu; satırın kendisi `DeletedAt` ile duruyor, `AssignAsync` gerektiğinde diriltiyor. Tekil indeks `(UserId, PermissionId, CompanyId)` filtresiz olduğu için zaten başka seçenek de yoktu.
- *Teknik borç #41 kapandı.* #40 (yetki anahtarı ↔ uygulama yeri denetimi) açık kalmaya devam ediyor ve bu turda önemi arttı: bu oturumda üçüncü kez yetki-çözümleme yolunda sessiz bir sapma bulundu.

### Faz 32 — Gereksinim denetimi (SOLID/Clean Arch) + deploy paketi ✅ (2026-08-05)
İstenen: `OneDrive/Belgeler` altındaki iki gereksinim notunun (`# SOFTSUPPORT ONINON CLEAN ARCHITEC.txt`,
`UI tarafında da yeterince code lar.txt`) tamamının koda karşı denetlenmesi — **özellikle SOLID ve clean
architecture maddeleri** — ve projenin deploy'a hazır hâle getirilmesi.

**Gereksinim → kod eşleşmesi (ikinci txt, "Beklenen mimari özellikler"):**

| Gereksinim | Durum | Kanıt |
|---|---|---|
| Tüm parametreler bir sistem dosyasında | ✅ (yorumlu) | Jenerik `Setting` KV tablosu + `/settings` ekranı (Faz 6). Karar: secret/infra dosya-env'de, iş parametresi DB'de (§13) — "tek dosya" yerine bilinçli ikiye ayrım. Teknik borç #21: tipli Options'ların bir kısmı hâlâ config'te |
| Katmanlı mimari (txt'de "nvc", MVC kastediliyor) | ✅ | Domain (0 bağımlılık) → Application → Infrastructure → Api; bağımlılık okları hep içeri. Denetlendi: `.csproj` referansları temiz, Domain'de EF/ASP.NET izi yok |
| RBAC + permission tablosu + rol dışı kullanıcı yetkisi | ✅ | `RolePermission` matrisi + `UserPermission` Grant/Deny (deny kazanır) + şirket-scope; UI'dan yönetiliyor (Faz 30-31) |
| Her metod tek iş (SOLID) | ✅ | Denetim: `src/` altında 45 satırı geçen 6 metod var, hepsi bildirimsel blok (EF `OnModelCreating`, DI kaydı, seeder). İş mantığı metodları use-case granülerliğinde |
| UI'da tekrar eden elemanlar component | ⚠️→✅ | **Bu fazda kapatıldı**, aşağıya bak |
| Her şey REST API üzerinden | ✅ | Tek yüzey `/api/*`; SPA'nın başka veri yolu yok |

**Yapılan (txt'nin açıkça şikâyet ettiği iki nokta):**
- [x] **"Hata mesajları componentin içine yazılmış, ortaklaştırılması lazım":** `toApiError` + `errorMessage`
  ikilisi **15 ekranda** birebir kopyalanmıştı. Tek `errorText(err)` yardımcısına indirildi
  (`lib/messages.ts` — katalog zaten oradaydı, zarf çözme de artık orada). Ekranlar `catch (e) { setError(errorText(e)) }`.
  Yeni bir hata kodu eklemek artık tek dosyaya dokunuyor.
- [x] **"Tekrar eden elemanlar component olmalı":** 10 ekranda elle yazılmış `<p className="…">Yükleniyor…</p>`
  → tek `Loading` primitive'i (`ui/primitives.tsx`), yerleşim `className` ile geçiliyor. Buton içindeki
  "Yükleniyor…" etiketleri **kasten** dokunulmadı: onlar yükleme bloğu değil, düğme durumu.

**Deploy hazırlığı:**
- [x] **Teknik borç #35 kapandı (log gürültüsü):** `UseSerilogRequestLogging` artık exception middleware'in
  **dışında**. Eski sırada her domain istisnası (401/403/404/400) log'a `responded 500` diye düşüyordu —
  istemciye giden statü doğruydu ama izleme/alarm tarafı yanlış 500 görüyordu. Prod'a çıkmadan önce
  düzeltilmesi gereken sınıf: ilk gerçek alarmda "500 var" diye kovalanacak sahte iz.
- [x] **Teknik borç #40 kapandı (yetki anahtarı ↔ uygulama yeri):** `PermissionEnforcementAuditTests` —
  `PermissionKeys.All`'daki her anahtar ya Application/Api kodunda **referans ediliyor** olmalı ya da
  `PermissionLabels.IsGlobalOnly` ile global-yalnız ilan edilmiş olmalı; ayrıca her anahtarın Türkçe
  etiketi + açıklaması olmalı. Etiket dosyasındaki string literal'ler sayılmıyor (aranan `PermissionKeys.X`
  sabiti), yani "katalogda var ama kimse kontrol etmiyor" durumu artık testten geçemez.
  **Negatif doğrulama yapıldı:** sahte bir anahtar eklenince test kırmızıya döndü, sonra geri alındı.
  Bu, Faz 28/30/31'de üç kez tekrarlayan "sessiz sapma" sınıfının kalıcı nöbetçisi.
- [x] **UTC serileştirici kültürden bağımsız:** `UtcDateTimeConverter` tarih formatını `CultureInfo.InvariantCulture`
  ile yazıyor (CA1305). Paylaşımlı hosting'in kültürü bizim seçimimiz değil; Gregoryen olmayan takvim ya da
  ASCII-dışı rakam kullanan bir kültürde sunucu, hiçbir istemcinin ayrıştıramayacağı bir zaman damgası
  üretebilirdi. `src/` artık **0 uyarı** ile derleniyor.
- [x] **Paket doğrulandı:** `./publish.ps1` uçtan uca çalıştı → `publish/` içinde `CrmKanban.Api.exe` +
  `web.config` + `wwwroot/index.html` (self-contained win-x64, ~155 MB). MonsterASP.NET'e yüklemeye hazır.
- [x] **Doğrulama:** 193 test yeşil (16 domain + 169 application + 8 API), `dotnet build -c Release` 0 uyarı/0 hata
  (`src/`), frontend `npm run build` + `oxlint` temiz (bilinen tek fast-refresh uyarısı).

**Karar/Varsayım (Faz 32):**
- *`errorText` `messages.ts`'e kondu, `api.ts`'e değil:* çıktısı kullanıcıya gösterilecek **metin**, katalog
  orada. `api.ts` taşıma katmanı kalsın (`toApiError` zarfı çözer, metni bilmez). Bağımlılık yönü tek: messages → api.
- *`Loading` bir primitive, `Spinner`/`Skeleton` değil:* istenen "tekrarı kaldır"; animasyon/skeleton ayrı bir
  UX kararı ve ONERILER P2'de zaten duruyor. Şimdi eklemek istenmeyen tasarım kararını koda gömerdi.
- *Yetki denetimi kaynak dosya tarayan bir test:* alternatif bir Roslyn analyzer'dı — aynı garantiyi
  ~50 satır test yerine bir analyzer projesi + paketleme ile verirdi. Anahtar sayısı 12, dosya sayısı sabit;
  test yeterli. Testin kaynak ağacını bulamaması (repo dışı çalıştırma) **hata** sayılır, sessizce geçmez.
- *Metod uzunluğu denetimi "her metod tek iş"in kanıtı değil, göstergesi:* uzun metodlar bildirimsel bloklara
  çıktı (EF config, seed, DI). `InvitationService.InviteUserAsync` (66 satır) tek use-case'i doğrusal
  yürütüyor — doğrula → yetkilendir → kullanıcı/üyelik çöz → token+mail → kaydet. Mikro-metodlara bölmek
  okunurluğu artırmaz, çağrı zincirini uzatır; SRP use-case granülerliğinde korunuyor sayıldı.
- *İlk txt (`SOFTSUPPORT`) ayrı bir projenin (todo/helpdesk) kurulum günlüğü:* katman/paket listesi ve
  "namespace klasörle uyumlu / DI kaydı / migration Infrastructure'da" sağlık kontrolü bu projede zaten
  sağlanıyor. Oradaki teknoloji seçimleri (AutoMapper, FluentValidation v10 sözdizimi, Identity tabanlı
  `IdentityDbContext`) bu projeye **taşınmadı**: AutoMapper'a karşılık elle DTO projeksiyonu var (daha az
  sihir, EF'e daha iyi çeviriliyor), FluentValidation zaten güncel pipeline ile bağlı, kimlik modeli
  Identity yerine kendi `User`/`Membership`/`RoleType` modelimiz (çok-kiracılı yetki Identity rollerine
  sığmıyordu). Yani o dosya bir gereksinim değil, bir geçmiş kayıt olarak okundu.

### Faz 33 — Mail teslimatı: "sadece benim adresime gidiyor" + canlı deploy ✅ (2026-08-06)

Kullanıcı bildirimi: *"mail için de sadece benim emailim değil, bütün maillere gönderim yapılması lazım."*

**Önce teşhis, sonra kod.** Şikâyet "gönderim yapılmıyor" gibi okunuyordu; ölçüm başka bir şey söyledi.

- [x] **Alıcı kısıtı YOK — doğrulandı, varsayılmadı.** Üç ayrı kanıt: (1) uygulama kodunda alıcı
  allowlist/override araması boş döndü (`NotificationService` alıcıyı matristen çözüyor, adrese
  bakmıyor); (2) Brevo relay'ine ham SMTP oturumu açıldı, `RCPT TO` üç farklı sağlayıcıya
  (gmail/outlook/yandex) `250` döndü; (3) uygulamanın kendi akışından (`/api/public/form/{slug}/register`)
  üçüncü taraf bir adrese gerçek mail gönderildi, `EmailQueue.Status=Sent`, saniyeler içinde teslim.
  **Yani mail zaten herkese gidiyordu.** `.env`'deki "mail YALNIZ hesabın adresine gider" yorumu
  Resend döneminden kalma ölü bilgiydi (Brevo'ya Faz 25'te geçilmiş, yorum güncellenmemişti) — silindi.
- [x] **Gerçek sorun: spam eşiği.** mail-tester.com'a uygulamanın kendi `account_code` maili
  gönderildi → **4.9/10, SpamAssassin 4.1/5.0**. Eşik 5.0. Yani her mail spam klasörüne bir adım
  uzaktaydı. Kullanıcının kendi Gmail'i kendi adresinden geleni beyaz listeye aldığı için ona
  düşüyordu; başkasının sağlayıcısı filtreliyordu. Şikâyetin fiziksel karşılığı tam olarak bu.
- [x] **Fix 1 — `text/plain` parçası (`SmtpEmailSender`).** Mailler HTML-only gidiyordu:
  `MIME_HTML_ONLY` (+0.1) ve relay kendi açılma-takip pikselini enjekte edince `HTML_IMAGE_ONLY_16`
  (+1.0). Artık `multipart/alternative`; düz metin şablonun HTML'inden türetiliyor (şablonlar tek
  kaynak kalsın). +5 test (`PlainTextPartTests`): kod/entity çözümü, buton linkinin URL'inin metinde
  korunması, boşluk sadeleştirme, boş gövde.
- [x] **Fix 2 — `Email__ReplyTo` (asıl kazanç).** `From` freemail olduğu için Brevo onu
  `<id>.brevosend.com`'a yeniden yazıyor **ve Reply-To'ya orijinal gmail adresini enjekte ediyordu**;
  SpamAssassin bunu klasik sahtecilik imzası sayıyor: `FREEMAIL_FORGED_REPLYTO` +2.5 +
  `FREEMAIL_REPLYTO_END_DIGIT` +0.25. Kendi Reply-To'muzu vermek enjeksiyonu engelliyor.
- [x] **Ölçülen sonuç (mail-tester, canlı Brevo relay'i, üçüncü taraf alıcı):**

  | Yapılandırma | SpamAssassin (eşik 5.0) | mail-tester |
  |---|---|---|
  | Başlangıç | 4.1 | 4.9/10 |
  | `+ text/plain` | 4.0 | 5.0/10 |
  | `+ ReplyTo` (freemail olmayan) | **1.2** | **7.8/10** |

- [x] **Config bağlandı:** `docker-compose.yml` (`Email__ReplyTo`), `.env`, `.env.example`,
  `DEPLOY-monsterasp.md` (yeni §9 + sorun giderme maddesi). Container'da `printenv` ile doğrulandı.
- [x] **`publish-fd/` `.gitignore`'a eklendi** — içinde doldurulmuş `appsettings.Production.json`
  (gerçek connection string, SMTP key, S3 anahtarları) var. Commit edilmemişti ama bir `git add -A`
  ile edilebilirdi; kapatıldı.
- [x] **Doğrulama:** 198 test yeşil (16 domain + 174 application + 8 API), lokal stack
  (docker compose: api/db/minio/web) ayakta, SPA + kanban çalışıyor.

**Karar/Varsayım (Faz 33):**
- *`From` olarak `<id>.brevosend.com` adresi DENENDİ ve REDDEDİLDİ.* "From'u Brevo'nun kendi
  doğrulanmış alanına taşıyalım, yeniden yazma da olmaz" en temiz çözüm gibiydi.
  `no-reply@11818676.brevosend.com` ile gönderildi: relay SMTP'de **kabul etti** (`SmtpClient.Send`
  hatasız döndü, `250 OK`) ama mesaj **hiç teslim edilmedi** — 10+ dakika beklendi, gelmedi; kontrol
  olarak aynı mesaj gmail From'uyla saniyeler içinde geldi. Brevo yalnız doğrulanmış göndericiden
  gönderiyor, gerisini sessizce düşürüyor. **Bu yol maili tamamen kesiyordu ve SMTP hatası da
  vermiyordu** (kuyruk `Sent` görünürdü, kimse mail almazdı) — canlıya çıksaydı teşhisi en zor
  arıza sınıfı olurdu. Kayda geçiyor ki bir daha denenmesin.
- *Reply-To olarak `no-reply@crm-kanban.runasp.net` seçildi:* gerçek bir posta kutusu değil, ama
  gerekmiyor — şablonlar zaten "bu otomatik bir bildirimdir; lütfen yanıtlamayın" diyor. Kriter tek:
  freemail **olmaması**. Alternatif (Reply-To'yu tamamen boş bırakmak) işe yaramıyor, çünkü boşsa
  Brevo kendi enjeksiyonunu yapıyor — başlığı sahiplenmek gerekiyor.
- *Düz metin şablondan türetiliyor, ayrı bir `BodyText` sütunu eklenmedi:* her şablonun ikinci bir
  elle yazılmış kopyası bakım yükü ve senkronizasyon borcu olurdu. Şablonlar bu uygulamanın kendi
  seed'inden gelen sabit, basit bir markup kümesi (paragraf, kalın, tek buton) — regex ile
  düzleştirme yeterli, HTML parser bağımlılığı değil. Şablonlar kullanıcı-yazımı HTML'e dönerse
  (`ponytail:` notu kodda) AngleSharp'a geçilir.
- *`HTML_IMAGE_ONLY_16` (+1.0) kapatılmadı:* kaynağı Brevo'nun açılma-takip pikseli, kod tarafında
  değil Brevo panelinde (Senders & IP → tracking) kapatılıyor. 1.2/5.0'da riski yok; kullanıcının
  panel kararı, koda gömülmez.
- *Kalıcı çözüm hâlâ teknik borç #33 (domain doğrulama).* Bu faz borcu kapatmadı, **zararını**
  ~%70 azalttı. Domain doğrulanınca `EMAIL_FROM=destek@<domain>` olur, yeniden yazma biter,
  `EMAIL_REPLY_TO` gereksizleşir (boş bırakılabilir) ve DMARC hizalanır.

### Faz 34 — MonsterASP.NET canlı deploy + HTTPS ✅ (2026-08-06)

- [x] **Canlıda HTTPS hiç yoktu — bulundu ve açıldı.** `https://crm-kanban.runasp.net` TLS el sıkışmasında
  düşüyordu; panelde domain yalnız `http://` bağlıydı, SSL sertifikası sütunu boştu. **Bu sessiz ama
  ağır bir arızaydı:** `App:PublicBaseUrl` zaten `https://...` olarak yapılandırılmış, yani maillerdeki
  **her davet/aktivasyon/ticket derin bağlantısı açılmayan bir adrese gidiyordu.** Mail teslimatını
  düzeltmenin tek başına bir anlamı olmazdı. Let's Encrypt sertifikası verildi (CN=YR1, son kullanma
  03.10.2026, otomatik yenileniyor) + **Force redirect to HTTPS** açıldı (doğrulandı: `http://` → 307).
- [x] **Deploy — tam paket yerine iki dosya.** Yerel `publish-fd/` ile yeni publish çıktısı bayt bayt
  karşılaştırıldı: yalnız **`CrmKanban.Infrastructure.dll`** ve **`appsettings.Production.json`**
  farklıydı; kalan 90 dosya birebir aynı. Sunucudaki 52 MB disk kullanımı yeni paketin 52,1 MB'ıyla
  örtüşüyor → `publish-fd` gerçekten canlıdaki sürüm. 18 MB'lık zip yerine 315 KB yüklendi.
- [x] **Kilit sorunu `app_offline.htm` ile çözüldü:** IIS `hostingModel="inprocess"` DLL'i w3wp içine
  kilitliyor, üzerine yazma başarısız olurdu. Sırayla: `app_offline.htm` yükle (uygulama durur, kilit
  serbest) → iki dosyayı yükle → `app_offline.htm` sil (uygulama döner). Kesinti ~1 dakika.
- [x] **Config canlıda:** `Email:ReplyTo` = `no-reply@crm-kanban.runasp.net` eklendi; diğer tüm canlı
  değerler (connection string, SMTP key, S3) olduğu gibi korundu (dosya JSON olarak parse edilip tek
  alan eklendi, elle yeniden yazılmadı).
- [x] **Canlı uçtan uca doğrulama:** `https://crm-kanban.runasp.net` üzerinden `mermer` şirketine
  **tamamen ilgisiz üçüncü taraf bir adrese** (mailinator public inbox) kayıt yapıldı → doğrulama
  kodu maili saniyeler içinde ulaştı. Mailde **`text/plain` parçası mevcut** ve Türkçe karakterler
  doğru (`Ege Mermer için kaydınızı tamamlamak üzere…`) → yeni `Infrastructure.dll` canlıda aktif ve
  `HtmlToPlainText` gerçek seed şablonlarında çalışıyor. Brevo'nun DKIM `h=` listesinde `reply-to`
  görünüyor → `Email:ReplyTo` de canlıda uygulanıyor.

**Karar/Varsayım (Faz 34):**
- *Tam paket değil, iki dosya yüklendi:* panelin tarayıcı yükleyicisi 10 MB sınırlıydı, paket 18 MB.
  Alternatif FTP idi; ama fark **kanıtlanmış** biçimde iki dosyaydı (bayt karşılaştırması + sunucu disk
  kullanımının eşleşmesi), dolayısıyla kısmi deploy bir tahmin değil ölçüm. `EmailOptions`'a alan
  eklemek Api/Application referanslarını bozmaz (yalnız config binding okur).
- *`app_offline.htm` seçildi, panelin "restart app pool" seçeneği değil:* silme diyaloğundaki
  "Restart application pool before deleting" yalnız **silme** akışında var, **yükleme** akışında yok —
  yani DLL'i üzerine yazarken kilit yine sorun olurdu. `app_offline.htm` ASP.NET Core'un standart
  mekanizması ve panel özelliğinden bağımsız.
- *`Seed:Demo` canlıda `true` bırakıldı:* DEPLOY dokümanı gerçek prod için `false` diyor ve bu
  değiştirilmeli — ama seed idempotent, zaten çalışmış, ve kapatmak istenmeyen bir davranış değişikliği
  olurdu (demo şirketler `tekstil`/`mermer` üzerinden canlı doğrulama yapıldı). **Açık iş olarak
  bırakıldı**, sessizce değiştirilmedi.
- *`publish-fd/` deploy edilen sürümün aynası olarak güncellendi:* bir sonraki deploy'da "ne değişti"
  farkının doğru çıkması buna bağlı. Gitignore'da, secret taşıdığı için commit edilmiyor.

**Açık iş (Faz 34 sonrası):**
- `Seed:Demo=false` yapılıp demo şirket/ticket verisi canlıdan temizlenmeli (gerçek kullanıma geçişte).
- Brevo panelinde açılma-takip pikseli kapatılırsa `HTML_IMAGE_ONLY_16` (+1.0) da düşer.
- Teknik borç #33 (domain doğrulama) hâlâ kalıcı çözüm.
- Mailinator public inbox'ta bir test maili kaldı (`crmkanban-prod-check@mailinator.com`) — içindeki
  kod 15 dakikada geçersizleşiyor, hesap atılabilir bir test hesabı.

### Faz 35 — Giriş kuralı: davet edilen müşteri vs. linki bulan yabancı ✅ (2026-08-06)

Kullanıcı bildirimi: Anadolu Tekstil'de Derya Yönetici hesabından müşteri linki üretilip bir arkadaşa
gönderildi; arkadaş kayıt olup talep açtı ve talep **onay kutusuna değil doğrudan kanbana** düştü.
İstenen: *"random bir müşteri o linki bulursa onay kutusuna düşsün, eğer ben link attıysam kanbana
düşsün, kaydı olan bir müşteri başka bir ticket açarsa onaya düşsün."*

- [x] **Teşhis: bug değil, tasarım boşluğu.** `SubmitAsCustomerAsync` `pendingApproval: false` ile
  çağrılıyordu — giriş yapmış her müşterinin talebi doğrudan havuza gidiyordu (Faz 23 kararı,
  gerekçe: "adresini kodla kanıtladı"). Asıl kök neden daha derinde: **`CustomerLink.tsx` "müşteri
  linki oluştur" butonu `{origin}/c/{slug}` üretiyordu** — herkes için aynı URL, token yok, kişiye
  özel değil, süresiz. Yani *"ben link attım"* ile *"rastgele biri linki buldu"* sunucu için
  **birebir aynı istek**. Kullanıcının 1. ve 2. kuralı mevcut link tasarımıyla ayrıştırılamazdı;
  linkin bir kimlik taşıması gerekiyordu.
- [x] **Yeni kural — tek cümle:** *otomatik onaylanmanın tek yolu geçerli bir personel davetidir.*

  | Yol | Önce | Sonra |
  |---|---|---|
  | Geçerli davet token'ı ile **ilk** talep | — (yoktu) | **Kanban** |
  | Çıplak `/c/{slug}` (yabancı) | Kanban | **Onay kutusu** |
  | Kayıtlı müşterinin sonraki talebi | Kanban | **Onay kutusu** |
  | Anonim `/form/{slug}`, tanınan e-posta | Kanban | **Onay kutusu** |
  | Anonim `/form/{slug}`, tanınmayan e-posta | Onay kutusu | Onay kutusu |
  | "Güvenilir" işaretli müşteri | — (yoktu) | **Kanban (her seferinde)** |
  | Personelin kendi açtığı talep | Kanban | Kanban |

- [x] **`InvitationKind.CustomerAccess`** — mevcut `Invitation` tablosuna üçüncü tür. Hesap
  token'larından (`Link`/`Code`) ayrı tutuldu: bu token **kimlik bilgisi değil**, parola belirlemez,
  kimseyi giriş yaptırmaz; tek etkisi ilk talebin kuyruğu atlaması. Ayrı tür olması `/invite` parola
  akışının onu tüketmesini ve tersini engelliyor.
- [x] **`Invitation.CompanyId` eklendi (kiracı sınırı).** Davet "bu şirkete gel" demek; şirket kapsamı
  olmadan bir şirketin daveti **diğer tüm şirketlerde geçerli bir maymuncuk** olurdu. Domain
  constructor'ında guard: `CustomerAccess` + `companyId == null` → `ArgumentNullException`.
- [x] **`IntakeTrustService`** — tek karar noktası. Her iki giriş yolu (anonim form + giriş yapmış
  müşteri) buradan geçiyor, kural ikisi arasında ayrışamıyor. Token doğrulaması altı koşulun hepsini
  arıyor: token var + tür `CustomerAccess` + **bu** müşteriye ait + **bu** şirkete ait + kullanılmamış
  + süresi geçmemiş. Tümü `IgnoreQueryFilters` + **açık `CompanyId` koşulu** ile (çağıran anonim/müşteri,
  şirket kapsamı yok — Faz 28'deki sızıntı tam olarak bu koşulun düşmesiyle olmuştu).
- [x] **`CustomerTrust` entity + migration** — "bu şirket bu müşteriye güveniyor". Şirket×müşteri
  bazında, `GrantedById` ile denetlenebilir. Onay ekranında **"Onayla + güven"** butonu.
- [x] **`CustomerInviteService` + `POST /companies/{id}/customer-invites`** — `user.invite` yetkisi ve
  şirket kapsamı ister. Yeni link üretmek öncekini iptal ediyor (yanlış numaraya gitmiş link geri alınır).
  7 gün geçerli. UI: e-posta alanı + "Link üret" → kopyala/WhatsApp/e-posta.
- [x] **Doğrulama:** 214 test yeşil (16 + 190 + 8), +26 test. `IntakeTrustTests` (14) giriş kuralının
  yürütülebilir spec'i; ayrıca lokal stack'te uçtan uca üç senaryo koşuldu (davetsiz / davetli ilk+ikinci /
  token'ı başkası kullanıyor). Frontend `tsc --noEmit` + `oxlint` temiz.

**Karar/Varsayım (Faz 35):**
- *Token e-postaya bağlandı, anonim tek kullanımlık link seçilmedi (kullanıcı kararı).* Anonim
  seçenek `Invitation.UserId`'yi nullable yapmayı gerektiriyordu ve iletilen link yanlış kişinin
  elinde güvenilir olurdu. E-postaya bağlı token mevcut tabloyu aynen kullanıyor ve iletilse bile
  işe yaramıyor — daha güvenli **ve** daha küçük değişiklik.
- *Token API cevabında dönüyor (hesap token'larının aksine, onlar yalnız e-postayla çıkar).* Amaç
  zaten linki personele verip WhatsApp'a yapıştırtmak. Güvenli, çünkü bu bir oturum açma bilgisi
  değil: parola belirlemez, giriş yaptırmaz, sahibi olmayan için değersiz.
- *"Güvenilir müşteri" ayrı tablo oldu, `Membership`'e eklenmedi.* Membership = personel; müşteriye
  membership vermek kiracı kapsamını ve yetki çözümlemesini bozardı (müşteri şirket verisini görürdü).
  Müşteri↔şirket ilişkisi bugüne dek **türetilmişti** (ticket'lardan `ListMyCompaniesAsync`); asılacak
  bir satır yoktu, bu yüzden yeni entity gerçek bir ihtiyaç.
- *Güven, davetin kendisine bağlı ve tek kullanımlık.* Kullanıcının 3. kuralı bunu söylüyor: kişi
  tanınıyor olması yetmiyor. Süreklilik isteyen ilişki için ayrı ve **açık** bir eylem var
  ("Onayla + güven") — böylece "her talep onaya düşer mi?" yükü tek tıkla kapatılabiliyor.
- *`ShouldHoldForApprovalAsync` kaydetmiyor (`SaveChanges` çağırmıyor).* Çağıran, ticket'ı ve tüketilen
  token'ı **tek transaction'da** yazıyor; aksi halde başarısız bir submit müşterinin davetini yakardı.
- *Eski iki test silinmedi, yeniden yazıldı.* `Submit_by_a_known_customer_enters_the_pool_directly` ve
  `A_signed_in_customer_request_enters_the_pool_directly...` eski kuralı kodluyordu; yeni kurala
  çevrildi ve **neden değiştiği** docstring'e yazıldı. Test silmek davranış değişikliğinin kaydını da
  silerdi.

**Açık iş (Faz 35 sonrası):**
- Dolaşımdaki eski çıplak `/c/{slug}` linkleri artık onay kutusuna düşürüyor — istenen davranış, ama
  müşterilere daha önce o link verildiyse ilk talepleri onay bekleyecek.
- Onay kutusu ekranında müşterinin e-postası/geçmişi görünmüyor; "güven" kararını vermek için
  yararlı olurdu (ticket detayına gidip bakmak gerekiyor).

### Faz 36 — Talep silme + pano kutu ızgarası ✅ (2026-08-06)

Kullanıcı üç madde bildirdi: (1) ticket'lar silinemiyor, (2) pano şirket bazlı mı kullanıcı bazlı mı,
(3) 6 sütunun yalnız 4'ü görünüyor, ticket'lar diğer sütunlara aktarılamıyor.

- [x] **(1) Silme — backend hazırdı, UI'da buton yoktu.** `DELETE /api/tickets/{id}`,
  `ticket.delete` yetkisi (seed'de Admin+SuperAdmin) ve interceptor'lı soft-delete zaten vardı;
  `frontend/src/lib/tickets.ts` içinde silme mutation'ı, `TicketDetail`'de buton **hiç yazılmamıştı**.
  `useDeleteTicket` + iki adımlı onaylı "Sil" eklendi (silince panoya döner, kanban/liste/onay kuyruğu
  cache'i tazelenir). Canlı doğrulandı: `DELETE` → 204, satır duruyor + `DeletedAt` dolu.
- [x] **(2) Zaten şirket bazlı — kod okundu, değişiklik yapılmadı (kullanıcı kararı).**
  `CompanyService.ListAsync` yalnız üye olunan şirketleri döner; pano tek aktif şirkete scope'lu
  (`useActiveCompany` → `useKanban(companyId)`); navbar'daki seçici yalnız 1'den fazla şirket varsa
  görünür (`Shell.tsx:161`). Yani A'nın yöneticisi C'yi açtığında panoda ikisini birden **görmüyor**.
- [x] **(3) Pano kutu ızgarasına geçti.** Eski düzen `flex … overflow-x-auto` + `w-72` sabit genişlikti:
  6 statüde ~1800px ediyor, ekrana 4'ü sığıyor, kalan ikisi sağda kalıyordu. **Asıl engel görünürlük
  değil sürüklemeydi — HTML5 drag kendi kapsayıcısını otomatik kaydırmaz, yani ekranda olmayan sütuna
  kart bırakmak fiziksel olarak mümkün değildi.** Yeni düzen `grid-cols-[repeat(auto-fill,minmax(15rem,1fr))]`:
  bütün statüler aynı sayfada, yatay kaydırma yok (ölçüldü: `scrollWidth 1382 ≈ innerWidth 1396`).
  Kutu başlığına tıklayınca `col-span-full` ile tam satıra genişliyor ve kartlar genişliğe yayılıyor;
  kapalıyken ilk 3 kart + "+N daha…" önizlemesi gösteriyor.

**Karar/Varsayım (Faz 36):**
- *Kutu ızgarası seçildi (kullanıcı kararı), "hepsi sığsın + daralt/genişlet" önerisi yerine.* Öneri
  sürükle-bırak ergonomisini daha iyi koruyordu; kullanıcı kutu düzenini istedi. **Uyarılan kaybı
  telafi etmek için kapalı kutular da geçerli bırakma hedefi bırakıldı** — kart taşımak için kutuyu
  açmak gerekmiyor, yani asıl şikâyet (ulaşılamayan sütun) düzende de kapanıyor.
- *Aynı anda tek kutu açık.* Açık kutu tam satır kapladığı için iki açık kutu panonun kalanını ilk
  ekranın dışına iterdi — bu düzenin var olma sebebini geri getirirdi.
- *Silme butonu rol kontrolüyle gösteriliyor, efektif yetki sorgusuyla değil.* Seed matrisi
  `ticket.delete`'i Admin'e veriyor; ticket başına `/permissions/effective` çağırmak ağır olurdu.
  Bir admin bu anahtarı birine özel olarak reddetmişse buton görünür ve sunucu 403 döner — kodda
  `ponytail:` notu var, override'lar yaygınlaşırsa efektif sorguya geçilir.
- *Silme soft-delete olarak bırakıldı (değiştirilmedi).* Kullanıcı "silinemiyor" dedi, "kalıcı silinsin"
  demedi; interceptor zaten `DeletedAt` yazıyor, ticket olayları/yorumları denetim izi için duruyor.
  Kalıcı silme istenirse KVKK anonimleştirme yolu (Faz 6) ayrı bir karar.

**Açık iş (Faz 36 sonrası):**
- **Süper admin hiçbir panoyu göremiyor.** `useActiveCompany` şirketi `user.companies`'ten (üyelikler)
  türetiyor; süper adminin üyeliği yok → "Bu kullanıcı bir şirkete bağlı değil". Bu oturumda pano
  ancak impersonation ile görülebildi. Süper admine tüm şirketler arasından seçim sunulmalı.
- Açık kutu durumu kalıcı değil (sayfa yenilenince kapanır); şirket bazında localStorage'a yazılabilir.

### Faz 37 — Süper admin pano erişimi + Faz 35-36 canlıya alındı ✅ (2026-08-06)

- [x] **Süper admin hiçbir panoyu göremiyordu (Faz 36'da açık iş olarak bırakılmıştı) — kapatıldı.**
  `useActiveCompany` aktif şirketi token'ın **üyelik claim'lerinden** türetiyordu. `Membership` yalnız
  Admin/Personel içindir (domain kuralı), yani süper adminin üyeliği yoktur → her şirkete erişebilen
  tek rol "Bu kullanıcı bir şirkete bağlı değil" ekranını görüyordu. Şirket seçici de
  `user.companies.length > 1` ile gizliydi, yani seçim yaparak kurtulması da mümkün değildi.
  Artık `GET /companies` yetkili kaynak (personele üyelikleri, süper admine tüm şirketleri döner —
  `CompanyService.ListAsync` zaten böyle çalışıyordu). Claim'ler yalnız istek uçarken **ilk cevap**
  olarak kullanılıyor, böylece personel ekranları boş durum yanıp sönmüyor; yalnız **id** için, çünkü
  `/me` şirket adı döndürmüyor (`CompanyMembershipInfo(CompanyId, Role)`). Seçici artık kendi karar
  veriyor: 2'den az şirket varsa görünmüyor.
- [x] **Faz 35 + 36 + 37 canlıya alındı.** Kullanıcı sordu: "canlıda tüm değişiklikler güncel değil mi?"
  — **değildi.** Canlı, Faz 34'te (mail + HTTPS) kalmıştı; davet/onay kuralı ve pano/silme
  değişiklikleri deploy edilmemişti. CI (`.github/workflows/ci.yml`) yalnız build+test yapıyor,
  **deploy adımı yok** → auto-deploy yok, elle alınıyor.
- [x] **Deploy'da bir hata yakalandı: boyut karşılaştırması yetersiz.** Faz 34'te fark, dosya
  **boyutuna** bakılarak çıkarılmıştı. Bu turda aynı yöntem `wwwroot/index.html`, `CrmKanban.Api.exe`
  ve `staticwebassets.endpoints.json` dosyalarını **kaçırdı** — içerikleri değişmiş ama uzunlukları
  aynıydı (asset hash'leri aynı karakter sayısında). `index.html` yüklenmeseydi site **eski JS
  paketini** yüklerdi ve hiçbir frontend değişikliği görünmezdi. SHA256 ile yeniden karşılaştırıldı:
  16 değil **19 dosya**. Bundan sonra fark hep içerik hash'iyle alınacak.
- [x] **Deploy yöntemi:** `app_offline.htm` → 19 dosyalık 1 MB zip → panelin "Unzip" akışı
  (`/wwwroot/`'a, "üzerine yaz" + "app pool'u yeniden başlat" işaretli) → zip ve `app_offline` silindi.
- [x] **Canlı doğrulama (uçtan uca):**
  - `https://` 200, `http://` → 307 yönlendirme
  - `index.html` → `assets/index-CrgPuFhP.js` (yeni paket), dosya 200
  - `POST /api/companies/{id}/customer-invites` → **401** (404 değil) → yeni uç canlıda
  - `POST /api/public/form/mermer` → `{"ticketNumber":"MERMER-11","newAccount":true,"pendingApproval":true}`
    → yeni DTO alanı **ve** yeni giriş kuralı çalışıyor; `IntakeTrustService` `CustomerTrusts`
    tablosunu sorgulayabildiği için **`CustomerIntakeTrust` migration'ı canlı DB'ye uygulanmış**
    (uygulanmasaydı eksik tablo hatası dönerdi).

**Karar/Varsayım (Faz 37):**
- *Seçilebilir şirket listesi `GET /companies`'ten okunuyor, claim'lerden değil.* Claim'ler kimlik
  kanıtıdır ama **erişim kapsamının tamamı değildir** — süper adminin kapsamı üyeliklerinde yazmaz.
  Sunucu zaten doğru listeyi veriyordu; hata istemcinin yanlış kaynağa bakmasıydı.
- *Claim'ler yine de ilk cevap olarak duruyor.* Tamamen kaldırmak personelde (çoğunluk) her sayfa
  yüklemesinde kısa bir "şirkete bağlı değil" yanıp sönmesi üretirdi. İsim gerektiren tek yer seçici,
  o da yüklenene kadar hiç render edilmiyor.
- *Faz 34'ün boyut-farkı yöntemi yanlıştı, düzeltildi.* Aynı uzunlukta farklı içerik gerçek bir durum
  (hash'li asset adları sabit uzunlukta). Faz 34 deploy'u yine de doğruydu çünkü orada değişen iki
  dosyanın boyutu da farklıydı — ama yöntem şanslıydı, doğru değildi.

**Açık iş (Faz 37 sonrası):**
- Canlıda `Seed:Demo` hâlâ `true` (Faz 34'ten devreden).
- Canlı onay kuyruğunda bir test talebi duruyor: **MERMER-11 "Canli kural testi"**
  (`crmkanban-kural-test@mailinator.com`) — deploy doğrulaması için açıldı, reddedilebilir.
- Açık kutu durumu hâlâ kalıcı değil (Faz 36'dan devreden).

## Bir sonraki oturum — açık uçlar (spec §18.23-24)

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

- **[Faz 7b] Per-şirket sütun = ilk özelleştirmede global set'i FORK + ticket migrasyonu (klonlama).** Kullanıcı "per-şirket özel sütunlar" seçti. Seçenek A "company statuses global order'a interleave" reddedildi: global Order'lar paylaşımlı (0-5), tek şirketten değiştirilemez; fraktal/rescale ordering karmaşık ve kırılgan. Seçenek B "önce boş own-set, tek sütun ekleyince global'ler kaybolur" reddedildi: mevcut ticket'lar (global StatusId'de) kanban'dan düşerdi. Seçilen: ilk mutasyonda global set tam klonlanır (yeni GUID'ler, aynı category/color/order/terminal), transition grafiği maplenir, şirketin ticket'ları `MigrateStatus` ile klona taşınır. Sonrası tüm ordering lokal → istenilen konuma ekleme temiz, orphan yok. `StatusSet.EffectiveAsync` ("own varsa own, yoksa global") tek predicate; kanban/dropdown/initial-status oradan okur. ponytail ceiling: fork geri alınamaz (global'e dönüş yok) — v1'de gerek yok.
- **[Faz 7b] Yeni sütun transition zincirleme = seed mesh kuralının kopyası, ama biraz daha izinli.** Non-terminal sütun → diğer TÜM sütunlar (non-terminal + terminal); her non-terminal → yeni sütun. Seed "New'e geri dönülemez" nüansını (New target değil) per-şirket board'da uygulamadım — özel board'da tam sürükleme esnekliği daha değerli. State machine zaten test edilmiş; yeni sütun sadece edge ekliyor.
- **[Faz 7b] Sütun yönetimi `status.manage` permission key + Admin baseline.** Alternatif "membership Admin rolü kontrolü" reddedildi: yetki data-katmanında, RBAC UI'ında görünür ve grant/deny edilebilir olmalı (CLAUDE.md). SuperAdmin bypass. Silme guard'ları: sütunda ticket varsa (`status.in_use` 409), son Open sütunuysa (`status.last_open` 409) — initial-status hep bulunmalı.
- **[Faz 7c] "İlk-kez müşteri" = yeni User (invite token üretilen).** `PublicFormService.ResolveCustomerAsync` yeni kullanıcıda inviteToken döner; o durumda `ticket.MarkPendingApproval()`. Bilinen e-posta (mevcut User) → Approved, direkt havuz. Basit ve doğru: "ilk defa havuza giren" = daha önce hesabı olmayan.
- **[Faz 7c] Moderasyon = ticket-seviyesi ApprovalState, ayrı kuyruk tablosu değil.** Default Approved → migration mevcut/staff ticket'larını otomatik geçirir (column defaultValue 0), geri-uyumlu. Kanban/liste `ApprovalState == Approved` filtreler; moderasyon view `Pending`. Approve/Reject yeni TicketEvent tipi EKLEMEDİ (bildirim matrisine dokunmamak için) — Created olayı submit'te zaten yazılıyor, admin "yeni talep" maili alıp moderasyona gidiyor. ponytail ceiling: Rejected müşteriye "reddedildi" bildirimi yok; Created makbuzu pending ticket için de gidiyor (kabul edildi).
- **[Faz 7c] Zero-trust = public upload backend'e alındı (presigned DEĞİL), byte denetimi.** Kullanıcı "backend'e al + magic-byte" seçti. Presigned PUT sunucuya byte göstermez (tech debt #13). Public path artık `IFileStorage.PutAsync` ile API üstünden akıyor; `PublicFileValidator` uzantı+content-type+magic byte üçlüsünü eşliyor, boyut sunucuda cap'li MemoryStream ile ÖLÇÜLÜYOR (client Size'ı güvenilmez). Yalnız pdf/txt/doc/docx. Staff attachment yolu presigned kaldı (scope: "müşteri tarafından"). Magic tablosu + testleri `PublicFileValidator`'da birlikte (config'e bölmedim — güvenlik politikası, kod). ponytail ceiling: docx = PK-zip imzası + .docx uzantısı (içindeki `[Content_Types].xml` denetlenmiyor); txt = NUL/kontrol-karakter sniff, tam UTF-8 decode değil.
- **[UI] Minimalist/modern token seti (indigo/slate), Odoo moru bırakıldı.** `index.css` @theme token'ları tek yerden tüm app'i çeviriyor (primary indigo-600, canvas slate-50, hairline border, gölge yerine border). Public form marka rengi hâlâ Settings'ten override. Tüm 11 ekran yeniden yazılmadı — paylaşılan token + primitives + Shell + dokunulan/yeni ekranlar; sistemik etki token'dan geliyor (SCOPE DISCIPLINE: geniş diff yerine kök).
- **[Deploy/Faz 11] Prod dosya deposu = host diski (LocalFileStorage), kullanıcı seçti (2026-08-03).** "S3 (AWS) değil, Google/Microsoft" ve sonra "hiç ödeme yok / 1-2 gün test edip sileceğim, en optimal olanı seç" dendi. MonsterASP cloud DB nesne deposu içermez → dosya için depo şart. Karar zinciri: GCS (S3-uyumlu, sıfır kod) → Azure Blob (kullanıcı seçti, yazıldı) → ama "ücretsiz + hızlı throwaway" netleşince **host diski** en optimal: bulut hesabı/provizyon yok, ücret yok, secret yok, testten sonra silinecek harici şey yok. `IFileStorage` seam'inin artık **üç gerçek implementasyonu** var (`Files:Provider`): `local` (host diski, seçilen prod default), `azure` (`AzureBlobStorage`, `Azure.Storage.Blobs` 12.29.1 — kredili hesap gelirse hazır), `s3` (dev MinIO / herhangi S3, AWSSDK v4 CRC checksum fix'li). Aktif yol yalnız `PutAsync`/`GetAsync` (Faz 12 proxy) → hepsinde `Presign*` `NotSupportedException`. **LocalFileStorage:** dosyalar `App_Data/uploads` (content root altı, IIS servis etmez → yalnız yetkili API proxy'sinden iner); path-traversal guard (filesystem trust boundary) + 2 test (round-trip, traversal reddi, Api.Tests). ponytail ceiling: tek-instance + host disk kotası; deploy'da `App_Data` korunmalı; çok-instance/kalıcı gerekirse `azure`/`s3`'e geç (tek env). **Mail = Resend (transactional, ücretsiz 3k/ay), kullanıcı domain'i doğrulayacak.** `SmtpEmailSender` değişmez: `Host=smtp.resend.com`, `Port=587` (**465 değil** — .NET `System.Net.Mail` implicit SSL desteklemez, STARTTLS/587 şart), `Username=resend`, `Password=<API key>`, `From=<doğrulanmış domain>`. Resend paylaşımlı-host SMTP bloğu riskini de çözer (Gmail'e göre avantaj). **Kalan:** Resend domain doğrulama (DNS, kullanıcıda) + MonsterASP env + canlı e2e.
- **[Faz 8] Deploy = Docker Compose (api+mssql+minio+nginx), kullanıcı seçti.** API imajı plain HTTP:8080 (TLS reverse-proxy'de); nginx SPA'yı serve + `/api`'yi proxy'ler (same-origin, CORS yok). Migration+seed startup'ta (tek-instance; multi-instance için tech debt #6). Secret'lar `.env`/orchestrator'dan, dosyada değil. `docker compose build` bu ortamda çalıştırılamadı (daemon kapalı) — Dockerfile komutları (`dotnet publish -c Release`, `npm run build`) ve `docker compose config` ayrı ayrı doğrulandı; gerçek imaj build ilk deploy'da (tech debt #25).

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
| 11 | ~~gerçek yükleme/indirme baytları uçtan uca doğrulanmadı~~ → **Faz 12'de kapandı (staff/müşteri yolu):** Docker MinIO'ya karşı upload→store→download bayt round-trip tarayıcıda doğrulandı (`roundTripMatch: true`). Public form yolu da aynı `PutAsync` proxy'sini kullanıyor. Kalan: prod deposu **host diski** (`LocalFileStorage`, seçilen default; `azure`/`s3` de mevcut) — MonsterASP'de upload→download canlı doğrulanmalı. | ✅ kapandı (MinIO); local canlı bekliyor |
| 11b | Depolama artık API-proxy (staff+public); büyük dosyalarda API belleğe cap'li buffer + stream. Çok yüksek hacimde presigned direct-to-S3 tekrar değerlendirilebilir (o zaman browser-erişilebilir S3 endpoint şart). | Düşük |
| 12 | CAPTCHA provider bağlı değil (açılırsa fail-closed). Provider seçilince `CaptchaValidator`'a tek branch. | Orta |
| 13 | ~~Presigned PUT S3'te dosya boyutunu zorlayamaz~~ → **Faz 12'de kapandı:** staff/müşteri yolu artık API-proxy; boyut sunucuda cap'li buffer ile ölçülüyor (client bildirimi güvenilmiyor). Presigned staff yolu tamamen kaldırıldı. | ✅ kapandı |
| 14 | Rate limiter in-memory + per-instance; çok-instance prod'da distributed (Redis vb.) limiter gerekir. | Düşük |
| 15 | Token hash'leme 3. kez kopyalandı (refresh/invite/public-form SHA256). Auth'a bir dahaki dokunuşta `TokenHasher` helper'ına çıkar. | Düşük |
| 16 | SMTP sağlayıcı = **Resend** seçildi (2026-08-03); kod hazır (`SmtpEmailSender`), env: `smtp.resend.com`/587/`resend`/API-key/doğrulanmış-From. Transactional servis olduğundan paylaşımlı-host bloğu/SPF-DKIM-DMARC'ı kendi çözüyor. Kalan: kullanıcı domain doğrulaması (DNS) + canlı gönderim testi. | Orta |
| 17 | Bildirim debounce tick-içi tekilleştirme; gerçek zaman-pencereli çapraz-tip özet mail değil (§14). | Düşük |
| 18 | EmailQueue retry backoff'suz (her tick tekrar dener); üstel backoff + next-retry zamanı eklenebilir. | Düşük |
| 19 | Bildirim worker'ı migration+seed gibi tek-instance varsayar; çok-instance prod'da kuyruk çekişi için satır kilidi/`SKIP LOCKED` gerekir. | Orta |
| 20 | SettingsService cache'siz (unique-key okuma). Hot per-request okuyucu (ör. her istekte branding/limit) çıkarsa cache + değişince invalidate; çok-instance'ta invalidation stratejisi gerekir. | Düşük |
| 21 | Var olan tipli Options (Ticket/Notification/File) hâlâ config'ten; §13 gereği DB Settings'e taşınmalı. Seam hazır; her tüketici dokunulunca geçirilir. | Düşük |
| 22 | Export yalnız CSV; gerçek `.xlsx` yok (bağımlılık ister). İstenirse ClosedXML/EPPlus ile eklenir. | Düşük |
| 23 | KVKK anonimleştirme SuperAdmin only; per-şirket admin'in kendi müşterisinin talebini işlemesi yok (çok-şirketli kullanıcı kimlik çakışması). Kapsamlı çözüm: şirket-scope'lu maskeleme veya membership kaldırma. | Orta |
| 24 | `kvkk.retention_days` saklanıyor ama otomatik saklama-süresi purge/anonimleştirme job'ı yok. | Düşük |
| 25 | Docker imajları bu ortamda build edilmedi (daemon kapalı); Dockerfile komutları + compose config ayrı doğrulandı. İlk deploy'da `docker compose up --build` + e2e stack (login→public form→moderasyon→kanban→upload indir) testi. | Orta |
| 26 | docx magic doğrulaması PK-zip imzası + uzantı ile; içindeki `[Content_Types].xml`/word/ yapısı denetlenmiyor (herhangi bir zip .docx sayılır). Gerçek OOXML doğrulama gerekirse zip entry kontrolü eklenir. | Düşük |
| 27 | ~~Approve/Reject için TicketEvent tipi yok~~ → **Faz 13'te kapandı:** `Approved`/`Rejected` event tipi + matris girdisi (Rejected müşteriye kibar red bildirimi, kritik). Kalan minör: Created makbuzu pending ticket için de gidiyor (kabul). | ✅ kapandı |
| 28 | Sütun fork geri alınamaz (şirket global default'a dönemez) ve fork sonrası yeni global default sütun o şirkete yansımaz. v1'de gerek yok; "varsayılana sıfırla" istenirse eklenir. | Düşük |
| 29 | DevSeeder yeni ApprovalState/pending ticket veya per-şirket sütun demo verisi kurmuyor; demo hep Approved + global set. Moderasyon/sütun akışını demoda görmek için seed'e birkaç pending + örnek özel sütun eklenebilir. | Düşük |
| 30 | Global `/register` ekranı (firma linki olmadan kayıt) Faz 23 sonrası ikincil kaldı: ilişkisiz müşteri portalde yazacak firma göremez. Kaldırılabilir veya "firma linkinizi kullanın" yönlendirmesine çevrilebilir — ürün kararı. | Düşük |
| 31 | Kod doğrulama uçları (`/register`, `/verify`) IP başına `public-form` rate limiter'ı ile korunuyor; deneme sayacı satır bazında. Çok-instance prod'da limiter in-memory (borç #14 ile aynı) — dağıtık limiter gelince kod ucu da kapsanır. | Orta |
| 32 | Lokal `dotnet run` mailleri konsola yazar (`Email:Provider=log`); gerçek Resend gönderimi yalnız docker/prod env'inde. Lokalde de gerçek mail istenirse user-secrets'a SMTP değerleri konmalı. | Düşük |
| 33 | Brevo gönderici adresi `gmail.com` → Brevo From'u `...@<id>.brevosend.com` olarak yeniden yazıyor; Google/Yahoo gönderici uyumu için kendi domainini Brevo > Domains'te DKIM/DMARC ile doğrulayıp `EMAIL_FROM`'u o domaine çek. | Yüksek (üretim öncesi) |
| 34 | AWS'e geçmeden önce MinIO'ya yüklenmiş eski ekler (demo seed + ilk testler) yeni bucket'ta yok → indirilmeye çalışılınca 500 (`NoSuchKey`). Yeni yüklemeler sorunsuz. Gerekirse eski nesneler MinIO'dan S3'e kopyalanır ya da demo veri yeniden seed'lenir. | Düşük |
| 39 | Faz 30'da `TicketQueryService.ListAsync` personelin **her şirketi için ayrı** `ticket.view` sorgusu yapıyor (tipik 1-2 şirket, ucuz). Çok şirketli kullanıcıda N sorgu; gerekirse tek sorguda toplu çözüme çevrilir — **ölçmeden yapma**. | Düşük |
| 40 | ~~**Yetki anahtarı ↔ uygulama yeri eşleşmesi denetlenmiyor.**~~ → **Faz 32'de kapandı:** `PermissionEnforcementAuditTests` her anahtarın Application/Api'de referans edildiğini ya da `IsGlobalOnly` ilan edildiğini + etiket/açıklamasının bulunduğunu doğruluyor; sahte anahtarla negatif doğrulama yapıldı. | ✅ kapandı |
| 41 | ~~Yetki UI'ında "rol varsayılanına döndür" yok~~ → **Faz 31'de kapandı:** `DELETE /permissions/assign` + override varken çıkan "rol varsayılanına dön" düğmesi. | ✅ kapandı |
| 42 | **Soft-delete + `IgnoreQueryFilters` tuzağı üçüncü kez vurdu** (Faz 28 üyelikler, Faz 31 yetki satırları). #36'daki analyzer/inceleme kuralı artık yalnız kiracı filtresini değil, **soft-delete koşulunu** da kapsamalı: filtreyi baypas eden her sorgu ya `DeletedAt == null` yazmalı ya da neden yazmadığını gerekçelendirmeli (ör. `InvitationService`'in tekillik kontrolü). | Orta |
| 36 | **`IgnoreQueryFilters()` sorgu genelinde etkili** — bir join'in tek tarafına yazılsa bile TÜM sorgunun kiracı filtresini kapatır (Faz 28'deki sızıntının kök nedeni buydu). Kodda hâlâ 30+ meşru kullanım var (auth bootstrap, müşteri kendi kaydı). Kalıcı koruma: filtreli set ile filtresiz seti aynı sorguda **join etmeyi** yasaklayan bir analyzer/kod incelemesi kuralı, veya kiracı-scope'lu okumalar için ortak bir yardımcı. Şimdilik yeni servis testleri nöbette. | Orta |
| 37 | Faz 28 düzeltmesi statü tablosunun **tamamını** okuyor (`LoadStatusesAsync`, global 6 + şirket başına fork). Yüzlerce şirkette gereksiz satır gelir; gerekirse sayfadaki statü id'leri + kategori kümesiyle daraltılır. Ölçülmeden yapılmasın. | Düşük |
| 38 | `/api/public/companies` `PROGRESS.md`'de canlı görünüyordu ama Faz 17'de kaldırılmış (404). Doküman düzeltildi; benzer ölü uç kalmadığı taranmadı. | Düşük |
| 35 | ~~**Log gürültüsü:** her domain istisnası log'a `responded 500` diye düşüyor~~ → **Faz 32'de kapandı:** `UseSerilogRequestLogging` exception middleware'in dışına alındı; log artık istemciye giden gerçek statüyü yazıyor. | ✅ kapandı |

## Ortam gereksinimleri

| Gereksinim | Durum |
|---|---|
| .NET 10 SDK (10.0.302) | ✅ kurulu |
| Node 20 + npm | ✅ kurulu |
| SQL Server 2022 (localhost, Windows auth) | ✅ çalışıyor; `CrmKanban` DB migration ile oluşacak |
| JWT signing key (secret) | ❌ Faz 2'de user-secrets: `Jwt:SigningKey` |
| İlk süper admin (env) | ❌ Faz 1/2 seed: `SUPERADMIN_EMAIL`, `SUPERADMIN_PASSWORD` |
| GitHub push yetkisi | Gerekli |
