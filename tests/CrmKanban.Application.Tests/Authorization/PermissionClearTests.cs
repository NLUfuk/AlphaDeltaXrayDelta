using CrmKanban.Application.Abstractions;
using CrmKanban.Application.Auth;
using CrmKanban.Application.Authorization;
using CrmKanban.Application.Common;
using CrmKanban.Domain.Authorization;
using CrmKanban.Domain.Entities;
using CrmKanban.Domain.Enums;
using CrmKanban.Infrastructure.Persistence;
using CrmKanban.Infrastructure.Persistence.Interceptors;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace CrmKanban.Application.Tests.Authorization;

/// <summary>
/// The third permission state: no user-level row at all, so the key follows the ROLE. Grant and Deny can
/// only pin a key on or off; without a way back, an admin who denied something once could never restore
/// the role default — only pin it to the opposite, which is a different thing and drifts as roles change.
///
/// <para>Clearing is guarded exactly like assigning, and these tests say why: dropping a Deny hands the
/// key back through the role baseline, so an ungated clear would be a grant wearing a delete's clothes.</para>
/// </summary>
public class PermissionClearTests
{
    private sealed class Caller(Guid id, bool super, params Guid[] companies) : ICurrentUserService
    {
        public Guid? UserId => id;
        public bool IsAuthenticated => true;
        public bool IsSuperAdmin => super;
        public IReadOnlyCollection<Guid> CompanyIds { get; } = companies;
    }

    private sealed class Perms(params string[] held) : IPermissionService
    {
        private readonly IReadOnlySet<string> _held = new HashSet<string>(held, StringComparer.Ordinal);
        public Task<IReadOnlySet<string>> GetPermissionsAsync(Guid u, Guid c, CancellationToken ct = default) =>
            Task.FromResult(_held);
        public Task<bool> HasPermissionAsync(Guid u, Guid c, string k, CancellationToken ct = default) =>
            Task.FromResult(_held.Contains(k));
    }

    private sealed class FixedClock : IClock { public DateTime UtcNow => new(2026, 8, 5, 0, 0, 0, DateTimeKind.Utc); }

    private static readonly Guid CompanyId = Guid.NewGuid();

    private static DbContextOptions<CrmDbContext> Store() =>
        new DbContextOptionsBuilder<CrmDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString(), new InMemoryDatabaseRoot())
            .AddInterceptors(new AuditableEntityInterceptor(new FixedClock())).Options;

    /// <summary>A target who is a Personel (role baseline: ticket.view) with an explicit Deny on it —
    /// the exact situation the reset control exists for.</summary>
    private static async Task<(Guid TargetId, Guid PermissionId)> SeedDeniedAsync(
        DbContextOptions<CrmDbContext> options, ICurrentUserService caller)
    {
        await using var db = new CrmDbContext(options, caller);
        // The target must be a real, active User row: permission resolution starts by loading the user and
        // returns nothing for an unknown or inactive one, which would make every assertion here pass or
        // fail for the wrong reason.
        var target = new User("target@x.io", "T", "X");
        db.Users.Add(target);
        var permission = new Permission(PermissionKeys.TicketView);
        db.Permissions.Add(permission);
        db.RolePermissions.Add(new RolePermission(RoleType.Personel, permission.Id)); // the role default to fall back to
        db.Memberships.Add(new Membership(target.Id, CompanyId, RoleType.Personel));
        db.UserPermissions.Add(new UserPermission(target.Id, permission.Id, UserPermissionType.Deny, CompanyId));
        await db.SaveChangesAsync();
        return (target.Id, permission.Id);
    }

    private static PermissionAssignmentService Service(DbContextOptions<CrmDbContext> options, ICurrentUserService caller, Perms perms) =>
        new(new CrmDbContext(options, caller), perms, caller);

    [Fact]
    public async Task Clearing_removes_the_override_so_the_role_decides_again()
    {
        var options = Store();
        var superAdmin = new Caller(Guid.NewGuid(), super: true);
        var (targetId, permissionId) = await SeedDeniedAsync(options, superAdmin);

        await Service(options, superAdmin, new Perms()).ClearAsync(targetId, CompanyId, PermissionKeys.TicketView);

        await using var db = new CrmDbContext(options, superAdmin);
        var row = await db.UserPermissions.IgnoreQueryFilters()
            .SingleAsync(up => up.UserId == targetId && up.PermissionId == permissionId);
        row.DeletedAt.Should().NotBeNull("the override is soft-deleted, so nothing is physically lost");
        (await db.UserPermissions.CountAsync(up => up.UserId == targetId))
            .Should().Be(0, "and it no longer counts as an override");
    }

    // The resolution side of the same story: clearing only means something if the resolver stops reading
    // the removed row. It did not — overrides were loaded with IgnoreQueryFilters and no DeletedAt guard,
    // so a cleared Deny went on denying and "back to role default" was a no-op with a 204.
    [Fact]
    public async Task A_cleared_override_stops_being_applied()
    {
        var options = Store();
        var superAdmin = new Caller(Guid.NewGuid(), super: true);
        var (targetId, _) = await SeedDeniedAsync(options, superAdmin);
        await using var db = new CrmDbContext(options, superAdmin);
        var resolver = new CrmKanban.Infrastructure.Identity.PermissionService(db);

        (await resolver.HasPermissionAsync(targetId, CompanyId, PermissionKeys.TicketView))
            .Should().BeFalse("the Deny is live to begin with");

        await Service(options, superAdmin, new Perms()).ClearAsync(targetId, CompanyId, PermissionKeys.TicketView);

        await using var fresh = new CrmDbContext(options, superAdmin);
        (await new CrmKanban.Infrastructure.Identity.PermissionService(fresh)
            .HasPermissionAsync(targetId, CompanyId, PermissionKeys.TicketView))
            .Should().BeTrue("with the override gone the Personel role baseline decides again");
    }

    [Fact]
    public async Task Clearing_an_already_default_key_is_a_no_op_not_an_error()
    {
        var options = Store();
        var superAdmin = new Caller(Guid.NewGuid(), super: true);
        var (targetId, _) = await SeedDeniedAsync(options, superAdmin);

        var act = () => Service(options, superAdmin, new Perms()).ClearAsync(targetId, CompanyId, PermissionKeys.TicketView);

        await act.Should().NotThrowAsync();
        await act.Should().NotThrowAsync("clearing twice is the same request, not a missing resource");
    }

    [Fact]
    public async Task Re_granting_after_a_clear_revives_the_row_instead_of_inserting_a_second_one()
    {
        var options = Store();
        var superAdmin = new Caller(Guid.NewGuid(), super: true);
        var (targetId, _) = await SeedDeniedAsync(options, superAdmin);
        var svc = Service(options, superAdmin, new Perms());

        await svc.ClearAsync(targetId, CompanyId, PermissionKeys.TicketView);
        await Service(options, superAdmin, new Perms())
            .AssignAsync(new AssignPermissionRequest(targetId, CompanyId, PermissionKeys.TicketView, UserPermissionType.Grant));

        await using var db = new CrmDbContext(options, superAdmin);
        var rows = await db.UserPermissions.IgnoreQueryFilters()
            .Where(up => up.UserId == targetId).ToListAsync();
        rows.Should().ContainSingle("(UserId, PermissionId, CompanyId) is uniquely indexed — a second row would collide");
        rows[0].DeletedAt.Should().BeNull();
        rows[0].Type.Should().Be(UserPermissionType.Grant);
    }

    [Fact]
    public async Task Clearing_needs_the_same_authority_as_granting()
    {
        var options = Store();
        var superAdmin = new Caller(Guid.NewGuid(), super: true);
        var (targetId, _) = await SeedDeniedAsync(options, superAdmin);

        // Holds ticket.view, but may not manage permissions: clearing the Deny would hand ticket.view
        // back through the role, so it must be refused exactly like a grant.
        var staff = new Caller(Guid.NewGuid(), super: false, CompanyId);
        var act = () => Service(options, staff, new Perms(PermissionKeys.TicketView))
            .ClearAsync(targetId, CompanyId, PermissionKeys.TicketView);

        await act.Should().ThrowAsync<ForbiddenException>()
            .Where(e => e.Code == "permission.assign.forbidden");
    }

    [Fact]
    public async Task Clearing_a_permission_you_do_not_hold_is_refused()
    {
        var options = Store();
        var superAdmin = new Caller(Guid.NewGuid(), super: true);
        var (targetId, _) = await SeedDeniedAsync(options, superAdmin);

        var admin = new Caller(Guid.NewGuid(), super: false, CompanyId);
        var act = () => Service(options, admin, new Perms(PermissionKeys.PermissionAssign))
            .ClearAsync(targetId, CompanyId, PermissionKeys.TicketView);

        await act.Should().ThrowAsync<ForbiddenException>()
            .Where(e => e.Code == "permission.assign.not_held", "you cannot restore what you could not have granted");
    }
}
