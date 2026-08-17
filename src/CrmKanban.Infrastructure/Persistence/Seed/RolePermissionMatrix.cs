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
            PermissionKeys.TicketView, PermissionKeys.TicketViewAll, PermissionKeys.TicketEdit, PermissionKeys.TicketDelete,
            PermissionKeys.TicketAssign, PermissionKeys.TicketStatusChange, PermissionKeys.TicketValue,
            PermissionKeys.CommentInternal,
            PermissionKeys.ReportCompany, PermissionKeys.SettingsManage, PermissionKeys.StatusManage,
            PermissionKeys.UserInvite, PermissionKeys.PermissionAssign,
        ],
        // Personel deliberately does NOT get TicketViewAll: their default view is their own workspace —
        // tickets assigned to them plus the ones they opened. An admin who wants a particular person to
        // see the whole pipeline grants it to them individually on the permissions screen.
        [RoleType.Personel] =
        [
            PermissionKeys.TicketView, PermissionKeys.TicketStatusChange, PermissionKeys.CommentInternal,
        ],
    };
}
