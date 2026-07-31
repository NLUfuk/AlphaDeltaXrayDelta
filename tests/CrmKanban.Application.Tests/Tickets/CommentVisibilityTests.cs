using CrmKanban.Application.Abstractions;
using CrmKanban.Application.Common;
using CrmKanban.Application.Files;
using CrmKanban.Application.Tickets;
using CrmKanban.Domain.Entities;
using CrmKanban.Domain.Enums;
using CrmKanban.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CrmKanban.Application.Tests.Tickets;

/// <summary>
/// The number-one privacy invariant (spec §14, §20): an internal note must NEVER surface to the
/// customer. This exercises the real read path — <see cref="TicketQueryService.GetDetailAsync"/> —
/// under a customer scope and a staff scope over the same ticket. Core, test-first (spec §17).
/// </summary>
public class CommentVisibilityTests
{
    private sealed class FakeCurrentUser(Guid userId, bool isSuperAdmin, params Guid[] companyIds) : ICurrentUserService
    {
        public Guid? UserId { get; } = userId;
        public bool IsAuthenticated => true;
        public bool IsSuperAdmin { get; } = isSuperAdmin;
        public IReadOnlyCollection<Guid> CompanyIds { get; } = companyIds;
    }

    private sealed class FakePermissionService : IPermissionService
    {
        public Task<IReadOnlySet<string>> GetPermissionsAsync(Guid userId, Guid companyId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlySet<string>>(new HashSet<string>(StringComparer.Ordinal));
        public Task<bool> HasPermissionAsync(Guid userId, Guid companyId, string permissionKey, CancellationToken ct = default) =>
            Task.FromResult(false);
    }

    private sealed class FakeFileStorage : IFileStorage
    {
        public string PresignPut(string key, string contentType, TimeSpan expiry) => $"https://storage.test/put/{key}";
        public string PresignGet(string key, TimeSpan expiry) => $"https://storage.test/get/{key}";
        public Task PutAsync(string key, Stream content, string contentType, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FixedClock : IClock
    {
        public DateTime UtcNow => new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    }

    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid CustomerId = Guid.NewGuid();
    private static readonly Guid StaffId = Guid.NewGuid();
    private static readonly Guid StatusId = Guid.NewGuid();

    private static DbContextOptions<CrmDbContext> SharedStore()
    {
        var root = new Microsoft.EntityFrameworkCore.Storage.InMemoryDatabaseRoot();
        return new DbContextOptionsBuilder<CrmDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString(), root).Options; // isolated store per test
    }

    private static async Task<Guid> SeedAsync(DbContextOptions<CrmDbContext> options)
    {
        await using var db = new CrmDbContext(options, new FakeCurrentUser(Guid.NewGuid(), isSuperAdmin: true));
        db.TicketStatuses.Add(new TicketStatus("Open", StatusCategory.Open, "#000", 1, isTerminal: false, id: StatusId));
        db.Memberships.Add(new Membership(StaffId, CompanyId, RoleType.Personel));
        var ticket = new Ticket(CompanyId, "ACME-1", CustomerId, StatusId, "Help", "body");
        db.Tickets.Add(ticket);
        db.Comments.Add(new Comment(CompanyId, ticket.Id, StaffId, "public reply", isInternal: false));
        db.Comments.Add(new Comment(CompanyId, ticket.Id, StaffId, "secret internal note", isInternal: true));
        await db.SaveChangesAsync();
        return ticket.Id;
    }

    private static TicketQueryService QueryServiceFor(DbContextOptions<CrmDbContext> options, ICurrentUserService user)
    {
        var db = new CrmDbContext(options, user);
        var authz = new TicketAuthorizationService(user, new FakePermissionService(), db);
        var attachments = new AttachmentService(db, new FakeFileStorage(), authz, new FixedClock(),
            Options.Create(new Application.Files.FileOptions()));
        return new TicketQueryService(db, user, authz, attachments, Options.Create(new TicketOptions()));
    }

    [Fact]
    public async Task Customer_never_sees_internal_notes_on_their_own_ticket()
    {
        var options = SharedStore();
        var ticketId = await SeedAsync(options);

        var customer = new FakeCurrentUser(CustomerId, isSuperAdmin: false); // no company scope
        var detail = await QueryServiceFor(options, customer).GetDetailAsync(ticketId);

        detail.Comments.Should().ContainSingle().Which.IsInternal.Should().BeFalse();
        detail.Comments.Should().NotContain(c => c.Body.Contains("internal"));
    }

    [Fact]
    public async Task Staff_sees_both_public_and_internal_comments()
    {
        var options = SharedStore();
        var ticketId = await SeedAsync(options);

        var staff = new FakeCurrentUser(StaffId, isSuperAdmin: false, CompanyId);
        var detail = await QueryServiceFor(options, staff).GetDetailAsync(ticketId);

        detail.Comments.Should().HaveCount(2);
        detail.Comments.Should().Contain(c => c.IsInternal);
    }

    [Fact]
    public async Task Customer_cannot_open_a_ticket_they_did_not_report()
    {
        var options = SharedStore();
        var ticketId = await SeedAsync(options);

        var stranger = new FakeCurrentUser(Guid.NewGuid(), isSuperAdmin: false); // not opener, not a member
        var act = () => QueryServiceFor(options, stranger).GetDetailAsync(ticketId);

        await act.Should().ThrowAsync<ForbiddenException>();
    }
}
