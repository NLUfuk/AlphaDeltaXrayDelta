using CrmKanban.Application.Abstractions;
using CrmKanban.Application.Notifications;
using CrmKanban.Domain.Entities;
using CrmKanban.Domain.Enums;
using CrmKanban.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CrmKanban.Application.Tests.Notifications;

/// <summary>
/// The notification trigger — one of the spec's core-four, test-first (spec §17). The invariants that
/// matter: an internal note NEVER emails the customer (§14/§20), nobody is mailed their own action, the
/// Created receipt still reaches the opener, per-user opt-outs are honored, fan-out is idempotent, and
/// a permanently failing email dead-letters. Runs over the real DbContext (InMemory), SuperAdmin scope.
/// </summary>
public class NotificationServiceTests
{
    private sealed class SuperAdmin : ICurrentUserService
    {
        public Guid? UserId => null;
        public bool IsAuthenticated => false;
        public bool IsSuperAdmin => true;
        public IReadOnlyCollection<Guid> CompanyIds => [];
    }

    private sealed class RecordingSender : IEmailSender
    {
        public List<string> Sent { get; } = [];
        public bool ThrowAlways { get; init; }
        public Task SendAsync(string to, string subject, string body, CancellationToken ct = default)
        {
            if (ThrowAlways) throw new InvalidOperationException("smtp down");
            Sent.Add(to);
            return Task.CompletedTask;
        }
    }

    private sealed class FixedClock : IClock
    {
        public DateTime UtcNow => new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    }

    private static readonly Guid Company = Guid.NewGuid();
    private const string CustomerEmail = "customer@x.com";
    private const string StaffEmail = "staff@x.com";
    private const string AdminEmail = "admin@x.com";

    private sealed record Scenario(Guid TicketId, Guid CustomerId, Guid StaffId, Guid AdminId);

    private static DbContextOptions<CrmDbContext> Store() =>
        new DbContextOptionsBuilder<CrmDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString(),
                new Microsoft.EntityFrameworkCore.Storage.InMemoryDatabaseRoot()).Options;

    private static async Task<Scenario> SeedAsync(DbContextOptions<CrmDbContext> options)
    {
        await using var db = new CrmDbContext(options, new SuperAdmin());
        var customer = new User(CustomerEmail, "Cust", "Omer");
        var staff = new User(StaffEmail, "Sam", "Staff");
        var admin = new User(AdminEmail, "Ada", "Admin");
        db.Users.AddRange(customer, staff, admin);
        db.Memberships.Add(new Membership(staff.Id, Company, RoleType.Personel));
        db.Memberships.Add(new Membership(admin.Id, Company, RoleType.Admin));
        var ticket = new Ticket(Company, "ACME-1", customer.Id, Guid.NewGuid(), "Broken", "body");
        ticket.Assign(staff.Id);
        db.Tickets.Add(ticket);
        await db.SaveChangesAsync();
        return new Scenario(ticket.Id, customer.Id, staff.Id, admin.Id);
    }

    private static NotificationService ServiceOver(CrmDbContext db, IEmailSender sender, int maxAttempts = 5) =>
        new(db, sender, new FixedClock(),
            Options.Create(new NotificationOptions { MaxAttempts = maxAttempts }),
            Options.Create(new AppOptions { PublicBaseUrl = "http://test.local" }));

    private static async Task<List<string>> QueuedRecipientsAsync(DbContextOptions<CrmDbContext> options)
    {
        await using var read = new CrmDbContext(options, new SuperAdmin());
        return await read.EmailQueue.Select(e => e.ToEmail).ToListAsync();
    }

    private static async Task<List<(string Email, string Template)>> QueuedAsync(DbContextOptions<CrmDbContext> options)
    {
        await using var read = new CrmDbContext(options, new SuperAdmin());
        return (await read.EmailQueue.Select(e => new { e.ToEmail, e.TemplateKey }).ToListAsync())
            .Select(x => (x.ToEmail, x.TemplateKey)).ToList();
    }

    [Fact]
    public async Task Internal_note_never_emails_the_customer_and_not_the_actor()
    {
        var options = Store();
        var s = await SeedAsync(options);
        await using (var db = new CrmDbContext(options, new SuperAdmin()))
        {
            db.TicketEvents.Add(new TicketEvent(Company, s.TicketId, s.AdminId, TicketEventType.InternalNoteAdded, null, null));
            await db.SaveChangesAsync();
            await ServiceOver(db, new RecordingSender()).FanOutEventsAsync();
        }

        var recipients = await QueuedRecipientsAsync(options);
        recipients.Should().NotContain(CustomerEmail, "internal notes must never reach the customer (§20)");
        recipients.Should().NotContain(AdminEmail, "the admin authored it — no self-notify");
        recipients.Should().ContainSingle().Which.Should().Be(StaffEmail); // the assignee
    }

    [Fact]
    public async Task Created_emails_the_opener_receipt_and_the_admin()
    {
        var options = Store();
        var s = await SeedAsync(options);
        await using (var db = new CrmDbContext(options, new SuperAdmin()))
        {
            // Customer opened it via the form → the customer is the actor but still gets the receipt.
            db.TicketEvents.Add(new TicketEvent(Company, s.TicketId, s.CustomerId, TicketEventType.Created, null, "ACME-1"));
            await db.SaveChangesAsync();
            await ServiceOver(db, new RecordingSender()).FanOutEventsAsync();
        }

        var recipients = await QueuedRecipientsAsync(options);
        recipients.Should().BeEquivalentTo([CustomerEmail, AdminEmail]);
    }

    [Fact]
    public async Task Nobody_is_emailed_their_own_status_change()
    {
        var options = Store();
        var s = await SeedAsync(options);
        await using (var db = new CrmDbContext(options, new SuperAdmin()))
        {
            // The assignee changes status → opener is notified, the actor (assignee) is not.
            db.TicketEvents.Add(new TicketEvent(Company, s.TicketId, s.StaffId, TicketEventType.StatusChanged, "Open", "Answered"));
            await db.SaveChangesAsync();
            await ServiceOver(db, new RecordingSender()).FanOutEventsAsync();
        }

        var recipients = await QueuedRecipientsAsync(options);
        recipients.Should().ContainSingle().Which.Should().Be(CustomerEmail);
    }

    [Fact]
    public async Task A_user_opted_out_of_an_event_is_skipped()
    {
        var options = Store();
        var s = await SeedAsync(options);
        await using (var db = new CrmDbContext(options, new SuperAdmin()))
        {
            db.UserNotificationPrefs.Add(new UserNotificationPref(s.StaffId, TicketEventType.StatusChanged, enabled: false));
            // Admin changes status → opener + assignee are candidates; assignee opted out.
            db.TicketEvents.Add(new TicketEvent(Company, s.TicketId, s.AdminId, TicketEventType.StatusChanged, "Open", "Answered"));
            await db.SaveChangesAsync();
            await ServiceOver(db, new RecordingSender()).FanOutEventsAsync();
        }

        var recipients = await QueuedRecipientsAsync(options);
        recipients.Should().ContainSingle().Which.Should().Be(CustomerEmail);
    }

    [Fact]
    public async Task Fan_out_is_idempotent()
    {
        var options = Store();
        var s = await SeedAsync(options);
        await using (var db = new CrmDbContext(options, new SuperAdmin()))
        {
            db.TicketEvents.Add(new TicketEvent(Company, s.TicketId, s.AdminId, TicketEventType.StatusChanged, "Open", "Answered"));
            await db.SaveChangesAsync();
            var svc = ServiceOver(db, new RecordingSender());
            (await svc.FanOutEventsAsync()).Should().Be(1);
            (await svc.FanOutEventsAsync()).Should().Be(0, "the event was already marked notified");
        }

        // Admin's status change reaches opener + assignee (2), and the second run adds no duplicates.
        (await QueuedRecipientsAsync(options)).Should().BeEquivalentTo([CustomerEmail, StaffEmail]);
    }

    [Fact]
    public async Task A_permanently_failing_email_dead_letters_after_max_attempts()
    {
        var options = Store();
        await SeedAsync(options);
        await using var db = new CrmDbContext(options, new SuperAdmin());
        db.EmailTemplates.Add(new EmailTemplate("ticket_created", "S {{ticketNumber}}", "B {{title}}"));
        db.EmailQueue.Add(new EmailQueue(CustomerEmail, "ticket_created", "{\"ticketNumber\":\"ACME-1\",\"title\":\"x\"}"));
        await db.SaveChangesAsync();

        var svc = ServiceOver(db, new RecordingSender { ThrowAlways = true }, maxAttempts: 2);
        await svc.SendQueuedAsync(); // attempt 1 → Failed
        await svc.SendQueuedAsync(); // attempt 2 → DeadLetter

        var row = await db.EmailQueue.IgnoreQueryFilters().SingleAsync();
        row.Status.Should().Be(EmailStatus.DeadLetter);
        row.Attempts.Should().Be(2);
    }

    [Fact]
    public async Task A_successful_send_renders_the_template_and_marks_sent()
    {
        var options = Store();
        await SeedAsync(options);
        var sender = new RecordingSender();
        await using var db = new CrmDbContext(options, new SuperAdmin());
        db.EmailTemplates.Add(new EmailTemplate("ticket_created", "Ticket {{ticketNumber}}", "<p>{{title}}</p>"));
        db.EmailQueue.Add(new EmailQueue(CustomerEmail, "ticket_created", "{\"ticketNumber\":\"ACME-1\",\"title\":\"Broken\"}"));
        await db.SaveChangesAsync();

        await ServiceOver(db, sender).SendQueuedAsync();

        sender.Sent.Should().ContainSingle().Which.Should().Be(CustomerEmail);
        (await db.EmailQueue.IgnoreQueryFilters().SingleAsync()).Status.Should().Be(EmailStatus.Sent);
    }

    // ---- audience: the customer reads "talebiniz…", staff read one generic update notice ----

    [Fact]
    public async Task A_status_change_reaches_both_the_customer_and_the_assignee_in_their_own_voice()
    {
        var options = Store();
        var s = await SeedAsync(options);
        await using (var db = new CrmDbContext(options, new SuperAdmin()))
        {
            // An admin (neither opener nor assignee) moves the ticket → both sides must hear about it.
            db.TicketEvents.Add(new TicketEvent(Company, s.TicketId, s.AdminId, TicketEventType.StatusChanged, "Yeni", "İşlemde"));
            await db.SaveChangesAsync();
            await ServiceOver(db, new RecordingSender()).FanOutEventsAsync();
        }

        var queued = await QueuedAsync(options);
        queued.Should().Contain((CustomerEmail, "ticket_status_changed"), "the opener is a customer");
        queued.Should().Contain((StaffEmail, "ticket_staff_update"), "the assignee works at the company");
    }

    [Fact]
    public async Task A_staff_opened_ticket_never_sends_its_opener_the_customer_worded_mail()
    {
        var options = Store();
        var s = await SeedAsync(options);
        await using (var db = new CrmDbContext(options, new SuperAdmin()))
        {
            // Staff-opened ticket: the admin is the opener, the personel is the assignee.
            var ticket = new Ticket(Company, "ACME-2", s.AdminId, Guid.NewGuid(), "Internal", "body");
            ticket.Assign(s.StaffId);
            db.Tickets.Add(ticket);
            db.TicketEvents.Add(new TicketEvent(Company, ticket.Id, s.CustomerId, TicketEventType.CommentAdded, null, null));
            await db.SaveChangesAsync();
            await ServiceOver(db, new RecordingSender()).FanOutEventsAsync();
        }

        var queued = await QueuedAsync(options);
        queued.Should().OnlyContain(q => q.Template == "ticket_staff_update",
            "both recipients work at the company — nobody gets 'talebiniz…'");
        queued.Select(q => q.Email).Should().BeEquivalentTo([AdminEmail, StaffEmail]);
    }

    [Fact]
    public async Task A_priority_change_tells_the_assignee_but_never_the_customer()
    {
        var options = Store();
        var s = await SeedAsync(options);
        await using (var db = new CrmDbContext(options, new SuperAdmin()))
        {
            db.TicketEvents.Add(new TicketEvent(Company, s.TicketId, s.AdminId, TicketEventType.PriorityChanged, "Normal", "High"));
            await db.SaveChangesAsync();
            await ServiceOver(db, new RecordingSender()).FanOutEventsAsync();
        }

        var queued = await QueuedAsync(options);
        queued.Should().ContainSingle().Which.Should().Be((StaffEmail, "ticket_staff_update"),
            "priority is internal triage — the customer is not told");
    }

    [Fact]
    public async Task The_staff_notice_payload_names_what_changed()
    {
        var options = Store();
        var s = await SeedAsync(options);
        await using (var db = new CrmDbContext(options, new SuperAdmin()))
        {
            db.TicketEvents.Add(new TicketEvent(Company, s.TicketId, s.AdminId, TicketEventType.StatusChanged, "Yeni", "İşlemde"));
            await db.SaveChangesAsync();
            await ServiceOver(db, new RecordingSender()).FanOutEventsAsync();
        }

        await using var read = new CrmDbContext(options, new SuperAdmin());
        var staffMail = await read.EmailQueue.SingleAsync(e => e.TemplateKey == "ticket_staff_update");
        // Read it the way the sender does — the stored JSON escapes non-ASCII.
        var payload = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(staffMail.Payload)!;
        payload["change"].Should().Be("durum güncellendi: İşlemde", "the {{change}} token names the change");
    }
}
