using CrmKanban.Application.Abstractions;
using CrmKanban.Application.Common;
using CrmKanban.Domain.Entities;
using CrmKanban.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CrmKanban.Application.Auth;

/// <summary>
/// Company-scoped staff invitation and account activation (spec §9): an admin invites a Personel or
/// a second Admin into their company; the invitee sets a password via the emailed link. Self-service
/// signup is Customer-only (spec §18.5), so this is the only way staff accounts are born.
/// The caller must already hold user.invite in the target company (enforced by the endpoint policy).
/// </summary>
public sealed class InvitationService(
    IAppDbContext db,
    IPasswordHasher passwordHasher,
    IClock clock,
    ICurrentUserService currentUser,
    IPermissionService permissions,
    IOptions<AuthOptions> authOptions,
    IOptions<AppOptions> appOptions)
{
    private readonly AuthOptions _auth = authOptions.Value;
    private readonly AppOptions _app = appOptions.Value;

    public async Task<InviteResult> InviteUserAsync(InviteUserRequest request, CancellationToken ct = default)
    {
        if (request.Role is not (RoleType.Admin or RoleType.Personel))
            throw new ForbiddenException("invite.invalid_role", "Only Admin or Personel can be invited to a company.");

        var inviterUserId = currentUser.UserId
            ?? throw new UnauthorizedException("auth.required", "Authentication required.");

        // Authorization: the inviter must hold user.invite in the target company (SuperAdmin bypasses).
        if (!currentUser.IsSuperAdmin &&
            !await permissions.HasPermissionAsync(inviterUserId, request.CompanyId, Domain.Authorization.PermissionKeys.UserInvite, ct))
            throw new ForbiddenException("invite.forbidden", "You cannot invite users to this company.");

        var email = request.Email.Trim().ToLowerInvariant();
        var now = clock.UtcNow;

        var user = await db.Users.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.Email == email, ct);
        if (user is null)
        {
            user = new User(email, request.FirstName, request.LastName);
            user.Deactivate(); // activated on invite acceptance
            db.Users.Add(user);
        }

        var alreadyMember = await db.Memberships.IgnoreQueryFilters()
            .AnyAsync(m => m.UserId == user.Id && m.CompanyId == request.CompanyId, ct);
        if (alreadyMember)
            throw new ConflictException("invite.already_member", "This user is already a member of the company.");

        db.Memberships.Add(new Membership(user.Id, request.CompanyId, request.Role));

        // An already-active user (invited elsewhere before) needs no password setup.
        if (!user.IsInvitedPending)
        {
            await db.SaveChangesAsync(ct);
            return new InviteResult(user.Id, RawToken: "", ExpiresAt: now);
        }

        var raw = TokenHasher.NewRawToken();
        var invitation = new Invitation(user.Id, TokenHasher.Hash(raw), now.AddDays(_auth.InviteTokenDays), inviterUserId);
        db.Invitations.Add(invitation);

        var companyName = await db.Companies.IgnoreQueryFilters()
            .Where(c => c.Id == request.CompanyId).Select(c => c.Name).FirstOrDefaultAsync(ct) ?? "";
        InviteEmail.Enqueue(db, _app.PublicBaseUrl, email, "staff_invite", raw,
            new Dictionary<string, string>
            {
                ["name"] = $"{request.FirstName} {request.LastName}".Trim(),
                ["companyName"] = companyName,
                ["role"] = request.Role.ToString(),
            });

        await db.SaveChangesAsync(ct);

        return new InviteResult(user.Id, raw, invitation.ExpiresAt);
    }

    public async Task AcceptInviteAsync(AcceptInviteRequest request, CancellationToken ct = default)
    {
        var hash = TokenHasher.Hash(request.Token);
        // Link tokens only: the short sign-in codes live in the same table but must never be redeemable
        // here — their 6-digit space would be brute-forceable through this by-token lookup.
        var invitation = await db.Invitations.IgnoreQueryFilters()
            .FirstOrDefaultAsync(i => i.TokenHash == hash && i.Kind == InvitationKind.Link, ct);
        var now = clock.UtcNow;
        if (invitation is null || !invitation.IsPending(now))
            throw new UnauthorizedException("invite.invalid", "This invitation is invalid or has expired.");

        var user = await db.Users.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.Id == invitation.UserId, ct)
            ?? throw new NotFoundException("user.not_found", "User not found.");

        user.SetPasswordHash(passwordHasher.Hash(user, request.NewPassword));
        user.Activate();
        invitation.Accept(now);

        // Setting a password invalidates every existing session. No-op for a fresh account (none yet);
        // for a password reset it kills any session an attacker may hold. (Mirrors ChangePassword.)
        var activeTokens = await db.RefreshTokens.IgnoreQueryFilters()
            .Where(t => t.UserId == user.Id && t.RevokedAt == null)
            .ToListAsync(ct);
        foreach (var t in activeTokens)
            t.Revoke(now);

        await db.SaveChangesAsync(ct);
    }
}
