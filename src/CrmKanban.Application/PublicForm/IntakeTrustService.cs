using CrmKanban.Application.Abstractions;
using CrmKanban.Application.Auth;
using CrmKanban.Domain.Entities;
using CrmKanban.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CrmKanban.Application.PublicForm;

/// <summary>
/// The single decision point for "does this incoming ticket go straight to the board, or wait in the
/// moderation queue?" (spec §10, Faz 35). Every intake path routes through here — the anonymous form
/// and the signed-in customer portal alike — so the rule cannot drift between them.
/// <para>
/// A ticket enters the pool directly for exactly two reasons:
/// <list type="number">
///   <item><b>Standing trust</b>: staff vouched for this customer at this company (<see cref="CustomerTrust"/>).</item>
///   <item><b>A valid staff invitation</b>: a one-shot <see cref="InvitationKind.CustomerAccess"/> token,
///   consumed by the ticket it lets through.</item>
/// </list>
/// Everything else waits: a stranger who found the company link, and a known customer's later tickets.
/// Recognising an email address is not vouching for it — the previous rule ("known email → straight
/// in") meant anyone who had ever submitted the public form bypassed moderation forever.
/// </para>
/// <para>
/// All reads use IgnoreQueryFilters and carry an explicit CompanyId predicate: the caller here is a
/// customer or anonymous, so they have no company scope and the tenant filter would hide their own
/// rows. Dropping the filter without re-stating the scope by hand is how Faz 28 leaked across
/// tenants — the CompanyId conditions below are the replacement, not an optimisation.
/// </para>
/// </summary>
public sealed class IntakeTrustService(IAppDbContext db, IClock clock)
{
    /// <summary>
    /// Decides moderation for one incoming ticket and consumes the invitation if that is what let it
    /// through. Does not save — the caller commits the ticket and the consumed token together, so a
    /// failed submit cannot burn the customer's invitation.
    /// </summary>
    public async Task<bool> ShouldHoldForApprovalAsync(
        Guid companyId, Guid customerId, string? rawInviteToken, CancellationToken ct = default)
    {
        // Trust first: a trusted customer holding an invite should not have it burned needlessly.
        if (await IsTrustedAsync(companyId, customerId, ct)) return false;
        return !await TryConsumeInviteAsync(companyId, customerId, rawInviteToken, ct);
    }

    public Task<bool> IsTrustedAsync(Guid companyId, Guid customerId, CancellationToken ct = default) =>
        db.CustomerTrusts.IgnoreQueryFilters()
            .AnyAsync(t => t.CompanyId == companyId && t.UserId == customerId && t.DeletedAt == null, ct);

    /// <summary>
    /// Redeems a customer-access token. Every condition is load-bearing: the token must exist, be of
    /// the customer-access kind (never an account token replayed here), belong to <b>this</b> customer
    /// (a link forwarded to a stranger is worthless), be scoped to <b>this</b> company, be unused, and
    /// be unexpired.
    /// </summary>
    private async Task<bool> TryConsumeInviteAsync(
        Guid companyId, Guid customerId, string? rawToken, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(rawToken)) return false;

        var hash = TokenHasher.Hash(rawToken);
        var now = clock.UtcNow;
        var invite = await db.Invitations.IgnoreQueryFilters().FirstOrDefaultAsync(
            i => i.TokenHash == hash
                 && i.Kind == InvitationKind.CustomerAccess
                 && i.UserId == customerId
                 && i.CompanyId == companyId
                 && i.AcceptedAt == null
                 && i.DeletedAt == null,
            ct);

        if (invite is null || !invite.IsPending(now)) return false;

        invite.Accept(now); // one-shot: the next ticket from this customer queues for approval again
        return true;
    }
}
