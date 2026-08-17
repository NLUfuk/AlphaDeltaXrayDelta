namespace CrmKanban.Domain.Authorization;

/// <summary>
/// Canonical permission keys (spec §7). Single source of truth: seed, policies, and the
/// admin permission UI all read from here. Grouped by prefix for the settings screen.
/// </summary>
public static class PermissionKeys
{
    public const string TicketView = "ticket.view";

    /// <summary>See the WHOLE company's tickets rather than just your own work. Without it a staff
    /// member still holds ticket.view, but "the tickets" means the ones assigned to them plus the ones
    /// they opened — their workspace. Split out because membership in a company was being read as a
    /// licence to read every request in it: an accountant added to the company could page through
    /// sales, legal and HR tickets in full. Reading the pipeline is a different job from working in it,
    /// so it is a different key (compare TicketValue, split from TicketView for the same reason).</summary>
    public const string TicketViewAll = "ticket.view.all";

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
        TicketView, TicketViewAll, TicketEdit, TicketDelete, TicketAssign, TicketStatusChange, TicketValue,
        CommentInternal, ReportCompany, ReportGlobal, SettingsManage, StatusManage,
        UserInvite, PermissionAssign,
    ];
}
