# Geliştirme Önerileri & Yol Haritası

| | |
|---|---|
| Tarih | 2026-07-31 |
| Bağlam | Faz 0-12 tamam (portal + dosya eki API-proxy dahil). 101 test yeşil. Stack Docker'da canlı doğrulandı (8080). Bu doküman "deploy öncesi son hâli"nden sonrasına dair önceliklendirilmiş öneri listesidir. |
| İlgili kayıtlar | `PROGRESS.md` (tarihsel faz/karar günlüğü + teknik borç #1-29), `crm-kanban-mimari.md` (spec Rev 2) |

> **Güncelleme (2026-08-01, Faz 13-15):** Aşağıdaki P1 maddeleri kapandı — moderasyon audit/bildirim (#8), controller/HTTP smoke testleri (#14). Ayrıca checklist'teki tüm kod eksikleri kapatıldı: forgot-password, kanban filtre, dosya+edit bildirimi, şirketten üye çıkarma, mail şablon UI, **konfigüre edilebilir form alanları**. 135 test yeşil. **P0'ın tamamı hâlâ açık** (operasyonel — kullanıcı hesapları gerekir). Ayrıntı: `PROGRESS.md` Faz 13-15 + `crm-kanban-checklist.md`.

Bu dosya kararların **gerekçesini kesmez**; kod minimal tutuldu ama neyin neden ertelendiği burada. Öncelikler: **P0 = ilk gerçek deploy'dan önce zorunlu**, **P1 = ilk hafta içinde**, **P2 = iyileştirme/ölçek**.

---

## P0 — İlk gerçek deploy'dan ÖNCE (bloklayıcı)

Bunlar yapılmadan prod'a çıkılırsa ya güvenlik açığı ya da "çalışmıyor" durumu var.

### 1. Docker imajlarını gerçekten build edip stack'i uçtan uca test et
- **Durum:** `docker compose config` + `dotnet publish -c Release` + `npm run build` ayrı ayrı doğrulandı; **gerçek `docker compose up --build` çalışmadı** (bu makinede Docker daemon kapalıydı — teknik borç #25).
- **Yap:** `cp .env.example .env` → secret'ları doldur → `docker compose up --build`. Doğrula: login → public form (`/form/tekstil`) → moderasyon onayı → kanban → dosya yükle/indir. MinIO console `:9001`, bucket oluştu mu bak.
- **Riskli noktalar:** (a) `USER $APP_UID` non-root ile `/app` yazma izni; (b) MSSQL healthcheck `mssql-tools18` yolu imaj sürümüne bağlı; (c) API startup'ta DB'ye bağlanamazsa retry yok → healthcheck timing'i önemli.

### 2. JWT signing key + secret yönetimi
- **Durum:** Dev'de user-secrets; prod env bekliyor (teknik borç #3). `.env.example`'da placeholder var.
- **Yap:** `JWT_SIGNING_KEY` = `openssl rand -base64 48` (min 32 byte). **Repoya asla girmesin** (`.env` gitignore'da). Prod'da orchestrator secret store (K8s Secret / Docker secret / cloud KMS). Aynısı `MSSQL_SA_PASSWORD`, `S3_SECRET_KEY`, `SUPERADMIN_PASSWORD` için.

### 3. TLS / HTTPS
- **Durum:** API konteyner içinde plain HTTP:8080; nginx `web` servisi 80'de, TLS yok. `Program.cs` `UseHttpsRedirection()` proxy arkasında no-op (https portu bilmiyor).
- **Yap:** nginx'e gerçek sertifika (Let's Encrypt / kurumsal CA) veya önüne bir TLS-terminating proxy (Traefik/Caddy/ALB). HSTS ekle. Public form kişisel veri taşıdığından bu **P0**.

### 4. CAPTCHA sağlayıcısını bağla (veya bilinçli kapalı bırak)
- **Durum:** `appsettings.Production.json`'da `Captcha:Enabled=false` (provider'sız `true` fail-closed olup public formu tamamen bloklar). Seam hazır: `CaptchaValidator` (teknik borç #12).
- **Yap:** Turnstile veya reCAPTCHA seç → `CaptchaValidator`'a tek branch + client'a widget → `Enabled=true`. Bot koruması olmadan public form spam'e açık. Rate limiter var ama tek başına yetmez.

### 5. Gerçek S3/MinIO bucket ile bayt uçtan uca doğrula — kısmen kapandı (Faz 12)
- **Durum:** Docker MinIO'ya karşı staff/müşteri upload→store→download bayt round-trip **tarayıcıda doğrulandı** (`roundTripMatch: true`, teknik borç #11 MinIO için kapandı). Yol artık API-proxy (presigned kaldırıldı).
- **Kalan:** Prod'da **gerçek AWS S3** (MinIO değil) ile aynı round-trip + iç-not dosyası müşteriye kapalı doğrulaması. Bucket **private** kalmalı (public-read ASLA). Not: proxy yol tarayıcıya S3 host'u açmaz → private bucket yeterli, presigned gerektirmez.

### 6. SMTP sağlayıcısı (bildirim gerçekten gitsin)
- **Durum:** Dev'de `DevLogEmailSender` (log). Prod `Email:Provider=smtp` seam hazır (teknik borç #16).
- **Yap:** SMTP host/port/user/pass (env) + SPF/DKIM/DMARC. Aksi hâlde davet linkleri, ticket bildirimleri, "kayıt bağlantısı" hiç ulaşmaz — onboarding kırılır. Şu an public form invite token'ı response'ta dönüyor (dev kolaylığı); mail gelince kaldırılmalı.

### 7. Migration'ı startup'tan ayır (çok-instance)
- **Durum:** `Program.cs` startup'ta `MigrateAsync` + seed çalıştırıyor — tek instance için iyi, çok instance'ta yarış (teknik borç #6).
- **Yap:** Tek instance kalacaksa dokunma. Ölçeklenecekse migration'ı ayrı bir init-container/job'a al (`dotnet ef database update` veya idempotent migration bundle), app instance'ları sadece çalışsın.

---

## P1 — İlk hafta (güvenlik sertleştirme + yeni özelliklerin tamamlanması)

### 8. Bu session'da eklenen özelliklerin bilinen tavanları

| Konu | Şu an | Öneri | Dosya |
|---|---|---|---|
| Moderasyon audit/bildirim | Approve/Reject sadece state çeviriyor, `TicketEvent` yok → audit ve bildirim üretmiyor; Rejected müşteriye bildirilmiyor; Created makbuzu pending ticket için de gidiyor (teknik borç #27) | `Approved`/`Rejected` event tipi + `NotificationMatrix` girdisi. Rejected'da müşteriye kibar "talebiniz işleme alınamadı" | `TicketCommandService.ApproveAsync/RejectAsync`, `Enums.cs`, `NotificationMatrix.cs` |
| docx magic doğrulama | PK-zip imzası + `.docx` uzantısı yeterli sayılıyor; herhangi bir zip .docx geçer (teknik borç #26) | Gerekirse zip entry kontrolü (`[Content_Types].xml` + `word/document.xml`) | `PublicFileValidator.cs` |
| Sütun fork geri alınamaz | Şirket global default'a dönemez; fork sonrası yeni global default sütun o şirkete yansımaz (teknik borç #28) | "Varsayılana sıfırla" gerekirse: company statülerini soft-delete + ticket'ları global'e migrate | `StatusManagementService.cs` |
| ~~Staff dosya yükleme UI~~ | **Faz 12'de tamamlandı:** ticket detay "Ekler" bölümü (API-proxy yükleme+indirme). Presigned yerine backend-proxy (tarayıcıdan çalışan tek yol; bkz. PROGRESS Faz 12). Kalan yalnız **yorum-seviyesi** ek (şu an ticket-seviyesi). | (kapandı) | `TicketDetail.tsx`, `tickets.ts`, `AttachmentService.cs`, `TicketsController.cs` |

### 9. ~~Presigned PUT'ta gerçek boyut zorlaması (staff yolu)~~ → Faz 12'de kapandı
- **Durum:** Staff/müşteri yolu da backend-proxy'ye çekildi (public gibi); boyut sunucuda cap'li buffer ile ölçülüyor. Presigned staff yolu kaldırıldı. Teknik borç #13 kapandı.

### 10. Rate limiter'ı dağıtık yap
- **Durum:** Native ASP.NET fixed-window, **in-memory + per-instance** (teknik borç #14). Çok instance'ta limit instance başına.
- **Yap:** Tek instance kalacaksa dokunma. Ölçekte Redis tabanlı distributed limiter.

### 11. Bildirim worker'ı çok-instance güvenliği
- **Durum:** Worker tek-instance varsayar; çok instance'ta aynı `EmailQueue` satırını iki worker çekebilir (teknik borç #19). Ayrıca retry backoff'suz (teknik borç #18).
- **Yap:** `UPDATE ... WITH (READPAST/ROWLOCK)` veya `SKIP LOCKED` benzeri satır kilidi; retry'a üstel backoff + `NextRetryAt`.

### 12. KVKK anonimleştirme kapsamı
- **Durum:** Sadece SuperAdmin; per-şirket admin kendi müşterisinin talebini işleyemiyor (çok-şirketli kimlik çakışması — teknik borç #23). Otomatik saklama-süresi purge job'ı yok (teknik borç #24).
- **Yap:** Şirket-scope'lu maskeleme veya membership kaldırma; `kvkk.retention_days` için zamanlanmış job.

### 13. `TokenHasher` helper'ı çıkar
- **Durum:** SHA256 token hash'leme 3. kez kopyalandı (refresh / invite / public-form — teknik borç #15).
- **Yap:** Auth'a bir sonraki dokunuşta tek helper'a çıkar (küçük, düşük risk).

### 14. Controller-seviyesi entegrasyon testleri
- **Durum:** 109 test var ama çoğu servis/domain seviyesinde; `Program.cs` `public partial` (WebApplicationFactory'ye açık) ama HTTP-seviyesi test yok.
- **Yap:** Yeni endpoint'ler için birkaç `WebApplicationFactory` smoke: `POST /companies/{id}/statuses` (403 gate), `POST /tickets/{id}/approve`, `POST /public/form/{slug}/upload` (bad magic → 400). Auth + pipeline + serileştirmeyi bir arada doğrular.

---

## P2 — İyileştirme, UX, ölçek

### 15. UI cilası (kalan ekranlar)
Bu session token seti + Shell + primitives + Kanban + yeni ekranlar minimalist yapıldı; **tüm 11 ekran yeniden yazılmadı** (bilinçli — sistemik etki token'dan geliyor). Elden geçirilebilecekler:
- `Dashboard.tsx`, `Settings.tsx`, `admin/*` — hâlâ `text-slate-*`, `bg-white`, `shadow-sm` gibi eski sınıflar var; token'lara (`text-muted`, `bg-surface`, border-öncelikli) çevrilebilir.
- **Erişilebilirlik:** kanban DnD klavye ile yapılamıyor (sadece pointer); sütun taşıma butonlarına `aria-label` var ama board sürükleme için klavye alternatifi yok.
- **Loading/empty state'ler:** çoğu "Yükleniyor…" düz metin; skeleton/spinner tutarlılığı.
- **Toast/bildirim:** mutasyon başarıları sessiz; hafif toast sistemi UX'i iyileştirir.

### 16. i18n mesaj kataloğu
- **Durum:** FluentValidation hataları OS kültürüne göre (dev'de Türkçe) geliyor; frontend `messages.ts` katalogu var ama backend validation mesajlarıyla tam birleşmemiş (teknik borç #7).
- **Yap:** Backend validation mesajlarını da kod-tabanlı yap (kültürden bağımsız), frontend katalogla eşle.

### 17. Arama & rapor ölçeklenmesi
- Ticket araması `LIKE '%term%'` — sargable değil, büyük tabloda tarama (teknik borç #8). Ölçekte full-text index.
- Rapor agregasyonu bellek içi LINQ (teknik borç); hacim artarsa `GROUP BY` SQL'e it.

### 18. DevSeeder zenginleştirme (opsiyonel)
- Şu an iki şirket (tekstil/mermer), her biri 9 approved + 1 pending ticket, global sütunlar.
- İstenirse: her şirkete birer özel pipeline sütunu (Tekstil "Numune Gönderildi", Mermer "Kesim/Cila") + birkaç yorum/iç-not + attachment örneği → demo daha zengin. (Kullanıcıya soruldu, yanıt beklemede.)

### 19. Config: tipli Options → DB Settings taşıma
- **Durum:** Ticket/Notification/File options hâlâ config'ten; §13 bunları "iş parametresi = DB" sayıyor (teknik borç #21). Seam (`SettingsService`) hazır.
- **Yap:** Her tüketici dokunulduğunda tek tek DB Settings'e geçir (toplu değil — gereksiz geniş diff). `SettingsService` cache'siz; hot okuyucu çıkarsa cache + invalidation (teknik borç #20).

### 20. Export .xlsx
- **Durum:** Sadece CSV (RFC 4180, bağımlılıksız — teknik borç #22).
- **Yap:** Gerçek `.xlsx` istenirse ClosedXML/EPPlus.

---

## Repo/süreç hijyeni

- **Branch koruma:** `main` doğrudan push'a açık (teknik borç #4). PR akışı + CI gate (zaten `.github/workflows/ci.yml` var) + en az 1 review kuralı.
- **Repo klasör adı:** `Yeni klasör` — ASCII-dışı + boşluk; bazı CI/araç yol sorunu çıkarabilir (teknik borç #2). Yeniden adlandırma düşük öncelik ama Docker/CI'ya taşımadan önce değerlendir.
- **Bu değişiklikler commit edilmedi.** Session boyunca hiçbir şey push edilmedi. Bir feature branch'te toplayıp PR açmak mantıklı: `feat/kanban-columns-moderation-deploy`.

---

## Özet öncelik sırası

1. **P0 (deploy bloklayıcı):** Docker e2e → secret/JWT → TLS → CAPTCHA → S3 bayt e2e → SMTP → migration ayrımı.
2. **P1 (ilk hafta):** moderasyon audit/bildirim → docx derin doğrulama → staff upload UI → presigned boyut → dağıtık rate limit → worker çok-instance → KVKK kapsam → controller testleri.
3. **P2 (iyileştirme):** UI cilası/erişilebilirlik → i18n → arama/rapor ölçek → config→DB → xlsx → repo hijyeni.

**Tek cümlelik durum:** İşlevsel olarak uçtan uca çalışıyor ve demo edilebilir; "gerçek internete açık prod" için eksik olan şey kod değil, **operasyonel sertleştirme** (TLS, secret, CAPTCHA, SMTP, gerçek S3/Docker e2e).
