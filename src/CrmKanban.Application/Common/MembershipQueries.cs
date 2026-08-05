using CrmKanban.Application.Abstractions;
using CrmKanban.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CrmKanban.Application.Common;

/// <summary>
/// The one way to read memberships as AUTHORITY (spec §6, §7).
///
/// Authorization reads have to bypass the tenant query filter — they are the bootstrap that computes
/// the caller's scope in the first place, and a customer has no scope at all. But bypassing it also
/// drops the soft-delete guard, and memberships are only ever soft-deleted: removing a member
/// (<c>CompanyService.RemoveMemberAsync</c>) and deleting a company (<c>DeleteAsync</c>) both just stamp
/// DeletedAt. Every call site had to remember the guard by hand, and the two that mattered most — the
/// access token's company_id claims and /me — did not, so a removed member kept full staff scope after a
/// fresh login. One predicate, so forgetting it is no longer possible.
/// </summary>
public static class MembershipQueries
{
    /// <summary>Memberships that currently grant access. Tenant filter bypassed on purpose; revoked
    /// (soft-deleted) rows excluded explicitly.</summary>
    public static IQueryable<Membership> ActiveMemberships(this IAppDbContext db) =>
        db.Memberships.IgnoreQueryFilters().Where(m => m.DeletedAt == null);
}
