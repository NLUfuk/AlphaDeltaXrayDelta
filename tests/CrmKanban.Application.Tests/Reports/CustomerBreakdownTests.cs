using CrmKanban.Application.Abstractions;
using CrmKanban.Application.Reports;
using CrmKanban.Domain.Authorization;
using CrmKanban.Domain.Entities;
using CrmKanban.Domain.Enums;
using CrmKanban.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace CrmKanban.Application.Tests.Reports;

/// <summary>
/// The per-customer section of the report (Faz 41) — the table the PDF is built around.
/// <para>
/// <b>Who counts as a customer</b> is the invariant with teeth. A "customer" is the ticket's opener
/// WITHOUT a membership in that ticket's company. Get this wrong and the report titled "müşteri
/// kırılımı" silently lists the company's own staff next to real customers, and every per-customer
/// average is computed over the wrong population. Membership is per company, so the exclusion is on
/// the (company, opener) PAIR: the same person can be staff at one company and a genuine customer at
/// another, and must show up only in the second.
/// </para>
/// <para>
/// <b>Money</b> obeys the same <c>ticket.value</c> gate as the revenue summary. The export is the
/// obvious back door — a report is a file that leaves the building — so it is asserted here directly.
/// </para>
/// </summary>
public class CustomerBreakdownTests
{
    private sealed class FakeUser(bool isSuperAdmin, Guid? userId, params Guid[] companyIds) : ICurrentUserService
    {
        public Guid? UserId { get; } = userId;
        public bool IsAuthenticated => true;
        public bool IsSuperAdmin { get; } = isSuperAdmin;
        public IReadOnlyCollection<Guid> CompanyIds { get; } = companyIds;
    }

    private sealed class FakePermissions(Guid company, params string[] granted) : IPermissionService
    {
        public Task<IReadOnlySet<string>> GetPermissionsAsync(Guid userId, Guid companyId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlySet<string>>(companyId == company ? granted.ToHashSet() : new HashSet<string>());
        public async Task<bool> HasPermissionAsync(Guid userId, Guid companyId, string key, CancellationToken ct = default) =>
            (await GetPermissionsAsync(userId, companyId, ct)).Contains(key);
    }

    private sealed class FixedClock : IClock { public DateTime UtcNow => new(2026, 8, 6, 9, 0, 0, DateTimeKind.Utc); }

    private static readonly Guid CompanyA = Guid.NewGuid();
    private static readonly Guid CompanyB = Guid.NewGuid();
    private static readonly Guid OpenStatus = Guid.NewGuid();
    private static readonly Guid WonStatus = Guid.NewGuid();
    private static readonly Guid LostStatus = Guid.NewGuid();

    private static DbContextOptions<CrmDbContext> Store() =>
        new DbContextOptionsBuilder<CrmDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString(), new InMemoryDatabaseRoot()).Options;

    private static async Task SeedStatusesAsync(CrmDbContext db)
    {
        db.TicketStatuses.Add(new TicketStatus("Görüşülüyor", StatusCategory.Open, "#000", 1, false, null, OpenStatus));
        db.TicketStatuses.Add(new TicketStatus("Teslim edildi", StatusCategory.Closed, "#0a0", 2, true, null, WonStatus));
        db.TicketStatuses.Add(new TicketStatus("Vazgeçildi", StatusCategory.Cancelled, "#a00", 3, true, null, LostStatus));
        await db.SaveChangesAsync();
    }

    private static ReportService Service(CrmDbContext db, ICurrentUserService user, IPermissionService perms) =>
        new(db, user, perms, new Application.Settings.SettingsService(db, user), new FixedClock());

    private static IPermissionService WithMoney =>
        new FakePermissions(CompanyA, PermissionKeys.ReportCompany, PermissionKeys.TicketValue);

    private static IPermissionService WithoutMoney =>
        new FakePermissions(CompanyA, PermissionKeys.ReportCompany);

    private static Ticket NewTicket(Guid company, string number, Guid opener, Guid status, decimal? value = null)
    {
        var t = new Ticket(company, number, opener, status, "t", "b");
        if (value is not null) t.SetValue(value, null);
        return t;
    }

    // ---- who is a customer ----

    [Fact]
    public async Task A_ticket_opened_by_a_company_member_is_not_a_customer()
    {
        var options = Store();
        var admin = new FakeUser(false, Guid.NewGuid(), CompanyA);
        await using var db = new CrmDbContext(options, admin);
        await SeedStatusesAsync(db);

        var customer = new User("musteri@ornek.com", "Ayşe", "Yılmaz");
        var staff = new User("personel@sirket.com", "Mehmet", "Demir");
        db.Users.AddRange(customer, staff);
        db.Memberships.Add(new Membership(staff.Id, CompanyA, RoleType.Personel));
        db.Tickets.Add(NewTicket(CompanyA, "A-1", customer.Id, OpenStatus));
        db.Tickets.Add(NewTicket(CompanyA, "A-2", staff.Id, OpenStatus)); // staff-created, internal
        await db.SaveChangesAsync();

        var report = await Service(db, admin, WithMoney).CompanyReportAsync(CompanyA, null, null);

        // Both tickets still count in the totals — only the customer table filters.
        report.TotalTickets.Should().Be(2);
        report.Customers.Should().ContainSingle().Which.Name.Should().Be("Ayşe Yılmaz");
    }

    [Fact]
    public async Task The_same_person_can_be_staff_at_one_company_and_a_customer_at_another()
    {
        var options = Store();
        var superAdmin = new FakeUser(true, Guid.NewGuid());
        await using var db = new CrmDbContext(options, superAdmin);
        await SeedStatusesAsync(db);

        var person = new User("iki@rol.com", "Zeynep", "Kaya");
        db.Users.Add(person);
        db.Memberships.Add(new Membership(person.Id, CompanyA, RoleType.Personel)); // staff at A only
        db.Tickets.Add(NewTicket(CompanyA, "A-1", person.Id, OpenStatus)); // as staff → excluded
        db.Tickets.Add(NewTicket(CompanyB, "B-1", person.Id, OpenStatus)); // as customer → counted
        db.Tickets.Add(NewTicket(CompanyB, "B-2", person.Id, WonStatus));
        await db.SaveChangesAsync();

        var report = await Service(db, superAdmin, WithMoney).GlobalReportAsync(null, null);

        // Membership is per company: exclusion must key on the pair, not on the user.
        var row = report.Customers.Should().ContainSingle().Subject;
        row.Name.Should().Be("Zeynep Kaya");
        row.TicketCount.Should().Be(2);
    }

    [Fact]
    public async Task A_super_admin_is_never_a_customer()
    {
        var options = Store();
        var superAdmin = new FakeUser(true, Guid.NewGuid());
        await using var db = new CrmDbContext(options, superAdmin);
        await SeedStatusesAsync(db);

        var root = new User("root@sistem.com", "Super", "Admin");
        root.PromoteToSuperAdmin();
        var customer = new User("musteri@ornek.com", "Ayşe", "Yılmaz");
        db.Users.AddRange(root, customer);
        db.Tickets.Add(NewTicket(CompanyA, "A-1", root.Id, OpenStatus));
        db.Tickets.Add(NewTicket(CompanyA, "A-2", customer.Id, OpenStatus));
        await db.SaveChangesAsync();

        var report = await Service(db, superAdmin, WithMoney).GlobalReportAsync(null, null);

        // A super admin holds no membership anywhere (the flag IS the role), so the membership test
        // alone would file them as a customer of every company they ever opened a ticket in.
        report.Customers.Should().ContainSingle().Which.Name.Should().Be("Ayşe Yılmaz");
    }

    [Fact]
    public async Task A_removed_membership_turns_its_holder_back_into_a_customer()
    {
        var options = Store();
        var superAdmin = new FakeUser(true, Guid.NewGuid());
        await using var db = new CrmDbContext(options, superAdmin);
        await SeedStatusesAsync(db);

        var person = new User("eski@personel.com", "Ali", "Vural");
        db.Users.Add(person);
        var membership = new Membership(person.Id, CompanyA, RoleType.Personel);
        db.Memberships.Add(membership);
        db.Tickets.Add(NewTicket(CompanyA, "A-1", person.Id, OpenStatus));
        await db.SaveChangesAsync();

        // Soft-deleted membership must not keep counting: the same class of bug closed in Faz 28/31.
        membership.SoftDelete(DateTime.UtcNow);
        await db.SaveChangesAsync();

        var report = await Service(db, superAdmin, WithMoney).GlobalReportAsync(null, null);

        report.Customers.Should().ContainSingle().Which.Name.Should().Be("Ali Vural");
    }

    // ---- money ----

    [Fact]
    public async Task Customer_money_is_withheld_without_ticket_value()
    {
        var options = Store();
        var admin = new FakeUser(false, Guid.NewGuid(), CompanyA);
        await using var db = new CrmDbContext(options, admin);
        await SeedStatusesAsync(db);

        var customer = new User("musteri@ornek.com", "Ayşe", "Yılmaz");
        db.Users.Add(customer);
        db.Tickets.Add(NewTicket(CompanyA, "A-1", customer.Id, WonStatus, 250_000m));
        await db.SaveChangesAsync();

        var report = await Service(db, admin, WithoutMoney).CompanyReportAsync(CompanyA, null, null);

        var row = report.Customers.Should().ContainSingle().Subject;
        row.WonCount.Should().Be(1);          // the activity is not a secret
        row.WonTotal.Should().BeNull();       // the amount is
        row.OpenTotal.Should().BeNull();
        report.Revenue.Should().BeNull();     // and the summary agrees with the table
    }

    [Fact]
    public async Task Customer_totals_split_won_from_open_and_ignore_unpriced_tickets()
    {
        var options = Store();
        var admin = new FakeUser(false, Guid.NewGuid(), CompanyA);
        await using var db = new CrmDbContext(options, admin);
        await SeedStatusesAsync(db);

        var customer = new User("musteri@ornek.com", "Ayşe", "Yılmaz");
        db.Users.Add(customer);
        db.Tickets.Add(NewTicket(CompanyA, "A-1", customer.Id, WonStatus, 100_000m));
        db.Tickets.Add(NewTicket(CompanyA, "A-2", customer.Id, OpenStatus, 40_000m));
        db.Tickets.Add(NewTicket(CompanyA, "A-3", customer.Id, OpenStatus));  // unpriced
        db.Tickets.Add(NewTicket(CompanyA, "A-4", customer.Id, LostStatus, 9_000m));
        await db.SaveChangesAsync();

        var report = await Service(db, admin, WithMoney).CompanyReportAsync(CompanyA, null, null);

        var row = report.Customers.Should().ContainSingle().Subject;
        row.TicketCount.Should().Be(4);
        row.WonCount.Should().Be(1);
        row.LostCount.Should().Be(1);
        row.OpenCount.Should().Be(2);
        row.WonTotal.Should().Be(100_000m);
        // The unpriced open ticket adds nothing rather than a zero — same rule as RevenueSummary, so
        // the PDF's two sections can never contradict each other.
        row.OpenTotal.Should().Be(40_000m);
    }

    // ---- scope ----

    [Fact]
    public async Task A_company_report_never_lists_another_companys_customers()
    {
        var options = Store();
        var admin = new FakeUser(false, Guid.NewGuid(), CompanyA);
        await using var db = new CrmDbContext(options, admin);
        await SeedStatusesAsync(db);

        var mine = new User("benim@musteri.com", "Ayşe", "Yılmaz");
        var theirs = new User("baska@musteri.com", "Can", "Öz");
        db.Users.AddRange(mine, theirs);
        db.Tickets.Add(NewTicket(CompanyA, "A-1", mine.Id, OpenStatus, 10m));
        db.Tickets.Add(NewTicket(CompanyB, "B-1", theirs.Id, OpenStatus, 999_999m));
        await db.SaveChangesAsync();

        var report = await Service(db, admin, WithMoney).CompanyReportAsync(CompanyA, null, null);

        report.Customers.Should().ContainSingle().Which.Email.Should().Be("benim@musteri.com");
    }

    // ---- time ----

    [Fact]
    public async Task Handling_time_sums_and_averages_only_resolved_tickets()
    {
        var options = Store();
        var superAdmin = new FakeUser(true, Guid.NewGuid());
        await using var db = new CrmDbContext(options, superAdmin);
        await SeedStatusesAsync(db);

        var customer = new User("musteri@ornek.com", "Ayşe", "Yılmaz");
        db.Users.Add(customer);
        var resolvedFast = NewTicket(CompanyA, "A-1", customer.Id, WonStatus);
        var resolvedSlow = NewTicket(CompanyA, "A-2", customer.Id, WonStatus);
        var stillOpen = NewTicket(CompanyA, "A-3", customer.Id, OpenStatus);
        db.Tickets.AddRange(resolvedFast, resolvedSlow, stillOpen);
        await db.SaveChangesAsync();

        var start = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);
        Set(resolvedFast, start, start.AddHours(2));
        Set(resolvedSlow, start, start.AddHours(8));
        Set(stillOpen, start, null);
        await db.SaveChangesAsync();

        var report = await Service(db, superAdmin, WithMoney).GlobalReportAsync(null, null);

        var row = report.Customers.Should().ContainSingle().Subject;
        // The open ticket must not drag the average down towards zero: it has no duration yet,
        // which is different from a duration of nothing.
        row.AvgResolutionHours.Should().Be(5);
        row.TotalResolutionHours.Should().Be(10);
    }

    /// <summary>Timestamps are set by the pipeline in real life; here they are written directly so the
    /// arithmetic under test is not entangled with the state machine.</summary>
    private static void Set(Ticket t, DateTime createdAt, DateTime? resolvedAt)
    {
        typeof(Domain.Common.Entity).GetProperty(nameof(Ticket.CreatedAt))!.SetValue(t, createdAt);
        typeof(Ticket).GetProperty(nameof(Ticket.ResolvedAt))!.SetValue(t, resolvedAt);
    }
}
