using CrmKanban.Application.Abstractions;
using CrmKanban.Application.Common;
using CrmKanban.Domain.Authorization;
using Microsoft.EntityFrameworkCore;

namespace CrmKanban.Application.Authorization;

public sealed record PermissionInfo(string Key, string Group);
public sealed record EffectivePermissions(Guid UserId, Guid CompanyId, IReadOnlyList<string> Permissions);

/// <summary>
/// Read side for the permission-assignment UI (spec §7): the catalog of assignable keys, and a user's
/// effective permissions in a company (to pre-check the boxes). Viewing another user's permissions is
/// gated the same way as assigning them — super admin, or an admin holding permission.assign in that
/// company — so it can't be used to enumerate scopes the caller can't manage.
/// </summary>
public sealed class PermissionQueryService(IAppDbContext db, ICurrentUserService currentUser, IPermissionService permissions)
{
    public async Task<IReadOnlyList<PermissionInfo>> ListCatalogAsync(CancellationToken ct = default) =>
        await db.Permissions.OrderBy(p => p.Group).ThenBy(p => p.Key)
            .Select(p => new PermissionInfo(p.Key, p.Group)).ToListAsync(ct);

    public async Task<EffectivePermissions> GetEffectiveAsync(Guid userId, Guid companyId, CancellationToken ct = default)
    {
        var callerId = currentUser.UserId
            ?? throw new UnauthorizedException("auth.required", "Authentication required.");
        if (!currentUser.IsSuperAdmin &&
            !await permissions.HasPermissionAsync(callerId, companyId, PermissionKeys.PermissionAssign, ct))
            throw new ForbiddenException("permission.view_forbidden", "You cannot view permissions for this company.");

        var effective = await permissions.GetPermissionsAsync(userId, companyId, ct);
        return new EffectivePermissions(userId, companyId, effective.ToList());
    }
}
