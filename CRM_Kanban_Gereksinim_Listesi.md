# CRM + KANBAN Projesi Gereksinimleri ve Kontrol Listesi

> **Kontrol tarihi: 2026-08-05.** Her madde çalışan sistemde (Docker stack, `http://localhost:8080`)
> veya kodda doğrulandı; kanıt satır sonunda. `⚠️` = karşılanıyor ama bir kararı/sınırı var, okunmalı.
> Bu turda **iki güvenlik bug'ı** bulunup düzeltildi; ikisi de bu listedeki maddeleri doğrudan
> çürütüyordu (bkz. §4 madde 3 ve §2 madde 5). Ayrıntı: `PROGRESS.md` Faz 28.

## 1. Mimari ve Backend Standartları
- [x] Tüm proje MVC (Model-View-Controller) mimarisine uygun olarak tasarlanmalıdır. — ASP.NET Core MVC controller'ları (`src/CrmKanban.Api/Controllers/`, 13 controller, attribute routing). *View katmanı Razor değil ayrı React SPA'i; API+SPA ayrımı bilinçli (madde 2 ile birlikte).*
- [x] Sistem tamamen RESTful API mimarisi ile haberleşecek şekilde kurulmalıdır. — Tüm istemci trafiği `/api/*` üzerinden; SPA'in backend'e başka yolu yok.
- [x] Backend metodları SOLID prensiplerine (özellikle Single Responsibility) tam uyumlu olmalı; her metod sadece tek bir iş yapmalıdır. — Okuma/yazma ayrı servisler (`TicketQueryService` / `TicketCommandService`), yetki ayrı (`TicketAuthorizationService`), depolama/mail/hash arayüz arkasında.
- [x] Clean code yaklaşımları benimsenmeli (Sorumlulukların ayrılması için MediatR handler davranışları ve FluentValidation tabanlı request validasyon katmanları kullanılabilir). — FluentValidation + otomatik pipeline (`ValidationFilter`) + tek hata zarfı. **MediatR kullanılmadı**: gereksinim "kullanılabilir" diyor, tek uygulaması olan bir handler katmanı bu boyutta maliyet olurdu.
- [x] ⚠️ Tüm sistem parametreleri ve ayarları merkezi bir sistem konfigürasyon dosyasında tutulmalıdır. — **Bilinçli olarak ikiye ayrıldı:** secret/altyapı (DB bağlantısı, JWT anahtarı, S3, SMTP) yalnız env/`appsettings` dosyasında; iş parametreleri (statü, şablon, limit, marka, KVKK metni — 15 satır, 7 grup) DB `Settings` tablosunda ve süper admin arayüzünden düzenlenebilir. Gerekçe: parola/anahtarın UI'dan düzenlenebilir olması güvenlik açığıdır. Tek dosyada toplanması isteniyorsa bu ayrım geri alınmalı — o kararı istemci vermeli.

## 2. Kimlik Doğrulama ve Yetkilendirme (Auth & RBAC)
- [x] Kullanıcı kayıt (Register) ve giriş (Login) altyapısı oluşturulmalıdır. — Üç yol: `/register` (self-servis), firma giriş sayfası `/c/{slug}` (e-postaya 6 haneli kod), davet linki. Canlı doğrulandı: kayıt → kod maili → doğrulama → oturum.
- [x] Yetkilendirme protokolleri tamamen RBAC (Role-Based Access Control) mimarisine uygun olarak tasarlanmalıdır. — Rol tabanı + kullanıcıya özel Ver/Reddet; reddetme kazanır; şirket kapsamlı.
- [x] Sistemde 3 temel rol tanımlanmalıdır: **Süper Admin**, **Admin (Şirket)** ve **Müşteri**. — Üçü de var; ayrıca **Personel** rolü eklendi çünkü §4'teki "ticket'lar personellere atanabilmeli" maddesi atanacak bir rol gerektiriyor.
- [x] Roller dışında esnek yetkilendirme yapılabilmesi için bir **Permission (İzin) tablosu** oluşturulmalı ve kullanıcılara özel yetkiler atanabilmelidir. — `Permissions` (11 anahtar) + `RolePermissions` (rol tabanı) + `UserPermissions` (kullanıcıya özel Grant/Deny). "Sahip olmadığın yetkiyi veremezsin" koruması ayrıca audit'li.
- [x] Tüm yetkilendirme ve rol atama işlemleri proje arayüzünden kolayca yönetilebilmelidir. — `/admin/permissions` (şirket → üye → yetki kataloğu, Ver/Reddet), `/admin/users`, `/admin/companies` (davet + üye çıkarma). **Bu turda düzeltilen bug:** üye çıkarma arayüzde çalışıyordu ama erişimi gerçekten kesmiyordu (aşağıya bkz.) — artık kesiyor, canlı doğrulandı.

## 3. Müşteri (Customer) Modülü
- [x] Müşteriler e-posta adresleri aracılığıyla sisteme kayıt olabilmelidir. — Kod/aktivasyon maili Brevo SMTP ile gerçekten gönderiliyor (kuyrukta `Status=Sent`, hata yok).
- [x] Müşteriler dış bir link aracılığıyla form ekranına ulaşıp istedikleri talepleri (ticket) doldurup gönderebilmelidir. — Anonim `/form/{slug}` + kimlikli `/c/{slug}`. Canlı: anonim gönderim → TEKSTIL-17.
- [x] Kendi açtıkları ticket'lara yorum/cevap yazabilmelidir. — Müşteri kendi talebine yorum yazabiliyor; iç notları ne görüyor ne yazabiliyor.
- [x] Ticket'lara görsel ve dosya yükleme yapabilmelidir. — png/jpg/webp + pdf/doc/docx/txt. Tip **magic byte** ile doğrulanıyor, boyut sunucuda ölçülüyor (istemcinin beyanına güvenilmiyor).
- [x] Yüklenen görseller ve dosyalar AWS S3 veya benzeri bir bulut depolama servisinde saklanmalıdır. — Gerçek AWS S3 (`crm-kanban-fiskirmacoop`, eu-north-1). Canlı yükleme→indirme bayt round-trip'i doğrulandı.
- [x] Müşteriler, kendi inisiyatifleriyle açık ticket'ı "İptal" edebilir veya "Tamamlandı" statüsüne çevirebilir. — Durum makinesinin müşteri kolu; terminal statüde butonlar kapanıyor.

## 4. Admin / Şirket (Company) Modülü
- [x] Her admin, sistemde bir veya birden fazla şirket (multi-tenant) hesabı oluşturabilmelidir. — Şirket başına ayrı Admin üyeliği, adet sınırı yok; navbar'da aktif şirket seçici. Silme de var (çift onay, ad yazdırmalı).
- [x] Sisteme dahil edilen her şirket için özel panolar (dashboard/kanban) oluşturulmalıdır. — Şirket bazlı kanban + rapor; sütunlar da şirkete özel (`/admin/columns`: ekle/sırala/renk/sil).
- [x] Adminler, sadece kendi şirketleri altındaki müşterilerin açtığı ticket'ları görebilmelidir. — **2026-08-05'e kadar İHLAL EDİLİYORDU.** `GET /api/tickets` her personele tüm firmaların taleplerini döndürüyordu (mermer personeli 22 talebin hepsini görüyordu); moderasyon kuyruğu da başka firmanın talebini veriyordu. Kök neden: sorguya eklenen `IgnoreQueryFilters()` join'i EF'te **tüm sorgunun** kiracı filtresini kapatıyor. Düzeltildi + 5 servis-seviyesi test eklendi; canlı doğrulandı (mermer personeli artık yalnız 9 MERMER talebi görüyor, yabancı moderasyon kuyruğu boş).
- [x] Adminler ticket detayına girip yorum yazabilmeli ve dosya/görsel yükleyebilmelidir. — Canlı doğrulandı (yorum + S3 eki).
- [x] Gelen ticket'lar şirket içindeki personellere atanabilmelidir. — Atanan kişinin **aktif** şirket üyesi olması zorunlu (bu tur düzeltildi: çıkarılmış üye artık atanamıyor).
- [x] Ticket statüleri admin tarafından değiştirilebilmelidir (Örn: Açık, Kapalı, İptal, Beklemede, Cevaplandı, Bekleniyor vb.). — 6 varsayılan statü + geçiş grafiği; şirket kendi setini özelleştirebiliyor.
- [x] Adminler, ticket'ın başlığını, içeriğini ve içindeki yorumları düzenleyebilmeli veya silebilmelidir. — Canlı doğrulandı: ticket PUT 204, yorum düzenleme (revizyon + "düzenlendi" işareti) ve silme (soft) çalışıyor.
- [x] Adminler, yönettiği şirkete veya şirketlere ait istatistiksel raporlar alabilmelidir. — Statü-kategori dağılımı, ort. ilk yanıt/çözüm süresi, personel yükü, kategori kırılımı, trend + CSV dışa aktarma.

## 5. Süper Admin (Superadmin) Modülü
- [x] Süper admin, şirketlerden ve adminlerden bağımsız, tüm sistemi kapsayan global raporlar oluşturabilmelidir. — Canlı: global rapor 29 talep; aynı uç nokta firma adminine **403**.
- [x] Sistem içinde kullanılacak olan tüm global ayarlar (konfigürasyonlar) süper admin tarafından arayüz aracılığıyla yönetilebilmelidir. — `/settings`, 15 ayar / 7 grup (ticket, notification, file, form, brand, system, kvkk); yalnız süper admin, her değişiklik audit'li. Sınır: madde 1.5'teki secret'lar bilinçli olarak burada değil.

## 6. Bildirim ve E-posta Sistemi
- [x] E-posta gönderim servisi entegre edilmelidir. — Brevo SMTP; arka plan kuyruğu (`EmailQueue`) + retry + dead-letter. Canlı gönderim doğrulandı.
- [x] ⚠️ Ticket üzerinde yapılan en küçük bir değişiklikte dahi (durum değişimi, yeni yorum vb.) ticket'ı açan müşteriye otomatik bilgilendirme maili gönderilmelidir. — Müşteriye giden olaylar: **oluşturma, statü değişimi, yeniden açma, yorum, dosya eklenmesi, başlık/içerik düzenlemesi, onay, ret**. Bilinçli olarak gönderilmeyen üç durum: **öncelik** ve **kategori** değişimi (personelin iç triyaj bilgisi — müşteriye gitmesi hem gürültü hem iç bilgi sızıntısı), **silme**. Kural literal okunacaksa ("istisnasız her olay") bu üçü de eklenmeli — bu bir ürün kararı, kod tarafında matrise üç satır. Ayrıca kimse kendi işleminin mailini almıyor ve iç notlar müşteriye asla gitmiyor.

## 7. Önyüz (UI / Frontend) Gereksinimleri
- [x] UI tasarımında tekrar eden elemanlar bağımsız bileşenler (component) halinde tasarlanıp kullanılmalıdır. — `ui/primitives.tsx` (Button/Input/Field/Alert/Badge/Icon), `TicketCard`, `charts`, `CustomerLink`; 16 ekran bunları paylaşıyor.
- [x] ⚠️ Arayüz hızının iyileştirilmesi için performans ve render optimizasyonları yapılmalıdır. — TanStack Query önbelleği + hedefli invalidation (her mutasyondan sonra tam sayfa yenileme yok), sunucu tarafı arama/filtre/sayfalama (liste hiçbir zaman tümüyle indirilmiyor), Vite üretim derlemesi. **Yapılmayan:** ölçüm (profil/Lighthouse) ve kanban sütunlarında sanallaştırma. Sütun başına talep sayısı yüzlere çıkarsa ilk darboğaz orası olur.

---

## Bu turda kapatılan iki açık (2026-08-05)

| # | Bulgu | Etki | Durum |
|---|---|---|---|
| 1 | `GET /api/tickets` ve moderasyon kuyruğu kiracı filtresini kaybediyordu (`IgnoreQueryFilters` join'i sorgu geneline uygulanıyor) | Her personel **tüm firmaların** taleplerini okuyabiliyordu — §4 madde 3'ün doğrudan ihlali | Düzeltildi, +5 test, canlı doğrulandı |
| 2 | Üyelik yumuşak silindiği hâlde oturum/token sorgusu silinmişleri de okuyordu | Şirketten çıkarılan personel yeni girişte tam yetkiyle dönüyordu; silinen şirket oturumda kalıyordu — §2 madde 5'in ihlali | Düzeltildi (tek ortak sorgu), +5 test, canlı doğrulandı |

Yan etki olarak çıkarılan personelin **yeniden davet edilebilmesi** de düzeltildi: `(kullanıcı, şirket)` tekil indeksli olduğu için eski satır diriltiliyor, ikinci satır eklenmiyor.
