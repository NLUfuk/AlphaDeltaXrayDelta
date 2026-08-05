using CrmKanban.Application.Abstractions;
using CrmKanban.Application.Tickets;
using CrmKanban.Domain.Entities;
using CrmKanban.Infrastructure.Persistence;
using CrmKanban.Infrastructure.Persistence.Seed;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Options;

namespace CrmKanban.Application.Tests.Tickets;

/// <summary>
/// Tenant isolation on the ticket READ paths (spec §6, §7, §20 "#1 leak"). <see cref="Persistence.TenantIsolationTests"/>
/// proves the query filter predicate itself; these prove the services actually run under it.
/// They exist because they didn't: <c>PaginateAsync</c>/<c>ModerationQueueAsync</c> joined
/// <c>TicketStatuses.IgnoreQueryFilters()</c>, and EF applies IgnoreQueryFilters to the WHOLE query —
/// so the join silently switched the tenant filter off for the tickets too and every staff user could
/// list every tenant's tickets. A filter one call away from being disabled needs a test at the service,
/// not only at the DbContext.
/// </summary>
public class TicketTenantScopeTests
{
    private sealed class Staff(params Guid[] companyIds) : ICurrentUserService
    {
        public Guid? UserId { get; } = Guid.NewGuid();
        public bool IsAuthenticated => true;
        public bool IsSuperAdmin => false;
        public IReadOnlyCollection<Guid> CompanyIds { get; } = companyIds;
    }

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

    private static DbContextOptions<CrmDbContext> Store() =>
        new DbContextOptionsBuilder<CrmDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString(), new InMemoryDatabaseRoot()).Options;

    private static TicketQueryService Query(DbContextOptions<CrmDbContext> options, ICurrentUserService user)
    {
        var db = new CrmDbContext(options, user);
        return new TicketQueryService(db, user, new TicketAuthorizationService(user, new Perms(), db),
            new Perms(), Options.Create(new TicketOptions()));
    }

    /// <summary>Two tenants, each with an approved and a pending ticket. Company B also gets its OWN
    /// status column: a company-scoped status is exactly the row a tenant-scoped caller must not read,
    /// which is why the status join was told to ignore filters in the first place.</summary>
    private static async Task<(Guid A, Guid B)> SeedTwoTenantsAsync(DbContextOptions<CrmDbContext> options)
    {
        await using var db = new CrmDbContext(options, new SuperAdmin());
        foreach (var s in DefaultStatuses.All)
            db.TicketStatuses.Add(new TicketStatus(s.Name, s.Category, s.Color, s.Order, s.IsTerminal, companyId: null, id: s.Id));

        var a = new Company("Acme", "acme", Guid.NewGuid());
        var b = new Company("Globex", "globex", Guid.NewGuid());
        db.Companies.AddRange(a, b);

        var bOwnStatus = new TicketStatus("Globex'e özel", DefaultStatuses.New.Category, "#111", 1, false, b.Id);
        db.TicketStatuses.Add(bOwnStatus);

        db.Tickets.Add(new Ticket(a.Id, "ACME-1", Guid.NewGuid(), DefaultStatuses.New.Id, "A açık", "b"));
        db.Tickets.Add(new Ticket(b.Id, "GLOBEX-1", Guid.NewGuid(), bOwnStatus.Id, "B açık", "b"));

        var aPending = new Ticket(a.Id, "ACME-2", Guid.NewGuid(), DefaultStatuses.New.Id, "A beklemede", "b");
        var bPending = new Ticket(b.Id, "GLOBEX-2", Guid.NewGuid(), bOwnStatus.Id, "B beklemede", "b");
        aPending.MarkPendingApproval();
        bPending.MarkPendingApproval();
        db.Tickets.AddRange(aPending, bPending);

        await db.SaveChangesAsync();
        return (a.Id, b.Id);
    }

    [Fact]
    public async Task Staff_listing_tickets_never_sees_another_tenants_tickets()
    {
        var options = Store();
        var (a, _) = await SeedTwoTenantsAsync(options);

        var page = await Query(options, new Staff(a)).ListAsync(new TicketListQuery());

        page.Total.Should().Be(1);
        page.Items.Should().ContainSingle().Which.Number.Should().Be("ACME-1");
    }

    [Fact]
    public async Task Staff_moderation_queue_never_reaches_another_tenants_company_id()
    {
        var options = Store();
        var (a, b) = await SeedTwoTenantsAsync(options);

        var own = await Query(options, new Staff(a)).ModerationQueueAsync(a);
        own.Should().ContainSingle().Which.Number.Should().Be("ACME-2");

        // Passing someone else's company id is the whole attack: the caller picks the parameter.
        var foreign = await Query(options, new Staff(a)).ModerationQueueAsync(b);
        foreign.Should().BeEmpty("the tenant filter, not the caller's parameter, decides what is readable");
    }

    [Fact]
    public async Task A_tenants_own_status_column_still_renders_in_their_list()
    {
        var options = Store();
        var (_, b) = await SeedTwoTenantsAsync(options);

        var page = await Query(options, new Staff(b)).ListAsync(new TicketListQuery());

        page.Items.Should().ContainSingle().Which.StatusName.Should().Be("Globex'e özel",
            "status metadata must still resolve — that need is what the unsafe join was solving");
    }

    [Fact]
    public async Task A_customer_sees_their_own_tickets_across_tenants_with_status_names()
    {
        var options = Store();
        var (a, b) = await SeedTwoTenantsAsync(options);
        var customer = new Staff(); // no company scope at all — the portal case

        await using (var db = new CrmDbContext(options, new SuperAdmin()))
        {
            var bStatusId = await db.TicketStatuses.IgnoreQueryFilters()
                .Where(s => s.CompanyId == b).Select(s => s.Id).FirstAsync();
            db.Tickets.Add(new Ticket(a, "ACME-3", customer.UserId!.Value, DefaultStatuses.New.Id, "Kendi talebim", "b"));
            db.Tickets.Add(new Ticket(b, "GLOBEX-3", customer.UserId!.Value, bStatusId, "Diğer firmadaki talebim", "b"));
            await db.SaveChangesAsync();
        }

        var page = await Query(options, customer).ListAsync(new TicketListQuery());

        page.Items.Select(i => i.Number).Should().BeEquivalentTo(["ACME-3", "GLOBEX-3"]);
        page.Items.Should().OnlyContain(i => i.StatusName != null && i.StatusName != "");
    }

    [Fact]
    public async Task Filtering_by_status_category_stays_inside_the_tenant()
    {
        var options = Store();
        var (a, _) = await SeedTwoTenantsAsync(options);

        var page = await Query(options, new Staff(a))
            .ListAsync(new TicketListQuery { Category = DefaultStatuses.New.Category });

        page.Items.Should().ContainSingle().Which.Number.Should().Be("ACME-1");
    }
}
