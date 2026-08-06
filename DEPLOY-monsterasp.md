# MonsterASP.NET'e Deploy (IIS tek-site, self-contained)

Bu proje lokalde Docker (API + MSSQL + MinIO + nginx) ile çalışır. MonsterASP.NET **Docker çalıştırmaz** —
IIS tek-site'tir. Bu yüzden deploy şekli farklı:

- **SPA + API tek sitede:** React build'i API'nin `wwwroot`'una kopyalanır; nginx'in işini ASP.NET yapar
  (`UseStaticFiles` + `MapFallbackToFile`). `/api/*` controller'lara, gerisi `index.html`'e gider.
- **Self-contained publish:** .NET 10 runtime paketin içinde gelir → host'ta .NET kurulu olmasa da çalışır.
- **DB:** MonsterASP.NET'in ücretsiz MSSQL'i (MinIO/SQL container yok).
- **Bu dosya Docker yolunun alternatifidir**, ek adımı değil. Docker ile çalışıyorsan `README.md` + `up.ps1` yeterli.
- **Dosya yükleme:** MinIO yok → varsayılan `Files__Provider=local` (host diski, ücretsiz); Azure Blob
  veya S3 de seçilebilir. Deposuz mesajlaşma çalışır. Bkz. §6.

---

## 1. Paketi üret (lokal)

```powershell
./publish.ps1
```
`./publish` klasörü oluşur: API exe + `wwwroot` (SPA) + .NET runtime + `web.config`.

## 2. Yükle

MonsterASP.NET panelinden **File Manager** veya **FTP/WebDeploy** ile `./publish` **içeriğini** sitenin
kök klasörüne (genelde `wwwroot/` ya da `site/`) yükle. `web.config` kökte olmalı.

## 3. Config (secret'lar — dosyada değil, panelde)

MonsterASP.NET panelinde **Environment Variables** (veya "App Settings") olarak, `Section__Key`
formatında ayarla (repo'ya secret koyma):

| Anahtar | Değer |
|---|---|
| `ConnectionStrings__Default` | MonsterASP.NET MSSQL bağlantı dizesi (panelden; `TrustServerCertificate=True`) |
| `Jwt__SigningKey` | uzun rastgele (ör. 48+ bayt base64) |
| `SuperAdmin__Email` / `SuperAdmin__Password` | ilk süper admin |
| `App__PublicBaseUrl` | `https://<siten>.monsterasp.net` (maildeki linkler bunu kullanır) |
| `Email__Provider` | `smtp` |
| `Email__Host` / `Email__Port` / `Email__UseSsl` | `smtp-relay.brevo.com` / `587` / `true` (**Brevo** — projede kullanılan relay; 465 KULLANMA, .NET STARTTLS ister) |
| `Email__Username` / `Email__Password` | Brevo SMTP kullanıcısı (`...@smtp-brevo.com`) / Brevo **SMTP key** (`xsmtpsib-...`) |
| `Email__From` / `Email__FromName` | Brevo'da **doğrulanmış** gönderici (doğrulanmış domain'de bir adres, ör. `no-reply@<domain>`; doğrulanmış tek bir freemail adresi de olur) / görünen gönderici adı |
| `Email__ReplyTo` | `From` freemail (gmail/outlook/yahoo) ise **zorunlu** — ör. `no-reply@<siten>.runasp.net`. Bkz. §9 |
| `Seed__Demo` | ilk demo için `true`, gerçek prod'da `false` |
| `Files__Provider` | dosya deposu: `local` (ücretsiz, host diski — **bu deploy için önerilen**) / `azure` / `s3`. Kodda varsayılan `s3`, o yüzden `local` istiyorsan **yaz** (bkz. §6) |

> ASP.NET Core config sağlayıcısı env var'ları `Section:Key` olarak okur; `__` (çift alt çizgi) `:` demektir.

## 4. İlk çalıştırma

- Uygulama açılışta **migration'ları çalıştırır** (şemayı MonsterASP.NET MSSQL'inde oluşturur) ve süper
  admin'i seed'ler. `Seed__Demo=true` ise demo şirket/ticket da gelir.
- Siteyi aç → SPA gelir. `https://<siten>/api/health` → beklenen 200 değil (health `/api` altında değil,
  root'ta); onun yerine **login** ile test et: süper admin + panelde verdiğin şifre.
- Kayıt akışını kendi mailinle dene: `/register` → Gmail'ine gerçek doğrulama maili → `/invite` şifre →
  giriş → şirket seçip mesaj.

## 5. HTTPS / reverse proxy
MonsterASP.NET SSL sağlar. Uygulama `UseForwardedHeaders` ile gerçek şemayı okur (redirect döngüsü yok).
Ekstra ayar gerekmez.

## 6. Dosya yükleme deposu (3 seçenek — provider config'le seçilir)

`IFileStorage` seam'inin üç implementasyonu var; `Files:Provider` env'i seçer. Yükleme/indirme her
zaman backend-proxy (`PutAsync`/`GetAsync`), presigned yol kullanılmaz.

### 6a. Host diski — ÜCRETSİZ, seçilen varsayılan (MVP/test)
Ek bulut hesabı/ücret yok; dosyalar hosting'in kendi diskinde, servis edilmeyen bir klasörde.
- `Files__Provider=local` — **bunu açıkça yazman gerekiyor.** Kodda varsayılan `s3`
  (`DependencyInjection.cs`), yani env verilmezse S3 aranır ve dosya yolu ayağa kalkmaz.
- (opsiyonel) `LocalStorage__RootPath=<mutlak yol>` — boşsa `App_Data/uploads` (content root altında).
`App_Data` IIS tarafından public servis edilmez; indirme yalnız yetkili API proxy'sinden geçer.
**Dikkat:** dosyalar host diskinde durur → deploy'da `App_Data` klasörünü silme/üzerine yazma;
free tier disk kotası geçerli. Tek-instance için uygun (çok-instance'ta paylaşımlı disk gerekir).

### 6b. Azure Blob Storage (Microsoft) — küçük ücret
`AzureBlobStorage` (`Azure.Storage.Blobs`); container private, ilk kullanımda otomatik açılır.
- `Files__Provider=azure`
- `AzureBlob__ConnectionString=<connection string>` (Storage account → Access keys)
- `AzureBlob__ContainerName=attachments`

### 6c. S3-uyumlu (AWS S3 / MinIO / R2 / B2)
- `Files__Provider=s3` + `S3__ServiceUrl` (AWS'de boş) / `S3__BucketName` / `S3__AccessKey` /
  `S3__SecretKey` / `S3__ForcePathStyle` / `S3__Region`. (AWSSDK v4 CRC checksum'ları AWS-dışı
  store'lar için otomatik kapatıldı.)

Deposuz da çalışır: dosya yükleme buton/akışı hata verir, mesajlaşma ve diğer her şey çalışır.

## 7. Sorun giderme
- **500.30 / açılışta çöküyor:** genelde bağlantı dizesi. `web.config`'te geçici olarak
  `stdoutLogEnabled="true"` yapıp `logs/` altındaki çıktıyı oku, sonra kapat.
- **.NET sürüm hatası:** self-contained publish bunu çözer (runtime içeride). Framework-dependent
  yayınlama yaptıysan host'ta .NET 10 olmayabilir → self-contained kullan.
- **Mail "sadece bana geliyor":** Gitmiyor değil — **spam'e düşüyor.** Gönderim tarafında alıcı
  kısıtı yok (relay her alıcıyı kabul ediyor, uygulamada da filtre yok); kendi adresin kendi
  gönderdiğin maili beyaz listeye aldığı için sadece o görünüyor. Bkz. §9.
- **Mail gitmiyor:** Brevo'da domain **doğrulanmış** olmalı (Domains → DKIM/DMARC kayıtları); `From` o domain'den; user = Brevo SMTP kullanıcısı, pass = SMTP key; port **587** (465 değil — .NET `System.Net.Mail` implicit SSL/465 desteklemez, STARTTLS/587 ister). Domain doğrulanmadan Brevo gönderici adresini `...@<id>.brevosend.com` olarak yeniden yazar; Gmail/Yahoo bunu spam'e atabilir (PROGRESS teknik borç #33). Gönderim durumunu `EmailQueue.Status` / `LastError` sütunlarından oku.
- **Şema oluşmadı:** ilk request'te migration çalışır; DB kullanıcısının tablo oluşturma yetkisi olmalı.

## 8. Bilinen sınırlar (bu deploy)
- MonsterASP.NET ücretsiz katman inaktiviteden sonra uygulamayı uyutabilir (ilk istek yavaş).
- Migration'lar açılışta koşar (tek-instance için uygun). Çok-instance'a geçilirse ayrı migration adımı gerekir (PROGRESS teknik borç #6).
- **Dosya depolama `Files__Provider` ile seçilir** (§6). Bu deploy'da varsayılan olarak `local` (host diski) öneriliyor çünkü ücretsiz; `s3`/`azure` de çalışır. *(Bu madde daha önce "host disk kullanılmaz, S3'e bağlı" diyordu — §6a ile çelişiyordu; Faz 29'da düzeltildi. Spec §12 de artık üç provider'ı listeliyor.)*
- Gönderici domain'i doğrulanmadığı sürece DMARC hizalaması `none` kalır ve Brevo'nun açılma-takip
  pikseli SpamAssassin `HTML_IMAGE_ONLY_16` (+1.0) yazar. 1.2/5.0'da sorun değil, ama tek kalan
  başlık bu (§9).

## 9. Mail teslimatı — "sadece benim adresime gidiyor" (Faz 33'te ölçüldü)

Şikâyetin kaynağı **gönderim değil, spam filtresi**. Ölçüm (mail-tester.com, canlı Brevo relay'i,
üçüncü taraf bir alıcıya gerçek uygulama akışından):

| Yapılandırma | SpamAssassin | mail-tester |
|---|---|---|
| Başlangıç (HTML-only, Reply-To boş) | 4.1 / 5.0 — eşiğin dibinde | 4.9/10 |
| `+ text/plain` parçası | 4.0 / 5.0 | 5.0/10 |
| `+ Email__ReplyTo` (freemail olmayan) | **1.2 / 5.0** | **7.8/10** |

İki kural:

1. **`Email__ReplyTo` doldurulacak** — `From` bir freemail adresiyse. Brevo böyle bir `From`'u
   `<id>.brevosend.com`'a yeniden yazar ve Reply-To'ya orijinal gmail adresini enjekte eder;
   SpamAssassin bunu sahtecilik imzası sayar (`FREEMAIL_FORGED_REPLYTO` +2.5,
   `FREEMAIL_REPLYTO_END_DIGIT` +0.25). Kendi Reply-To'nu vermek enjeksiyonu engeller.
2. **`From` olarak `<id>.brevosend.com` adresi KULLANMA.** Denendi: relay SMTP'de kabul ediyor
   (250 OK, `SmtpClient.Send` hatasız dönüyor) ama mesaj **hiç teslim edilmiyor** — sessizce
   düşüyor. Yalnız Brevo'da doğrulanmış gönderici çalışır. Bu yol maili tamamen kesiyordu.

Kalıcı çözüm bir domain doğrulamaktır (Brevo → Domains → DKIM/DMARC): o zaman `From` gerçek
domain'de kalır, yeniden yazma olmaz, `ReplyTo` gereksizleşir ve DMARC hizalanır.
