using CrmKanban.Domain.Entities;
using CrmKanban.Domain.Enums;
using CrmKanban.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CrmKanban.Infrastructure.Persistence.Seed;

/// <summary>
/// Development-only demo data so the kanban/dashboard/moderation have something to show (spec §17
/// demo). Idempotent + gated: each company bails if its slug already exists, and Program only calls
/// this in the Development environment. Seeds two tenants — a textile firm and a marble firm — each
/// with an admin + personel, customers, tickets spread across every column, and one first-time
/// (pending) public submission so the moderation queue is populated. Never runs in production.
/// </summary>
public sealed class DevSeeder(
    DbContextOptions<CrmDbContext> options,
    PasswordHasher<User> passwordHasher,
    ILogger<DevSeeder> logger)
{
    private const string Password = "Demo!2026Pass";

    private sealed record Person(string Email, string First, string Last);
    // Body/Comments give the ticket a CRM feel (an offer thread, a customer request being worked).
    private sealed record TicketSpec(
        DefaultStatuses.StatusDef Status, bool Assigned, Priority Priority, string Title,
        string Body, string[] Conversation, bool Pending = false);

    public async Task SeedAsync(CancellationToken ct = default)
    {
        await using var db = new CrmDbContext(options, SystemCurrentUserService.System);

        await SeedCompanyAsync(db, "Anadolu Tekstil", "tekstil",
            admin: new Person("admin@tekstil.local", "Derya", "Yönetici"),
            personel: new Person("uretim@tekstil.local", "Pelin", "Üretim"),
            customers:
            [
                new Person("alici1@tekstil-musteri.local", "Ayşe", "Kaya"),
                new Person("alici2@tekstil-musteri.local", "Mehmet", "Demir"),
                new Person("alici3@tekstil-musteri.local", "Zeynep", "Şahin"),
            ],
            specs:
            [
                new(DefaultStatuses.New, false, Priority.Normal, "Teklif talebi: 5.000 m pamuklu poplin",
                    "Yaz koleksiyonu için 5.000 metre 120gr pamuklu poplin fiyat teklifi rica ediyoruz.",
                    ["Merhaba, en-boy toleransları ve teslim süresini de belirtir misiniz?"]),
                new(DefaultStatuses.New, false, Priority.High, "Sipariş metrajı eksik geldi",
                    "SIP-2041 numaralı siparişte 300 metre eksik teslim aldık, kontrol eder misiniz?",
                    []),
                new(DefaultStatuses.New, true, Priority.Urgent, "Sevkiyat termini kaçtı",
                    "İhracat sevkiyatının termini bugündü, kargo hâlâ çıkmadı. Acil dönüş bekliyoruz.",
                    ["Konuyu lojistik ekibine ilettim, gün içinde bilgi vereceğim."]),
                new(DefaultStatuses.InProgress, true, Priority.Normal, "Numune kumaş talebi (3 renk)",
                    "Antrasit, lacivert ve haki tonlarında birer metre numune gönderebilir misiniz?",
                    ["Numune talebini aldık, hazırlanıyor.", "Kargo takip numarasını paylaşır mısınız?"]),
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
            specs:
            [
                new(DefaultStatuses.New, false, Priority.High, "Plakada çatlak — değişim talebi",
                    "Teslim aldığımız 3 mermer plakanın ikisinde çatlak var, değişim rica ediyoruz.",
                    ["Fotoğrafları iletebilir misiniz? Kalite ekibine yönlendireceğiz."]),
                new(DefaultStatuses.New, true, Priority.Urgent, "Ebat ölçüleri yanlış kesilmiş",
                    "Sipariş ettiğimiz 60x60 yerine 60x40 kesilmiş, şantiyede iş durdu.",
                    ["Çok özür dileriz, doğru ebatları öncelikli üretime alıyoruz."]),
                new(DefaultStatuses.New, false, Priority.Normal, "Teklif talebi: 200 m² traverten",
                    "Villa projesi için 200 m² klasik traverten fiyat ve stok durumu rica ederiz.",
                    []),
                new(DefaultStatuses.InProgress, true, Priority.Normal, "Sevkiyatta kırık oluştu",
                    "Nakliye sırasında 4 plaka kırılmış, tutanak tuttuk. Tazmin süreci nasıl işler?",
                    ["Tutanağı aldık, sigorta sürecini başlattık.", "Değişim plakaları ne zaman çıkar?"]),
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
        IReadOnlyList<Person> customers, IReadOnlyList<TicketSpec> specs, CancellationToken ct)
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
            ticket.CreatedAt = now.AddDays(-rnd.Next(0, 12)); // spread for the trend chart
            db.Tickets.Add(ticket);
            db.TicketEvents.Add(new TicketEvent(company.Id, ticket.Id, opener.Id, TicketEventType.Created, null, ticket.Number));

            // The conversation reads customer → staff → customer …, giving each ticket a real CRM thread.
            for (var c = 0; c < spec.Conversation.Length; c++)
            {
                var isStaff = c % 2 == 0; // first reply is staff answering the opener's request
                db.Comments.Add(new Comment(company.Id, ticket.Id,
                    isStaff ? personelUser.Id : opener.Id, spec.Conversation[c], isInternal: false));
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
