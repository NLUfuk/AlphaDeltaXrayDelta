using CrmKanban.Application.Abstractions;
using CrmKanban.Application.Common;
using CrmKanban.Application.Tickets;
using CrmKanban.Domain.Entities;
using CrmKanban.Domain.Enums;
using CrmKanban.Infrastructure.Persistence;
using CrmKanban.Infrastructure.Persistence.Seed;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace CrmKanban.Application.Tests.Tickets;

/// <summary>
/// Per-company kanban column management (spec §12, §18.9) — a state-machine variation point, so tested.
/// The first customization forks the global set into company-owned rows and migrates the company's
/// tickets; a new column inserts at a chosen position and is auto-chained into the transition graph.
/// </summary>
public class StatusManagementServiceTests
{
    private sealed class SuperAdmin : ICurrentUserService
    {
        public Guid? UserId { get; } = Guid.NewGuid();
        public bool IsAuthenticated => true;
        public bool IsSuperAdmin => true;
        public IReadOnlyCollection<Guid> CompanyIds => [];
    }

    private sealed class Member(Guid userId, Guid companyId) : ICurrentUserService
    {
        public Guid? UserId => userId;
        public bool IsAuthenticated => true;
        public bool IsSuperAdmin => false;
        public IReadOnlyCollection<Guid> CompanyIds { get; } = [companyId];
    }

    private sealed class Perms(bool has) : IPermissionService
    {
        public Task<IReadOnlySet<string>> GetPermissionsAsync(Guid u, Guid c, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlySet<string>>(new HashSet<string>());
        public Task<bool> HasPermissionAsync(Guid u, Guid c, string k, CancellationToken ct = default) => Task.FromResult(has);
    }

    private sealed class FixedClock : IClock
    {
        public DateTime UtcNow => new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    }

    private static DbContextOptions<CrmDbContext> Store() =>
        new DbContextOptionsBuilder<CrmDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString(), new InMemoryDatabaseRoot()).Options;

    private static async Task<(Guid CompanyId, Guid TicketId, Guid OpenStatusId)> SeedAsync(DbContextOptions<CrmDbContext> options)
    {
        await using var db = new CrmDbContext(options, new SuperAdmin());
        foreach (var s in DefaultStatuses.All)
            db.TicketStatuses.Add(new TicketStatus(s.Name, s.Category, s.Color, s.Order, s.IsTerminal, companyId: null, id: s.Id));
        foreach (var (from, to) in DefaultStatuses.Transitions())
            db.StatusTransitions.Add(new StatusTransition(from, to, Domain.Authorization.PermissionKeys.TicketStatusChange));

        var company = new Company("Acme", "acme", Guid.NewGuid());
        db.Companies.Add(company);
        var ticket = new Ticket(company.Id, "ACME-1", Guid.NewGuid(), DefaultStatuses.New.Id, "t", "b");
        db.Tickets.Add(ticket);
        await db.SaveChangesAsync();
        return (company.Id, ticket.Id, DefaultStatuses.New.Id);
    }

    private static StatusManagementService Service(DbContextOptions<CrmDbContext> options, ICurrentUserService user, bool hasPerm = true) =>
        new(new CrmDbContext(options, user), user, new Perms(hasPerm), new FixedClock());

    [Fact]
    public async Task Create_forks_the_global_set_and_migrates_existing_tickets_onto_the_clone()
    {
        var options = Store();
        var (companyId, ticketId, openId) = await SeedAsync(options);

        await Service(options, new SuperAdmin()).CreateAsync(companyId,
            new CreateStatusRequest("Teklif Verildi", StatusCategory.Answered, "#0ea5e9", Position: 2));

        await using var read = new CrmDbContext(options, new SuperAdmin());
        var owned = await read.TicketStatuses.IgnoreQueryFilters()
            .Where(s => s.CompanyId == companyId && s.DeletedAt == null).OrderBy(s => s.Order).ToListAsync();
        owned.Should().HaveCount(DefaultStatuses.All.Count + 1, "the global set is cloned and one column added");

        var ticket = await read.Tickets.IgnoreQueryFilters().SingleAsync(t => t.Id == ticketId);
        ticket.StatusId.Should().NotBe(openId, "the ticket is migrated off the global status onto the company clone");
        owned.Select(s => s.Id).Should().Contain(ticket.StatusId);
    }

    [Fact]
    public async Task Create_inserts_the_column_at_the_requested_position()
    {
        var options = Store();
        var (companyId, _, _) = await SeedAsync(options);

        await Service(options, new SuperAdmin()).CreateAsync(companyId,
            new CreateStatusRequest("Teklif Verildi", StatusCategory.Answered, "#0ea5e9", Position: 2));

        await using var read = new CrmDbContext(options, new SuperAdmin());
        var ordered = await read.TicketStatuses.IgnoreQueryFilters()
            .Where(s => s.CompanyId == companyId && s.DeletedAt == null).OrderBy(s => s.Order).ToListAsync();
        ordered[2].Name.Should().Be("Teklif Verildi");
        ordered.Select(s => s.Order).Should().BeEquivalentTo(Enumerable.Range(0, ordered.Count), "orders stay a dense 0..n chain");
    }

    [Fact]
    public async Task A_new_non_terminal_column_is_chained_into_the_transition_graph()
    {
        var options = Store();
        var (companyId, _, _) = await SeedAsync(options);

        var newId = await Service(options, new SuperAdmin()).CreateAsync(companyId,
            new CreateStatusRequest("Teklif Verildi", StatusCategory.Answered, "#0ea5e9", Position: 2));

        await using var read = new CrmDbContext(options, new SuperAdmin());
        var into = await read.StatusTransitions.IgnoreQueryFilters().AnyAsync(t => t.ToStatusId == newId && t.DeletedAt == null);
        var outOf = await read.StatusTransitions.IgnoreQueryFilters().AnyAsync(t => t.FromStatusId == newId && t.DeletedAt == null);
        into.Should().BeTrue("staff must be able to drag a card into the new column");
        outOf.Should().BeTrue("and back out of it");
    }

    [Fact]
    public async Task Reorder_renumbers_the_board_by_index()
    {
        var options = Store();
        var (companyId, _, _) = await SeedAsync(options);
        var svc = Service(options, new SuperAdmin());
        await svc.CreateAsync(companyId, new CreateStatusRequest("Extra", StatusCategory.Waiting, "#999", 0));

        var current = await svc.ListAsync(companyId);
        var reversed = current.Select(c => c.Id).Reverse().ToList();
        await Service(options, new SuperAdmin()).ReorderAsync(companyId, new ReorderStatusesRequest(reversed));

        var after = await Service(options, new SuperAdmin()).ListAsync(companyId);
        after.Select(c => c.Id).Should().Equal(reversed);
    }

    [Fact]
    public async Task Delete_is_blocked_while_tickets_sit_in_the_column()
    {
        var options = Store();
        var (companyId, ticketId, _) = await SeedAsync(options);
        var svc = Service(options, new SuperAdmin());
        await svc.CreateAsync(companyId, new CreateStatusRequest("Temp", StatusCategory.Waiting, "#999", 1));

        // Park the ticket in the new column, then try to delete it.
        await using (var db = new CrmDbContext(options, new SuperAdmin()))
        {
            var col = await db.TicketStatuses.IgnoreQueryFilters().SingleAsync(s => s.CompanyId == companyId && s.Name == "Temp");
            var ticket = await db.Tickets.IgnoreQueryFilters().SingleAsync(t => t.Id == ticketId);
            ticket.MigrateStatus(col.Id);
            await db.SaveChangesAsync();
        }

        Guid tempId;
        await using (var db = new CrmDbContext(options, new SuperAdmin()))
            tempId = (await db.TicketStatuses.IgnoreQueryFilters().SingleAsync(s => s.CompanyId == companyId && s.Name == "Temp")).Id;

        var act = () => Service(options, new SuperAdmin()).DeleteAsync(companyId, tempId);
        await act.Should().ThrowAsync<ConflictException>().Where(e => e.Code == "status.in_use");
    }

    [Fact]
    public async Task Managing_columns_requires_the_status_manage_permission()
    {
        var options = Store();
        var (companyId, _, _) = await SeedAsync(options);
        var member = new Member(Guid.NewGuid(), companyId);

        var act = () => Service(options, member, hasPerm: false).CreateAsync(companyId,
            new CreateStatusRequest("Nope", StatusCategory.Waiting, "#999", 0));

        await act.Should().ThrowAsync<ForbiddenException>().Where(e => e.Code == "status.manage_forbidden");
    }
}
