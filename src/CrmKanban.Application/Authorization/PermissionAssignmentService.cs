using CrmKanban.Application.Abstractions;
using CrmKanban.Application.Auth;
using CrmKanban.Application.Common;
using CrmKanban.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CrmKanban.Application.Authorization;

/// <summary>
/// Assigns/revokes a user-level permission override (spec §7, §18.4), enforcing the escalation guard:
/// an admin may only assign permissions they hold, only within their own company. Every change is
/// audited (spec §11). The record scope is still the DbContext tenant filter — this is the "who can
/// grant" half, not the "which records" half.
/// </summary>
public sealed class PermissionAssignmentService(
    IAppDbContext db,
    IPermissionService permissions,
    ICurrentUserService currentUser)
{
    public async Task AssignAsync(AssignPermissionRequest request, CancellationToken ct = default)
    {
        var assignerId = currentUser.UserId
            ?? throw new UnauthorizedException("auth.required", "Authentication required.");

        // A super admin is invisible and untouchable to everyone below it — no non-super-admin may
        // grant/deny against a super-admin target (overrides on it are inert anyway, but it must not
        // even be addressable). Super admin itself is unrestricted.
        if (!currentUser.IsSuperAdmin &&
            await db.Users.IgnoreQueryFilters().AnyAsync(u => u.Id == request.UserId && u.IsSuperAdmin, ct))
            throw new ForbiddenException("permission.assign.forbidden_target", "You cannot modify this user's permissions.");

        IReadOnlySet<string> assignerPermissions = currentUser.IsSuperAdmin
            ? new HashSet<string>()
            : await permissions.GetPermissionsAsync(assignerId, request.CompanyId, ct);

        PermissionAssignmentGuard.EnsureCanAssign(
            currentUser.IsSuperAdmin,
            assignerPermissions,
            currentUser.CompanyIds,
            request.CompanyId,
            request.PermissionKey);

        var permission = await db.Permissions.FirstOrDefaultAsync(p => p.Key == request.PermissionKey, ct)
            ?? throw new NotFoundException("permission.unknown", $"Unknown permission '{request.PermissionKey}'.");

        var existing = await db.UserPermissions.IgnoreQueryFilters().FirstOrDefaultAsync(
            up => up.UserId == request.UserId && up.PermissionId == permission.Id && up.CompanyId == request.CompanyId, ct);

        if (existing is not null)
        {
            // Update in place — remove+add would soft-delete the old row (audit interceptor) and then
            // collide with it on the unique (UserId, PermissionId, CompanyId) index. Also heal any row
            // the previous remove+add path had already soft-deleted.
            existing.SetType(request.Type);
            if (existing.IsDeleted) existing.Restore();
        }
        else
        {
            db.UserPermissions.Add(new UserPermission(request.UserId, permission.Id, request.Type, request.CompanyId));
        }
        db.AuditLogs.Add(new AuditLog(assignerId, "permission.assign",
            $"{request.Type} {request.PermissionKey} to {request.UserId} in {request.CompanyId}"));

        await db.SaveChangesAsync(ct);
    }
}
