using System.Linq.Expressions;
using CrmKanban.Domain.Authorization;
using CrmKanban.Domain.Entities;

namespace CrmKanban.Application.Tickets;

/// <summary>
/// What "my tickets" means for a staff member without <see cref="PermissionKeys.TicketViewAll"/>:
/// the ones assigned to them plus the ones they opened. Their workspace, not the company's whole
/// pipeline. Membership used to imply reading everything, so an accountant added to the company could
/// page through every sales, legal and HR request in it.
///
/// <para>The rule is written twice — once as a query filter and once as a single-ticket check — because
/// a list is filtered in SQL and a detail is checked in memory. <b>They must stay identical.</b> A
/// disagreement is not a cosmetic bug: it is a ticket that is hidden from the board but readable by
/// typing its id, which is the same class of hole Faz 28 found twice. `WorkspaceAgreementTests` pins
/// the two forms to each other, so drifting one of them fails the build rather than leaking quietly.</para>
/// </summary>
public static class TicketWorkspace
{
    /// <summary>Query-side. <paramref name="seesAllIn"/> holds the companies where the caller does have
    /// ticket.view.all — per company, because a list can span memberships and someone may lead one
    /// company's pipeline while being a single contributor in another.</summary>
    public static Expression<Func<Ticket, bool>> Filter(Guid userId, IReadOnlySet<Guid> seesAllIn) =>
        t => seesAllIn.Contains(t.CompanyId) || t.AssignedToId == userId || t.OpenedById == userId;

    /// <summary>Single-ticket side. Same three clauses, same order.</summary>
    public static bool Contains(Ticket ticket, Guid userId, bool seesAllInThisCompany) =>
        seesAllInThisCompany || ticket.AssignedToId == userId || ticket.OpenedById == userId;
}
