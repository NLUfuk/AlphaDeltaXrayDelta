using CrmKanban.Application.Abstractions;
using CrmKanban.Domain.Entities;
using CrmKanban.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace CrmKanban.Application.Tests.Persistence;

/// <summary>
/// Verifies tenant isolation is enforced at the DATA layer by the global query filter (spec §6,
/// §7, §20 "#1 leak"). Uses the EF InMemory provider — the point is the filter predicate, not SQL
/// translation. Data is written with a SuperAdmin-scoped context and read back under each scope.
/// </summary>
public class TenantIsolationTests
{
    private sealed class FakeCurrentUser(bool isSuperAdmin, params Guid[] companyIds) : ICurrentUserService
    {
        public Guid? UserId => Guid.NewGuid();
        public bool IsAuthenticated => true;
        public bool IsSuperAdmin { get; } = isSuperAdmin;
        public IReadOnlyCollection<Guid> CompanyIds { get; } = companyIds;
    }

    private static readonly Guid CompanyA = Guid.NewGuid();
    private static readonly Guid CompanyB = Guid.NewGuid();
    private static readonly Guid StatusId = Guid.NewGuid();

    private static DbContextOptions<CrmDbContext> SharedStore()
    {
        // One shared in-memory root so separate context instances see the same data.
        var root = new Microsoft.EntityFrameworkCore.Storage.InMemoryDatabaseRoot();
        return new DbContextOptionsBuilder<CrmDbContext>()
            .UseInMemoryDatabase("tenant-isolation", root)
            .Options;
    }

    private static async Task SeedTwoCompaniesAsync(DbContextOptions<CrmDbContext> options)
    {
        await using var db = new CrmDbContext(options, new FakeCurrentUser(isSuperAdmin: true));
        db.Tickets.Add(new Ticket(CompanyA, "A-1", Guid.NewGuid(), StatusId, "A ticket", "body"));
        db.Tickets.Add(new Ticket(CompanyB, "B-1", Guid.NewGuid(), StatusId, "B ticket", "body"));
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Company_scoped_user_sees_only_their_company_tickets()
    {
        var options = SharedStore();
        await SeedTwoCompaniesAsync(options);

        await using var db = new CrmDbContext(options, new FakeCurrentUser(false, CompanyA));
        var tickets = await db.Tickets.ToListAsync();

        tickets.Should().ContainSingle().Which.CompanyId.Should().Be(CompanyA);
    }

    [Fact]
    public async Task Cannot_read_another_companys_ticket_by_id()
    {
        var options = SharedStore();
        await SeedTwoCompaniesAsync(options);

        await using var db = new CrmDbContext(options, new FakeCurrentUser(false, CompanyA));
        var bTicket = await db.Tickets.FirstOrDefaultAsync(t => t.CompanyId == CompanyB);

        bTicket.Should().BeNull("company A's scope must never surface company B's records, even by direct query");
    }

    [Fact]
    public async Task SuperAdmin_sees_all_companies()
    {
        var options = SharedStore();
        await SeedTwoCompaniesAsync(options);

        await using var db = new CrmDbContext(options, new FakeCurrentUser(isSuperAdmin: true));
        var tickets = await db.Tickets.ToListAsync();

        tickets.Should().HaveCount(2);
    }

    [Fact]
    public async Task Soft_deleted_rows_are_filtered_out()
    {
        var options = SharedStore();
        await SeedTwoCompaniesAsync(options);

        await using (var db = new CrmDbContext(options, new FakeCurrentUser(isSuperAdmin: true)))
        {
            var a = await db.Tickets.FirstAsync(t => t.CompanyId == CompanyA);
            a.SoftDelete(DateTime.UtcNow);
            await db.SaveChangesAsync();
        }

        await using var read = new CrmDbContext(options, new FakeCurrentUser(isSuperAdmin: true));
        (await read.Tickets.ToListAsync()).Should().ContainSingle().Which.CompanyId.Should().Be(CompanyB);
    }
}
