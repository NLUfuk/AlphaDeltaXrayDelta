# Kanby — Mimari ve Özellik Raporu

| | |
|---|---|
| **Tarih** | 2026-08-06 |
| **Kapsam** | Faz 0-41, commit `09562d5` |
| **Canlı** | https://crm-kanban.runasp.net (MonsterASP.NET, IIS tek-site) |
| **Kod** | ~20.700 satır C# (`src/`), ~4.800 satır TS/TSX (`frontend/src`), ~6.000 satır test |
| **Test** | 241 test, 36 test dosyası, hepsi yeşil |
| **Uçlar** | 70 HTTP endpoint (13 controller) |
| **Şema** | 23 entity, 12 migration (tek doğrusal additive zincir) |

Bu belge sistemin **bugünkü halini** anlatır. Ne yapılacağını değil ne yapıldığını, ve daha
önemlisi **neden öyle yapıldığını** kaydeder. Karar günlüğünün tamamı `PROGRESS.md`'dedir; burada
kararların *mimari sonuçları* toplanmıştır.

---

## 1. Sistem ne yapıyor

Şirketlerin müşteri taleplerini bir **kanban panosu** üzerinden yürüttüğü çok-kiracılı bir CRM.
Talep üç yoldan girer: müşteri portalı, şirkete özel public form, ya da personelin kendi açması.
Talep bir sütundan diğerine sürüklenerek ilerler; her geçiş sunucuda bir durum makinesinden geçer.
Faz 39'dan beri her talep **parasal bir değer** taşıyabilir, dolayısıyla pano aynı zamanda bir
fırsat hattıdır ve "bu müşteri bize ne kazandırdı" sorusunun cevabı sistemde vardır.

**Roller:** SüperAdmin (sistem sahibi, şirket-üstü) · Admin (şirket sahibi) · Personel · Müşteri.

---

## 2. Katmanlar ve bağımlılık yönü

```
CrmKanban.Api            ← controller, auth middleware, hata zarfı, SPA host
   ↓
CrmKanban.Application    ← use case servisleri, DTO, yetki çözümü, rapor/PDF
   ↓
CrmKanban.Domain         ← entity + davranış, durum makinesi, yetki anahtarları
   ↑
CrmKanban.Infrastructure ← EF Core, SQL Server, S3/disk, SMTP, seed
```

Bağımlılık **içe** akar. `Infrastructure` yukarı doğru `Application`'ın arayüzlerini uygular
(`IAppDbContext`, `IFileStorage`, `IEmailSender`, `IClock`); `Application` EF Core'un
*soyutlamalarına* referans verir ama `SqlServer` paketine vermez. Kompozisyon kökü
`Program.cs` + `AddApplication`/`AddInfrastructure`.

**Seam nerede var, nerede yok — ve neden:**

| Seam | Var mı | Gerekçe |
|---|---|---|
| `IFileStorage` | ✅ | 3 gerçek uygulama: S3, Azure Blob, host diski. Spec'in adlandırdığı varyasyon noktası. |
| `IEmailSender` | ✅ | Dev'de log, canlıda SMTP. İki gerçek durum. |
| `ICaptchaValidator` | ✅ | Provider seçilmedi ama **fail-closed** davranış gerekiyordu. |
| `IAppDbContext` | ✅ | DbContext = Unit of Work, DbSet = repository. Per-entity repo **yok**. |
| Per-entity repository | ❌ | İkinci uygulaması olmayacak bir soyutlama. Spec §4.1 UoW ile karşılandı. |
| `IReportRenderer` | ❌ | Tek uygulama (PDF). Formatı değiştirmek gerekirse o gün çıkarılır. |
| `Roles` tablosu | ❌ | Roller sabit 4'lü küme → enum. Değişken olan **rol-permission matrisi**, o veri. |

Ölçüt hep aynı: *esneklik yalnız spec'in adlandırdığı ya da ikinci gerçek durumun kanıtladığı
eksende*. Bilinen bir değişim eksenini sertleştirmek, bilinmeyen birine soyutlama koymak kadar hatalı.

---

## 3. Güvenlik modeli — sistemin kalbi

Üç ayrı katman var ve **birbirinin yedeği değiller**; her biri farklı bir hata sınıfını kapatıyor.

### 3.1 Kiracı izolasyonu — DATA katmanında, tek yerde

`CrmDbContext.OnModelCreating` içinde her kiracı-scope'lu entity'ye global query filter:

```csharp
e.HasQueryFilter(x => x.DeletedAt == null && (IsSuperAdmin || CompanyScope.Contains(x.CompanyId)));
```

`CompanyScope` ve `IsSuperAdmin` **yalnız imzalı JWT'den** gelir, istek gövdesinden değil.
Filtre uygulama kodunda değil veri katmanında olduğu için, birinin `.Where(t => t.CompanyId == x)`
yazmayı unutması veri sızdırmaz.

> **Bu kuralın bilinen tuzağı:** `IgnoreQueryFilters()` **sorgu genelinde** etkilidir — bir join'in
> tek tarafına yazılsa bile tüm sorgunun filtresini kapatır. Faz 28'deki iki gerçek sızıntının kök
> nedeni buydu. Kodda 30+ meşru kullanımı var (auth bootstrap, müşterinin kendi kaydı); her biri
> gerekçesiyle yorumlanmış durumda. Teknik borç #36/#42 bu kuralı kalıcı korumaya bağlamayı izliyor.

### 3.2 Yetkilendirme — iki aşamalı

**(a) Şirket bazlı yetki çözümü.** `PermissionResolver` saf bir fonksiyondur: *rol tabanı + kullanıcı
Grant − kullanıcı Deny*, **Deny her zaman kazanır**. Yetki kullanıcının üstünde global değil,
**şirket başına** çözülür — çok şirketli bir kullanıcıda "bu kişinin yetkileri" sorusunun şirketten
bağımsız cevabı yoktur. JWT yalnız kimlik + `company_ids` + `is_super_admin` taşır.

**(b) Kayıt bazlı aktör.** Ticket yetkisi kayda bağlıdır (talebin şirketi, atanan personel, talebi
açan müşteri). `TicketAuthorizationService.ResolveAsync` bir kez aktörü çözer
(`SuperAdmin | Staff | Customer`), operasyon boyu o kullanılır. Declarative `[HasPermission]`
attribute'u bilinçle **açılmadı**: ambient-company bir policy, kayda bağlı yetkiyi ifade edemez.

**Yetki atama guard'ı:** kimse sahip olmadığı yetkiyi veremez, başka şirkette yetki dağıtamaz
(`PermissionAssignmentGuard`, audit log'lu). Yetkiler üç durumludur: Ver / Reddet / **rol
varsayılanına dön** — "silinmiş satır hâlâ uygulanıyor" hatası Faz 31'de kapatıldı.

### 3.3 Görünürlük filtreleri — "gizlemek" ≠ "vermemek"

Sistemin tekrarlayan ilkesi: **yetkisiz kullanıcıdan veri gizlenmez, sunucu cevabından çıkarılır.**

| Veri | Kural |
|---|---|
| İç notlar | Müşteriye **hiçbir koşulda** gitmez; iç nota bağlı dosya da indirilemez. |
| Talep tutarı (`ticket.value`) | Detay, pano, liste, rapor — dört okuma yolunun **her biri** ayrı test edilir. Yetki yoksa alan `null` gelir. |
| PDF export | Para sütunları yetkisizde **tamamen kaldırılır**, boş bırakılmaz (boş hücre "sıfır" okunur). |
| Süper admin | Müşteri/personel listelerinde görünmez (Faz 19). |

UI'da gizlemek bir tasarım kararıdır; devtools açan görür. Bu yüzden testler DTO'nun kendisine bakar.

### 3.4 Kimlik

JWT (~15 dk) + refresh token (DB'de **SHA256 hash'li**, rotasyonlu). Reuse tespiti zinciri revoke
eder. Parola ASP.NET Identity hasher'ı ile. Parola değişimi tüm refresh'leri düşürür. Lokal ve canlı
imza anahtarları **ayrıdır** (Faz 38: aynıydı, yani lokalde üretilen token canlıda geçerliydi).

---

## 4. Alan modeli (23 entity)

**Kimlik/kiracı:** `User` · `Company` · `Membership` · `Invitation` · `RefreshToken` · `CustomerTrust`
**Yetki:** `Permission` · `RolePermission` · `UserPermission` · `AuditLog`
**Talep:** `Ticket` · `TicketStatus` · `StatusTransition` · `TicketCategory` · `TicketEvent` · `Comment` · `CommentRevision` · `Attachment`
**Yapılandırma:** `Setting` · `FormField` · `EmailTemplate` · `EmailQueue` · `UserNotificationPref`

Entity'ler **private setter + davranış metodu** ile yazılır (`ticket.SetValue(...)`,
`company.AllocateTicketNumber()`), dışarıdan alan atanmaz. İş kuralı entity'nin içindedir:
örneğin `SetValue` negatif tutarı reddeder — kaybedilen iş "negatif tutar" değil, **LOST sonuçlu**
tutardır; ikisine birden izin vermek aynı kaybı iki kez düşürürdü.

**Durum makinesi:** geçerli geçişler `StatusTransitions` **tablosunda** durur, kodda `switch`'te
değil. Statüler şirket başına özelleştirilebilir; bu yüzden tüm metrikler statü **kategorisine**
(`Open/Pending/Answered/Waiting/Closed/Cancelled`) bakar, asla görünen ada bakmaz. Bir şirket
"Tamamlandı"yı "Teslim edildi" yaparsa raporlar bozulmaz.

**Silme:** her yerde soft delete (`DeletedAt`), audit interceptor'ı zaman damgalarını yazar.
Talep silinince olayları ve yorumları denetim izi için kalır — yalnız listelerden düşer.

**Migration:** tek doğrusal additive zincir; her faz kendi tablosunu ekler. "Davranışsız kabuk
entity" kurulmaz — tablo, davranışıyla birlikte gelir.

---

## 5. Özellikler

**Talep hattı** — CRUD, şirket başına artan talep numarası, atama, öncelik (yalnız personel),
statü geçişleri, 7 günlük yeniden açma penceresi, yorum + iç not (düzenleme `CommentRevisions`'a
yazılır, "düzenlendi" işaretlenir), dosya eki (tip/boyut/adet **sunucuda** doğrulanır, magic-byte
kontrolü), sunucu taraflı arama/filtre/sayfalama.

**Giriş kapıları ve güven** — Şirkete özel müşteri portalı (`/c/{slug}`: kayıt → e-postaya 6 haneli
kod → doğrula). Anonim public form (alanları UI'dan yapılandırılabilir, KVKK onayı zorunlu, IP başına
5/dk rate limit, CAPTCHA seam'i fail-closed). **Otomatik onayın tek yolu geçerli bir personel
davetidir** (Faz 35): davet linki kişiye özel token taşır (e-posta + şirkete bağlı, tek kullanımlık,
7 gün); yabancı da, kayıtlı müşterinin sonraki talebi de **onay kutusuna** düşer. Süreklilik için
onay ekranında "Onayla + güven" (`CustomerTrust`).

**Pano** — `auto-fill` grid; her statü ekranda ve **bırakma hedefi** (eski yatay şerit düzeninde
ekran dışı sütuna kart bırakmak fiziksel olarak imkânsızdı, çünkü HTML5 drag oto-kaydırma yapmaz).
Başlığa tıklayınca kutu tam satıra genişler. Kartta öncelik yıldızları, atanan avatarı, tutar rozeti.
Sütun içi hızlı ekleme.

**Para ve raporlama** — Talepte tahmini/gerçekleşen tutar (ikisi ayrı: tek alanla "tahminlerim ne
kadar tutuyor" sorusu cevapsız kalırdı). Kazanç ekranı: kazanılan/kaybedilen/açık hat, kazanma oranı
**adetçe ve tutarca ayrı** (çok küçük kazanç + bir büyük kayıp, adetçe iyi tutarca kötü bir aydır),
tahmin isabeti, aylık trend. Rapor panosu + **PDF export** (A4 yatay: genel istatistikler, mali
durum, müşteri kırılımı, toplam satırı). Fiyatlanmamış talep hiçbir toplama katılmaz — "henüz
bilinmiyor" ile "sıfır" farklı şeylerdir.

**Yönetim** — Kullanıcılar (arama/filtre, KVKK hesap silme, kimliğine girme), yetki ekranı
(anahtar başına Türkçe açıklama + on/off switch), sütun/statü yönetimi, form alanı tasarımcısı,
mail şablonları (canlı önizleme), ayarlar, şirket açma/silme (çift onaylı: panel + sunucuda şirket
adı doğrulaması).

**Bildirim** — `EmailQueue` + şablon; dil **alıcıya göre** seçilir (müşteri dili / personel dili).
`multipart/alternative` (HTML + türetilmiş düz metin) ve `Reply-To` ile spam skoru 4.9 → 7.8'e
çıkarıldı (SpamAssassin 4.1 → 1.2, ölçüldü).

**KVKK** — Onay zorunluluğu, hesap silme (kişisel veri anonimleştirilir, talep geçmişi denetim için
kalır — raporda "(silinmiş kullanıcı)" olarak görünür), saklama süresi ayarı.

---

## 6. Frontend

React 19 + Vite 8 + TanStack Query v5 + Tailwind v4 + React Router (SPA data router).
Sunucu durumu **yalnız** TanStack Query'de; ayrı bir global state kütüphanesi yok — ihtiyaç
duyulmadı. Ekranlar "aptal" tutulur: yetki kararları sunucuda verilir, ekran gelen veriye bakar
(`canSeeValue` gibi alanlar "bilmeye hakkı yok" ile "henüz girilmemiş"i ayırır, böylece UI yanıltıcı
boş kutu basmaz).

23 ekran. Ortak parçalar: `primitives.tsx` (Button/Card/Input/Loading/Icon), `charts.tsx`
(bağımlılıksız inline SVG grafikler), `Logo.tsx`, `TicketCard.tsx`. Tekrarlayan hata mesajı ve
"Yükleniyor…" kopyaları Faz 32'de tek yere indirildi (15 ve 10 ekrandan).

Marka: sidebar + giriş ekranında inline SVG (koyu temayı `currentColor` ile takip eder; `<img>`
edemezdi), amber düğüm sabit.

---

## 7. Test stratejisi

241 test / 36 dosya. Kapsam yüzdesi hedefi **yok**; ölçüt şu: *her güvenlik ve iş değişmezinin
testi var mı?*

**Test-first yazılan çekirdek:** yetki çözümü, kiracı izolasyonu, iç not sızmaması, talep tutarı
görünürlüğü, müşteri kırılımı, durum makinesi geçişleri, davet/güven kuralı, para aritmetiği.
**Smoke seviyesinde bırakılan:** CRUD, UI glue, PDF render (üçüncü taraf yerleşim motoru — asıl
kontrol edilen, boru hattının gerçek bir PDF üretmesi).

Testler davranışa bakar, uygulamaya değil. Birkaç test doğrudan **geçmiş bir açığı** bekler:
`MembershipRevocationTests` (şirketten çıkarılan personel yeni girişte tam yetkiyle dönüyordu),
`PermissionClearTests` (silinmiş yetki satırı hâlâ uygulanıyordu),
`PermissionEnforcementAuditTests` (her yetki anahtarının gerçekten bir yerde uygulandığını, sahte
anahtarla negatif doğrulamalı olarak denetler).

---

## 8. Dağıtım ve operasyon

**Lokal:** SQL Server 2022 + `dotnet run` + Vite; Docker yolu da var (`up.ps1`).
**Canlı:** MonsterASP.NET, IIS tek-site. SPA API'nin `wwwroot`'una kopyalanır
(`UseStaticFiles` + `MapFallbackToFile`); `/api/*` controller'lara, gerisi `index.html`'e gider.
Secret'lar panelde environment variable olarak (`Section__Key`), repoda değil.
HTTPS Let's Encrypt + zorunlu yönlendirme. Migration ve seed **açılışta** çalışır.

**Deploy elle yapılır** — CI (`ci.yml`) yalnız build + test çalıştırır, deploy adımı yoktur.
Yöntem: `app_offline.htm` → değişen dosyaların zip'i → panelde "Unzip" (üzerine yaz + app pool
restart) → temizlik. Fark **SHA256 ile** alınır: Faz 37'de boyut karşılaştırması aynı uzunlukta
farklı içerikli üç dosyayı kaçırmıştı ve `index.html` atlanmış olsa site eski JS paketini yüklerdi.
`publish-fd/` klasörü canlının **aynasıdır**; bir sonraki farkın doğru çıkması buna bağlıdır.

> **İki tuzak, ikisi de ölçümle yakalandı:**
> 1. `publish.ps1` **self-contained** üretir ama canlı **framework-dependent** çalışır. Yanlış paketi
>    yüklemek siteyi düşürürdü.
> 2. Canlı app pool **32-bit** (`[x86]`). Yönetilen kod AnyCPU olduğu için 40 faz boyunca hiç fark
>    etmedi; QuestPDF ilk **native** bağımlılık oldu ve win-x64 DLL 32-bit sürece yüklenemedi
>    (Faz 41'de canlıda 500). Bundan sonra her native bağımlılığın x86 ikilisi de yüklenmeli.

---

## 9. Bilinen sınırlar ve teknik borç

Tam liste `PROGRESS.md`'de (48 kayıt). Mimari açıdan önemli olanlar:

| Konu | Durum |
|---|---|
| **QuestPDF lisansı** | Community, yıllık brüt gelir 1M USD altında ücretsiz. Üstünde satın alınmalı ya da MIT bir kütüphaneye geçilmeli (o zaman Linux için font resolver gerekir). Borç #46. |
| **32-bit app pool** | Native bağımlılıkları ve adres alanını sınırlar. Borç #48. |
| **`IgnoreQueryFilters` tuzağı** | Üç kez vurdu (Faz 28/31). Analyzer/inceleme kuralı hâlâ yok; şimdilik testler nöbette. Borç #36/#42. |
| **Rate limiter in-memory** | Tek instance'a bağlı; çok-instance'ta distributed limiter gerekir. Borç #14. |
| **Migration açılışta** | Çok-instance prod'da tek seferlik migration adımına taşınmalı. Borç #6. |
| **Arama sargable değil** | `LIKE '%term%'`; v1 ölçeğinde kabul, büyürse full-text index. Borç #8. |
| **Rapor bellek içi toplama** | Satırlar çekilip C#'ta gruplanıyor. Ölçülmeden SQL'e taşınmasın. |
| **Canlı `Seed:Demo=true`** | Demo şirket/talep verisi canlıda duruyor; gerçek kullanıma geçişte temizlenmeli. |
| **CAPTCHA / S3 e2e** | Provider bağlı değil (fail-closed); gerçek bucket ile uçtan uca bayt testi yapılmadı. Borç #11/#12. |

---

## 10. Bu mimariyi tanımlayan beş karar

1. **Kiracı filtresi veri katmanında, tek yerde.** Uygulama kodunda unutulabilecek bir `Where`,
   veri sızıntısıdır. Denetlenebilir olsun diye dağıtılmadı.
2. **Yetki şirkete göre çözülür, kullanıcıya göre değil.** Çok şirketli kullanıcıda "düz permission
   seti" diye bir şey yoktur; JWT yetki taşımaz, kimlik taşır.
3. **Metrikler statü kategorisine bakar, isme değil.** Şirketler sütunlarını yeniden adlandırır;
   isme bağlı bir toplam, birinin bir metni değiştirdiği gün sessizce sıfırlanır.
4. **Yetkisizden veri çıkarılır, gizlenmez.** Gizlemek istemci kararıdır. Bu yüzden her okuma yolu
   ayrı test edilir, tek ortak yardımcıya güvenilmez.
5. **Esneklik yalnız bilinen değişim eksenlerinde.** Statüler, yetki matrisi, dosya deposu, mail
   sağlayıcısı → veri ya da seam. Geri kalan her şey en basit somut hali. Bilinen bir ekseni
   sertleştirmek de, bilinmeyen birine soyutlama koymak kadar hatalıdır.

---

## 11. Sıradaki iş için öneri

Öncelik sırasıyla:

1. **`Seed:Demo=false` + canlı demo verisinin temizlenmesi.** Gerçek kullanıcı almadan önce.
2. **`IgnoreQueryFilters` koruması** (borç #36/#42) — bu sınıf hata üç kez döndü, dördüncüsü zaman
   meselesi. Ya analyzer kuralı ya kiracı-scope'lu okumalar için ortak yardımcı.
3. **CI'a deploy adımı.** Elle deploy iki kez yöntem hatası üretti (boyut farkı, yanlış paket biçimi);
   ikisi de yakalandı ama ikisi de otomatikleştirilebilir kontrollerdi.
4. **CAPTCHA provider + gerçek S3 e2e** — operasyonel açıklar.
5. **Personel adlarının raporda görünmesi** (borç #47) — CSV'de kapanan kusurun ekranda kalan hali.
