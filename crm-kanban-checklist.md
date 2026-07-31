# CRM + KANBAN — Gereksinim Checklist

Agent doğrulama listesi. Her madde bağımsız olarak test edilebilir olmalı.

---

## 1. Kimlik Doğrulama (Auth)

- [x] Kullanıcı kayıt (register) ekranı ve endpoint'i — müşteri kaydı public form `/form/:slug` üzerinden (§18.5 self-servis yalnız müşteri); `/invite` şifre-belirleme ekranı
- [x] Giriş (login) / çıkış (logout) — 8080'de doğrulandı
- [x] E-posta doğrulama ile hesap aktivasyonu — public form → `account_invite` maili → `/invite` token ile aktivasyon (token sahipliği = e-posta doğrulaması). E2e doğrulandı
- [ ] Şifre sıfırlama / şifre değiştirme akışı — change-password var; **forgot-password** akışı yok (açık iş)
- [x] Müşteriler e-posta aracılığıyla (davet linki) kayıt olabiliyor — e2e doğrulandı
- [x] Oturum / token yönetimi (JWT ~15dk + rotasyonlu refresh) ve yenileme
- [x] Yetkisiz erişimde 401/403 doğru dönüyor — çapraz-tenant/yabancı müşteri 403, kimliksiz 401

## 2. Roller ve Yetkilendirme (RBAC)

- [ ] Roller: Süper Admin, Admin (şirket), Personel (normal kullanıcı), Müşteri
- [ ] `roles`, `permissions`, `role_permissions`, `user_permissions` tabloları mevcut
- [ ] Yetkiler rol dışında **doğrudan kullanıcıya** da atanabiliyor
- [ ] Tüm yetki kontrolleri permission tablosu üzerinden yapılıyor (hard-coded rol kontrolü yok)
- [ ] Yetkiler arayüzden yönetilebiliyor (rol oluştur/düzenle, yetki ata/kaldır)
- [ ] Süper Admin şirket oluşturabiliyor
- [ ] Süper Admin bir şirkete admin ve normal kullanıcı atayabiliyor
- [ ] Şirket admini kendi şirketine başka admin ve normal çalışan atayabiliyor
- [ ] Admin bir veya birden fazla şirket hesabı oluşturabiliyor
- [ ] Şirket bazlı veri izolasyonu (bir şirket başka şirketin verisini göremiyor)
- [ ] Backend'de her endpoint permission middleware ile korunuyor
- [ ] UI'da yetkisi olmayan buton/menü gizleniyor (sadece UI değil, API de kontrol ediyor)

## 3. Şirket Yönetimi

- [ ] Şirket CRUD (oluştur, listele, düzenle, sil/pasifleştir)
- [ ] Her şirketin kendi Kanban panosu var
- [ ] Şirkete kullanıcı ekleme/çıkarma
- [ ] Kullanıcı birden fazla şirkete bağlı olabiliyor (rol şirket bazlı)

## 4. Müşteri Talep Formu (Dış Link)

- [ ] Kimlik doğrulama gerektirmeyen public form linki
- [ ] Link şirket bazlı (hangi şirkete gideceği belli)
- [ ] Form gönderiminde ticket oluşuyor
- [x] Form gönderiminde müşteriye onay e-postası gidiyor — `ticket_created` + yeni müşteride `account_invite` maili (e2e doğrulandı)
- [ ] Spam/rate limit koruması
- [ ] Form alanları yönetim panelinden konfigüre edilebiliyor

## 5. Ticket Yönetimi

### Müşteri tarafı
- [x] Müşteri kendi ticket'larını listeleyebiliyor — "Taleplerim" ekranı (OpenedById-scope), e2e doğrulandı
- [x] Ticket detayına yorum yazabiliyor — e2e doğrulandı (müşteri yazma-yolu bug'ı düzeltildi)
- [ ] Ticket'a görsel/dosya yükleyebiliyor — backend hazır; müşteri detayında dosya yükleme UI'ı henüz yok (açık iş)
- [x] Ticket'ı iptal edebiliyor — İptal butonu (state machine müşteri dalı)
- [x] Ticket'ı tamamlandı olarak işaretleyebiliyor — Tamamlandı butonu
- [x] Başkasının ticket'ını göremiyor — authz.ResolveAsync (opener değilse 403), test kapsamında

### Admin / Personel tarafı
- [ ] Kendi şirketine ait ticket'ları listeleyebiliyor
- [ ] Ticket detayına yorum ve dosya ekleyebiliyor
- [ ] Ticket'ı şirket personeline atayabiliyor
- [ ] Ticket statüsü değiştirilebiliyor (Açık, Kapalı, İptal, Beklemede, Cevaplandı, Cevap Bekleniyor vb.)
- [ ] Statü listesi sabit kodlanmamış, ayarlardan yönetilebiliyor
- [ ] Ticket başlığı, içeriği ve yorumları düzenlenebiliyor
- [ ] Ticket ve yorum silinebiliyor
- [ ] Ticket geçmişi / audit log tutuluyor (kim ne zaman ne değiştirdi)

### Kanban
- [x] Ticket'lar statü kolonlarında kart olarak gösteriliyor — seed ile 8080'de doğrulandı
- [x] Sürükle-bırak ile statü değişimi — native HTML5 DnD (dataTransfer + onDragEnd + no-op self-drop koruması); status endpoint e2e çalışıyor (not: tarayıcı otomasyonu native DnD'yi tetikleyemiyor, kod+endpoint doğrulandı)
- [ ] Filtreleme (atanan kişi, statü, tarih, müşteri) — backend ApplyFilters hazır; kanban UI filtre kontrolleri henüz yok (açık iş)

## 6. Dosya / Görsel Yükleme

- [ ] Yüklenen dosyalar S3 (veya S3 uyumlu bulut) üzerinde saklanıyor
- [ ] Dosyalar lokal diskte tutulmuyor
- [ ] Dosya tipi ve boyut validasyonu
- [ ] Erişim yetkisi kontrolü (imzalı/pre-signed URL)
- [ ] S3 konfigürasyonu sistem ayar dosyasından okunuyor

## 7. E-posta ve Bildirimler

- [ ] SMTP / mail servis entegrasyonu
- [ ] **Ticket'taki her değişiklikte ticket'ı açana otomatik mail gidiyor**
  - [ ] Statü değişimi
  - [ ] Yeni yorum
  - [ ] Atama değişikliği
  - [ ] İçerik/başlık düzenlemesi
  - [ ] Dosya eklenmesi
- [x] Kayıt/davet maili — `account_invite` (müşteri) + `staff_invite` (personel), kuyruk üzerinden gönderiliyor
- [ ] Mail şablonları arayüzden düzenlenebiliyor
- [ ] Mail gönderimi kuyruk (queue) üzerinden asenkron
- [ ] Gönderim hatası loglanıyor ve retry var

## 8. Raporlama

- [ ] Admin: şirket bazında raporlar
- [ ] Süper Admin: şirketten bağımsız global raporlar
- [ ] Rapor metrikleri: ticket sayısı, statü dağılımı, ortalama çözüm süresi, personel bazlı yük
- [ ] Tarih aralığı filtresi
- [ ] Dışa aktarım (Excel/PDF/CSV)

## 9. Global Ayarlar (Süper Admin)

- [ ] **Sistemde kullanılan tüm ayarlar arayüzden yönetilebiliyor**
- [ ] Mail ayarları (SMTP, gönderen adres, şablonlar)
- [ ] S3 / depolama ayarları
- [ ] Ticket statüleri ve öncelikleri
- [ ] Rol ve yetki tanımları
- [ ] Form alanları
- [ ] Kod içinde sabit (hard-coded) parametre kalmamış

## 10. Mimari Gereksinimler

- [ ] **Tüm parametreler tek bir sistem/config dosyasında tutuluyor**
- [ ] Proje MVC mimarisiyle yazılmış (Model / View / Controller katmanları ayrık)
- [ ] Yetkilendirmenin tamamı RBAC + permission tablosu üzerinden
- [ ] Backend metodları SOLID'e uygun
- [ ] **Her metod tek bir iş yapıyor (Single Responsibility)**
- [ ] Business logic controller'da değil, service katmanında
- [ ] Veri erişimi repository katmanında
- [ ] UI'da tekrar eden elemanlar component olarak yazılmış
- [ ] Frontend performans optimizasyonu (lazy load, memoization, sayfalama)
- [ ] **Tüm sistem REST API üzerinden haberleşiyor**
- [ ] API standart response formatı ve HTTP status kodları doğru
- [ ] API dokümantasyonu (Swagger/OpenAPI)
- [ ] Input validation her endpoint'te
- [ ] Veritabanı migration'ları mevcut

## 11. Doğrulama / Test

- [ ] Her rol için yetki matrisi testi (erişebilmesi/erişememesi gerekenler)
- [ ] Şirket izolasyonu testi
- [ ] Mail tetikleme testleri (her değişiklik tipi için)
- [ ] Dosya yükleme ve S3 erişim testi
- [ ] API endpoint testleri
