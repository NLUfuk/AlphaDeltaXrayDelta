# CRM + KANBAN Projesi Gereksinimleri ve Kontrol Listesi

## 1. Mimari ve Backend Standartları
- [ ] Tüm proje MVC (Model-View-Controller) mimarisine uygun olarak tasarlanmalıdır.
- [ ] Sistem tamamen RESTful API mimarisi ile haberleşecek şekilde kurulmalıdır.
- [ ] Backend metodları SOLID prensiplerine (özellikle Single Responsibility) tam uyumlu olmalı; her metod sadece tek bir iş yapmalıdır.
- [ ] Clean code yaklaşımları benimsenmeli (Sorumlulukların ayrılması için MediatR handler davranışları ve FluentValidation tabanlı request validasyon katmanları kullanılabilir).
- [ ] Tüm sistem parametreleri ve ayarları merkezi bir sistem konfigürasyon dosyasında tutulmalıdır.

## 2. Kimlik Doğrulama ve Yetkilendirme (Auth & RBAC)
- [ ] Kullanıcı kayıt (Register) ve giriş (Login) altyapısı oluşturulmalıdır.
- [ ] Yetkilendirme protokolleri tamamen RBAC (Role-Based Access Control) mimarisine uygun olarak tasarlanmalıdır.
- [ ] Sistemde 3 temel rol tanımlanmalıdır: **Süper Admin**, **Admin (Şirket)** ve **Müşteri**.
- [ ] Roller dışında esnek yetkilendirme yapılabilmesi için bir **Permission (İzin) tablosu** oluşturulmalı ve kullanıcılara özel yetkiler atanabilmelidir.
- [ ] Tüm yetkilendirme ve rol atama işlemleri proje arayüzünden kolayca yönetilebilmelidir.

## 3. Müşteri (Customer) Modülü
- [ ] Müşteriler e-posta adresleri aracılığıyla sisteme kayıt olabilmelidir.
- [ ] Müşteriler dış bir link aracılığıyla form ekranına ulaşıp istedikleri talepleri (ticket) doldurup gönderebilmelidir.
- [ ] Kendi açtıkları ticket'lara yorum/cevap yazabilmelidir.
- [ ] Ticket'lara görsel ve dosya yükleme yapabilmelidir.
- [ ] Yüklenen görseller ve dosyalar AWS S3 veya benzeri bir bulut depolama servisinde saklanmalıdır.
- [ ] Müşteriler, kendi inisiyatifleriyle açık ticket'ı "İptal" edebilir veya "Tamamlandı" statüsüne çevirebilir.

## 4. Admin / Şirket (Company) Modülü
- [ ] Her admin, sistemde bir veya birden fazla şirket (multi-tenant) hesabı oluşturabilmelidir.
- [ ] Sisteme dahil edilen her şirket için özel panolar (dashboard/kanban) oluşturulmalıdır.
- [ ] Adminler, sadece kendi şirketleri altındaki müşterilerin açtığı ticket'ları görebilmelidir.
- [ ] Adminler ticket detayına girip yorum yazabilmeli ve dosya/görsel yükleyebilmelidir.
- [ ] Gelen ticket'lar şirket içindeki personellere atanabilmelidir.
- [ ] Ticket statüleri admin tarafından değiştirilebilmelidir (Örn: Açık, Kapalı, İptal, Beklemede, Cevaplandı, Bekleniyor vb.).
- [ ] Adminler, ticket'ın başlığını, içeriğini ve içindeki yorumları düzenleyebilmeli veya silebilmelidir.
- [ ] Adminler, yönettiği şirkete veya şirketlere ait istatistiksel raporlar alabilmelidir.

## 5. Süper Admin (Superadmin) Modülü
- [ ] Süper admin, şirketlerden ve adminlerden bağımsız, tüm sistemi kapsayan global raporlar oluşturabilmelidir.
- [ ] Sistem içinde kullanılacak olan tüm global ayarlar (konfigürasyonlar) süper admin tarafından arayüz aracılığıyla yönetilebilmelidir.

## 6. Bildirim ve E-posta Sistemi
- [ ] E-posta gönderim servisi entegre edilmelidir.
- [ ] Ticket üzerinde yapılan en küçük bir değişiklikte dahi (durum değişimi, yeni yorum vb.) ticket'ı açan müşteriye otomatik bilgilendirme maili gönderilmelidir.

## 7. Önyüz (UI / Frontend) Gereksinimleri
- [ ] UI tasarımında tekrar eden elemanlar bağımsız bileşenler (component) halinde tasarlanıp kullanılmalıdır.
- [ ] Arayüz hızının iyileştirilmesi için performans ve render optimizasyonları yapılmalıdır.
