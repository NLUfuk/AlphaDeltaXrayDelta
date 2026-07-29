using CrmKanban.Application.Abstractions;
using CrmKanban.Application.Authorization;
using CrmKanban.Domain.Authorization;
using Microsoft.EntityFrameworkCore;

namespace CrmKanban.Infrastructure.Identity;

/// <summary>
/// Resolves effective permissions for (user, company) from the DB (spec §7). Queries bypass the
/// tenant filter (IgnoreQueryFilters) because this IS the authorization bootstrap — it reads the
/// caller's own membership/overrides by explicit id, and would otherwise deadlock against the very
/// filter it feeds. SuperAdmin short-circuits to "all permissions".
/// </summary>
public sealed class PermissionService(IAppDbContext db) : IPermissionService
{
    public async Task<IReadOnlySet<string>> GetPermissionsAsync(Guid userId, Guid companyId, CancellationToken ct = default)
    {
        var user = await db.Users.IgnoreQueryFilters()
            .Where(u => u.Id == userId)
            .Select(u => new { u.IsSuperAdmin, u.IsActive })
            .FirstOrDefaultAsync(ct);

        if (user is null || !user.IsActive)
            return new HashSet<string>();
        if (user.IsSuperAdmin)
            return PermissionKeys.All.ToHashSet(StringComparer.Ordinal);

        var membership = await db.Memberships.IgnoreQueryFilters()
            .FirstOrDefaultAsync(m => m.UserId == userId && m.CompanyId == companyId, ct);
        if (membership is null)
            return new HashSet<string>(); // not a member of this company → no permissions here

        var baseline = await db.RolePermissions.IgnoreQueryFilters()
            .Where(rp => rp.Role == membership.Role)
            .Join(db.Permissions.IgnoreQueryFilters(), rp => rp.PermissionId, p => p.Id, (_, p) => p.Key)
            .ToListAsync(ct);

        var overrides = await db.UserPermissions.IgnoreQueryFilters()
            .Where(up => up.UserId == userId)
            .Join(db.Permissions.IgnoreQueryFilters(), up => up.PermissionId, p => p.Id,
                (up, p) => new PermissionOverride(p.Key, up.Type, up.CompanyId))
            .ToListAsync(ct);

        return PermissionResolver.Resolve(baseline, overrides, companyId);
    }

    public async Task<bool> HasPermissionAsync(Guid userId, Guid companyId, string permissionKey, CancellationToken ct = default)
    {
        var permissions = await GetPermissionsAsync(userId, companyId, ct);
        return permissions.Contains(permissionKey);
    }
}
