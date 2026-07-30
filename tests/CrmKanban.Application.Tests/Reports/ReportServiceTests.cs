using CrmKanban.Application.Abstractions;
using CrmKanban.Application.Common;
using CrmKanban.Application.Reports;
using CrmKanban.Domain.Authorization;
using CrmKanban.Domain.Entities;
using CrmKanban.Domain.Enums;
using CrmKanban.Domain.Tickets;
using CrmKanban.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace CrmKanban.Application.Tests.Reports;

/// <summary>
/// Reports read across tickets, so the invariant that matters is scope (spec §15, §20 "#1 leak"): a
/// company report never crosses the tenant filter, needs report.company, and the global report is
/// super-admin only. Metrics are checked on a known scenario. Runs over the real DbContext (InMemory).
/// </summary>
public class ReportServiceTests
{
    private sealed class FakeUser(bool isSuperAdmin, Guid? userId, params Guid[] companyIds) : ICurrentUserService
    {
        public Guid? UserId { get; } = userId;
        public bool IsAuthenticated => true;
        public bool IsSuperAdmin { get; } = isSuperAdmin;
        public IReadOnlyCollection<Guid> CompanyIds { get; } = companyIds;
    }

    /// <summary>Grants the given permissions only within one company — mirrors the real resolver's
    /// "not a member of this company → no permissions" rule.</summary>
    private sealed class FakePermissions(Guid company, params string[] granted) : IPermissionService
    {
        public Task<IReadOnlySet<string>> GetPermissionsAsync(Guid userId, Guid companyId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlySet<string>>(companyId == company ? granted.ToHashSet() : new HashSet<string>());
        public async Task<bool> HasPermissionAsync(Guid userId, Guid companyId, string key, CancellationToken ct = default) =>
            (await GetPermissionsAsync(userId, companyId, ct)).Contains(key);
    }

    private static readonly Guid CompanyA = Guid.NewGuid();
    private static readonly Guid CompanyB = Guid.NewGuid();
    private static readonly Guid OpenStatus = Guid.NewGuid();
    private static readonly Guid AnsweredStatus = Guid.NewGuid();
    private static readonly Guid ClosedStatus = Guid.NewGuid();
    private static readonly Guid Staff1 = Guid.NewGuid();

    private static DbContextOptions<CrmDbContext> Store() =>
        new DbContextOptionsBuilder<CrmDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString(),
                new Microsoft.EntityFrameworkCore.Storage.InMemoryDatabaseRoot()).Options;

    private static async Task SeedStatusesAsync(CrmDbContext db)
    {
        db.TicketStatuses.Add(new TicketStatus("Open", StatusCategory.Open, "#000", 1, false, null, OpenStatus));
        db.TicketStatuses.Add(new TicketStatus("Answered", StatusCategory.Answered, "#111", 2, false, null, AnsweredStatus));
        db.TicketStatuses.Add(new TicketStatus("Closed", StatusCategory.Closed, "#222", 3, true, null, ClosedStatus));
        await db.SaveChangesAsync();
    }

    private static Ticket NewTicket(Guid company, string number, Guid status, Guid? assignee = null)
    {
        var t = new Ticket(company, number, Guid.NewGuid(), status, "t", "b");
        if (assignee is { } a) t.Assign(a);
        return t;
    }

    private static ReportService Service(CrmDbContext db, ICurrentUserService user, IPermissionService? perms = null) =>
        new(db, user, perms ?? new FakePermissions(CompanyA));

    // ---- scope ----

    [Fact]
    public async Task Company_report_counts_only_that_companys_tickets()
    {
        var options = Store();
        var admin = new FakeUser(false, Guid.NewGuid(), CompanyA);
        await using (var db = new CrmDbContext(options, new FakeUser(true, null)))
        {
            await SeedStatusesAsync(db);
            db.Tickets.Add(NewTicket(CompanyA, "A-1", OpenStatus));
            db.Tickets.Add(NewTicket(CompanyB, "B-1", OpenStatus)); // must NOT be counted
            await db.SaveChangesAsync();
        }

        await using var read = new CrmDbContext(options, admin);
        var report = await Service(read, admin, new FakePermissions(CompanyA, PermissionKeys.ReportCompany))
            .CompanyReportAsync(CompanyA, null, null);

        report.TotalTickets.Should().Be(1, "company B's ticket is behind the tenant filter");
    }

    [Fact]
    public async Task Admin_without_report_permission_is_forbidden()
    {
        var options = Store();
        var admin = new FakeUser(false, Guid.NewGuid(), CompanyA);
        await using var db = new CrmDbContext(options, admin);
        await SeedStatusesAsync(db);

        var act = () => Service(db, admin, new FakePermissions(CompanyA /* no report.company */))
            .CompanyReportAsync(CompanyA, null, null);

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task Admin_cannot_report_a_company_they_lack_permission_for()
    {
        var options = Store();
        var admin = new FakeUser(false, Guid.NewGuid(), CompanyA);
        await using var db = new CrmDbContext(options, admin);
        await SeedStatusesAsync(db);

        // Permission granted for A only → requesting B is forbidden (stage a), even before the filter.
        var act = () => Service(db, admin, new FakePermissions(CompanyA, PermissionKeys.ReportCompany))
            .CompanyReportAsync(CompanyB, null, null);

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task Global_report_is_super_admin_only()
    {
        var options = Store();
        var admin = new FakeUser(false, Guid.NewGuid(), CompanyA);
        await using var db = new CrmDbContext(options, admin);
        await SeedStatusesAsync(db);

        var act = () => Service(db, admin).GlobalReportAsync(null, null);
        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task Global_report_by_super_admin_spans_all_companies()
    {
        var options = Store();
        var superAdmin = new FakeUser(true, Guid.NewGuid());
        await using var db = new CrmDbContext(options, superAdmin);
        await SeedStatusesAsync(db);
        db.Tickets.Add(NewTicket(CompanyA, "A-1", OpenStatus));
        db.Tickets.Add(NewTicket(CompanyB, "B-1", OpenStatus));
        await db.SaveChangesAsync();

        var report = await Service(db, superAdmin).GlobalReportAsync(null, null);
        report.TotalTickets.Should().Be(2);
        report.CompanyId.Should().BeNull();
    }

    // ---- metrics ----

    [Fact]
    public async Task Staff_load_counts_open_tickets_and_excludes_terminal()
    {
        var options = Store();
        var superAdmin = new FakeUser(true, Guid.NewGuid());
        await using var db = new CrmDbContext(options, superAdmin);
        await SeedStatusesAsync(db);
        db.Tickets.Add(NewTicket(CompanyA, "A-1", OpenStatus, Staff1));
        db.Tickets.Add(NewTicket(CompanyA, "A-2", AnsweredStatus, Staff1));
        db.Tickets.Add(NewTicket(CompanyA, "A-3", ClosedStatus, Staff1)); // terminal → not load
        await db.SaveChangesAsync();

        var report = await Service(db, superAdmin).GlobalReportAsync(null, null);

        report.ByStatusCategory.Should().ContainEquivalentOf(new StatusCategoryCount(StatusCategory.Open, 1));
        report.ByStatusCategory.Should().ContainEquivalentOf(new StatusCategoryCount(StatusCategory.Closed, 1));
        report.StaffLoad.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new StaffLoadItem(Staff1, 2));
    }

    [Fact]
    public async Task Average_first_response_is_computed_from_the_status_timestamps()
    {
        var options = Store();
        var superAdmin = new FakeUser(true, Guid.NewGuid());
        await using var db = new CrmDbContext(options, superAdmin);
        await SeedStatusesAsync(db);

        var open = await db.TicketStatuses.FirstAsync(s => s.Id == OpenStatus);
        var answered = await db.TicketStatuses.FirstAsync(s => s.Id == AnsweredStatus);
        var transitions = new[] { new StatusTransition(OpenStatus, AnsweredStatus, PermissionKeys.TicketStatusChange) };
        var actor = StatusChangeActor.Staff(RoleType.Personel, [PermissionKeys.TicketStatusChange]);

        var t0 = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var ticket = NewTicket(CompanyA, "A-1", OpenStatus);
        ticket.CreatedAt = t0;
        ticket.ChangeStatus(open, answered, transitions, actor, t0.AddHours(3)); // FirstResponseAt = +3h
        db.Tickets.Add(ticket);
        await db.SaveChangesAsync();

        var report = await Service(db, superAdmin).GlobalReportAsync(null, null);
        report.AvgFirstResponseHours.Should().Be(3);
    }

    [Fact]
    public async Task Csv_export_has_a_header_plus_one_row_per_ticket_and_escapes_commas()
    {
        var options = Store();
        var superAdmin = new FakeUser(true, Guid.NewGuid());
        await using var db = new CrmDbContext(options, superAdmin);
        await SeedStatusesAsync(db);
        var ticket = new Ticket(CompanyA, "A-1", Guid.NewGuid(), OpenStatus, "Broken, urgent", "b");
        db.Tickets.Add(ticket);
        await db.SaveChangesAsync();

        var csv = await Service(db, superAdmin).GlobalExportCsvAsync(null, null);
        var lines = csv.TrimEnd().Split('\n');

        lines.Should().HaveCount(2); // header + one ticket
        lines[0].Should().StartWith("Number,Title,Status");
        lines[1].Should().Contain("\"Broken, urgent\""); // comma-bearing field is quoted (RFC 4180)
    }
}
