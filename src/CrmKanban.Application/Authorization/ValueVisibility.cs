using CrmKanban.Application.Abstractions;
using CrmKanban.Domain.Authorization;

namespace CrmKanban.Application.Authorization;

/// <summary>
/// The single definition of "may this caller see money for this company" (<c>ticket.value</c>, Faz 39).
///
/// <para>It lived as two byte-identical private copies — one in <c>ReportService</c>, one in
/// <c>TicketQueryService</c>. Ordinary duplication is cheap; this one was not. Both copies strip amounts
/// out of the DTO before it is serialized, so the day they drift is the day one endpoint starts shipping
/// another company's order book to someone who may not see it. A rule that decides what leaves the server
/// gets exactly one definition.</para>
/// </summary>
public static class ValueVisibility
{
    /// <summary>
    /// Super admin sees everything; anyone else needs <c>ticket.value</c> ON THAT COMPANY — never
    /// globally, because an admin at one company must not read another's amounts through a shared list.
    /// An anonymous caller (no user id) sees nothing.
    /// </summary>
    public static async Task<bool> CanSeeValueAsync(
        this IPermissionService permissions, ICurrentUserService currentUser, Guid companyId, CancellationToken ct)
    {
        if (currentUser.IsSuperAdmin) return true;
        var userId = currentUser.UserId;
        return userId is not null
            && await permissions.HasPermissionAsync(userId.Value, companyId, PermissionKeys.TicketValue, ct);
    }
}
