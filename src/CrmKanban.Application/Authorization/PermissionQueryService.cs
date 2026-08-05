using CrmKanban.Application.Abstractions;
using CrmKanban.Application.Common;
using CrmKanban.Domain.Authorization;
using Microsoft.EntityFrameworkCore;

namespace CrmKanban.Application.Authorization;

/// <param name="Description">What the key actually gates, written from the enforcement site.</param>
/// <param name="GlobalOnly">True when a per-company grant cannot satisfy the check (super-admin-only).</param>
public sealed record PermissionInfo(
    string Key, string Group, string Label, string GroupLabel, string Description, bool GlobalOnly);

/// <param name="Permissions">Effective set = role baseline + Grants − Denies.</param>
/// <param name="RoleBaseline">What the member's ROLE alone gives. The UI needs both to say whether a
/// switch is showing an explicit override or just the role default — without it, turning a switch off
/// and seeing it stay off looks identical to the switch doing nothing.</param>
/// <param name="Overridden">Keys carrying an explicit user-level row. Derived, not inferred: an override
/// can agree with the role (Grant on something the role already gives), and then comparing the two sets
/// would call it "role default" and hide the control that clears it.</param>
public sealed record EffectivePermissions(
    Guid UserId, Guid CompanyId, IReadOnlyList<string> Permissions,
    IReadOnlyList<string> RoleBaseline, IReadOnlyList<string> Overridden);

/// <summary>
/// Read side for the permission-assignment UI (spec §7): the catalog of assignable keys, and a user's
/// effective permissions in a company (to pre-check the boxes). Viewing another user's permissions is
/// gated the same way as assigning them — super admin, or an admin holding permission.assign in that
/// company — so it can't be used to enumerate scopes the caller can't manage.
/// </summary>
public sealed class PermissionQueryService(IAppDbContext db, ICurrentUserService currentUser, IPermissionService permissions)
{
    /// <summary>The assignable permission catalog. Gated: only someone who can actually manage permissions
    /// somewhere may read it — it used to answer any authenticated caller, including customers, handing out
    /// a map of the authorization model to people with no business seeing it.</summary>
    public async Task<IReadOnlyList<PermissionInfo>> ListCatalogAsync(CancellationToken ct = default)
    {
        var callerId = currentUser.UserId
            ?? throw new UnauthorizedException("auth.required", "Authentication required.");

        if (!currentUser.IsSuperAdmin)
        {
            var canManageSomewhere = false;
            foreach (var companyId in currentUser.CompanyIds)
                if (await permissions.HasPermissionAsync(callerId, companyId, PermissionKeys.PermissionAssign, ct))
                {
                    canManageSomewhere = true;
                    break;
                }
            if (!canManageSomewhere)
                throw new ForbiddenException("permission.catalog_forbidden", "You cannot manage permissions.");
        }

        var rows = await db.Permissions.OrderBy(p => p.Group).ThenBy(p => p.Key)
            .Select(p => new { p.Key, p.Group }).ToListAsync(ct);
        return rows.Select(p => new PermissionInfo(
            p.Key, p.Group, PermissionLabels.ForKey(p.Key), PermissionLabels.ForGroup(p.Group),
            PermissionLabels.DescriptionFor(p.Key), PermissionLabels.IsGlobalOnly(p.Key))).ToList();
    }

    public async Task<EffectivePermissions> GetEffectiveAsync(Guid userId, Guid companyId, CancellationToken ct = default)
    {
        var callerId = currentUser.UserId
            ?? throw new UnauthorizedException("auth.required", "Authentication required.");
        if (!currentUser.IsSuperAdmin &&
            !await permissions.HasPermissionAsync(callerId, companyId, PermissionKeys.PermissionAssign, ct))
            throw new ForbiddenException("permission.view_forbidden", "You cannot view permissions for this company.");

        // A super admin is invisible to everyone below it: never disclose its permission set. Same error
        // code as above so a caller can't tell "not allowed" apart from "target is a super admin".
        if (!currentUser.IsSuperAdmin &&
            await db.Users.IgnoreQueryFilters().AnyAsync(u => u.Id == userId && u.IsSuperAdmin, ct))
            throw new ForbiddenException("permission.view_forbidden", "You cannot view permissions for this company.");

        var effective = await permissions.GetPermissionsAsync(userId, companyId, ct);
        var overridden = await db.UserPermissions.IgnoreQueryFilters()
            .Where(up => up.UserId == userId && up.DeletedAt == null
                         && (up.CompanyId == null || up.CompanyId == companyId))
            .Join(db.Permissions, up => up.PermissionId, p => p.Id, (_, p) => p.Key)
            .ToListAsync(ct);

        return new EffectivePermissions(userId, companyId, effective.ToList(),
            await RoleBaselineAsync(userId, companyId, ct), overridden);
    }

    /// <summary>What the member's role alone grants here, with no user-level override applied. Lets the UI
    /// tell "on because the role says so" apart from "on because someone granted it".</summary>
    private async Task<IReadOnlyList<string>> RoleBaselineAsync(Guid userId, Guid companyId, CancellationToken ct)
    {
        var role = await db.ActiveMemberships()
            .Where(m => m.UserId == userId && m.CompanyId == companyId)
            .Select(m => (Domain.Enums.RoleType?)m.Role)
            .FirstOrDefaultAsync(ct);
        if (role is null)
            return []; // not a member: no role, so nothing is baseline — every effective key is an override

        return await db.RolePermissions.Where(rp => rp.Role == role)
            .Join(db.Permissions, rp => rp.PermissionId, p => p.Id, (_, p) => p.Key)
            .ToListAsync(ct);
    }
}
