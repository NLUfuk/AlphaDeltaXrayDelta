using System.Text;
using System.Text.Json;
using CrmKanban.Application.Abstractions;
using CrmKanban.Domain.Entities;
using CrmKanban.Domain.Enums;
using CrmKanban.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CrmKanban.Infrastructure.Persistence.Seed;

/// <summary>
/// Development-only demo data so the kanban/dashboard/moderation/detail have something real to show
/// (spec §17 demo). Idempotent + gated: each company bails if its slug already exists, and Program only
/// calls this in Development (or Seed:Demo=true). Seeds two tenants — a textile firm and a marble firm —
/// each with an admin + personel, customers, tickets spread across every column, first-time (pending)
/// public submissions for the moderation queue, internal notes (staff-only), real attachments, custom
/// public-form fields, and realistic historical timestamps. Never runs in production.
/// </summary>
public sealed class DevSeeder(
    DbContextOptions<CrmDbContext> options,
    PasswordHasher<User> passwordHasher,
    IFileStorage storage,
    ILogger<DevSeeder> logger)
{
    private const string Password = "Demo!2026Pass";

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
        string[]? Internal = null, FileSpec[]? Files = null, (string Label, string Value)[]? Fields = null);

    public async Task SeedAsync(CancellationToken ct = default)
    {
        await using var db = new CrmDbContext(options, SystemCurrentUserService.System);

        var pdf = new FileSpec("teklif-formu.txt", "text/plain", "Demo teklif dokumani — birim fiyat ve teslim kosullari.");
        var photo = new FileSpec("hasar-tutanagi.txt", "text/plain", "Demo tutanak — hasar tespiti ve fotograf referanslari.");

        await SeedCompanyAsync(db, "Anadolu Tekstil", "tekstil",
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
                    Files: [pdf],
                    Fields: [("Sipariş No", "SIP-2050"), ("Ürün Kategorisi", "Kumaş"), ("Ek açıklama", "Numune de rica ederiz.")]),
                new(DefaultStatuses.New, false, Priority.High, "Sipariş metrajı eksik geldi",
                    "SIP-2041 numaralı siparişte 300 metre eksik teslim aldık, kontrol eder misiniz?",
                    []),
                new(DefaultStatuses.InProgress, true, Priority.Urgent, "Sevkiyat termini kaçtı",
                    "İhracat sevkiyatının termini bugündü, kargo hâlâ çıkmadı. Acil dönüş bekliyoruz.",
                    ["Konuyu lojistik ekibine ilettim, gün içinde bilgi vereceğim."],
                    Internal: ["Lojistik: araç arızası nedeniyle gecikme; müşteriye tam saat vermeyelim.", "Tazminat riski var, admin bilgilendirildi."]),
                new(DefaultStatuses.InProgress, true, Priority.Normal, "Numune kumaş talebi (3 renk)",
                    "Antrasit, lacivert ve haki tonlarında birer metre numune gönderebilir misiniz?",
                    ["Numune talebini aldık, hazırlanıyor.", "Kargo takip numarasını paylaşır mısınız?"],
                    Files: [pdf]),
                new(DefaultStatuses.InProgress, true, Priority.Low, "Fatura tutarı sözleşmeyle uyumsuz",
                    "FTR-8890 faturasındaki birim fiyat, sözleşmedeki fiyattan yüksek görünüyor.",
                    ["Muhasebe ile kontrol ediyoruz, düzeltme faturası gerekebilir."]),
                new(DefaultStatuses.Answered, true, Priority.Normal, "Boya reçetesi paylaşımı",
                    "Geçen sezon kullandığımız bordo tonun reçetesini tekrar iletebilir misiniz?",
                    ["Reçeteyi ve pantone karşılığını e-postanıza gönderdik."]),
                new(DefaultStatuses.Waiting, true, Priority.Normal, "Onaylı numune bekleniyor",
                    "Gönderdiğiniz numuneyi müşteriye ilettik, onay dönüşünü bekliyoruz.",
                    ["Müşteri onayı gelince üretime alacağız."]),
                new(DefaultStatuses.Completed, true, Priority.Normal, "Top kumaş iadesi tamamlandı",
                    "Hatalı boyanan 2 top kumaşın iadesi ve değişimi tamamlandı.",
                    ["İade tarafımıza ulaştı, değişim sevk edildi. Teşekkürler."]),
                new(DefaultStatuses.Cancelled, false, Priority.Low, "Yanlış açılan sipariş",
                    "Bu talep yanlışlıkla oluşturuldu, iptal edilebilir.",
                    []),
                new(DefaultStatuses.New, false, Priority.Normal, "Yeni bayi — pamuklu kumaş fiyat listesi",
                    "Bayilik başvurusu sonrası toptan fiyat listenizi paylaşır mısınız?",
                    [], Pending: true),
            ], ct);

        await SeedCompanyAsync(db, "Ege Mermer", "mermer",
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
                    Files: [photo]),
                new(DefaultStatuses.InProgress, true, Priority.Urgent, "Ebat ölçüleri yanlış kesilmiş",
                    "Sipariş ettiğimiz 60x60 yerine 60x40 kesilmiş, şantiyede iş durdu.",
                    ["Çok özür dileriz, doğru ebatları öncelikli üretime alıyoruz."],
                    Internal: ["CNC operatör hatası; yeniden kesim maliyeti bizde. Vardiya amirine iletildi."],
                    Files: [photo]),
                new(DefaultStatuses.New, false, Priority.Normal, "Teklif talebi: 200 m² traverten",
                    "Villa projesi için 200 m² klasik traverten fiyat ve stok durumu rica ederiz.",
                    []),
                new(DefaultStatuses.InProgress, true, Priority.Normal, "Sevkiyatta kırık oluştu",
                    "Nakliye sırasında 4 plaka kırılmış, tutanak tuttuk. Tazmin süreci nasıl işler?",
                    ["Tutanağı aldık, sigorta sürecini başlattık.", "Değişim plakaları ne zaman çıkar?"],
                    Internal: ["Sigorta dosya no SGK-7781 açıldı; müşteriye 5 iş günü demeyelim, 3 gün hedef."]),
                new(DefaultStatuses.InProgress, true, Priority.Low, "Cila kalitesi beklenenden düşük",
                    "Son partide yüzey parlaklığı önceki siparişe göre mat kaldı.",
                    ["Numuneyle karşılaştırıyoruz, gerekirse yeniden cilalayacağız."]),
                new(DefaultStatuses.Answered, true, Priority.Normal, "Blok renk eşleşmesi gönderildi",
                    "Aynı bloktan devam plakası mümkün mü, renk tutması önemli.",
                    ["Aynı bloktan rezerve ettik, renk eşleşme fotoğrafını gönderdik."]),
                new(DefaultStatuses.Waiting, true, Priority.Normal, "Ödeme dekontu bekleniyor",
                    "Proforma onaylandı, havale dekontunu gönderdiğimizde üretim başlar mı?",
                    ["Dekont ulaşınca üretim planına alıyoruz."]),
                new(DefaultStatuses.Completed, true, Priority.Normal, "İade plakalar teslim alındı",
                    "Fazla gelen 5 plakanın iadesi tamamlandı, bakiye güncellendi.",
                    ["İade tamamlandı, güncel bakiye ekstrenizde. Teşekkürler."]),
                new(DefaultStatuses.Cancelled, false, Priority.Low, "Mükerrer talep",
                    "Aynı talebi iki kez oluşturmuşuz, bu kaydı kapatabilirsiniz.",
                    []),
                new(DefaultStatuses.New, false, Priority.Normal, "Yeni müteahhit — traverten teklifi",
                    "Toplu konut projesi için traverten fiyat teklifi ve referans talep ediyoruz.",
                    [], Pending: true),
            ], ct);

        await db.SaveChangesAsync(ct);
    }

    private async Task SeedCompanyAsync(
        CrmDbContext db, string name, string slug, Person admin, Person personel,
        IReadOnlyList<Person> customers, IReadOnlyList<FormFieldSpec> formFields,
        IReadOnlyList<TicketSpec> specs, CancellationToken ct)
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

        var now = DateTime.UtcNow;
        var rnd = new Random(slug.GetHashCode()); // deterministic per company
        for (var i = 0; i < specs.Count; i++)
        {
            var spec = specs[i];
            var opener = customerUsers[i % customerUsers.Count];
            var ticket = new Ticket(company.Id, company.AllocateTicketNumber(), opener.Id, spec.Status.Id, spec.Title,
                spec.Body, spec.Priority);
            if (spec.Assigned) ticket.Assign(personelUser.Id);
            if (spec.Pending) ticket.MarkPendingApproval(); // a first-time submission awaiting moderation
            if (spec.Fields is { Length: > 0 })
                ticket.SetCustomFields(JsonSerializer.Serialize(
                    spec.Fields.Select(f => new { f.Label, f.Value })));

            var created = now.AddDays(-rnd.Next(1, 12)).AddHours(-rnd.Next(0, 8));
            ticket.CreatedAt = created; // honored by the audit interceptor now → realistic trend + timestamps
            db.Tickets.Add(ticket);
            db.TicketEvents.Add(new TicketEvent(company.Id, ticket.Id, opener.Id, TicketEventType.Created, null, ticket.Number));

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
            foreach (var file in spec.Files ?? [])
            {
                var key = $"tickets/{ticket.Id:N}/{Guid.NewGuid():N}-{file.Name}";
                var bytes = Encoding.UTF8.GetBytes(file.Content);
                await storage.PutAsync(key, new MemoryStream(bytes), file.ContentType, ct);
                db.Attachments.Add(new Attachment(company.Id, ticket.Id, commentId: null, key,
                    file.Name, file.ContentType, bytes.Length, personelUser.Id) { CreatedAt = created });
            }
        }

        logger.LogInformation("Dev demo data seeded: company '{Slug}' ({Name}), admin {Admin} (pwd {Pwd}).",
            slug, name, adminUser.Email, Password);
    }

    private User ActiveUser(Person p)
    {
        var u = new User(p.Email, p.First, p.Last);
        u.SetPasswordHash(passwordHasher.HashPassword(u, Password));
        return u;
    }
}
