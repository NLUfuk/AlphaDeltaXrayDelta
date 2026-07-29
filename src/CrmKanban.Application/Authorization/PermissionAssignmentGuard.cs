using CrmKanban.Application.Common;

namespace CrmKanban.Application.Authorization;

/// <summary>
/// Guards permission assignment (spec §7, §18.4) — the rule that closes the privilege-escalation
/// hole: an admin may grant/revoke <b>only permissions they themselves hold</b> and <b>only within
/// their own company</b>. SuperAdmin is unrestricted. "You cannot give a permission you don't have."
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

        if (!assignerPermissionsInTargetCompany.Contains(permissionKey))
            throw new ForbiddenException(
                "permission.assign.not_held",
                "You cannot assign a permission you do not hold yourself.");
    }
}
