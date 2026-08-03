# CRM + KANBAN — Gereksinim Checklist

Agent doğrulama listesi. Her madde bağımsız olarak test edilebilir olmalı.

> **Durum (2026-08-01):** Kod uçtan uca çalışıyor (Faz 0-15). **135 test yeşil** (16 domain + 114 application + 5 API/HTTP).
> Bu liste PROGRESS.md + koda karşı doğrulandı. Kalan `[ ]` maddeleri iki gruba ayrılır:
> **(A) operasyonel deploy sertleştirmesi** — kullanıcının kendi bulut hesaplarını gerektirir (kod değil, bkz. ONERILER.md P0);
> **(B) kod eksiği** — açıkça işaretli. Spec (`crm-kanban-mimari.md`) ile çelişen istekler [~] ile gösterildi.

---

## 1. Kimlik Doğrulama (Auth)

- [x] Kullanıcı kayıt (register) ekranı ve endpoint'i — `/register` + `POST /api/auth/register`; public form `/form/:slug`; `/invite` şifre-belirleme
- [x] Giriş (login) / çıkış (logout) — 8080'de doğrulandı
- [x] E-posta doğrulama ile hesap aktivasyonu — davet token'ı = e-posta doğrulaması, e2e doğrulandı
- [x] Şifre sıfırlama / şifre değiştirme akışı — **Faz 13:** `POST /api/auth/forgot-password` (nötr 204) + `/forgot-password` ekranı; reset linki `/invite` set-password akışını yeniden kullanır ve mevcut oturumları iptal eder. change-password zaten vardı
- [x] Müşteriler e-posta aracılığıyla (davet linki) kayıt olabiliyor — e2e doğrulandı
- [x] Oturum / token yönetimi (JWT ~15dk + rotasyonlu refresh) ve yenileme
- [x] Yetkisiz erişimde 401/403 doğru dönüyor — çapraz-tenant/yabancı müşteri 403, kimliksiz 401

## 2. Roller ve Yetkilendirme (RBAC)

- [x] Roller: Süper Admin, Admin, Personel, Müşteri — `RoleType` enum (4 sabit rol; karar: roller kod, rol-yetki MATRİSİ data — PROGRESS Faz 1)
- [x] `permissions`, `role_permissions`, `user_permissions` tabloları mevcut (Permission/RolePermission/UserPermission). `roles` = enum (karar günlüğü)
- [x] Yetkiler rol dışında doğrudan kullanıcıya atanabiliyor — UserPermission Grant/Deny (deny kazanır)
- [x] Tüm yetki kontrolleri permission tablosu üzerinden (PermissionResolver + per-kayıt authz; hard-coded rol yok)
- [x] Yetkiler arayüzden yönetilebiliyor — `/admin/permissions` (katalog + Ver/Reddet)
- [x] Süper Admin şirket oluşturabiliyor — `/api/companies` (ownerAdminId ile)
- [x] Süper Admin bir şirkete admin ve normal kullanıcı atayabiliyor
- [x] Şirket admini kendi şirketine başka admin ve çalışan atayabiliyor — davet akışı
- [x] Admin bir veya birden fazla şirket hesabı oluşturabiliyor — `CanCreateCompany`
- [x] Şirket bazlı veri izolasyonu — DATA katmanı global query filter + testli
- [x] Backend'de her endpoint permission ile korunuyor — `[Authorize]` + servis içi PermissionService / per-kayıt authz
- [x] UI'da yetkisiz buton/menü gizleniyor + API de kontrol ediyor

## 3. Şirket Yönetimi

- [x] Şirket CRUD — oluştur/listele/arşivle. "Sil" = arşivle/soft (karar: veri/ticket korunur)
- [x] Her şirketin kendi Kanban panosu var
- [x] Şirkete kullanıcı ekleme/çıkarma — davet akışı + **Faz 15:** `DELETE /companies/{id}/members/{userId}` + Companies UI "Çıkar" (sahip çıkarılamaz)
- [x] Kullanıcı birden fazla şirkete bağlı olabiliyor (Membership rol şirket bazlı)

## 4. Müşteri Talep Formu (Dış Link)

- [x] Kimlik doğrulama gerektirmeyen public form linki — `/form/:slug`
- [x] Link şirket bazlı (slug→şirket)
- [x] Form gönderiminde ticket oluşuyor
- [x] Form gönderiminde müşteriye onay e-postası gidiyor — `ticket_created` + yeni müşteride `account_invite`
- [x] Spam/rate limit koruması — native ASP.NET fixed-window (IP 5/dk). CAPTCHA seam hazır, **provider bağlı değil** (deploy kararı, ONERILER P0#4)
- [x] Form alanları yönetim panelinden konfigüre edilebiliyor — **Faz 15:** per-şirket `FormField` (metin/uzun metin/sayı/seçim, zorunlu, sıra) → `/admin/form-fields`; public formda dinamik render, submit'te zorunlu doğrulama, değerler ticket'a (JSON) yazılır ve detayda gösterilir

## 5. Ticket Yönetimi

### Müşteri tarafı
- [x] Müşteri kendi ticket'larını listeleyebiliyor — "Taleplerim" (OpenedById-scope)
- [x] Ticket detayına yorum yazabiliyor
- [x] Ticket'a görsel/dosya yükleyebiliyor — **Faz 12** (API-proxy, ticket detay "Ekler")
- [x] Ticket'ı iptal edebiliyor
- [x] Ticket'ı tamamlandı olarak işaretleyebiliyor
- [x] Başkasının ticket'ını göremiyor — authz.ResolveAsync, testli

### Admin / Personel tarafı
- [x] Kendi şirketine ait ticket'ları listeleyebiliyor
- [x] Ticket detayına yorum ve dosya ekleyebiliyor
- [x] Ticket'ı şirket personeline atayabiliyor
- [x] Ticket statüsü değiştirilebiliyor
- [x] Statü listesi sabit kodlanmamış — per-şirket sütun yönetimi (`/admin/columns`, Faz 7b)
- [x] Ticket başlığı, içeriği ve yorumları düzenlenebiliyor
- [x] Ticket ve yorum silinebiliyor (soft)
- [x] Ticket geçmişi / audit log — `TicketEvents` her mutasyonda (moderasyon+dosya dahil, Faz 13/14)

### Kanban
- [x] Ticket'lar statü kolonlarında kart olarak gösteriliyor
- [x] Sürükle-bırak ile statü değişimi — native HTML5 DnD
- [x] Filtreleme — **Faz 14:** kanban filtre kontrolleri (ara / atanan / öncelik). Backend TicketListQuery zaten statü/kategori de destekliyor. **Not:** tarih ve müşteri filtreleri backend contract'ında yok (istenirse eklenir)

## 6. Dosya / Görsel Yükleme

- [x] Yüklenen dosyalar S3 (S3-uyumlu) üzerinde saklanıyor — `IFileStorage`/`S3FileStorage`
- [x] Dosyalar lokal diskte tutulmuyor
- [x] Dosya tipi ve boyut validasyonu — sunucu tarafı (magic-byte public yolda)
- [x] Erişim yetkisi kontrolü — API-proxy download + per-ticket authz (iç-not dosyası müşteriye kapalı). Presigned yerine backend-proxy (Faz 12 kararı)
- [x] S3 konfigürasyonu sistem ayar dosyasından okunuyor — config/env (secret split)
- [x] **Depolama seçildi (Faz 17):** `Files:Provider` = `local` (host diski, ücretsiz, MonsterASP default) / `azure` (Azure Blob) / `s3` (MinIO/AWS). Lokal MinIO bayt e2e + LocalFileStorage unit test (round-trip + path-traversal). Gerçek AWS S3 zorunlu değil.

## 7. E-posta ve Bildirimler

- [x] SMTP / mail servis entegrasyonu — `IEmailSender` seam (dev log / prod SMTP). **Faz 17: Resend bağlandı**, canlı gönderim doğrulandı (EmailQueue Sent + Resend Logs 200). `smtp.resend.com:587` STARTTLS
- [x] Ticket'taki değişikliklerde ticket'ı açana otomatik mail — `TicketEvent` outbox + `NotificationWorker` + `NotificationMatrix`
  - [x] Statü değişimi
  - [x] Yeni yorum
  - [x] Atama değişikliği
  - [x] İçerik/başlık düzenlemesi — **Faz 15:** kullanıcı kararıyla açıldı (checklist §7 > spec §14 default'u); `Edited` matris girdisi + `ticket_edited` şablonu (açan+atanan)
  - [x] Dosya eklenmesi — **Faz 14:** `AttachmentAdded` olayı + matris + şablon (açan+atanan, actor çıkarılır)
- [x] Kayıt/davet maili — `account_invite`/`staff_invite`/`account_verify`/`password_reset` (kuyruk)
- [x] Mail şablonları arayüzden düzenlenebiliyor — **Faz 15:** `/admin/templates` (super admin) konu/gövde editörü + placeholder ipuçları; `/api/email-templates`
- [x] Mail gönderimi kuyruk üzerinden asenkron
- [x] Gönderim hatası loglanıyor ve retry var — retry var; **üstel backoff yok** (teknik borç #18, düşük)

## 8. Raporlama

- [x] Admin: şirket bazında raporlar — `/api/reports/company` (tenant scope)
- [x] Süper Admin: global raporlar — `/api/reports/global`
- [x] Rapor metrikleri: ticket sayısı, statü dağılımı, ort. çözüm süresi, personel yükü
- [x] Tarih aralığı filtresi
- [x] Dışa aktarım — CSV (RFC 4180, BOM'lu). **[ ] .xlsx/PDF yok** (bağımlılık ister; teknik borç #22, düşük)

## 9. Global Ayarlar (Süper Admin)

- [x] İş parametreleri arayüzden yönetilebiliyor — jenerik `Setting` KV + `/settings` ekranı (super admin)
- [x] Mail ayarları — şablonlar `/admin/templates` UI'dan düzenlenir (Faz 15). SMTP **secret'ları** config/env (bilinçli split, §13 — UI'da değil, doğru)
- [~] S3 / depolama ayarları — **config/env (secret, bilinçli split).** UI'da düzenlenmez (doğru davranış)
- [x] Ticket statüleri (per-şirket sütun yönetimi). Öncelikler = enum (sabit küme)
- [x] Rol ve yetki tanımları — `/admin/permissions`
- [x] Form alanları — bkz. §4.6 (Faz 15: `/admin/form-fields`)
- [~] Kod içinde sabit parametre kalmamış — çoğu Settings/config'te; **bazı tipli Options hâlâ config'te** (teknik borç #21, "dokunulunca DB'ye taşı")

## 10. Mimari Gereksinimler

- [x] Parametreler tek config + DB Settings ayrımı (secret=file/env, iş param=DB)
- [x] Clean Architecture (Domain/Application/Infrastructure/Api) — MVC controller ince
- [x] Yetkilendirme RBAC + permission tablosu
- [x] Backend metodları SOLID / SRP
- [x] Business logic service katmanında (controller ince)
- [x] Veri erişimi — DbContext-as-UoW + DbSet (karar: per-entity repo YAGNI, PROGRESS Faz 2)
- [x] UI'da tekrar eden elemanlar component (primitives + Shell + TicketCard)
- [~] Frontend performans — sayfalama var, react-query cache var. **Route-level lazy load / memoization yaygın değil** (P2 iyileştirme, ONERILER #15)
- [x] Tüm sistem REST API üzerinden
- [x] Standart response formatı + HTTP status kodları (tek hata zarfı)
- [x] API dokümantasyonu — OpenAPI/Swagger
- [x] Input validation her endpoint'te — FluentValidation otomatik pipeline
- [x] Veritabanı migration'ları mevcut

## 11. Doğrulama / Test

- [x] Her rol için yetki matrisi testi — PermissionResolver + elevation guard testleri
- [x] Şirket izolasyonu testi
- [x] Mail tetikleme testleri — fan-out/matris/iç-not/self-notify testleri
- [x] Dosya yükleme ve S3 erişim testi — upload boyut/iç-not/download testleri
- [x] API endpoint testleri — **Faz 15:** `CrmKanban.Api.Tests` (`WebApplicationFactory` + InMemory) 5 HTTP smoke: /health, protected→401, forgot-password→204, bad login→401 zarf, süper admin login→/me. Daha fazlası eklenebilir (ör. upload bad-magic→400)

---

## Kalan işler — özet

**A) Operasyonel deploy sertleştirmesi (ONERILER.md P0):**
- ✅ **Faz 17'de kapandı:** Docker imaj build + stack e2e (`up.ps1`) · SMTP provider (Resend, canlı) · depolama (host diski `local`, ücretsiz — gerçek AWS S3 zorunlu değil).
- Kalan (kullanıcı/operasyonel): prod secret store · TLS/HTTPS (MonsterASP SSL sağlıyor) · CAPTCHA provider · çok-instance migration ayrımı (tek-instance'ta gerekmez).

**B) Kod eksikleri — Faz 13-15'te kapatıldı ✅:**
1. ~~Form alanları konfigüre (§4.6/§9)~~ → Faz 15 (`/admin/form-fields`).
2. ~~Mail şablon düzenleme UI (§7/§9)~~ → Faz 15 (`/admin/templates`).
3. ~~Controller/HTTP smoke testleri (§11)~~ → Faz 15 (`CrmKanban.Api.Tests`, 5 test).
4. ~~Şirketten üye çıkarma (§3)~~ → Faz 15.
5. ~~forgot-password (§1) / kanban filtre (§5) / dosya+edit bildirimi (§7) / moderasyon audit (§7)~~ → Faz 13-14.

**Kalan P2 (düşük, iyileştirme — istenirse):** route-level lazy load, xlsx/PDF export, dağıtık rate limit/worker (çok-instance), i18n backend mesajları. Bkz. ONERILER.md P2.

**~) Spec override (yapıldı):** İçerik/başlık düzenlemesinde mail (§7) — spec §14 `Edited→kimse` idi; kullanıcı kararıyla açıldı (Faz 15).
