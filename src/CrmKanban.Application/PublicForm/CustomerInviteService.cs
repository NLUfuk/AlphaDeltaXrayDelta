using CrmKanban.Application.Abstractions;
using CrmKanban.Application.Auth;
using CrmKanban.Application.Common;
using CrmKanban.Domain.Authorization;
using CrmKanban.Domain.Entities;
using CrmKanban.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CrmKanban.Application.PublicForm;

/// <summary>
/// Mints the per-customer sign-in link staff hand out (Faz 35): <c>/c/{slug}?davet={token}</c>.
/// <para>
/// Before this, "müşteri linki oluştur" produced the bare <c>/c/{slug}</c> — the same URL for
/// everyone, carrying nothing. The server could not tell a customer staff had invited from a stranger
/// who found the page, so it trusted both. The token is what makes that distinction expressible: it
/// is bound to one email, one company, and one use.
/// </para>
/// <para>
/// The token is returned to the caller (unlike account tokens, which only ever leave by email)
/// because handing the link over is the whole point — staff paste it into WhatsApp. That is safe
/// precisely because it is not a credential: it cannot log anyone in or set a password, and it is
/// worthless to anyone whose address it was not issued for.
/// </para>
/// </summary>
public sealed class CustomerInviteService(
    IAppDbContext db,
    IPermissionService permissions,
    ICurrentUserService currentUser,
    IClock clock,
    IOptions<AppOptions> appOptions)
{
    private readonly AppOptions _app = appOptions.Value;

    /// <summary>Days a customer-access link stays usable. Long enough for a WhatsApp message to be
    /// read over a weekend, short enough that a forwarded link goes stale.</summary>
    private const int ValidDays = 7;

    public async Task<CustomerInviteResult> CreateAsync(
        Guid companyId, CustomerInviteRequest request, CancellationToken ct = default)
    {
        var actorId = currentUser.UserId
            ?? throw new UnauthorizedException("auth.required", "Authentication required.");

        // Inviting a customer is inviting someone in — same permission as inviting staff, and the
        // company-scoped check is what stops an admin of company A vouching for someone at company B.
        if (!await permissions.HasPermissionAsync(actorId, companyId, PermissionKeys.UserInvite, ct))
            throw new ForbiddenException("permission.denied", "You cannot invite customers to this company.");

        var company = await db.Companies.IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.Id == companyId && c.DeletedAt == null && c.IsActive && c.ArchivedAt == null, ct)
            ?? throw new NotFoundException("company.not_found", "Company not found.");

        var email = request.Email.Trim().ToLowerInvariant();
        var now = clock.UtcNow;

        // Find-or-create: inviting someone who already has an account is normal (a returning customer
        // staff wants to fast-track again). A newly created shell stays inactive until they register.
        var user = await db.Users.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.Email == email, ct);
        if (user is null)
        {
            user = new User(email, request.FirstName?.Trim() ?? "", request.LastName?.Trim() ?? "");
            user.Deactivate();
            db.Users.Add(user);
        }

        // Only the newest link is live: re-issuing invalidates the previous one, so a link that was
        // sent to the wrong number can be revoked by simply generating another.
        var earlier = await db.Invitations.IgnoreQueryFilters()
            .Where(i => i.UserId == user.Id && i.CompanyId == companyId
                        && i.Kind == InvitationKind.CustomerAccess && i.AcceptedAt == null)
            .ToListAsync(ct);
        foreach (var old in earlier)
            old.Accept(now);

        var rawToken = TokenHasher.NewRawToken();
        db.Invitations.Add(new Invitation(
            user.Id, TokenHasher.Hash(rawToken), now.AddDays(ValidDays), actorId,
            InvitationKind.CustomerAccess, companyId));

        await db.SaveChangesAsync(ct);

        var url = $"{_app.PublicBaseUrl.TrimEnd('/')}/c/{company.Slug}?davet={rawToken}";
        return new CustomerInviteResult(url, email, now.AddDays(ValidDays));
    }
}

/// <summary>Who staff is inviting. Name is optional — it only prefills the sign-up form.</summary>
public sealed record CustomerInviteRequest(string Email, string? FirstName = null, string? LastName = null);

/// <summary>The link to hand over, plus what staff needs to see to sanity-check it before sending.</summary>
public sealed record CustomerInviteResult(string Url, string Email, DateTime ExpiresAt);
