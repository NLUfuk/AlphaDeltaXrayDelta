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

    /// <summary>See and set an opportunity's money figures, and open the revenue report (Faz 39).
    /// Separate from ticket.view because commercial value is a different kind of secret from the
    /// request itself: adding someone to a company should not hand them the whole order book.</summary>
    public const string TicketValue = "ticket.value";

    public const string CommentInternal = "comment.internal";

    public const string ReportCompany = "report.company";
    public const string ReportGlobal = "report.global";

    public const string SettingsManage = "settings.manage";

    /// <summary>Manage a company's kanban columns (statuses): add/rename/recolor/reorder/remove.</summary>
    public const string StatusManage = "status.manage";

    public const string UserInvite = "user.invite";
    public const string PermissionAssign = "permission.assign";

    /// <summary>Every defined permission key — used by idempotent seed.</summary>
    public static readonly IReadOnlyList<string> All =
    [
        TicketView, TicketEdit, TicketDelete, TicketAssign, TicketStatusChange, TicketValue,
        CommentInternal, ReportCompany, ReportGlobal, SettingsManage, StatusManage,
        UserInvite, PermissionAssign,
    ];
}
