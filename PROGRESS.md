# CRM + Kanban — Progress

| Alan | Değer |
|---|---|
| Son güncelleme | 2026-08-01 |
| Aktif faz | **Faz 0-15 tamam. 135 test yeşil** (16 domain + 114 application + 5 API/HTTP). **Faz 13-15 (2026-08-01):** checklist reconcile + tüm kalan kod eksikleri kapatıldı — (13) forgot-password + moderasyon audit/bildirim; (14) kanban filtre UI + dosya-eklendi bildirimi; (15) edit bildirimi, şirketten üye çıkarma, mail şablon düzenleme UI, controller/HTTP smoke testleri (WebApplicationFactory), **konfigüre edilebilir public form alanları** (§4.6). Sıradaki: operasyonel deploy sertleştirmesi (TLS/secret/CAPTCHA/SMTP/gerçek S3 — kullanıcı hesapları, ONERILER P0) |
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
- [x] **Public şirket listesi:** `GET /api/public/companies` (anonim) → aktif/arşivsiz şirketler `(id,name,slug)` — kayıt/mesaj için seçici besler. `PublicFormService.ListOpenCompaniesAsync`.
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

## Ortam gereksinimleri

| Gereksinim | Durum |
|---|---|
| .NET 10 SDK (10.0.302) | ✅ kurulu |
| Node 20 + npm | ✅ kurulu |
| SQL Server 2022 (localhost, Windows auth) | ✅ çalışıyor; `CrmKanban` DB migration ile oluşacak |
| JWT signing key (secret) | ❌ Faz 2'de user-secrets: `Jwt:SigningKey` |
| İlk süper admin (env) | ❌ Faz 1/2 seed: `SUPERADMIN_EMAIL`, `SUPERADMIN_PASSWORD` |
| GitHub push yetkisi | Gerekli |
