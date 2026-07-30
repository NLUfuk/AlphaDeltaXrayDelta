# CRM+KANBAN — Mimari & Uygulama Spec'i

> Multi-tenant helpdesk/ticket + kanban sistemi. Müşteriler dış form linkiyle talep açar,
> şirketler (admin + personel) ticket'ları yönetir, süper admin global kontrol eder.
> Bu doküman agent'a verilecek ana spec'tir. Fazlar §17'de, kararlar §18'de.
> Kod her zaman bu dokümanla senkron tutulur; ayrıştığı yerler PROGRESS.md'de kaydedilir.
>
> **Rev 2** — Açık uçlu noktalar karara bağlandı. Her karar §18'de "varsayıldı — onay bekliyor"
> olarak listelidir; onay gelince bu doküman güncellenir.

---

## 1. Amaç

Şirketlerin, müşterilerinden gelen talepleri (ticket) yönettiği **multi-tenant**, rol +
permission tabanlı, kanban'lı destek/CRM sistemi. Müşteri dış bir linkten formu doldurup ticket
açar; ticket ilgili şirketin panosuna düşer; personel üstlenip çözer; tanımlı olaylarda ilgili
kişilere e-posta gider. Süper admin sistemi ve global ayarları arayüzden yönetir.

## 2. Karar İlkeleri

Belirsiz noktalarda kullanılan üç kural (§18'deki kararlar bunlarla verildi):

1. **En güvenli varsayılan** — yanlış çıkarsa en az zarar veren seçenek.
2. **Geri dönüşü ucuz olan** — kararı değiştirmek migration gerektirmesin.
3. **Kararı ayara çevir** — brief zaten "tüm ayarlar arayüzden yönetilebilecek" diyor. Varyasyon
   ima edilen yerde karar Settings'e taşınır, varsayılanı biz seçeriz. Ama her şey ayar
   yapılmaz — yalnızca brief'in varyasyon ima ettiği yerler (SCOPE DISCIPLINE).

## 3. Teknoloji Kararları

| Konu | Karar | Not |
|---|---|---|
| Backend | **.NET 8 (LTS), Clean Architecture** | ÖNERİ (§18.6). v3'ün çalışan katman yapısı taşınır, craft hataları düzeltilerek (§4) |
| ORM / DB | EF Core + **MSSQL** | v3 ile aynı |
| Auth | **JWT (~15dk) + refresh (DB'de hash, rotasyonlu)** | `[Authorize]` + policy + permission-based authorization (§7) |
| ID'ler | **GUID** (tüm PK) | Ticket'ın ayrıca insan-okur numarası var (§11) |
| Dosya | **S3-uyumlu** (AWS S3 / MinIO) | Bucket public DEĞİL, presigned URL (§12) |
| E-posta | **SMTP + kuyruk** (arka plan worker) | İstek döngüsünde mail YOK; şablonlar DB'de (§14) |
| Frontend | **React 19 + Vite + TanStack Query v5 + Tailwind** | Component disiplini §4.2/§4.3 |
| Loglama | Serilog | v3'ten korunur |
| Validation | FluentValidation (otomatik pipeline filtresi) | |
| İletişim | **Tüm sistem REST API üstünden** | Brief gereği |

> **"nvc mimarisi" yorumu:** Büyük olasılıkla MVC yazım hatası. "Tüm sistem REST API ile
> iletişir" + "component tabanlı UI" ile birlikte kastedilen: **katmanlı backend (Clean
> Architecture) + React SPA, aralarında REST.** Klasik sunucu-render MVC değil.

## 4. Eski Projeden Taşınacak Düzeltmeler ( geri bildirimi — ÖDÜL KOŞULU)

Üç somut günah, üç somut kural. PROGRESS.md'de ayrı blokla, dosya referanslarıyla takip edilir —
review'da tek tek gösterilebilir olmalı.

### 4.1 Backend SOLID ("kodlar SOLID'e uygun değil")
- **Thin controller:** HTTP al, DTO doğrula (otomatik filtre), servisi çağır, sonucu dön. İş
  mantığı controller'da YOK.
- **Tek metot = tek iş** (brief'in kendi cümlesi). Bir metot hem doğruluyor hem kaydediyor hem
  mail atıyorsa → böl. Orkestrasyon ayrı, iş ayrı.
- **Bağımlılıklar abstraction üzerinden** (DI), somut sınıfa değil. Repository + UoW.
- **Fat service yok:** şişen servisi sorumluluk ekseninde parçala (`TicketCommandService` /
  `TicketQueryService` / `TicketAuthorizationService`).
- SCOPE DISCIPLINE geçerli: soyutlama yalnızca gerçek/spec'lenmiş varyasyon noktasında. Tek
  implementasyonlu "ihtimal" interface'i açma.

### 4.2 UI componentizasyonu ("yeterince component'e bölünmemiş")
- Tekrar eden her parça component: buton, input, badge, modal, kart, tablo satırı, boş-durum,
  yükleme iskeleti. İki yerde aynı markup → tek component.
- **Atomik → bileşik hiyerarşi:** primitives (Button, Input, Badge) → bileşenler (TicketCard,
  StatusBadge, CommentItem) → ekranlar. Ekran markup üretmez, bileşen dizer.
- **Performans:** uzun listelerde sanallaştırma, TanStack Query cache + invalidation,
  memo/`select` ile gereksiz render kesme.

### 4.3 Durum/hata mesajlarının ortaklaştırılması (en spesifik düzeltme)
Status/state kaynaklı metinler ve hata mesajları component içinde string olarak **YAŞAMAZ**.
- **Mesaj kataloğu:** status kodu → etiket/renk/açıklama; hata kodu → kullanıcı mesajı. Tek
  kaynak, anahtar bazlı (i18n'e hazır). Component katalogdan *tüketir*.
- **Kod semantik kategoriye bakar, görünen isme asla.** Etiketler süper admin tarafından
  değiştirilebilir (§13); mantık ve raporlar `open/pending/answered/waiting/closed/cancelled`
  kategorisine bakar.
- **API hataları tek yerden:** backend tutarlı zarf döner (`{ code, message, details }`);
  frontend'de tek hata-eşleme katmanı bunu mesaja çevirir. Her component kendi try/catch metnini
  yazmaz.

## 5. Katman Mimarisi

```
frontend (React SPA)  →  API (Controllers, JWT+permission auth, composition root)
                          → Infrastructure (DbContext, Repos, Migrations, Seed, S3, SMTP)
                          → Application (Services, DTOs, Validators, Abstractions, MessageCatalog)
                          → Domain (Entities, Enums, davranış metotları, state machine — bağımsız)
```

v3'ten taşınan iyi pratikler: private setter + davranış metotlarıyla encapsulation, thin
controller, DTO ile domain sızıntısını engelleme, repository + DI abstraction'ları, idempotent
seed, tek migration zinciri.

## 6. Multi-Tenancy

- **Tek DB + her ilgili tabloda `CompanyId`** ("şirket" = tenant birimi).
- EF Core **global query filter** ile otomatik izolasyon. Bir şirketin verisi diğerine sızmaz.
- `ICurrentUserService`: JWT'den `UserId`, roller, erişilebilir `CompanyId`'ler, permission seti.
- **SuperAdmin filtreyi bypass eder.**
- Bir admin **birden fazla şirkete** sahip olabilir; erişim Membership'ten doğrulanır, istekle
  şirket değiştirilemez.

## 7. RBAC + Permission Modeli (brief'in kalbi)

Brief: "RBAC + permission tablosu + **rol dışında kullanıcılara yetki atanabilmesi** + arayüzden
yetki verebilme." İki katmanlı yetki:

1. **Rol → permission** (`RolePermissions`): rolün taban yetkileri.
2. **Kullanıcı → permission** (`UserPermissions`): role EK ya da rolden İSTİSNA. `Type` =
   `Grant`/`Deny`, `CompanyId` (nullable = global).

**Kapsam zorunlu — iki aşamalı kontrol:**
(a) kullanıcının bu yetkisi var mı? (b) bu KAYDA erişim kapsamı var mı?
(b)'yi atlamak bu tür projelerdeki 1 numaralı açık → kapsam **DATA katmanında** (query filter)
garanti altına alınır, sadece uygulama if'lerine güvenilmez.

**Yetki atama yetkisi (§18.4):**
- Süper admin: global, sınırsız.
- Admin: **yalnızca kendi şirketinde** ve **yalnızca kendisinde olan yetkileri** atayabilir.
  ("Sahip olmadığın yetkiyi veremezsin" — yetki yükseltme açığını kapatan kural.)

Permission'lar anahtar bazlı, gruplu: `ticket.view` `ticket.edit` `ticket.delete` `ticket.assign`
`ticket.status.change` `comment.internal` `report.company` `report.global` `settings.manage`
`user.invite` `permission.assign` …

## 8. Roller & Yetki Matrisi

Roller: `SuperAdmin`, `Admin` (şirket sahibi/müdürü), `Personel` (şirket çalışanı, ticket
atanan), `Customer` (dış müşteri).

> **Personel rolü neden var (§18.2):** Brief roller olarak süper admin/admin/müşteri sayıyor, ama
> "ticket'lar şirket **personellerine** atanabilir" cümlesi 4. rolü zorunlu kılıyor — atanan kişi
> login olup işini görmeli.

| İşlem | SuperAdmin | Admin | Personel | Customer |
|---|---|---|---|---|
| Tüm şirketler / global rapor | ✔ | ✖ | ✖ | ✖ |
| Global ayarları yönetme | ✔ | ✖ | ✖ | ✖ |
| Şirket oluşturma | ✔ | ✔ | ✖ | ✖ |
| Şirket içi ticket görme | ✔ | ✔ (kendi şirketi) | ✔ (şirket kuyruğu) | kendi açtığı |
| Ticket açma | ✔ | ✔ | ✔ | ✔ (dış form) |
| Personele atama | ✔ | ✔ | ✖ | ✖ |
| Statü değiştirme | ✔ | ✔ | ✔ (atanan ise) | kendi ticket'ı, yalnız İptal/Tamamlandı, terminal değilken |
| Başlık/içerik/yorum düzenle-sil | ✔ | ✔ (kendi şirketi) | ✖ | ✖ |
| Yorum + dosya yükleme | ✔ | ✔ | ✔ | ✔ (kendi ticket'ı) |
| **İç not** (müşteriye görünmez) | ✔ | ✔ | ✔ | ✖ (göremez) |
| Şirket bazlı rapor | ✔ | ✔ | ✖ | ✖ |
| Personel/admin davet etme | ✔ | ✔ (kendi şirketi) | ✖ | ✖ |
| Kanban panosu | ✔ | ✔ | ✔ | ✖ (düz liste görür) |

> **Kapsam kuralı:** `ticket.delete` yetkisi olması başka şirketin ticket'ını silebilmek demek
> değildir. Yetki her zaman şirket kapsamıyla birlikte değerlendirilir (§7).

## 9. Auth, Kayıt ve Davet

**Self-servis kayıt yalnızca `Customer` içindir (§18.1).** Admin/Personel asla self-servis
kayıt olamaz — aksi hâlde biri kendini admin yapar.

- **Admin hesabı:** süper admin oluşturur. Admin sonra kendi şirket(ler)ini kendi açar (brief).
- **Personel:** admin **e-posta ile davet eder** → davet linkiyle şifre belirler, o şirkete
  scope'lu `Personel` rolü alır.
- **İkinci admin:** mevcut admin davet edebilir (şirket tek kişiye bağlı kalmasın).
- **Müşteri:** dış formdan talep açınca e-posta ile **şifre belirleme/doğrulama daveti** gider
  (§18.7 — magic link değil, klasik kayıt + davet linki).
- Şifreler ASP.NET Core Identity `PasswordHasher`. JWT claims: `sub`, roller, erişilebilir
  `company_id`'ler, `email`, `name`. Refresh token DB'de hash'li, rotasyonlu.
- **İlk süper admin:** seed ile, env'den okunur, ilk girişte şifre değiştirme zorunlu.

## 10. Public Form (dış link)

- **Şirket başına sabit public slug:** `/form/{company-slug}` → ticket doğru şirkete düşer.
- Giriş yapmamış müşteri doldurabilir. Gönderince: e-posta eşleşen kullanıcı varsa ona bağlanır;
  yoksa taslak müşteri + davet maili (§9).
- **İlk formda dosya eklenebilir** (§18.13) — ekran görüntüsü ekleyemeyen destek formu yarım.
- **Zorunlu:** rate limiting + CAPTCHA (kimliksiz endpoint → bot/spam koruması).
- KVKK aydınlatma metni + onay kutusu (§16).

## 11. DB Şeması

Tümü GUID PK, `CreatedAt`/`UpdatedAt`, soft-delete `DeletedAt`.

**Companies**: Id, Name, Slug (form linki), OwnerAdminId, IsActive, **ArchivedAt** (silme yerine
arşiv — §18.20)
**Users**: Id, Email (global unique), FirstName, LastName, PasswordHash, IsActive
**Memberships**: Id, UserId, CompanyId, Role (Admin/Personel) — çoka-çok
**Roles / Permissions / RolePermissions / UserPermissions** (§7)
**Tickets**: Id, **Number** (şirket başına artan, önekli — `ACME-1042`, format ayardan), CompanyId,
OpenedById, AssignedToId (nullable), StatusId, CategoryId (nullable), **Priority** (enum, vars.
Normal), Title, Body, **FirstResponseAt**, **ResolvedAt**, ClosedAt
**TicketStatuses**: Id, CompanyId (nullable = global default), Name, **Category** (enum:
open/pending/answered/waiting/closed/cancelled), Color, Order, IsTerminal
**StatusTransitions**: Id, FromStatusId, ToStatusId, AllowedByPermissionKey
**TicketCategories**: Id, CompanyId, Name, IsActive (rapor kırılımı için)
**Comments**: Id, TicketId, AuthorId, Body, **IsInternal**, **EditedAt** (nullable),
**Source** (enum: Web/Email — email-to-ticket v2 için hazır alan, §18.16)
**CommentRevisions**: Id, CommentId, OldBody, EditedById, EditedAt (düzenleme geçmişi — §18.2)
**Attachments**: Id, TicketId, CommentId (nullable → ilk formda ticket'a doğrudan), S3Key,
FileName, ContentType, Size, UploadedById
**TicketEvents** (audit + rapor + mail kaynağı): Id, TicketId, ActorId, EventType, OldValue,
NewValue, CreatedAt
**Settings**: Id, Key, Value(json), Type, Group, UpdatedById
**EmailTemplates**: Id, Key, Subject, Body(html), IsActive
**EmailQueue**: Id, ToEmail, TemplateKey, Payload(json), Status, Attempts, SentAt
**UserNotificationPrefs**: Id, UserId, EventType, Enabled (kritik-olmayan mailleri kapatma)
**RefreshTokens**: Id, UserId, TokenHash, ExpiresAt, RevokedAt
**AuditLogs**: Id, ActorId, Action, Detail (yetki/ayar değişiklikleri)

## 12. Ticket State Machine + Dosya

Statüler **veri-güdümlü** (`TicketStatuses`) — brief "vb." diyor ve süper admin arayüzden
yönetecek. **Kod kategoriye bakar, isme asla** (§4.3).

- Müşteri: yalnız **İptal** ve **Tamamlandı**, ve yalnızca ticket **terminal değilken**. Admin
  kapattıysa müdahale edemez (§18.10).
- **Kapanan ticket yeniden açılabilir: 7 gün** (ayardan). Yoksa müşteri kopya ticket açar.
- Admin/Personel: `StatusTransitions` + permission'a göre.
- **Kanban'da kart taşıma = statü değişimi** → aynı geçiş kuralları + permission. Geçersiz geçiş
  `DomainException`.
- Domain davranışları: `ChangeStatus()`, `Assign()`, `Cancel()`, `Complete()`, `Reopen()`, `Edit()`.
- **Müşteri kendi yorumunu düzenleyemez/silemez** (§18.12) — yorumlar kayıttır.
- **Admin düzenleme/silme:** brief gereği yetkili, ama izsiz değil — silme **soft delete**,
  düzenleme **CommentRevisions**'a yazılır, düzenlenen yorumda **"düzenlendi" rozeti** görünür
  (§18.2).

**Dosya/görsel (S3):**
- Bucket **public değil**. Yükleme **presigned PUT** ile doğrudan tarayıcıdan; okuma **kısa ömürlü
  presigned GET**. Sunucu tarafında tip + boyut doğrulaması.
- Varsayılan (ayardan): görsel + PDF + Office, **10 MB**, yorum başına **5 dosya** (§18.13).

## 13. Global Ayarlar & "Sistem Dosyası" Çelişkisinin Çözümü

Brief hem "parametreler sistem dosyasında" hem "tüm ayarlar süper admin arayüzünden" diyor. Bir
dosyayı runtime'da UI'dan yazmak kötü pratik (atomik değil, audit yok, çok-instance'ta tutarsız,
deploy'da ezilir). Ayrım:

- **Dosya/env (asla DB, asla UI):** DB bağlantısı, S3 anahtarları, SMTP kimlik, JWT secret.
- **DB `Settings` (UI-editable, versiyonlu, audit'li):** iş parametreleri.
- Açılışta ikisi birleşip cache'lenir; değişince invalidate.

**"Bütün ayarlar" nasıl karşılanıyor (§18.3):** Ayar sistemi **jenerik** (key/value/tip/grup) —
yeni ayar eklemek bir DB satırı, kod değişikliği değil. v1'de teslim edilen ayar seti:

| Grup | Ayarlar |
|---|---|
| Ticket | statüler + geçiş kuralları, kategoriler, öncelik varsayılanı, ticket no formatı, yeniden açma süresi |
| Bildirim | olay × alıcı matrisi (§14), mail şablonları, debounce süresi |
| Dosya | izinli tipler, max boyut, adet limiti |
| Form | CAPTCHA aç/kapa, rate limit, KVKK metni |
| Marka | logo, sistem adı, ana renk |
| Yetki | rol-permission matrisi |
| Sistem | zaman dilimi, dil, SLA süreleri |

## 14. Bildirim / E-posta

Brief: "ticket'ta en küçük değişiklikte açana otomatik mail." Harfiyen uygulanırsa **iç notlar
müşteriye sızar** — bu bir gizlilik sorunu. Çözüm: kural reddedilmez, **ayara çevrilir**. Olay ×
alıcı matrisi Settings'te yaşar, süper admin arayüzden açar/kapatır. Varsayılan (§18.1):

| Olay | Müşteri | Atanan personel | Şirket admini |
|---|---|---|---|
| Ticket açıldı | ✔ (onay + ticket no) | — | ✔ |
| Statü değişti | ✔ | ✔ | ✖ |
| Public yorum (personel/admin) | ✔ | ✔ | ✖ |
| **İç not yazıldı** | **✖** | ✔ | ✔ |
| Ticket atandı | ✖ | ✔ | ✖ |
| Müşteri yorum yazdı | ✖ | ✔ | ✖ |
| Öncelik/kategori değişti | ✖ | ✖ | ✖ |

**Altın kural: kimseye kendi yaptığı işlemin mailini gönderme.** Bu tek kural spam şikayetlerinin
yarısını keser.

- Mail **asla istek döngüsünde** gönderilmez → `EmailQueue` + worker (retry + dead-letter).
- Tetikleyici **`TicketEvents`'ten** beslenir; mail çağrıları iş mantığına serpilmez.
- **Debounce:** aynı ticket'a kısa sürede çok değişiklik → tek özet mail.
- Kullanıcı başına kritik-olmayan bildirimleri kapatma (`UserNotificationPrefs`).
- SPF/DKIM/DMARC + gönderim logu.

## 15. Raporlar

- **Admin:** kendi şirket(ler)i kapsamında. **SuperAdmin:** global.
- Metrikler: statü kategorisi dağılımı, **ilk yanıt süresi**, **çözüm süresi**, personel bazlı
  yük, kategori kırılımı, zaman içinde açılış/kapanış trendi.
- **Çıktı: ekranda dashboard + Excel/CSV dışa aktarım** (§18.14 — Türkçede "rapor almak" genelde
  dışa aktarım ima eder). PDF v2.
- Kaynak: `TicketEvents`. SLA v1'de **sadece ölçülür** (zaman damgaları); kural/alarm v2.

## 16. KVKK

- Formda aydınlatma metni + açık onay kutusu (metin ayardan).
- Saklama süresi ayarlanabilir.
- **Silme talebi = anonimleştirme** (hard delete audit zincirini bozar): kişisel alanlar
  maskelenir, ticket istatistiği korunur.

## 17. Fazlar

1. **Faz 0 — İskelet:** solution + 4 katman + React, Serilog, Swagger, DI, CI.
2. **Faz 1 — Domain & DB:** entities, enums, state machine + unit testler, DbContext, global query
   filter, migration, idempotent seed (statüler, permission'lar, ilk süper admin).
3. **Faz 2 — Auth + RBAC/Permission + Tenancy (ÖNCE, sona bırakılmaz):** JWT + refresh,
   PasswordHasher, `ICurrentUserService`, permission-based authorization, iki katmanlı yetki,
   kapsam kontrolü, davet akışları. **Test-first.**
4. **Faz 3 — Ticket Pipeline:** CRUD (kapsam guard'ı), statü/geçiş, atama, yorum + iç not,
   düzenleme geçmişi, TicketEvents, **arama/filtre/sayfalama (sunucu tarafı — opsiyonel değil)**.
   → ** demo noktası** (§18.21).
5. **Faz 4 — Public Form + S3:** slug'lı form, kimliksiz açma + davet, rate limit + CAPTCHA,
   presigned upload/download, formda dosya.
6. **Faz 5 — Bildirim:** EmailQueue + worker, şablonlar, olay matrisi, debounce, tercihler.
7. **Faz 6 — Ayarlar + Raporlar:** Settings tablosu + süper admin UI, config split, şirket +
   global rapor, Excel/CSV export, KVKK.
8. **Faz 7 — UI:** login, müşteri formu, kanban (dnd, izin kontrollü), ticket detay, yorum/dosya,
   ayar ekranı, dashboard. §4.2/§4.3 disiplini burada kanıtlanır. Mobilde kanban **liste
   görünümüne düşer** (ayrı mobil kanban yazılmaz).
9. **Faz 8 — Deploy:** hosting, secret'lar env'de, CORS prod, migration stratejisi.

**Çekirdek dörtlü (test-first):** RBAC+permission kapsamı, tenant izolasyonu, bildirim
tetikleyici (özellikle iç not sızmaması), statü geçiş makinesi. Gerisi test-after + smoke.

Her faz sonunda: build + testler yeşil, bu MD + PROGRESS.md güncellenir.

## 18. Kararlar — Varsayıldı, Onay Bekliyor

Aşağıdakiler §2'deki ilkelerle karara bağlandı. Kod bu varsayımlarla yazılır; onay/itiraz gelince
doküman güncellenir.

**Bildirim & gizlilik**
1. "Her değişiklikte mail" → olay × alıcı matrisi, ayarlanabilir (§14). İç not müşteriye ASLA
   gitmez. Kimseye kendi işleminin maili gitmez.

**Düzenleme & kayıt**
2. Admin müşteri yorumunu düzenleyebilir/silebilir (brief), ama: soft delete + `CommentRevisions`
   geçmişi + "düzenlendi" rozeti. *Rozetin müşteriye görünmesi — onay bekliyor.*
3. "Bütün ayarlar" → jenerik ayar altyapısı + §13'teki kapalı v1 listesi.

**Yetki & kimlik**
4. Permission atama: süper admin global; admin yalnız kendi şirketinde ve yalnız kendisinde olan
   yetkileri.
5. Self-servis kayıt yalnızca Customer. Admin'i süper admin açar; personeli/2. admini admin davet
   eder.
6. Yığın: .NET 8 + MSSQL + React — **öneri**, onaylanabilir.
7. "E-posta ile kayıt" = klasik kayıt + davet/şifre belirleme linki (magic link değil).
8. Şirketi admin kendi oluşturur (brief), hesabı süper admin açar.
9. Statüler: süper admin global varsayılan set. Şema şirket-özel override'a hazır ama v1'de kapalı
   (seam var, özellik yok).

**Ticket davranışı**
10. Müşteri İptal/Tamamlandı yapabilir, yalnız terminal olmayan statüde.
11. Kapanan ticket 7 gün içinde yeniden açılabilir (ayar).
12. Müşteri kendi yorumunu düzenleyemez/silemez.
13. Dosya: görsel + PDF + Office, 10 MB, yorum başına 5 — ayardan. İlk formda dosya eklenebilir.
14. "Rapor almak" = dashboard + Excel/CSV export. PDF v2.
15. Müşteri kanban görmez, kendi ticket'larının düz listesini görür.
16. Ticket no şirket başına artan + önekli (`ACME-1042`), format ayardan.
17. Öncelik: 4 seviye, varsayılan Normal, **müşteri değil admin/personel** belirler.
18. Kategori: şirket bazında tanımlanabilir liste (rapor kırılımı).
19. Email-to-ticket **v2**, ama `Comment.Source` alanı şimdi konur (sonra ucuz olsun).
20. Şirket silinmez, **arşivlenir**: form linki kapanır, veri okunur kalır. Cascade delete YOK.

23. Hazır mockup/tasarım var mı? Yoksa 2-3 ana ekran önce onaya sunulur.
24. Deploy ortamı, S3 sağlayıcısı (AWS/MinIO), SMTP sağlayıcısı — Faz 4-5'i doğrudan etkiler.

## 19. Scope DIŞI — Outbound Kampanya/Teklif

S�zlü konuşulan "telekom/call-center tarzı, profile göre kampanya/teklif" katmanı bu brief'te
YOK. Core ticketing'i şişirmemek için scope dışı (YAGNI). İleride ayrı modül olarak: müşteri
havuzu + segment + teklif + kampanya + izin/İYS süzgeci + sonuç kodu. Şimdilik kod yazılmaz.

## 20. Risk Analizi

| Risk | Etki | Önlem |
|---|---|---|
| Kapsam kontrolünün atlanması | Bir şirketin verisi diğerine sızar | Kapsam DATA katmanında (query filter); test-first |
| **İç notun müşteriye mail'lenmesi** | **Gizlilik ihlali, güven kaybı** | **Olay matrisi (§14); çekirdek test** |
| Admin'in müşteri yorumunu izsiz değiştirmesi | Uyuşmazlıkta kanıt yok | CommentRevisions + soft delete + rozet |
| Kimliksiz public form | Bot/spam | Rate limit + CAPTCHA |
| S3 yanlış yapılandırma | Görsel URL'i sızar | Public değil + kısa ömürlü presigned URL |
| İstek döngüsünde mail | API yavaşlar, spam | Kuyruk + worker + debounce |
| Statü'nün isimle kodlanması | İsim değişince rapor/mantık bozulur | Kategori enum'u (§4.3, §12) |
| Ayar dosyasını UI'dan yazma | Tutarsızlık, audit yok | Config split (§13) |
| "Bütün ayarlar" kapsamı | Teslimde kapsam patlaması | Jenerik altyapı + yazılı v1 listesi (§13) |
| KVKK | Yasal | Aydınlatma, saklama, anonimleştirme (§16) |
| Teslim tarihi belirsiz | Beklenti çatışması | Faz 3 demo + fazlı teslim (§18.21) |
