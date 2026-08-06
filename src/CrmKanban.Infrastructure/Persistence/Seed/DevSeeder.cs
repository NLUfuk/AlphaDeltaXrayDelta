using System.Text;
using System.Text.Json;
using CrmKanban.Application.Abstractions;
using CrmKanban.Domain.Authorization;
using CrmKanban.Domain.Entities;
using CrmKanban.Domain.Enums;
using CrmKanban.Domain.Tickets;
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
        string[]? Internal = null, FileSpec[]? Files = null, (string Label, string Value)[]? Fields = null,
        // Money (Faz 39). Estimated is what it was quoted at; Actual is what it came to — set only on
        // tickets that closed, so "tahmin isabeti" has both numbers to divide. Leaving both null is a
        // deliberate case too: the report must show unpriced tickets as unpriced, not as zero.
        decimal? Estimated = null, decimal? Actual = null);

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

        await db.SaveChangesAsync(ct);
        await SeedCrossCuttingAsync(db, ct);
    }

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
        const string secondSlug = "tekstil-ihracat";
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

        // The real status rows and transition table the application uses — the seeder drives the same
        // state machine rather than a private copy of the rules.
        var statuses = await db.TicketStatuses.IgnoreQueryFilters()
            .Where(s => s.CompanyId == null).ToDictionaryAsync(s => s.Id, ct);
        var transitions = await db.StatusTransitions.IgnoreQueryFilters().ToListAsync(ct);

        var now = DateTime.UtcNow;
        var rnd = new Random(slug.GetHashCode()); // deterministic per company
        for (var i = 0; i < specs.Count; i++)
        {
            var spec = specs[i];
            var opener = customerUsers[i % customerUsers.Count];
            var ticket = new Ticket(company.Id, company.AllocateTicketNumber(), opener.Id, DefaultStatuses.New.Id, spec.Title,
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
            MoveTo(ticket, spec.Status, created, statuses, transitions, company.Id, personelUser.Id, db, rnd);

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

        logger.LogInformation("Dev demo data seeded: company '{Slug}' ({Name}), admin {Admin} (pwd {Pwd}).",
            slug, name, adminUser.Email, Password);
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
        IReadOnlyDictionary<Guid, TicketStatus> statuses, IReadOnlyCollection<StatusTransition> transitions,
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
            var to = statuses[step.Id];
            ticket.ChangeStatus(from, to, transitions, actor, clock);
            db.TicketEvents.Add(new TicketEvent(companyId, ticket.Id, staffId,
                TicketEventType.StatusChanged, from.Name, to.Name) { CreatedAt = clock });
        }
    }

    private User ActiveUser(Person p)
    {
        var u = new User(p.Email, p.First, p.Last);
        u.SetPasswordHash(passwordHasher.HashPassword(u, Password));
        return u;
    }
}
