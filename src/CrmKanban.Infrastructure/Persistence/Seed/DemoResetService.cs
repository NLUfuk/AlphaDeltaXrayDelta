using CrmKanban.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CrmKanban.Infrastructure.Persistence.Seed;

/// <summary>
/// Puts the demo tenants back to their seeded state: delete everything that belongs to them, then run
/// <see cref="DevSeeder"/> again.
///
/// <para><b>Why this exists.</b> A demo that strangers can sign in to will eventually be edited, emptied
/// or filled with nonsense — by a curious evaluator as easily as by a bad actor. Locking the demo down
/// (read-only) would take away the thing being demonstrated. So instead of preventing damage we make it
/// temporary: whatever happens, the tenants return to a known state on the next reset. Together with the
/// password moving out of the repository (<see cref="DemoOptions.DemoPassword"/>) that is the whole
/// mitigation — one closes the door, the other cleans the room.</para>
///
/// <para><b>Blast radius.</b> Only rows belonging to the tenants in <see cref="DemoTenants.Slugs"/>, plus
/// the accounts that exist solely for them. A super admin is never touched, and neither is a user who has
/// any membership or ticket OUTSIDE the demo tenants — if a real person ever gets attached to the demo,
/// the reset leaves them alone rather than deleting their account.</para>
///
/// <para>Deletes are per-table by CompanyId because the schema has no foreign keys between these tables
/// (CompanyId is denormalized so the tenant filter can be a direct predicate) — there is no cascade to
/// lean on and no ordering to respect.</para>
///
/// ponytail: stored attachment blobs are left in place; the rows pointing at them go, so they become
/// unreferenced objects in the bucket. Add a storage sweep if the demo ever runs long enough for that
/// to cost anything.
/// </summary>
public sealed class DemoResetService(
    DbContextOptions<CrmDbContext> options,
    DevSeeder seeder,
    IOptions<DemoOptions> demoOptions,
    ILogger<DemoResetService> logger)
{
    public async Task ResetAsync(CancellationToken ct = default)
    {
        if (!demoOptions.Value.HasUsablePassword)
        {
            logger.LogWarning("Demo reset skipped: Seed:DemoPassword is not set.");
            return;
        }

        await using (var db = new CrmDbContext(options, SystemCurrentUserService.System))
        {
            var companyIds = await db.Companies.IgnoreQueryFilters()
                .Where(c => DemoTenants.Slugs.Contains(c.Slug))
                .Select(c => c.Id).ToListAsync(ct);

            if (companyIds.Count > 0)
            {
                var userIds = await DemoOnlyUserIdsAsync(db, companyIds, ct);
                await WipeAsync(db, companyIds, userIds, ct);
                logger.LogInformation("Demo tenants wiped: {Companies} company/companies, {Users} account(s).",
                    companyIds.Count, userIds.Count);
            }
        }

        await seeder.SeedAsync(ct);
        logger.LogInformation("Demo tenants re-seeded.");
    }

    /// <summary>Accounts that exist only for the demo: a member of, or the opener of a ticket in, a demo
    /// tenant — and nowhere else. Super admins are excluded outright.</summary>
    private static async Task<List<Guid>> DemoOnlyUserIdsAsync(CrmDbContext db, List<Guid> companyIds, CancellationToken ct)
    {
        var candidates = await db.Memberships.IgnoreQueryFilters()
            .Where(m => companyIds.Contains(m.CompanyId)).Select(m => m.UserId)
            .Union(db.Tickets.IgnoreQueryFilters()
                .Where(t => companyIds.Contains(t.CompanyId)).Select(t => t.OpenedById))
            .ToListAsync(ct);

        var elsewhereByMembership = await db.Memberships.IgnoreQueryFilters()
            .Where(m => !companyIds.Contains(m.CompanyId) && candidates.Contains(m.UserId))
            .Select(m => m.UserId).ToListAsync(ct);
        var elsewhereByTicket = await db.Tickets.IgnoreQueryFilters()
            .Where(t => !companyIds.Contains(t.CompanyId) && candidates.Contains(t.OpenedById))
            .Select(t => t.OpenedById).ToListAsync(ct);
        var elsewhere = elsewhereByMembership.Concat(elsewhereByTicket).ToHashSet();

        var superAdmins = await db.Users.IgnoreQueryFilters()
            .Where(u => u.IsSuperAdmin).Select(u => u.Id).ToListAsync(ct);

        return candidates.Distinct().Where(id => !elsewhere.Contains(id) && !superAdmins.Contains(id)).ToList();
    }

    private static async Task WipeAsync(CrmDbContext db, List<Guid> companyIds, List<Guid> userIds, CancellationToken ct)
    {
        // Company-scoped rows. TicketStatus and UserPermission carry a NULLABLE CompanyId where null means
        // "global" — the predicate must never match those, or the reset would delete the default columns
        // and the role baseline for the whole system.
        var statusIds = await db.TicketStatuses.IgnoreQueryFilters()
            .Where(s => s.CompanyId != null && companyIds.Contains(s.CompanyId.Value))
            .Select(s => s.Id).ToListAsync(ct);
        await db.StatusTransitions.IgnoreQueryFilters()
            .Where(t => statusIds.Contains(t.FromStatusId) || statusIds.Contains(t.ToStatusId))
            .ExecuteDeleteAsync(ct);

        await db.Attachments.IgnoreQueryFilters().Where(x => companyIds.Contains(x.CompanyId)).ExecuteDeleteAsync(ct);
        await db.CommentRevisions.IgnoreQueryFilters().Where(x => companyIds.Contains(x.CompanyId)).ExecuteDeleteAsync(ct);
        await db.Comments.IgnoreQueryFilters().Where(x => companyIds.Contains(x.CompanyId)).ExecuteDeleteAsync(ct);
        await db.TicketEvents.IgnoreQueryFilters().Where(x => companyIds.Contains(x.CompanyId)).ExecuteDeleteAsync(ct);
        await db.Tickets.IgnoreQueryFilters().Where(x => companyIds.Contains(x.CompanyId)).ExecuteDeleteAsync(ct);
        await db.FormFields.IgnoreQueryFilters().Where(x => companyIds.Contains(x.CompanyId)).ExecuteDeleteAsync(ct);
        await db.CustomerTrusts.IgnoreQueryFilters().Where(x => companyIds.Contains(x.CompanyId)).ExecuteDeleteAsync(ct);
        await db.Memberships.IgnoreQueryFilters().Where(x => companyIds.Contains(x.CompanyId)).ExecuteDeleteAsync(ct);
        await db.TicketStatuses.IgnoreQueryFilters().Where(x => statusIds.Contains(x.Id)).ExecuteDeleteAsync(ct);
        await db.UserPermissions.IgnoreQueryFilters()
            .Where(x => x.CompanyId != null && companyIds.Contains(x.CompanyId.Value)).ExecuteDeleteAsync(ct);
        await db.Invitations.IgnoreQueryFilters()
            .Where(x => x.CompanyId != null && companyIds.Contains(x.CompanyId.Value)).ExecuteDeleteAsync(ct);
        await db.Companies.IgnoreQueryFilters().Where(x => companyIds.Contains(x.Id)).ExecuteDeleteAsync(ct);

        if (userIds.Count == 0) return;

        // The demo accounts themselves and everything hanging off them, including invitations that were
        // never company-scoped (self-registration) and the queued mail addressed to them.
        var emails = await db.Users.IgnoreQueryFilters()
            .Where(u => userIds.Contains(u.Id)).Select(u => u.Email).ToListAsync(ct);
        await db.RefreshTokens.IgnoreQueryFilters().Where(x => userIds.Contains(x.UserId)).ExecuteDeleteAsync(ct);
        await db.UserNotificationPrefs.IgnoreQueryFilters().Where(x => userIds.Contains(x.UserId)).ExecuteDeleteAsync(ct);
        await db.UserPermissions.IgnoreQueryFilters().Where(x => userIds.Contains(x.UserId)).ExecuteDeleteAsync(ct);
        await db.Invitations.IgnoreQueryFilters().Where(x => userIds.Contains(x.UserId)).ExecuteDeleteAsync(ct);
        await db.EmailQueue.IgnoreQueryFilters().Where(x => emails.Contains(x.ToEmail)).ExecuteDeleteAsync(ct);
        await db.Users.IgnoreQueryFilters().Where(x => userIds.Contains(x.Id)).ExecuteDeleteAsync(ct);
    }
}
