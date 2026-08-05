using CrmKanban.Application.Common;
using CrmKanban.Domain.Authorization;

namespace CrmKanban.Application.Authorization;

/// <summary>
/// Guards permission assignment (spec §7, §18.4) — the rule that closes the privilege-escalation
/// hole. Three checks, all of which must pass for a non-super-admin:
/// <list type="number">
///   <item>the caller may manage permissions at all in this company (<c>permission.assign</c>);</item>
///   <item>the company is one of their own;</item>
///   <item>they already hold the key they are handing out ("you cannot give what you don't have").</item>
/// </list>
/// SuperAdmin is unrestricted.
/// <para><b>Check 1 was missing until Faz 30</b> and the read side had it, so viewing someone's
/// permissions 403'd while writing them succeeded: any staff member could hand their own permissions
/// to anyone else in the company, silently. Read-gated + write-open is the worst pairing — it hides
/// the hole from whoever would notice it.</para>
/// </summary>
public static class PermissionAssignmentGuard
{
    public static void EnsureCanAssign(
        bool assignerIsSuperAdmin,
        IReadOnlySet<string> assignerPermissionsInTargetCompany,
        IReadOnlyCollection<Guid> assignerCompanyIds,
        Guid targetCompanyId,
        string permissionKey)
    {
        if (assignerIsSuperAdmin)
            return;

        if (!assignerCompanyIds.Contains(targetCompanyId))
            throw new ForbiddenException(
                "permission.assign.out_of_scope",
                "You can only assign permissions within your own company.");

        if (!assignerPermissionsInTargetCompany.Contains(PermissionKeys.PermissionAssign))
            throw new ForbiddenException(
                "permission.assign.forbidden",
                "You are not allowed to manage permissions in this company.");

        if (!assignerPermissionsInTargetCompany.Contains(permissionKey))
            throw new ForbiddenException(
                "permission.assign.not_held",
                "You cannot assign a permission you do not hold yourself.");
    }
}
