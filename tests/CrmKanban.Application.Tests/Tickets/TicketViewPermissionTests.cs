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
/// `ticket.view` must actually gate reading (spec §7, §8 matrix). It did not: the key was seeded, listed
/// in the admin UI and assignable, but no read path ever asked for it — membership alone was treated as
/// permission to read. An admin could switch it off and watch the user keep the whole board. A permission
/// that is offered but never enforced is worse than a missing one: it reads as a control that works.
///
/// The customer side is the other half of the invariant: customers are not members, so they hold no
/// permissions at all, and gating them on `ticket.view` would lock them out of their own requests.
/// </summary>
public class TicketViewPermissionTests
{
    private sealed class Staff(Guid userId, Guid companyId) : ICurrentUserService
    {
        public Guid? UserId => userId;
        public bool IsAuthenticated => true;
        public bool IsSuperAdmin => false;
        public IReadOnlyCollection<Guid> CompanyIds { get; } = [companyId];
    }

    private sealed class Customer(Guid userId) : ICurrentUserService
    {
        public Guid? UserId => userId;
        public bool IsAuthenticated => true;
        public bool IsSuperAdmin => false;
        public IReadOnlyCollection<Guid> CompanyIds => [];
    }

    private sealed class SuperAdmin : ICurrentUserService
    {
        public Guid? UserId { get; } = Guid.NewGuid();
        public bool IsAuthenticated => true;
        public bool IsSuperAdmin => true;
        public IReadOnlyCollection<Guid> CompanyIds => [];
    }

    /// <summary>Permission service driven by an explicit key set — the point of these tests is what
    /// happens when ticket.view is absent, so it must be absent for real, not stubbed to true.</summary>
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

    /// <summary>A company with one approved and one pending ticket, plus a real Membership row for the
    /// staff user: per-record authorization resolves the actor from Membership, so without one the caller
    /// is refused as a stranger and the permission check under test is never reached.</summary>
    private static async Task<(Guid CompanyId, Guid TicketId, Guid OpenerId)> SeedAsync(
        DbContextOptions<CrmDbContext> options, Guid? staffUserId)
    {
        await using var db = new CrmDbContext(options, new SuperAdmin());
        foreach (var s in DefaultStatuses.All)
            db.TicketStatuses.Add(new TicketStatus(s.Name, s.Category, s.Color, s.Order, s.IsTerminal, companyId: null, id: s.Id));

        var company = new Company("Acme", "acme", Guid.NewGuid());
        db.Companies.Add(company);
        if (staffUserId is { } staff)
            db.Memberships.Add(new Membership(staff, company.Id, RoleType.Personel));

        var openerId = Guid.NewGuid();
        var ticket = new Ticket(company.Id, "ACME-1", openerId, DefaultStatuses.New.Id, "Talep", "gövde");
        var pending = new Ticket(company.Id, "ACME-2", Guid.NewGuid(), DefaultStatuses.New.Id, "Beklemede", "gövde");
        pending.MarkPendingApproval();
        db.Tickets.AddRange(ticket, pending);
        await db.SaveChangesAsync();
        return (company.Id, ticket.Id, openerId);
    }

    [Fact]
    public async Task Staff_without_ticket_view_get_no_tickets_in_the_list()
    {
        var options = Store();
        var staffId = Guid.NewGuid();
        var (companyId, _, _) = await SeedAsync(options, staffId);
        var staff = new Staff(staffId, companyId);

        var withView = await Query(options, staff, new Perms(PermissionKeys.TicketView)).ListAsync(new TicketListQuery());
        withView.Total.Should().Be(1, "the permission is what makes the board readable");

        var withoutView = await Query(options, staff, new Perms(PermissionKeys.TicketStatusChange)).ListAsync(new TicketListQuery());
        withoutView.Total.Should().Be(0, "revoking ticket.view must actually take the tickets away");
    }

    [Fact]
    public async Task Staff_without_ticket_view_cannot_open_the_kanban_or_the_moderation_queue()
    {
        var options = Store();
        var staffId = Guid.NewGuid();
        var (companyId, _, _) = await SeedAsync(options, staffId);
        var svc = Query(options, new Staff(staffId, companyId), new Perms(PermissionKeys.TicketStatusChange));

        await ((Func<Task>)(() => svc.KanbanAsync(companyId, new TicketListQuery())))
            .Should().ThrowAsync<ForbiddenException>().Where(e => e.Code == "ticket.view_forbidden");
        await ((Func<Task>)(() => svc.ModerationQueueAsync(companyId)))
            .Should().ThrowAsync<ForbiddenException>().Where(e => e.Code == "ticket.view_forbidden");
    }

    [Fact]
    public async Task Staff_without_ticket_view_cannot_open_a_ticket_detail()
    {
        var options = Store();
        var staffId = Guid.NewGuid();
        var (companyId, ticketId, _) = await SeedAsync(options, staffId);
        var staff = new Staff(staffId, companyId);

        var act = () => Query(options, staff, new Perms(PermissionKeys.TicketStatusChange)).GetDetailAsync(ticketId);

        await act.Should().ThrowAsync<ForbiddenException>()
            .Where(e => e.Code == "ticket.permission_denied", "the detail is a read like any other");
    }

    [Fact]
    public async Task A_customer_still_reaches_their_own_ticket_without_holding_anything()
    {
        var options = Store();
        var (_, ticketId, openerId) = await SeedAsync(options, staffUserId: null);
        var customer = new Customer(openerId);

        var detail = await Query(options, customer, new Perms()).GetDetailAsync(ticketId);
        detail.Id.Should().Be(ticketId);

        var list = await Query(options, customer, new Perms()).ListAsync(new TicketListQuery());
        list.Total.Should().Be(1, "customers hold no permissions; their own requests must stay visible");
    }

    [Fact]
    public async Task A_super_admin_reads_everything_without_a_membership()
    {
        var options = Store();
        var (companyId, _, _) = await SeedAsync(options, staffUserId: null);
        var svc = Query(options, new SuperAdmin(), new Perms());

        (await svc.ListAsync(new TicketListQuery())).Total.Should().Be(1);
        (await svc.KanbanAsync(companyId, new TicketListQuery())).Should().NotBeEmpty();
        (await svc.ModerationQueueAsync(companyId)).Should().ContainSingle();
    }
}
