using CrmKanban.Application.Abstractions;
using CrmKanban.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CrmKanban.Application.Tickets;

/// <summary>
/// The effective status set for a company (spec §12, §18.9). A company that has customized its board
/// owns a full set of TicketStatus rows (CompanyId == companyId); until then it uses the global
/// default set (CompanyId == null). One predicate, reused by the kanban, the status dropdowns, and
/// the initial-status lookup, so "which columns does this company have?" is answered in exactly one
/// place. Reads bypass the tenant filter and re-apply DeletedAt == null explicitly.
/// </summary>
public static class StatusSet
{
    public static async Task<List<TicketStatus>> EffectiveAsync(IAppDbContext db, Guid companyId, CancellationToken ct)
    {
        var own = await db.TicketStatuses.IgnoreQueryFilters()
            .Where(s => s.CompanyId == companyId && s.DeletedAt == null)
            .OrderBy(s => s.Order).ToListAsync(ct);
        if (own.Count > 0) return own;

        return await db.TicketStatuses.IgnoreQueryFilters()
            .Where(s => s.CompanyId == null && s.DeletedAt == null)
            .OrderBy(s => s.Order).ToListAsync(ct);
    }
}
