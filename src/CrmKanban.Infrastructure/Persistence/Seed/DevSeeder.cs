using CrmKanban.Domain.Entities;
using CrmKanban.Domain.Enums;
using CrmKanban.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CrmKanban.Infrastructure.Persistence.Seed;

/// <summary>
/// Development-only demo data so the kanban/dashboard have something to show (spec §17 demo).
/// Idempotent + gated: bails if the demo company already exists, and Program only calls it in the
/// Development environment. Creates a company, an admin + personel, a few customers, and tickets
/// spread across every status so the board fills. Never runs in production.
/// </summary>
public sealed class DevSeeder(
    DbContextOptions<CrmDbContext> options,
    PasswordHasher<User> passwordHasher,
    ILogger<DevSeeder> logger)
{
    private const string Password = "Demo!2026Pass";
    private const string CompanySlug = "demo";

    public async Task SeedAsync(CancellationToken ct = default)
    {
        await using var db = new CrmDbContext(options, SystemCurrentUserService.System);

        if (await db.Companies.IgnoreQueryFilters().AnyAsync(c => c.Slug == CompanySlug, ct))
            return; // already seeded

        var admin = ActiveUser("admin@demo.local", "Derya", "Yönetici");
        admin.AllowCompanyCreation();
        var personel = ActiveUser("personel@demo.local", "Pelin", "Personel");
        var customers = new[]
        {
            ActiveUser("ayse@musteri.local", "Ayşe", "Kaya"),
            ActiveUser("mehmet@musteri.local", "Mehmet", "Demir"),
            ActiveUser("zeynep@musteri.local", "Zeynep", "Şahin"),
        };
        db.Users.AddRange([admin, personel, .. customers]);

        var company = new Company("Demo Destek", CompanySlug, admin.Id);
        db.Companies.Add(company);
        db.Memberships.Add(new Membership(admin.Id, company.Id, RoleType.Admin));
        db.Memberships.Add(new Membership(personel.Id, company.Id, RoleType.Personel));

        // (status, assignToPersonel, priority, title) — spread across every column.
        var specs = new[]
        {
            (DefaultStatuses.New,        false, Priority.Normal, "Yazıcı çıktı vermiyor"),
            (DefaultStatuses.New,        false, Priority.High,   "Fatura tutarı hatalı"),
            (DefaultStatuses.New,        true,  Priority.Urgent, "Sisteme giriş yapamıyorum"),
            (DefaultStatuses.InProgress, true,  Priority.Normal, "Sipariş kargoda görünmüyor"),
            (DefaultStatuses.InProgress, true,  Priority.Low,    "E-posta bildirimleri gelmiyor"),
            (DefaultStatuses.Answered,   true,  Priority.Normal, "Şifre sıfırlama talebi"),
            (DefaultStatuses.Waiting,    true,  Priority.Normal, "Ek belge talebi"),
            (DefaultStatuses.Completed,  true,  Priority.Normal, "Ürün iadesi tamamlandı"),
            (DefaultStatuses.Completed,  false, Priority.Low,    "Adres güncellendi"),
            (DefaultStatuses.Cancelled,  false, Priority.Low,    "Yanlış açılan talep"),
        };

        var now = DateTime.UtcNow;
        var rnd = new Random(42);
        for (var i = 0; i < specs.Length; i++)
        {
            var (status, assigned, priority, title) = specs[i];
            var opener = customers[i % customers.Length];
            var ticket = new Ticket(company.Id, company.AllocateTicketNumber(), opener.Id, status.Id, title,
                "Müşteri talebi: " + title + ".", priority);
            if (assigned) ticket.Assign(personel.Id);
            ticket.CreatedAt = now.AddDays(-rnd.Next(0, 12)); // spread for the trend chart
            db.Tickets.Add(ticket);
            db.TicketEvents.Add(new TicketEvent(company.Id, ticket.Id, opener.Id, TicketEventType.Created, null, ticket.Number));
        }

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Dev demo data seeded: company '{Slug}', admin {Admin} (pwd {Pwd}).", CompanySlug, admin.Email, Password);
    }

    private User ActiveUser(string email, string first, string last)
    {
        var u = new User(email, first, last);
        u.SetPasswordHash(passwordHasher.HashPassword(u, Password));
        return u;
    }
}
