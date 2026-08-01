using CrmKanban.Application.Abstractions;
using CrmKanban.Application.Common;
using CrmKanban.Application.Files;
using CrmKanban.Application.Tickets;
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
/// Zero-trust intake moderation (spec §10): a pending (first-time public) ticket is held out of the
/// kanban pool and surfaces only in the moderation queue; approving it lets it into the board, rejecting
/// keeps it out for good. Core intake gate, so tested.
/// </summary>
public class TicketModerationTests
{
    private sealed class SuperAdmin : ICurrentUserService
    {
        public Guid? UserId { get; } = Guid.NewGuid();
        public bool IsAuthenticated => true;
        public bool IsSuperAdmin => true;
        public IReadOnlyCollection<Guid> CompanyIds => [];
    }

    private sealed class Perms : IPermissionService
    {
        public Task<IReadOnlySet<string>> GetPermissionsAsync(Guid u, Guid c, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlySet<string>>(new HashSet<string>());
        public Task<bool> HasPermissionAsync(Guid u, Guid c, string k, CancellationToken ct = default) => Task.FromResult(true);
    }

    private sealed class FixedClock : IClock
    {
        public DateTime UtcNow => new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    }

    private static DbContextOptions<CrmDbContext> Store() =>
        new DbContextOptionsBuilder<CrmDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString(), new InMemoryDatabaseRoot()).Options;

    private static async Task<(Guid CompanyId, Guid PendingId, Guid ApprovedId)> SeedAsync(DbContextOptions<CrmDbContext> options)
    {
        await using var db = new CrmDbContext(options, new SuperAdmin());
        foreach (var s in DefaultStatuses.All)
            db.TicketStatuses.Add(new TicketStatus(s.Name, s.Category, s.Color, s.Order, s.IsTerminal, companyId: null, id: s.Id));
        var company = new Company("Acme", "acme", Guid.NewGuid());
        db.Companies.Add(company);

        var approved = new Ticket(company.Id, "ACME-1", Guid.NewGuid(), DefaultStatuses.New.Id, "known", "b");
        var pending = new Ticket(company.Id, "ACME-2", Guid.NewGuid(), DefaultStatuses.New.Id, "first-timer", "b");
        pending.MarkPendingApproval();
        db.Tickets.AddRange(approved, pending);
        await db.SaveChangesAsync();
        return (company.Id, pending.Id, approved.Id);
    }

    private static TicketQueryService Query(DbContextOptions<CrmDbContext> options)
    {
        var user = new SuperAdmin();
        var db = new CrmDbContext(options, user);
        var authz = new TicketAuthorizationService(user, new Perms(), db);
        return new TicketQueryService(db, user, authz, Options.Create(new TicketOptions()));
    }

    private static TicketCommandService Command(DbContextOptions<CrmDbContext> options)
    {
        var user = new SuperAdmin();
        var db = new CrmDbContext(options, user);
        var authz = new TicketAuthorizationService(user, new Perms(), db);
        return new TicketCommandService(db, authz, user, new FixedClock(), Options.Create(new TicketOptions()));
    }

    private sealed class FakeStorage : IFileStorage
    {
        public string PresignPut(string key, string contentType, TimeSpan expiry) => "put";
        public string PresignGet(string key, TimeSpan expiry) => "get";
        public Task PutAsync(string key, Stream content, string contentType, CancellationToken ct = default) => Task.CompletedTask;
        public Task<Stream> GetAsync(string key, CancellationToken ct = default) => Task.FromResult<Stream>(new MemoryStream());
    }

    [Fact]
    public async Task Pending_tickets_are_hidden_from_the_kanban_but_shown_in_moderation()
    {
        var options = Store();
        var (companyId, pendingId, approvedId) = await SeedAsync(options);

        var board = await Query(options).KanbanAsync(companyId, new TicketListQuery());
        board.SelectMany(c => c.Tickets).Select(t => t.Id).Should().Contain(approvedId).And.NotContain(pendingId);

        var queue = await Query(options).ModerationQueueAsync(companyId);
        queue.Select(t => t.Id).Should().ContainSingle().Which.Should().Be(pendingId);
    }

    [Fact]
    public async Task Approving_lets_the_ticket_into_the_pool()
    {
        var options = Store();
        var (companyId, pendingId, _) = await SeedAsync(options);

        await Command(options).ApproveAsync(pendingId);

        var board = await Query(options).KanbanAsync(companyId, new TicketListQuery());
        board.SelectMany(c => c.Tickets).Select(t => t.Id).Should().Contain(pendingId);
        (await Query(options).ModerationQueueAsync(companyId)).Should().BeEmpty();

        await using var db = new CrmDbContext(options, new SuperAdmin());
        (await db.TicketEvents.IgnoreQueryFilters().CountAsync(e => e.TicketId == pendingId && e.EventType == TicketEventType.Approved))
            .Should().Be(1, "approval is audited and drives the customer notification (#27)");
    }

    [Fact]
    public async Task Rejecting_keeps_the_ticket_out_of_every_pool()
    {
        var options = Store();
        var (companyId, pendingId, _) = await SeedAsync(options);

        await Command(options).RejectAsync(pendingId);

        var board = await Query(options).KanbanAsync(companyId, new TicketListQuery());
        board.SelectMany(c => c.Tickets).Select(t => t.Id).Should().NotContain(pendingId);
        (await Query(options).ModerationQueueAsync(companyId)).Should().BeEmpty();

        await using var db = new CrmDbContext(options, new SuperAdmin());
        (await db.TicketEvents.IgnoreQueryFilters().CountAsync(e => e.TicketId == pendingId && e.EventType == TicketEventType.Rejected))
            .Should().Be(1, "rejection is audited and notifies the customer politely (#27)");
    }

    [Fact]
    public async Task Approving_a_non_pending_ticket_is_rejected()
    {
        var options = Store();
        var (_, _, approvedId) = await SeedAsync(options);

        var act = () => Command(options).ApproveAsync(approvedId);

        await act.Should().ThrowAsync<NotFoundException>();
    }
}
