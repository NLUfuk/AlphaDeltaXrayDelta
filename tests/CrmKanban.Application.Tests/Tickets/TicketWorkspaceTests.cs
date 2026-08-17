using CrmKanban.Application.Abstractions;
using CrmKanban.Application.Common;
using CrmKanban.Application.Tickets;
using CrmKanban.Domain.Authorization;
using CrmKanban.Domain.Entities;
using CrmKanban.Domain.Enums;
using CrmKanban.Infrastructure.Persistence;
using CrmKanban.Infrastructure.Persistence.Seed;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Options;

namespace CrmKanban.Application.Tests.Tickets;

/// <summary>
/// Being added to a company is not the same as being handed everything in it. Without
/// <see cref="PermissionKeys.TicketViewAll"/> a staff member sees their workspace — tickets assigned to
/// them plus ones they opened — and nothing else, on every path: list, board, detail, and the write
/// operations. The reported symptom was an accountant reading the company's entire sales pipeline.
///
/// The filter is applied in the database, not in the UI: these tests assert on service output, so a
/// screen that "just hides" the extra rows would not satisfy them.
/// </summary>
public class TicketWorkspaceTests
{
    private sealed class Staff(Guid userId, Guid companyId) : ICurrentUserService
    {
        public Guid? UserId => userId;
        public bool IsAuthenticated => true;
        public bool IsSuperAdmin => false;
        public IReadOnlyCollection<Guid> CompanyIds { get; } = [companyId];
    }

    private sealed class SuperAdmin : ICurrentUserService
    {
        public Guid? UserId { get; } = Guid.NewGuid();
        public bool IsAuthenticated => true;
        public bool IsSuperAdmin => true;
        public IReadOnlyCollection<Guid> CompanyIds => [];
    }

    private sealed class Perms(params string[] held) : IPermissionService
    {
        private readonly IReadOnlySet<string> _held = new HashSet<string>(held, StringComparer.Ordinal);
        public Task<IReadOnlySet<string>> GetPermissionsAsync(Guid u, Guid c, CancellationToken ct = default) =>
            Task.FromResult(_held);
        public Task<bool> HasPermissionAsync(Guid u, Guid c, string k, CancellationToken ct = default) =>
            Task.FromResult(_held.Contains(k));
    }

    private static DbContextOptions<CrmDbContext> Store() =>
        new DbContextOptionsBuilder<CrmDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString(), new InMemoryDatabaseRoot()).Options;

    private static TicketQueryService Query(DbContextOptions<CrmDbContext> options, ICurrentUserService user, Perms perms)
    {
        var db = new CrmDbContext(options, user);
        return new TicketQueryService(db, user, new TicketAuthorizationService(user, perms, db), perms,
            Options.Create(new TicketOptions()));
    }

    /// <summary>Three approved tickets in one company: one assigned to the staff member, one they opened
    /// themselves, and one that is neither — the ticket the boundary is supposed to hide.</summary>
    private static async Task<(Guid CompanyId, Guid Mine, Guid Opened, Guid Someone)> SeedAsync(
        DbContextOptions<CrmDbContext> options, Guid staffId)
    {
        await using var db = new CrmDbContext(options, new SuperAdmin());
        foreach (var s in DefaultStatuses.All)
            db.TicketStatuses.Add(new TicketStatus(s.Name, s.Category, s.Color, s.Order, s.IsTerminal, companyId: null, id: s.Id));

        var company = new Company("Acme", "acme", Guid.NewGuid());
        db.Companies.Add(company);
        db.Memberships.Add(new Membership(staffId, company.Id, RoleType.Personel));

        var assigned = new Ticket(company.Id, "ACME-1", Guid.NewGuid(), DefaultStatuses.New.Id, "Bana atanan", "g");
        assigned.Assign(staffId);
        var opened = new Ticket(company.Id, "ACME-2", staffId, DefaultStatuses.New.Id, "Benim açtığım", "g");
        var someone = new Ticket(company.Id, "ACME-3", Guid.NewGuid(), DefaultStatuses.New.Id, "Başkasının", "g");
        db.Tickets.AddRange(assigned, opened, someone);
        await db.SaveChangesAsync();
        return (company.Id, assigned.Id, opened.Id, someone.Id);
    }

    [Fact]
    public async Task Without_view_all_the_list_holds_only_my_own_work()
    {
        var options = Store();
        var staffId = Guid.NewGuid();
        var (companyId, mine, opened, someone) = await SeedAsync(options, staffId);

        var page = await Query(options, new Staff(staffId, companyId), new Perms(PermissionKeys.TicketView))
            .ListAsync(new TicketListQuery());

        page.Items.Select(i => i.Id).Should().BeEquivalentTo([mine, opened]);
        page.Items.Select(i => i.Id).Should().NotContain(someone);
        page.Total.Should().Be(2, "the total is counted after the filter, so paging does not leak the count either");
    }

    [Fact]
    public async Task With_view_all_the_list_holds_the_whole_company()
    {
        var options = Store();
        var staffId = Guid.NewGuid();
        var (companyId, mine, opened, someone) = await SeedAsync(options, staffId);

        var page = await Query(options, new Staff(staffId, companyId),
                new Perms(PermissionKeys.TicketView, PermissionKeys.TicketViewAll))
            .ListAsync(new TicketListQuery());

        page.Items.Select(i => i.Id).Should().BeEquivalentTo([mine, opened, someone]);
    }

    [Fact]
    public async Task Without_view_all_the_board_columns_hold_only_my_own_work()
    {
        var options = Store();
        var staffId = Guid.NewGuid();
        var (companyId, mine, opened, someone) = await SeedAsync(options, staffId);

        var columns = await Query(options, new Staff(staffId, companyId), new Perms(PermissionKeys.TicketView))
            .KanbanAsync(companyId, new TicketListQuery());

        var onBoard = columns.SelectMany(c => c.Tickets).Select(t => t.Id).ToList();
        onBoard.Should().BeEquivalentTo([mine, opened]);
        onBoard.Should().NotContain(someone);
    }

    /// <summary>The hole this closes: hidden from the board is worthless if the id still opens it.</summary>
    [Fact]
    public async Task Without_view_all_someone_elses_ticket_cannot_be_opened_by_id()
    {
        var options = Store();
        var staffId = Guid.NewGuid();
        var (companyId, _, _, someone) = await SeedAsync(options, staffId);

        var act = () => Query(options, new Staff(staffId, companyId), new Perms(PermissionKeys.TicketView))
            .GetDetailAsync(someone);

        await act.Should().ThrowAsync<ForbiddenException>().Where(e => e.Code == "ticket.out_of_scope");
    }

    [Fact]
    public async Task My_own_tickets_stay_readable_by_id()
    {
        var options = Store();
        var staffId = Guid.NewGuid();
        var (companyId, mine, opened, _) = await SeedAsync(options, staffId);
        var svc = Query(options, new Staff(staffId, companyId), new Perms(PermissionKeys.TicketView));

        (await svc.GetDetailAsync(mine)).Id.Should().Be(mine, "a ticket assigned to me is my work");
        (await svc.GetDetailAsync(opened)).Id.Should().Be(opened, "so is one I opened");
    }

    /// <summary>Reading and writing must draw the same line. Holding ticket.status.change is not a way
    /// around the boundary — otherwise the filter would only be cosmetic for anyone with edit rights.</summary>
    [Fact]
    public async Task The_write_path_obeys_the_same_boundary()
    {
        var options = Store();
        var staffId = Guid.NewGuid();
        var (companyId, _, _, someone) = await SeedAsync(options, staffId);
        var user = new Staff(staffId, companyId);
        await using var db = new CrmDbContext(options, user);
        var authz = new TicketAuthorizationService(user,
            new Perms(PermissionKeys.TicketView, PermissionKeys.TicketEdit, PermissionKeys.TicketStatusChange), db);

        var ticket = await db.Tickets.IgnoreQueryFilters().FirstAsync(t => t.Id == someone);
        var act = () => authz.ResolveAsync(ticket);

        await act.Should().ThrowAsync<ForbiddenException>().Where(e => e.Code == "ticket.out_of_scope",
            "every ticket command resolves the actor through this call");
    }

    /// <summary>Triage is company-wide by definition: pending tickets are unassigned and were opened by
    /// an outsider, so nobody's workspace contains them.</summary>
    [Fact]
    public async Task The_moderation_queue_needs_the_company_wide_key()
    {
        var options = Store();
        var staffId = Guid.NewGuid();
        var (companyId, _, _, _) = await SeedAsync(options, staffId);

        var act = () => Query(options, new Staff(staffId, companyId), new Perms(PermissionKeys.TicketView))
            .ModerationQueueAsync(companyId);

        await act.Should().ThrowAsync<ForbiddenException>().Where(e => e.Code == "moderation.forbidden");
    }

    /// <summary>
    /// The two forms of the rule in <see cref="TicketWorkspace"/> — a SQL filter for lists and an
    /// in-memory check for one ticket — must agree on every combination. If they drift, a ticket is
    /// hidden from the board while its id still opens it, which is the exact hole these tests exist for.
    /// </summary>
    [Fact]
    public void The_query_filter_and_the_single_ticket_check_never_disagree()
    {
        var me = Guid.NewGuid();
        var other = Guid.NewGuid();
        var company = Guid.NewGuid();

        var cases = new List<Ticket>();
        foreach (var opener in new[] { me, other })
            foreach (var assignee in new Guid?[] { me, other, null })
            {
                var t = new Ticket(company, "T", opener, Guid.NewGuid(), "t", "b");
                if (assignee is { } a) t.Assign(a);
                cases.Add(t);
            }

        foreach (var seesAll in new[] { true, false })
        {
            var seeAllIn = seesAll ? new HashSet<Guid> { company } : [];
            var filter = TicketWorkspace.Filter(me, seeAllIn).Compile();
            foreach (var t in cases)
                filter(t).Should().Be(TicketWorkspace.Contains(t, me, seesAll),
                    $"opener={(t.OpenedById == me ? "me" : "other")}, assignee={t.AssignedToId}, seesAll={seesAll}");
        }
    }
}
