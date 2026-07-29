using CrmKanban.Domain.Authorization;
using CrmKanban.Domain.Enums;

namespace CrmKanban.Infrastructure.Persistence.Seed;

/// <summary>
/// Default role→permission baseline (spec §8). This is seed data for an editable matrix
/// (spec §13 "rol-permission matrisi"), not hardcoded logic. Customer has no rows: customer
/// actions are governed by ownership + the state-machine customer branch, not permission keys.
/// SuperAdmin gets everything (it also bypasses the tenant filter).
/// </summary>
public static class RolePermissionMatrix
{
    public static readonly IReadOnlyDictionary<RoleType, string[]> Defaults = new Dictionary<RoleType, string[]>
    {
        [RoleType.SuperAdmin] = [.. PermissionKeys.All],
        [RoleType.Admin] =
        [
            PermissionKeys.TicketView, PermissionKeys.TicketEdit, PermissionKeys.TicketDelete,
            PermissionKeys.TicketAssign, PermissionKeys.TicketStatusChange, PermissionKeys.CommentInternal,
            PermissionKeys.ReportCompany, PermissionKeys.SettingsManage,
            PermissionKeys.UserInvite, PermissionKeys.PermissionAssign,
        ],
        [RoleType.Personel] =
        [
            PermissionKeys.TicketView, PermissionKeys.TicketStatusChange, PermissionKeys.CommentInternal,
        ],
    };
}
