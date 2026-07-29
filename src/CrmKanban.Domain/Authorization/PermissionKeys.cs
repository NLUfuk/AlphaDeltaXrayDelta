namespace CrmKanban.Domain.Authorization;

/// <summary>
/// Canonical permission keys (spec §7). Single source of truth: seed, policies, and the
/// admin permission UI all read from here. Grouped by prefix for the settings screen.
/// </summary>
public static class PermissionKeys
{
    public const string TicketView = "ticket.view";
    public const string TicketEdit = "ticket.edit";
    public const string TicketDelete = "ticket.delete";
    public const string TicketAssign = "ticket.assign";
    public const string TicketStatusChange = "ticket.status.change";

    public const string CommentInternal = "comment.internal";

    public const string ReportCompany = "report.company";
    public const string ReportGlobal = "report.global";

    public const string SettingsManage = "settings.manage";

    public const string UserInvite = "user.invite";
    public const string PermissionAssign = "permission.assign";

    /// <summary>Every defined permission key — used by idempotent seed.</summary>
    public static readonly IReadOnlyList<string> All =
    [
        TicketView, TicketEdit, TicketDelete, TicketAssign, TicketStatusChange,
        CommentInternal, ReportCompany, ReportGlobal, SettingsManage,
        UserInvite, PermissionAssign,
    ];
}
