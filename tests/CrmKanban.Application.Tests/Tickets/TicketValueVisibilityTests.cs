using CrmKanban.Application.Abstractions;
using CrmKanban.Application.Common;
using CrmKanban.Application.Tickets;
using CrmKanban.Domain.Authorization;
using CrmKanban.Domain.Entities;
using CrmKanban.Domain.Enums;
using CrmKanban.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Options;

namespace CrmKanban.Application.Tests.Tickets;

/// <summary>
/// `ticket.value` must strip the money from the DTO, not merely hide a field in the UI (Faz 39).
/// <para>
/// This is the leak that would be easy to ship and impossible to notice: the amount input is gated in
/// React, the screen looks correct, and the number still rides along in the JSON for anyone who opens
/// devtools. Adding a person to a company should not hand them the order book, so every read path —
/// detail, board, list — is asserted here rather than trusting one shared helper.
/// </para>
/// </summary>
public class TicketValueVisibilityTests
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
        private readonly HashSet<string> _held = new(held, StringComparer.Ordinal);
        public Task<IReadOnlySet<string>> GetPermissionsAsync(Guid u, Guid c, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlySet<string>>(_held);
        public Task<bool> HasPermissionAsync(Guid u, Guid c, string k, CancellationToken ct = default) =>
            Task.FromResult(_held.Contains(k));
    }

    private sealed class FixedClock : IClock
    {
        public DateTime UtcNow => new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    }

    private static readonly Guid StatusId = Guid.NewGuid();

    private static DbContextOptions<CrmDbContext> Store() =>
        new DbContextOptionsBuilder<CrmDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString(), new InMemoryDatabaseRoot()).Options;

    /// <summary>Seeds a company with one priced ticket and a staff membership for <paramref name="staffId"/>
    /// (record-level authz resolves the actor through Membership, not the token alone).</summary>
    private static async Task<(Guid CompanyId, Guid TicketId)> SeedAsync(
        DbContextOptions<CrmDbContext> options, Guid staffId)
    {
        await using var db = new CrmDbContext(options, new SuperAdmin());
        db.TicketStatuses.Add(new TicketStatus("Açık", StatusCategory.Open, "#000", 1, false, null, StatusId));
        var company = new Company("Acme", $"acme-{Guid.NewGuid():N}", Guid.NewGuid());
        db.Companies.Add(company);
        db.Memberships.Add(new Membership(staffId, company.Id, RoleType.Personel));
        var ticket = new Ticket(company.Id, "ACME-1", Guid.NewGuid(), StatusId, "Teklif", "gövde");
        ticket.SetValue(145_000m, null);
        db.Tickets.Add(ticket);
        await db.SaveChangesAsync();
        return (company.Id, ticket.Id);
    }

    private static TicketQueryService QueriesFor(
        DbContextOptions<CrmDbContext> options, ICurrentUserService user, params string[] held)
    {
        var db = new CrmDbContext(options, user);
        var perms = new Perms(held);
        var authz = new TicketAuthorizationService(user, perms, db);
        return new TicketQueryService(db, user, authz, perms, Options.Create(new TicketOptions()));
    }

    // ---- detail ----

    [Fact]
    public async Task Detail_hides_the_amount_without_ticket_value()
    {
        var options = Store();
        var staffId = Guid.NewGuid();
        var (companyId, ticketId) = await SeedAsync(options, staffId);
        var staff = new Staff(staffId, companyId);

        var detail = await QueriesFor(options, staff, PermissionKeys.TicketView).GetDetailAsync(ticketId);

        detail.EstimatedValue.Should().BeNull();
        detail.ActualValue.Should().BeNull();
        detail.CanSeeValue.Should().BeFalse("the UI needs to know it may not ask, not just find a null");
        detail.Title.Should().Be("Teklif", "the ticket itself is still readable");
    }

    [Fact]
    public async Task Detail_shows_the_amount_with_ticket_value()
    {
        var options = Store();
        var staffId = Guid.NewGuid();
        var (companyId, ticketId) = await SeedAsync(options, staffId);
        var staff = new Staff(staffId, companyId);

        var detail = await QueriesFor(options, staff, PermissionKeys.TicketView, PermissionKeys.TicketValue)
            .GetDetailAsync(ticketId);

        detail.EstimatedValue.Should().Be(145_000m);
        detail.CanSeeValue.Should().BeTrue();
    }

    // ---- board ----

    [Fact]
    public async Task Kanban_cards_carry_no_amount_without_ticket_value()
    {
        var options = Store();
        var staffId = Guid.NewGuid();
        var (companyId, _) = await SeedAsync(options, staffId);
        var staff = new Staff(staffId, companyId);

        var columns = await QueriesFor(options, staff, PermissionKeys.TicketView)
            .KanbanAsync(companyId, new TicketListQuery());

        columns.SelectMany(c => c.Tickets).Should().ContainSingle()
            .Which.Value.Should().BeNull();
    }

    [Fact]
    public async Task Kanban_cards_carry_the_amount_with_ticket_value()
    {
        var options = Store();
        var staffId = Guid.NewGuid();
        var (companyId, _) = await SeedAsync(options, staffId);
        var staff = new Staff(staffId, companyId);

        var columns = await QueriesFor(options, staff, PermissionKeys.TicketView, PermissionKeys.TicketValue)
            .KanbanAsync(companyId, new TicketListQuery());

        columns.SelectMany(c => c.Tickets).Should().ContainSingle()
            .Which.Value.Should().Be(145_000m);
    }

    // ---- list ----

    [Fact]
    public async Task List_strips_the_amount_without_ticket_value()
    {
        var options = Store();
        var staffId = Guid.NewGuid();
        var (companyId, _) = await SeedAsync(options, staffId);
        var staff = new Staff(staffId, companyId);

        var page = await QueriesFor(options, staff, PermissionKeys.TicketView).ListAsync(new TicketListQuery());

        page.Items.Should().ContainSingle().Which.Value.Should().BeNull();
    }

    // ---- write ----

    [Fact]
    public async Task Setting_a_value_without_the_permission_is_forbidden()
    {
        var options = Store();
        var staffId = Guid.NewGuid();
        var (companyId, ticketId) = await SeedAsync(options, staffId);
        var staff = new Staff(staffId, companyId);
        var db = new CrmDbContext(options, staff);
        var perms = new Perms(PermissionKeys.TicketView, PermissionKeys.TicketEdit); // edit, but not value
        var authz = new TicketAuthorizationService(staff, perms, db);
        var commands = new TicketCommandService(db, authz, staff, new FixedClock(), Options.Create(new TicketOptions()));

        var act = () => commands.SetValueAsync(ticketId, new SetTicketValueRequest(1m, null));

        // Deliberately not folded into ticket.edit: fixing a typo in a request and deciding what a deal
        // is worth are different authorities.
        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task Setting_a_value_works_with_the_permission()
    {
        var options = Store();
        var staffId = Guid.NewGuid();
        var (companyId, ticketId) = await SeedAsync(options, staffId);
        var staff = new Staff(staffId, companyId);
        var db = new CrmDbContext(options, staff);
        var perms = new Perms(PermissionKeys.TicketView, PermissionKeys.TicketValue);
        var authz = new TicketAuthorizationService(staff, perms, db);
        var commands = new TicketCommandService(db, authz, staff, new FixedClock(), Options.Create(new TicketOptions()));

        await commands.SetValueAsync(ticketId, new SetTicketValueRequest(200_000m, 180_000m));

        await using var read = new CrmDbContext(options, new SuperAdmin());
        var ticket = await read.Tickets.IgnoreQueryFilters().SingleAsync(t => t.Id == ticketId);
        ticket.EstimatedValue.Should().Be(200_000m);
        ticket.ActualValue.Should().Be(180_000m);
        ticket.ReportableValue.Should().Be(180_000m);
    }

    [Fact]
    public async Task Changing_the_amount_leaves_an_audit_trail()
    {
        var options = Store();
        var staffId = Guid.NewGuid();
        var (companyId, ticketId) = await SeedAsync(options, staffId);
        var staff = new Staff(staffId, companyId);
        var db = new CrmDbContext(options, staff);
        var authz = new TicketAuthorizationService(staff, new Perms(PermissionKeys.TicketView, PermissionKeys.TicketValue), db);
        var commands = new TicketCommandService(db, authz, staff, new FixedClock(), Options.Create(new TicketOptions()));

        await commands.SetValueAsync(ticketId, new SetTicketValueRequest(100m, null));
        await commands.SetValueAsync(ticketId, new SetTicketValueRequest(100m, 90m));

        // Money is the field people argue about afterwards; who moved it and from what has to survive.
        await using var read = new CrmDbContext(options, new SuperAdmin());
        var trail = await read.TicketEvents.IgnoreQueryFilters()
            .Where(e => e.TicketId == ticketId && e.EventType == TicketEventType.ValueChanged)
            .ToListAsync();
        trail.Should().HaveCount(2);
        trail.Should().AllSatisfy(e => e.ActorId.Should().Be(staffId));
        // The clock is fixed, so assert on content rather than on row order: the seeded estimate being
        // repriced, then the realised figure landing on top of the untouched estimate.
        trail.Should().ContainSingle(e => e.OldValue == "145000/-" && e.NewValue == "100/-");
        trail.Should().ContainSingle(e => e.OldValue == "100/-" && e.NewValue == "100/90");
    }
}
