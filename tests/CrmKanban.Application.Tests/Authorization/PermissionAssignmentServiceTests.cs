using CrmKanban.Application.Abstractions;
using CrmKanban.Application.Auth;
using CrmKanban.Application.Authorization;
using CrmKanban.Domain.Authorization;
using CrmKanban.Domain.Entities;
using CrmKanban.Domain.Enums;
using CrmKanban.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace CrmKanban.Application.Tests.Authorization;

/// <summary>
/// Assign/re-assign a user permission override over the real DbContext (InMemory). The bug this pins:
/// re-assigning the same key (Grant→Deny, the "change my mind" toggle) must update in place, not
/// remove+add — the audit interceptor soft-deletes the removed row, which then collides with the new
/// row on the unique (UserId, PermissionId, CompanyId) index and 500s.
/// </summary>
public class PermissionAssignmentServiceTests
{
    private sealed class FakeUser(bool isSuperAdmin, Guid userId) : ICurrentUserService
    {
        public Guid? UserId => userId;
        public bool IsAuthenticated => true;
        public bool IsSuperAdmin { get; } = isSuperAdmin;
        public IReadOnlyCollection<Guid> CompanyIds => [];
    }

    // Super admin skips this in the service, so a no-op stub is enough.
    private sealed class StubPermissions : IPermissionService
    {
        public Task<IReadOnlySet<string>> GetPermissionsAsync(Guid u, Guid c, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlySet<string>>(new HashSet<string>());
        public Task<bool> HasPermissionAsync(Guid u, Guid c, string k, CancellationToken ct = default) =>
            Task.FromResult(false);
    }

    private static DbContextOptions<CrmDbContext> Store() =>
        new DbContextOptionsBuilder<CrmDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString(),
                new Microsoft.EntityFrameworkCore.Storage.InMemoryDatabaseRoot()).Options;

    [Fact]
    public async Task Re_assigning_the_same_key_updates_in_place_and_does_not_throw()
    {
        var options = Store();
        var superId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var permissionId = Guid.NewGuid();

        await using (var db = new CrmDbContext(options, new FakeUser(true, superId)))
        {
            db.Users.Add(new User("t@x.com", "T", "U"));
            db.Permissions.Add(new Permission(PermissionKeys.TicketView));
            await db.SaveChangesAsync();
            // Line up the ids we assert against.
            var perm = await db.Permissions.SingleAsync();
            permissionId = perm.Id;
            targetId = (await db.Users.SingleAsync()).Id;
        }

        var su = new FakeUser(true, superId);
        async Task Assign(UserPermissionType type)
        {
            await using var db = new CrmDbContext(options, su);
            await new PermissionAssignmentService(db, new StubPermissions(), su)
                .AssignAsync(new AssignPermissionRequest(targetId, companyId, PermissionKeys.TicketView, type));
        }

        await Assign(UserPermissionType.Grant);
        await Assign(UserPermissionType.Deny);  // the toggle that used to 500
        await Assign(UserPermissionType.Grant);

        await using var read = new CrmDbContext(options, su);
        var rows = await read.UserPermissions.IgnoreQueryFilters()
            .Where(up => up.UserId == targetId && up.PermissionId == permissionId).ToListAsync();
        rows.Should().ContainSingle("the override is updated in place, never duplicated");
        rows[0].Type.Should().Be(UserPermissionType.Grant);
        rows[0].IsDeleted.Should().BeFalse();
    }
}
