using System.Text;
using System.Text.Json;
using CrmKanban.Application.Abstractions;
using CrmKanban.Application.Auth;
using CrmKanban.Domain.Authorization;
using CrmKanban.Domain.Entities;
using CrmKanban.Domain.Enums;
using CrmKanban.Domain.Tickets;
using CrmKanban.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CrmKanban.Infrastructure.Persistence.Seed;

/// <summary>
/// Development-only demo data so the kanban/dashboard/moderation/detail have something real to show
/// (spec §17 demo). Idempotent + gated: each company bails if its slug already exists, and Program only
/// calls this in Development (or Seed:Demo=true).
///
/// <para>Four tenants across four sectors — textile, marble, a software agency and a logistics firm —
/// deliberately different from each other rather than four copies of one story, because the point is to
/// show that the same board carries unlike businesses. Each gets an admin + personel, customers, tickets
/// spread across every column, first-time (pending) public submissions for the moderation queue, internal
/// notes (staff-only), real attachments, custom public-form fields and realistic historical timestamps.
/// The agency additionally runs a FORKED board (its own cloned columns plus "Analiz" and "Test") — the
/// one place per-company kanban is visible as data. <see cref="SeedCrossCuttingAsync"/> adds what lives
/// BETWEEN tenants (a second company for one admin, permission overrides, customer trust, an opt-out) and
/// <see cref="SeedHistoryAsync"/> fills the record tables (comment revisions, a pending invite, the
/// permission audit trail, mail queue history including one stuck row).</para>
///
/// <para>Money is spread unevenly ON PURPOSE: several tickets carry no amount, so the report's "tutarı
/// girilmemiş" case is real data. A demo where every number is populated hides exactly the states an
/// operator will actually meet.</para>
/// </summary>
public sealed class DevSeeder(
    DbContextOptions<CrmDbContext> options,
    PasswordHasher<User> passwordHasher,
    IFileStorage storage,
    IOptions<DemoOptions> demoOptions,
    ILogger<DevSeeder> logger)
{
    /// <summary>The demo accounts' shared password, from configuration (<c>Seed:DemoPassword</c>).
    /// It used to be a constant right here — and this repository is public, so that constant was a
    /// working live credential for anyone who read it. Never put it back.</summary>
    private string Password => demoOptions.Value.DemoPassword!;

    private sealed record Person(string Email, string First, string Last);
    // A tiny attachment: filename + text content the seeder actually stores, so the download round-trips.
    private sealed record FileSpec(string Name, string ContentType, string Content);
    // A configurable public-form field (companyId is filled in once the company exists).
    private sealed record FormFieldSpec(string Label, FormFieldType Type, bool Required, int SortOrder, string? Options = null);
    // Body/Comments give the ticket a CRM feel; Internal notes are staff-only (amber, hidden from the
    // customer); Files become real attachments; Fields render as the custom public-form values (§4.6).
    private sealed record TicketSpec(
        DefaultStatuses.StatusDef Status, bool Assigned, Priority Priority, string Title,
        string Body, string[] Conversation, bool Pending = false,
        string[]? Internal = null, FileSpec[]? Files = null, (string Label, string Value)[]? Fields = null,
        // Money (Faz 39). Estimated is what it was quoted at; Actual is what it came to — set only on
        // tickets that closed, so "tahmin isabeti" has both numbers to divide. Leaving both null is a
        // deliberate case too: the report must show unpriced tickets as unpriced, not as zero.
        decimal? Estimated = null, decimal? Actual = null);

    public async Task SeedAsync(CancellationToken ct = default)
    {
        // Fail closed. Without a configured password there is nothing sensible to give these accounts:
        // inventing one would either be guessable (the old constant) or unknowable to the operator.
        if (!demoOptions.Value.HasUsablePassword)
        {
            logger.LogWarning(
                "Demo seeding skipped: Seed:DemoPassword is not set (or shorter than {Min} characters). "
                + "Set it via environment (Seed__DemoPassword) to enable the demo tenants.",
                DemoOptions.MinPasswordLength);
            return;
        }

        await using var db = new CrmDbContext(options, SystemCurrentUserService.System);

        var pdf = new FileSpec("teklif-formu.txt", "text/plain", "Demo teklif dokumani — birim fiyat ve teslim kosullari.");
        var photo = new FileSpec("hasar-tutanagi.txt", "text/plain", "Demo tutanak — hasar tespiti ve fotograf referanslari.");

        await SeedCompanyAsync(db, "Anadolu Tekstil", DemoTenants.Tekstil,
            admin: new Person("admin@tekstil.local", "Derya", "Yönetici"),
            personel: new Person("uretim@tekstil.local", "Pelin", "Üretim"),
            customers:
            [
                new Person("alici1@tekstil-musteri.local", "Ayşe", "Kaya"),
                new Person("alici2@tekstil-musteri.local", "Mehmet", "Demir"),
                new Person("alici3@tekstil-musteri.local", "Zeynep", "Şahin"),
            ],
            // Custom public-form fields for this tenant — shown on the public form and the admin form-fields
            // screen; the first ticket below carries matching captured values so the detail dl renders.
            formFields:
            [
                new("Sipariş No", FormFieldType.Text, true, 0),
                new("Ürün Kategorisi", FormFieldType.Select, true, 1, "Kumaş\nİplik\nAksesuar"),
                new("Ek açıklama", FormFieldType.TextArea, false, 2),
            ],
            specs:
            [
                new(DefaultStatuses.New, false, Priority.Normal, "Teklif talebi: 5.000 m pamuklu poplin",
                    "Yaz koleksiyonu için 5.000 metre 120gr pamuklu poplin fiyat teklifi rica ediyoruz.",
                    ["Merhaba, en-boy toleransları ve teslim süresini de belirtir misiniz?"],
                    Estimated: 420_000m,
                    Files: [pdf],
                    Fields: [("Sipariş No", "SIP-2050"), ("Ürün Kategorisi", "Kumaş"), ("Ek açıklama", "Numune de rica ederiz.")]),
                new(DefaultStatuses.New, false, Priority.High, "Sipariş metrajı eksik geldi",
                    "SIP-2041 numaralı siparişte 300 metre eksik teslim aldık, kontrol eder misiniz?",
                    []), // bilerek fiyatsız: rapor "tutarı girilmemiş" sayısını göstersin
                new(DefaultStatuses.InProgress, true, Priority.Urgent, "Sevkiyat termini kaçtı",
                    "İhracat sevkiyatının termini bugündü, kargo hâlâ çıkmadı. Acil dönüş bekliyoruz.",
                    ["Konuyu lojistik ekibine ilettim, gün içinde bilgi vereceğim."],
                    Internal: ["Lojistik: araç arızası nedeniyle gecikme; müşteriye tam saat vermeyelim.", "Tazminat riski var, admin bilgilendirildi."], Estimated: 95_000m),
                new(DefaultStatuses.InProgress, true, Priority.Normal, "Numune kumaş talebi (3 renk)",
                    "Antrasit, lacivert ve haki tonlarında birer metre numune gönderebilir misiniz?",
                    ["Numune talebini aldık, hazırlanıyor.", "Kargo takip numarasını paylaşır mısınız?"],
                    Files: [pdf], Estimated: 18_500m),
                new(DefaultStatuses.InProgress, true, Priority.Low, "Fatura tutarı sözleşmeyle uyumsuz",
                    "FTR-8890 faturasındaki birim fiyat, sözleşmedeki fiyattan yüksek görünüyor.",
                    ["Muhasebe ile kontrol ediyoruz, düzeltme faturası gerekebilir."], Estimated: 62_000m),
                new(DefaultStatuses.Answered, true, Priority.Normal, "Boya reçetesi paylaşımı",
                    "Geçen sezon kullandığımız bordo tonun reçetesini tekrar iletebilir misiniz?",
                    ["Reçeteyi ve pantone karşılığını e-postanıza gönderdik."], Estimated: 24_000m),
                new(DefaultStatuses.Waiting, true, Priority.Normal, "Onaylı numune bekleniyor",
                    "Gönderdiğiniz numuneyi müşteriye ilettik, onay dönüşünü bekliyoruz.",
                    ["Müşteri onayı gelince üretime alacağız."], Estimated: 310_000m),
                new(DefaultStatuses.Completed, true, Priority.Normal, "Top kumaş iadesi tamamlandı",
                    "Hatalı boyanan 2 top kumaşın iadesi ve değişimi tamamlandı.",
                    ["İade tarafımıza ulaştı, değişim sevk edildi. Teşekkürler."], Estimated: 140_000m, Actual: 132_500m),
                new(DefaultStatuses.Cancelled, false, Priority.Low, "Yanlış açılan sipariş",
                    "Bu talep yanlışlıkla oluşturuldu, iptal edilebilir.",
                    [], Estimated: 47_000m),
                new(DefaultStatuses.New, false, Priority.Normal, "Yeni bayi — pamuklu kumaş fiyat listesi",
                    "Bayilik başvurusu sonrası toptan fiyat listenizi paylaşır mısınız?",
                    [], Pending: true),
            ], ct);

        await SeedCompanyAsync(db, "Ege Mermer", DemoTenants.Mermer,
            admin: new Person("admin@mermer.local", "Kerem", "Yönetici"),
            personel: new Person("saha@mermer.local", "Burak", "Saha"),
            customers:
            [
                new Person("alici1@mermer-musteri.local", "Elif", "Aydın"),
                new Person("alici2@mermer-musteri.local", "Can", "Yılmaz"),
                new Person("alici3@mermer-musteri.local", "Deniz", "Arslan"),
            ],
            formFields: [],
            specs:
            [
                new(DefaultStatuses.New, false, Priority.High, "Plakada çatlak — değişim talebi",
                    "Teslim aldığımız 3 mermer plakanın ikisinde çatlak var, değişim rica ediyoruz.",
                    ["Fotoğrafları iletebilir misiniz? Kalite ekibine yönlendireceğiz."],
                    Files: [photo], Estimated: 76_000m),
                new(DefaultStatuses.InProgress, true, Priority.Urgent, "Ebat ölçüleri yanlış kesilmiş",
                    "Sipariş ettiğimiz 60x60 yerine 60x40 kesilmiş, şantiyede iş durdu.",
                    ["Çok özür dileriz, doğru ebatları öncelikli üretime alıyoruz."],
                    Internal: ["CNC operatör hatası; yeniden kesim maliyeti bizde. Vardiya amirine iletildi."],
                    Files: [photo], Estimated: 210_000m),
                new(DefaultStatuses.New, false, Priority.Normal, "Teklif talebi: 200 m² traverten",
                    "Villa projesi için 200 m² klasik traverten fiyat ve stok durumu rica ederiz.",
                    [], Estimated: 485_000m),
                new(DefaultStatuses.InProgress, true, Priority.Normal, "Sevkiyatta kırık oluştu",
                    "Nakliye sırasında 4 plaka kırılmış, tutanak tuttuk. Tazmin süreci nasıl işler?",
                    ["Tutanağı aldık, sigorta sürecini başlattık.", "Değişim plakaları ne zaman çıkar?"],
                    Internal: ["Sigorta dosya no SGK-7781 açıldı; müşteriye 5 iş günü demeyelim, 3 gün hedef."], Estimated: 33_000m),
                new(DefaultStatuses.InProgress, true, Priority.Low, "Cila kalitesi beklenenden düşük",
                    "Son partide yüzey parlaklığı önceki siparişe göre mat kaldı.",
                    ["Numuneyle karşılaştırıyoruz, gerekirse yeniden cilalayacağız."]),
                new(DefaultStatuses.Answered, true, Priority.Normal, "Blok renk eşleşmesi gönderildi",
                    "Aynı bloktan devam plakası mümkün mü, renk tutması önemli.",
                    ["Aynı bloktan rezerve ettik, renk eşleşme fotoğrafını gönderdik."], Estimated: 158_000m),
                new(DefaultStatuses.Waiting, true, Priority.Normal, "Ödeme dekontu bekleniyor",
                    "Proforma onaylandı, havale dekontunu gönderdiğimizde üretim başlar mı?",
                    ["Dekont ulaşınca üretim planına alıyoruz."], Estimated: 92_000m),
                new(DefaultStatuses.Completed, true, Priority.Normal, "İade plakalar teslim alındı",
                    "Fazla gelen 5 plakanın iadesi tamamlandı, bakiye güncellendi.",
                    ["İade tamamlandı, güncel bakiye ekstrenizde. Teşekkürler."], Estimated: 260_000m, Actual: 287_000m),
                new(DefaultStatuses.Cancelled, false, Priority.Low, "Mükerrer talep",
                    "Aynı talebi iki kez oluşturmuşuz, bu kaydı kapatabilirsiniz.",
                    [], Estimated: 15_000m),
                new(DefaultStatuses.New, false, Priority.Normal, "Yeni müteahhit — traverten teklifi",
                    "Toplu konut projesi için traverten fiyat teklifi ve referans talep ediyoruz.",
                    [], Pending: true),
            ], ct);

        // A services firm, on a FORKED board: the two extra columns are the only place in the demo where
        // "her şirketin kendi panosu" is visible as data rather than as a claim. Money is on nearly every
        // ticket here — an agency prices per job, so the revenue report has a dense month to draw.
        await SeedCompanyAsync(db, "Piksel Yazılım Ajansı", DemoTenants.Yazilim,
            admin: new Person("admin@piksel.local", "Selin", "Yönetici"),
            personel: new Person("gelistirici@piksel.local", "Emre", "Geliştirici"),
            customers:
            [
                new Person("musteri1@piksel-musteri.local", "Gökhan", "Tunç"),
                new Person("musteri2@piksel-musteri.local", "Nazlı", "Ergin"),
                new Person("musteri3@piksel-musteri.local", "Serkan", "Öz"),
            ],
            formFields:
            [
                new("Proje Adı", FormFieldType.Text, true, 0),
                new("Talep Tipi", FormFieldType.Select, true, 1, "Hata\nYeni özellik\nEntegrasyon\nDanışmanlık"),
                new("Aciliyet gerekçesi", FormFieldType.TextArea, false, 2),
            ],
            specs:
            [
                new(DefaultStatuses.New, false, Priority.High, "Ödeme adımında 500 hatası",
                    "Sepetten ödemeye geçerken kullanıcıların yaklaşık %10'u hata alıyor, sipariş düşüyor.",
                    ["Log kayıtlarını inceliyoruz, ödeme sağlayıcı tarafında zaman aşımı görünüyor."],
                    Internal: ["Sağlayıcının sandbox'ı da yavaş; kendi retry'ımızı ekleyelim, suçu onlara atmayalım."],
                    Estimated: 45_000m,
                    Fields: [("Proje Adı", "B2C Mağaza"), ("Talep Tipi", "Hata"), ("Aciliyet gerekçesi", "Ciro kaybı var.")]),
                new(Analiz, true, Priority.Normal, "Kargo entegrasyonu (3 firma)",
                    "Üç kargo firmasının API'siyle otomatik gönderi oluşturmak istiyoruz.",
                    ["Üç sağlayıcının dokümanını inceledik, ortak bir arayüz öneriyoruz."],
                    Estimated: 180_000m,
                    Fields: [("Proje Adı", "Lojistik Modülü"), ("Talep Tipi", "Entegrasyon")]),
                new(Analiz, true, Priority.Normal, "Bayi paneli için yetki matrisi",
                    "Bayilerin sadece kendi siparişlerini görmesi gerekiyor, şu an hepsini görüyorlar.",
                    ["Yetki modelini çıkardık, onayınıza sunacağız."],
                    Internal: ["Mevcut sorguda şirket filtresi yok — bu bir veri sızıntısı, öncelik yükseltilmeli."],
                    Estimated: 96_000m),
                new(Test, true, Priority.High, "Mobil görünümde menü açılmıyor",
                    "iOS Safari'de hamburger menüye basınca hiçbir şey olmuyor.",
                    ["Sorunu tekrar ürettik, düzeltme test ortamında.", "Test ortamında bizde de düzeldi, teşekkürler."],
                    Estimated: 12_000m),
                new(Test, true, Priority.Normal, "Rapor ekranına PDF çıktısı",
                    "Aylık raporu PDF olarak indirebilmek istiyoruz.",
                    ["PDF çıktısı hazır, yazı tipi ve logo yerleşimini kontrol eder misiniz?"],
                    Files: [pdf], Estimated: 34_000m),
                new(DefaultStatuses.Answered, true, Priority.Normal, "SEO meta etiketleri güncellensin",
                    "Ürün sayfalarında başlık ve açıklama etiketleri boş görünüyor.",
                    ["Şablonu güncelledik, yeni etiketler yayında."], Estimated: 8_500m, Actual: 8_500m),
                new(DefaultStatuses.Waiting, true, Priority.Low, "Marka kılavuzu bekleniyor",
                    "Yeni tasarım için kurumsal renk ve font kılavuzunu iletecektik.",
                    ["Kılavuz ulaşınca arayüzü güncelleyeceğiz."], Estimated: 60_000m),
                new(DefaultStatuses.Completed, true, Priority.Normal, "Yıllık bakım paketi tamamlandı",
                    "2025 yılı bakım ve güncelleme paketinin kapanışı.",
                    ["Tüm bakım kalemleri tamamlandı, kapanış raporu ektedir."],
                    Files: [pdf], Estimated: 240_000m, Actual: 265_000m),
                new(DefaultStatuses.Completed, true, Priority.Normal, "Arama hızlandırma çalışması",
                    "Ürün aramasının 3 saniyeden aşağı inmesi gerekiyordu.",
                    ["Ortalama 400 ms'ye indi, ölçüm raporunu paylaştık."], Estimated: 75_000m, Actual: 71_000m),
                new(DefaultStatuses.Cancelled, false, Priority.Low, "Eski panelin yeniden yazımı",
                    "Bütçe onayı çıkmadığı için bu talebi kapatıyoruz.",
                    [], Estimated: 520_000m),
                new(DefaultStatuses.New, false, Priority.Normal, "Yeni müşteri — e-ticaret altyapısı",
                    "Sıfırdan e-ticaret sitesi için teklif ve örnek çalışma talep ediyoruz.",
                    [], Pending: true),
            ], ct, extraColumns: [Analiz, Test]);

        // Operations, global board, deliberately money-light: several tickets carry no amount at all so
        // the report's "tutarı girilmemiş" case is real data and not a hypothetical.
        await SeedCompanyAsync(db, "Marmara Lojistik", DemoTenants.Lojistik,
            admin: new Person("admin@marmara.local", "Hakan", "Yönetici"),
            personel: new Person("operasyon@marmara.local", "İpek", "Operasyon"),
            customers:
            [
                new Person("musteri1@marmara-musteri.local", "Buse", "Çetin"),
                new Person("musteri2@marmara-musteri.local", "Onur", "Kılıç"),
            ],
            formFields:
            [
                new("Sevkiyat No", FormFieldType.Text, true, 0),
                new("Yük Tipi", FormFieldType.Select, false, 1, "Paletli\nDökme\nSoğuk zincir\nTehlikeli madde"),
            ],
            specs:
            [
                new(DefaultStatuses.New, false, Priority.Urgent, "Soğuk zincir kırıldı — 2 palet",
                    "Sıcaklık kaydı 8 dereceye çıkmış görünüyor, ürünler risk altında.",
                    ["Veri kaydediciyi çektik, inceliyoruz. Ürünleri şimdilik ayırın."],
                    Internal: ["Araç 34 ABC 123 — kompresör arızası. Sigorta bildirimi bugün yapılmalı."],
                    Files: [photo], Estimated: 130_000m),
                new(DefaultStatuses.New, false, Priority.Normal, "İrsaliye numarası hatalı basılmış",
                    "SVK-4410 sevkiyatının irsaliyesinde numara yanlış, düzeltme gerekiyor.",
                    []), // bilerek fiyatsız
                new(DefaultStatuses.InProgress, true, Priority.High, "Gümrükte bekleyen konteyner",
                    "Konteyner 3 gündür gümrükte, evrak eksiği olduğu söylendi.",
                    ["Eksik evrak menşe belgesiydi, bugün ilettik."], Estimated: 88_000m),
                new(DefaultStatuses.InProgress, true, Priority.Normal, "Teslimat adresi değişikliği",
                    "Alıcı adres değiştirdi, araç yola çıkmadan güncellenebilir mi?",
                    ["Adres güncellendi, sürücüye bildirildi."]), // bilerek fiyatsız
                new(DefaultStatuses.Answered, true, Priority.Normal, "Aylık sevkiyat raporu talebi",
                    "Geçen ayın sevkiyat özetini tablo halinde alabilir miyiz?",
                    ["Rapor hazırlandı ve e-postanıza gönderildi."]),
                new(DefaultStatuses.Waiting, true, Priority.Normal, "Palet ölçüleri bekleniyor",
                    "Fiyat verebilmemiz için palet ebat ve ağırlıklarını iletmeniz gerekiyor.",
                    ["Ölçüler ulaşınca teklifi geçiyoruz."], Estimated: 26_000m),
                new(DefaultStatuses.Completed, true, Priority.Normal, "Yurt içi dağıtım turu tamamlandı",
                    "12 noktalı dağıtım turunun kapanışı ve teslim tutanakları.",
                    ["Tüm noktalar teslim edildi, tutanaklar ektedir."],
                    Files: [photo], Estimated: 190_000m, Actual: 178_000m),
                new(DefaultStatuses.Cancelled, false, Priority.Low, "İptal edilen sevkiyat",
                    "Alıcı siparişi iptal etti, sevkiyata gerek kalmadı.",
                    [], Estimated: 41_000m),
                new(DefaultStatuses.New, false, Priority.Normal, "Yeni müşteri — depo + dağıtım teklifi",
                    "Depolama ve şehir içi dağıtım için fiyat çalışması rica ediyoruz.",
                    [], Pending: true),
            ], ct);

        await db.SaveChangesAsync(ct);
        await SeedCrossCuttingAsync(db, ct);
        await SeedHistoryAsync(db, ct);

        // Runs on EVERY startup, not just the first: an environment seeded under the old constant is
        // exactly the case this has to fix.
        await RotateDemoPasswordsAsync(db, ct);
    }

    // The two workflow columns that make "Piksel Yazılım" a FORKED board rather than a copy of the
    // global one. Fixed ids keep the seed idempotent, exactly like DefaultStatuses. Order here is
    // ignored — ResolveBoardAsync re-indexes by board position.
    private static readonly DefaultStatuses.StatusDef Analiz = new(
        Guid.Parse("22222222-0000-0000-0000-000000000001"), "Analiz", StatusCategory.Pending, "#0ea5e9", 0, false);
    private static readonly DefaultStatuses.StatusDef Test = new(
        Guid.Parse("22222222-0000-0000-0000-000000000002"), "Test", StatusCategory.Answered, "#a855f7", 0, false);

    /// <summary>
    /// The features the two-tenant demo above cannot show, because they are about the relationships
    /// BETWEEN tenants and users rather than about tickets (Faz 42). Each one exists because a
    /// requirement claims it works and nothing in the seed data proved it:
    /// <list type="bullet">
    ///   <item><b>One admin, two companies.</b> "Her admin bir veya birden fazla şirket hesabı
    ///   oluşturabilir" — with one company each, the navbar company switcher never even renders.</item>
    ///   <item><b>Per-user permission overrides.</b> This is what makes the RBAC a permission table
    ///   and not just four roles: a Grant above the role and a Deny below it, with Deny winning.</item>
    ///   <item><b>Customer trust.</b> Faz 35's rule (auto-approval only via a staff invite) is
    ///   invisible until one customer is trusted and another is not.</item>
    ///   <item><b>A notification opt-out</b>, so the fan-out has someone to skip.</item>
    /// </list>
    /// Idempotent like the rest: bails if the extra company already exists.
    /// </summary>
    private async Task SeedCrossCuttingAsync(CrmDbContext db, CancellationToken ct)
    {
        const string secondSlug = DemoTenants.TekstilIhracat;
        if (await db.Companies.IgnoreQueryFilters().AnyAsync(c => c.Slug == secondSlug, ct)) return;

        var admin = await db.Users.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.Email == "admin@tekstil.local", ct);
        var personel = await db.Users.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.Email == "uretim@tekstil.local", ct);
        var trusted = await db.Users.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.Email == "alici1@tekstil-musteri.local", ct);
        if (admin is null || personel is null || trusted is null) return;

        // 1. A second company for the same admin → the company switcher has something to switch.
        var second = new Company("Anadolu Tekstil İhracat", secondSlug, admin.Id);
        db.Companies.Add(second);
        db.Memberships.Add(new Membership(admin.Id, second.Id, RoleType.Admin));

        // 2. Per-user overrides at the FIRST company, where the demo tickets live.
        // NOT SingleAsync over IgnoreQueryFilters: that set includes soft-deleted rows, so a company
        // that was created, deleted and re-created under the same slug returns two and Single throws.
        // This is tech debt #42's trap (filter bypassed without restating DeletedAt) — it took the
        // live site down once already, from this very line.
        var firstCompanyId = await db.Companies.IgnoreQueryFilters()
            .Where(c => c.Slug == "tekstil" && c.DeletedAt == null)
            .Select(c => c.Id).FirstOrDefaultAsync(ct);
        if (firstCompanyId == Guid.Empty) return;
        var permissions = await db.Permissions.IgnoreQueryFilters().ToDictionaryAsync(p => p.Key, ct);

        // Everything below is INSERT-IF-MISSING, like the rest of the seeders. The slug guard above
        // only proves this block never ran before; it says nothing about rows that arrived some other
        // way. A real example took the live site down: an operator had already trusted this customer
        // through the moderation screen ("Onayla + güven"), so the plain insert hit the CustomerTrusts
        // unique index and killed startup. Demo data must fit around whatever is already there.
        var existingOverrides = await db.UserPermissions.IgnoreQueryFilters()
            .Where(p => p.UserId == personel.Id && p.CompanyId == firstCompanyId && p.DeletedAt == null)
            .Select(p => p.PermissionId).ToListAsync(ct);

        // Grant: personel normally has no money access; this one is trusted with the order book.
        if (permissions.TryGetValue(PermissionKeys.TicketValue, out var value) && !existingOverrides.Contains(value.Id))
            db.UserPermissions.Add(new UserPermission(personel.Id, value.Id, UserPermissionType.Grant, firstCompanyId));

        // Deny: the same person may not delete tickets even though nothing in their role grants it —
        // an explicit Deny is what proves "Deny wins" is reachable from real data, not only from tests.
        if (permissions.TryGetValue(PermissionKeys.TicketDelete, out var del) && !existingOverrides.Contains(del.Id))
            db.UserPermissions.Add(new UserPermission(personel.Id, del.Id, UserPermissionType.Deny, firstCompanyId));

        // 3. One trusted customer; the other two stay untrusted so a new submission from them still
        //    lands in the moderation queue (Faz 35).
        if (!await db.CustomerTrusts.IgnoreQueryFilters()
                .AnyAsync(t => t.CompanyId == firstCompanyId && t.UserId == trusted.Id && t.DeletedAt == null, ct))
            db.CustomerTrusts.Add(new CustomerTrust(firstCompanyId, trusted.Id, admin.Id));

        // 4. An opt-out: this customer does not want comment mail, so fan-out has a skip to honour.
        if (!await db.UserNotificationPrefs.IgnoreQueryFilters()
                .AnyAsync(p => p.UserId == trusted.Id && p.EventType == TicketEventType.CommentAdded, ct))
            db.UserNotificationPrefs.Add(new UserNotificationPref(trusted.Id, TicketEventType.CommentAdded, enabled: false));

        await db.SaveChangesAsync(ct);
        logger.LogInformation(
            "Dev demo extras seeded: second company '{Slug}', 1 grant + 1 deny override, 1 trusted customer, 1 opt-out.",
            secondSlug);
    }

    /// <summary>
    /// The four tables the demo never wrote to, so a schema walkthrough hit empty relations exactly where
    /// the interesting history lives (Faz 43). Each one is a RECORD the application produces at runtime;
    /// seeding them is what makes "her tablo bir işe yarıyor" checkable instead of assertable:
    /// <list type="bullet">
    ///   <item><b>CommentRevision</b> — a staff edit keeps the pre-edit body. The comment is edited
    ///   through <c>Comment.Edit</c>, which returns the old text, rather than by writing both rows by
    ///   hand: the same call the service makes, so the "düzenlendi" marker and the revision cannot
    ///   disagree.</item>
    ///   <item><b>Invitation</b> — one UNACCEPTED staff invite, so the flow has a live token and not
    ///   just a code path. Hashed, never stored raw; the raw value only ever leaves by mail.</item>
    ///   <item><b>AuditLog</b> — the permission grant/deny that SeedCrossCuttingAsync creates leaves no
    ///   trail unless someone writes one, and "kim hangi yetkiyi verdi" is the first question asked
    ///   after an incident.</item>
    ///   <item><b>EmailQueue</b> — sent history plus ONE failed row. The failure is deliberate: a queue
    ///   that only ever shows green teaches nobody what a stuck notification looks like.</item>
    /// </list>
    /// Insert-if-missing throughout, and every step bails quietly if its subject is absent — a partially
    /// seeded database must not take startup down (the demo seed is a convenience, never a dependency).
    /// </summary>
    private async Task SeedHistoryAsync(CrmDbContext db, CancellationToken ct)
    {
        var editor = await db.Users.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.Email == "uretim@tekstil.local", ct);
        var admin = await db.Users.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.Email == "admin@tekstil.local", ct);
        if (editor is null || admin is null) return;

        var companyId = await db.Companies.IgnoreQueryFilters()
            .Where(c => c.Slug == "tekstil" && c.DeletedAt == null)
            .Select(c => c.Id).FirstOrDefaultAsync(ct);
        if (companyId == Guid.Empty) return;

        // 1. Comment edit + revision. Pick the oldest staff comment that has not been edited yet.
        var comment = await db.Comments.IgnoreQueryFilters()
            .Where(c => c.CompanyId == companyId && c.AuthorId == editor.Id && !c.IsInternal
                        && c.EditedAt == null && c.DeletedAt == null)
            .OrderBy(c => c.CreatedAt).FirstOrDefaultAsync(ct);
        if (comment is not null)
        {
            var editedAt = comment.CreatedAt.AddHours(3);
            var oldBody = comment.Edit(comment.Body + " (Düzeltme: teslim süresi 10 iş günü, 7 değil.)", editedAt);
            db.CommentRevisions.Add(new CommentRevision(companyId, comment.Id, oldBody, editor.Id) { CreatedAt = editedAt });
        }

        // 2. A pending staff invite: the user row exists but has never signed in, and the token is live.
        const string inviteEmail = "muhasebe@tekstil.local";
        if (!await db.Users.IgnoreQueryFilters().AnyAsync(u => u.Email == inviteEmail, ct))
        {
            var invited = new User(inviteEmail, "Tuna", "Muhasebe");
            db.Users.Add(invited);
            db.Memberships.Add(new Membership(invited.Id, companyId, RoleType.Personel));
            // The raw token is a demo constant so the invite link can actually be walked; what is stored
            // is only its hash, which is the property that matters and the one worth demonstrating.
            db.Invitations.Add(new Invitation(
                invited.Id, TokenHasher.Hash("demo-invite-token-tekstil"),
                DateTime.UtcNow.AddDays(7), admin.Id, InvitationKind.Link));
        }

        // 3. Audit trail for the two overrides SeedCrossCuttingAsync grants.
        if (!await db.AuditLogs.IgnoreQueryFilters().AnyAsync(a => a.Action == "permission.assign" && a.ActorId == admin.Id, ct))
        {
            db.AuditLogs.Add(new AuditLog(admin.Id, "permission.assign",
                $"Grant ticket.value → {editor.Email} (company {companyId})"));
            db.AuditLogs.Add(new AuditLog(admin.Id, "permission.assign",
                $"Deny ticket.delete → {editor.Email} (company {companyId})"));
        }

        // 4. Mail history: two delivered, one stuck. The stuck row carries a real-looking SMTP error so
        //    the retry path has something recognisable to show.
        if (!await db.EmailQueue.IgnoreQueryFilters().AnyAsync(ct))
        {
            var ticket = await db.Tickets.IgnoreQueryFilters()
                .Where(t => t.CompanyId == companyId && t.DeletedAt == null)
                .OrderBy(t => t.CreatedAt).FirstOrDefaultAsync(ct);
            if (ticket is not null)
            {
                var openerEmail = await db.Users.IgnoreQueryFilters()
                    .Where(u => u.Id == ticket.OpenedById).Select(u => u.Email).FirstAsync(ct);

                var sent1 = Mail(openerEmail, "ticket_created", ticket.Number, ticket.Title, "yeni talep oluşturuldu");
                sent1.MarkSent(ticket.CreatedAt.AddMinutes(2));
                var sent2 = Mail(editor.Email, "ticket_staff_update", ticket.Number, ticket.Title, "durum güncellendi: İşlemde");
                sent2.MarkSent(ticket.CreatedAt.AddHours(6));
                var stuck = Mail("kapali-kutu@tekstil-musteri.local", "ticket_comment_added", ticket.Number, ticket.Title, "yeni yorum eklendi");
                stuck.MarkFailed("550 5.1.1 Recipient address rejected: User unknown", maxAttempts: 5);

                db.EmailQueue.AddRange(sent1, sent2, stuck);
            }
        }

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Dev demo history seeded: comment revision, pending invite, permission audit, mail queue.");
    }

    /// <summary>A queue row shaped like the ones NotificationService writes (same payload keys).</summary>
    private static EmailQueue Mail(string to, string templateKey, string number, string title, string change) =>
        new(to, templateKey, JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["ticketNumber"] = number,
            ["title"] = title,
            ["change"] = change,
        }));

    private async Task SeedCompanyAsync(
        CrmDbContext db, string name, string slug, Person admin, Person personel,
        IReadOnlyList<Person> customers, IReadOnlyList<FormFieldSpec> formFields,
        IReadOnlyList<TicketSpec> specs, CancellationToken ct,
        IReadOnlyList<DefaultStatuses.StatusDef>? extraColumns = null)
    {
        if (await db.Companies.IgnoreQueryFilters().AnyAsync(c => c.Slug == slug, ct))
            return; // this tenant already seeded

        var adminUser = ActiveUser(admin);
        adminUser.AllowCompanyCreation();
        var personelUser = ActiveUser(personel);
        var customerUsers = customers.Select(ActiveUser).ToList();
        db.Users.AddRange([adminUser, personelUser, .. customerUsers]);

        var company = new Company(name, slug, adminUser.Id);
        db.Companies.Add(company);
        db.Memberships.Add(new Membership(adminUser.Id, company.Id, RoleType.Admin));
        db.Memberships.Add(new Membership(personelUser.Id, company.Id, RoleType.Personel));

        foreach (var f in formFields)
            db.FormFields.Add(new FormField(company.Id, f.Label, f.Type, f.Required, f.SortOrder, f.Options));

        // The real status rows and transition table the application uses — the seeder drives the same
        // state machine rather than a private copy of the rules.
        var (statuses, transitions, statusForDef) =
            await ResolveBoardAsync(db, company.Id, extraColumns ?? [], ct);

        var now = DateTime.UtcNow;
        var rnd = new Random(slug.GetHashCode()); // deterministic per company
        for (var i = 0; i < specs.Count; i++)
        {
            var spec = specs[i];
            var opener = customerUsers[i % customerUsers.Count];
            var ticket = new Ticket(company.Id, company.AllocateTicketNumber(), opener.Id,
                statusForDef[DefaultStatuses.New.Id].Id, spec.Title,
                spec.Body, spec.Priority);
            if (spec.Assigned) ticket.Assign(personelUser.Id);
            if (spec.Pending) ticket.MarkPendingApproval(); // a first-time submission awaiting moderation
            if (spec.Fields is { Length: > 0 })
                ticket.SetCustomFields(JsonSerializer.Serialize(
                    spec.Fields.Select(f => new { f.Label, f.Value })));

            // Spread over ~5 months so the monthly revenue trend has more than one bar to draw.
            var created = now.AddDays(-rnd.Next(1, 150)).AddHours(-rnd.Next(0, 8));
            ticket.CreatedAt = created; // honored by the audit interceptor now → realistic trend + timestamps
            if (spec.Estimated is not null || spec.Actual is not null)
                ticket.SetValue(spec.Estimated, spec.Actual);
            db.Tickets.Add(ticket);
            db.TicketEvents.Add(new TicketEvent(company.Id, ticket.Id, opener.Id, TicketEventType.Created, null, ticket.Number));

            // Walk the ticket to its target status through the REAL state machine instead of
            // materialising the end state. Two reasons, both of which bit us:
            //   1. Only ApplyStatus sets FirstResponseAt/ResolvedAt/ClosedAt. Tickets born "Tamamlandı"
            //      carried none of them, so every duration in the report and the PDF printed "—".
            //   2. Seed rows that never went through EnsureCanTransition can encode a state the
            //      application itself would reject — demo data that lies about what the system allows.
            MoveTo(ticket, spec.Status, created, statuses, statusForDef, transitions, company.Id, personelUser.Id, db, rnd);

            // The conversation reads customer → staff → customer …; each reply is spaced an hour or two
            // later so the detail page shows a real chronology (and distinct Istanbul times).
            var clock = created;
            for (var c = 0; c < spec.Conversation.Length; c++)
            {
                clock = clock.AddHours(2 + rnd.Next(0, 6));
                var isStaff = c % 2 == 0; // first reply is staff answering the opener's request
                db.Comments.Add(new Comment(company.Id, ticket.Id,
                    isStaff ? personelUser.Id : opener.Id, spec.Conversation[c], isInternal: false)
                { CreatedAt = clock });
            }

            // Internal notes: staff-only (amber in the UI, never shown to the customer).
            foreach (var note in spec.Internal ?? [])
            {
                clock = clock.AddHours(1 + rnd.Next(0, 4));
                db.Comments.Add(new Comment(company.Id, ticket.Id, personelUser.Id, note, isInternal: true)
                { CreatedAt = clock });
            }

            // Attachments: store the bytes for real so the download round-trips, then add the row.
            // The store is a network dependency (S3/MinIO) that a fresh machine may not have running.
            // If it is unreachable, skip the attachment and keep seeding — demo data is a convenience,
            // and an unavailable bucket must not take the whole host down at startup, which is exactly
            // what it did before this guard. The row is skipped too: an Attachment pointing at bytes
            // that were never written would 500 the moment someone clicked download.
            foreach (var file in spec.Files ?? [])
            {
                var key = $"tickets/{ticket.Id:N}/{Guid.NewGuid():N}-{file.Name}";
                var bytes = Encoding.UTF8.GetBytes(file.Content);
                try
                {
                    await storage.PutAsync(key, new MemoryStream(bytes), file.ContentType, ct);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex,
                        "Demo attachment '{File}' skipped: the file store is unreachable. Seeding continues.",
                        file.Name);
                    continue;
                }
                db.Attachments.Add(new Attachment(company.Id, ticket.Id, commentId: null, key,
                    file.Name, file.ContentType, bytes.Length, personelUser.Id) { CreatedAt = created });
            }
        }

        // The password is a secret now — it is configured, not printed. Logs on shared hosting are
        // readable from the control panel, which would undo the whole point of taking it out of the repo.
        logger.LogInformation("Dev demo data seeded: company '{Slug}' ({Name}), admin {Admin}.",
            slug, name, adminUser.Email);
    }

    /// <summary>
    /// Drives a freshly created ticket to <paramref name="target"/> through <see cref="Ticket.ChangeStatus"/>,
    /// recording a <see cref="TicketEvent"/> for each hop with a plausible timestamp.
    /// <para>
    /// Terminal targets go via an intermediate hop (New → İşlemde → Tamamlandı) because that is how a
    /// real ticket travels, and because passing through an Answered status is what gives the report a
    /// FirstResponseAt to average. A single job: move one ticket, leave the trail.
    /// </para>
    /// </summary>
    private static void MoveTo(
        Ticket ticket, DefaultStatuses.StatusDef target, DateTime created,
        IReadOnlyDictionary<Guid, TicketStatus> statuses,
        IReadOnlyDictionary<Guid, TicketStatus> statusForDef,
        IReadOnlyCollection<StatusTransition> transitions,
        Guid companyId, Guid staffId, CrmDbContext db, Random rnd)
    {
        if (target.Id == DefaultStatuses.New.Id) return;

        // Admin voice: the seeder is standing in for staff, so it carries the staff permissions the
        // transition table asks for.
        var actor = StatusChangeActor.Staff(RoleType.Admin,
            [PermissionKeys.TicketStatusChange, PermissionKeys.TicketEdit]);

        // A completed ticket got answered before it was completed — routing it through Answered is
        // what gives the report a FirstResponseAt AND a ResolvedAt on the same row, so "ort. ilk
        // yanıt" and "ort. çözüm" are both measured rather than both dashes.
        var path = target.Id == DefaultStatuses.InProgress.Id
            ? [DefaultStatuses.InProgress]
            : target.Id == DefaultStatuses.Completed.Id
                ? [DefaultStatuses.InProgress, DefaultStatuses.Answered, target]
                : new[] { DefaultStatuses.InProgress, target };

        var clock = created;
        foreach (var step in path)
        {
            clock = clock.AddHours(3 + rnd.Next(1, 40));
            var from = statuses[ticket.StatusId];
            // statusForDef, not statuses: on a forked board the row that means "İşlemde" is the company's
            // own clone with its own id, so the spec's global StatusDef has to be translated first.
            var to = statusForDef[step.Id];
            ticket.ChangeStatus(from, to, transitions, actor, clock);
            db.TicketEvents.Add(new TicketEvent(companyId, ticket.Id, staffId,
                TicketEventType.StatusChanged, from.Name, to.Name) { CreatedAt = clock });
        }
    }

    /// <summary>
    /// The board this company's tickets live on, and the translation from the spec's global
    /// <see cref="DefaultStatuses.StatusDef"/> to the row that actually carries that meaning here.
    ///
    /// <para>With no <paramref name="extraColumns"/> the company rides the shared global set and the
    /// translation is the identity. With extra columns it gets a FORK — the exact thing
    /// <c>StatusManagementService</c> does on a company's first column edit: the six global statuses are
    /// cloned under new ids, the extras are appended, and a fresh transition mesh is built over the
    /// clones. Cloning rather than referencing is the whole point of the fork: a company that renames or
    /// reorders its board must not touch anyone else's.</para>
    ///
    /// <para>The mesh follows the Faz 7b rule: every non-terminal column may reach every other column;
    /// terminals are sinks. Nothing may return to "Yeni" — a reopened ticket goes through Reopen, which
    /// is a different path with its own window check.</para>
    /// </summary>
    private static async Task<(Dictionary<Guid, TicketStatus> ById,
                               List<StatusTransition> Transitions,
                               Dictionary<Guid, TicketStatus> ByDef)> ResolveBoardAsync(
        CrmDbContext db, Guid companyId, IReadOnlyList<DefaultStatuses.StatusDef> extraColumns, CancellationToken ct)
    {
        var global = await db.TicketStatuses.IgnoreQueryFilters()
            .Where(s => s.CompanyId == null).ToListAsync(ct);
        var globalTransitions = await db.StatusTransitions.IgnoreQueryFilters().ToListAsync(ct);

        if (extraColumns.Count == 0)
            return (global.ToDictionary(s => s.Id), globalTransitions, global.ToDictionary(s => s.Id));

        // Board order is built explicitly rather than sorted by StatusDef.Order: the custom columns slot
        // between "İşlemde" and "Yanıtlandı" (that is where a workflow step belongs — after work starts,
        // before the customer is answered), and Order is re-indexed to the position so the kanban never
        // shows two columns claiming the same slot. Terminals stay last.
        List<DefaultStatuses.StatusDef> ordered =
        [
            DefaultStatuses.New, DefaultStatuses.InProgress,
            .. extraColumns,
            DefaultStatuses.Answered, DefaultStatuses.Waiting,
            DefaultStatuses.Completed, DefaultStatuses.Cancelled,
        ];

        var byDef = new Dictionary<Guid, TicketStatus>();
        var rows = new List<TicketStatus>();
        for (var i = 0; i < ordered.Count; i++)
        {
            var def = ordered[i];
            var row = new TicketStatus(def.Name, def.Category, def.Color, i, def.IsTerminal, companyId);
            rows.Add(row);
            byDef[def.Id] = row;
        }
        db.TicketStatuses.AddRange(rows);

        var transitions = new List<StatusTransition>();
        foreach (var from in rows.Where(s => !s.IsTerminal))
            foreach (var to in rows.Where(to => to.Id != from.Id && to.Name != DefaultStatuses.New.Name))
                transitions.Add(new StatusTransition(from.Id, to.Id, null));
        db.StatusTransitions.AddRange(transitions);

        return (rows.ToDictionary(s => s.Id), transitions, byDef);
    }

    /// <summary>
    /// Brings every existing demo account onto the configured password.
    ///
    /// <para>This is the half that actually closes the hole. <see cref="SeedCompanyAsync"/> bails out as
    /// soon as the company slug exists, so on an environment that was already seeded, changing the
    /// password in configuration would change NOTHING — the old hashes would keep working and the fix
    /// would be imaginary. (Faz 53 taught this exact lesson with the allowed file types: a code change
    /// that never touches existing rows does not change the running system.)</para>
    ///
    /// <para>Who counts as a demo account: anyone who is a member of a demo tenant, or who opened a
    /// ticket in one. Never a super admin — they hold the real credentials from
    /// <see cref="DatabaseSeeder"/> and must not be touched — and never an account that has no password
    /// yet, because those are pending invitations and setting a password would silently activate them.</para>
    /// </summary>
    private async Task<int> RotateDemoPasswordsAsync(CrmDbContext db, CancellationToken ct)
    {
        var companyIds = await db.Companies.IgnoreQueryFilters()
            .Where(c => DemoTenants.Slugs.Contains(c.Slug)).Select(c => c.Id).ToListAsync(ct);
        if (companyIds.Count == 0) return 0;

        var memberIds = await db.Memberships.IgnoreQueryFilters()
            .Where(m => companyIds.Contains(m.CompanyId)).Select(m => m.UserId).ToListAsync(ct);
        var openerIds = await db.Tickets.IgnoreQueryFilters()
            .Where(t => companyIds.Contains(t.CompanyId)).Select(t => t.OpenedById).ToListAsync(ct);
        var ids = memberIds.Concat(openerIds).Distinct().ToList();

        var users = await db.Users.IgnoreQueryFilters()
            .Where(u => ids.Contains(u.Id) && !u.IsSuperAdmin && u.PasswordHash != null)
            .ToListAsync(ct);

        var rotated = 0;
        foreach (var user in users)
        {
            // Verify first so a run that changes nothing writes nothing (the hash is salted, so a blind
            // re-hash would rewrite every row on every startup).
            if (passwordHasher.VerifyHashedPassword(user, user.PasswordHash!, Password) != PasswordVerificationResult.Failed)
                continue;
            user.SetPasswordHash(passwordHasher.HashPassword(user, Password));
            rotated++;
        }

        if (rotated > 0)
        {
            await db.SaveChangesAsync(ct);
            logger.LogInformation("Demo passwords rotated for {Count} account(s).", rotated);
        }
        return rotated;
    }

    private User ActiveUser(Person p)
    {
        var u = new User(p.Email, p.First, p.Last);
        u.SetPasswordHash(passwordHasher.HashPassword(u, Password));
        return u;
    }
}
