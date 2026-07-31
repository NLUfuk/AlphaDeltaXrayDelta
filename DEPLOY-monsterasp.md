# MonsterASP.NET'e Deploy (IIS tek-site, self-contained)

Bu proje lokalde Docker (API + MSSQL + MinIO + nginx) ile çalışır. MonsterASP.NET **Docker çalıştırmaz** —
IIS tek-site'tir. Bu yüzden deploy şekli farklı:

- **SPA + API tek sitede:** React build'i API'nin `wwwroot`'una kopyalanır; nginx'in işini ASP.NET yapar
  (`UseStaticFiles` + `MapFallbackToFile`). `/api/*` controller'lara, gerisi `index.html`'e gider.
- **Self-contained publish:** .NET 10 runtime paketin içinde gelir → host'ta .NET kurulu olmasa da çalışır.
- **DB:** MonsterASP.NET'in ücretsiz MSSQL'i (MinIO/SQL container yok).
- **Dosya yükleme:** MinIO yok → S3-uyumlu bir depo (Cloudflare R2 / Backblaze B2) gerekir, yoksa dosya
  yükleme çalışmaz (mesajlaşma dosyasız çalışır — MVP'de ertelenebilir).

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
| `Email__Host` / `Email__Port` / `Email__UseSsl` | `smtp.gmail.com` / `587` / `true` |
| `Email__Username` / `Email__Password` / `Email__From` | Gmail + **App Password** (2FA gerekli); From = Username |
| `Seed__Demo` | ilk demo için `true`, gerçek prod'da `false` |
| `S3__ServiceUrl` / `S3__BucketName` / `S3__AccessKey` / `S3__SecretKey` / `S3__ForcePathStyle` | dosya için R2/B2 (bkz. §6) — yoksa dosya yükleme kapalı |

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

## 6. Dosya yükleme için S3 (opsiyonel — Cloudflare R2 örneği)
1. Cloudflare R2'de bir bucket aç (ücretsiz katman). API token → Access Key / Secret Key.
2. Panelde ayarla: `S3__ServiceUrl=https://<accountid>.r2.cloudflarestorage.com`,
   `S3__BucketName=<bucket>`, `S3__AccessKey`, `S3__SecretKey`, `S3__ForcePathStyle=true`.
3. `S3FileStorage` değişmez — presigned PUT/GET R2 ile çalışır. (Backblaze B2 de aynı mantık.)
Bunu atlar isen: dosya yükleme buton/akışı hata verir; mesajlaşma ve diğer her şey çalışır.

## 7. Sorun giderme
- **500.30 / açılışta çöküyor:** genelde bağlantı dizesi. `web.config`'te geçici olarak
  `stdoutLogEnabled="true"` yapıp `logs/` altındaki çıktıyı oku, sonra kapat.
- **.NET sürüm hatası:** self-contained publish bunu çözer (runtime içeride). Framework-dependent
  yayınlama yaptıysan host'ta .NET 10 olmayabilir → self-contained kullan.
- **Mail gitmiyor:** Gmail App Password kullan (normal şifre değil), `From==Username`, 587+SSL.
- **Şema oluşmadı:** ilk request'te migration çalışır; DB kullanıcısının tablo oluşturma yetkisi olmalı.

## 8. Bilinen sınırlar (bu deploy)
- MonsterASP.NET ücretsiz katman inaktiviteden sonra uygulamayı uyutabilir (ilk istek yavaş).
- Migration'lar açılışta koşar (tek-instance için uygun). Çok-instance'a geçilirse ayrı migration adımı gerekir (PROGRESS teknik borç #6).
- Dosya depolama S3'e bağlı (yukarıda); host disk kullanılmaz (spec §12).
