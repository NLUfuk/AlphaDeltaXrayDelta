using CrmKanban.Application.Abstractions;
using CrmKanban.Application.Common;
using CrmKanban.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CrmKanban.Application.Auth;

/// <summary>
/// Login, refresh-with-rotation, logout, and password change (spec §9). Each responsibility is a
/// single method (spec §4.1). Refresh tokens are stored hashed and rotated on every use; presenting
/// an already-revoked token is treated as theft and revokes the user's whole token chain.
/// </summary>
public sealed class AuthService(
    IAppDbContext db,
    IJwtTokenService jwt,
    IPasswordHasher passwordHasher,
    IClock clock,
    IOptions<AuthOptions> authOptions,
    IOptions<AppOptions> appOptions,
    ICurrentUserService currentUser)
{
    private readonly AuthOptions _auth = authOptions.Value;
    private readonly AppOptions _app = appOptions.Value;

    /// <summary>Self-service customer registration (spec §18.5). Creates an inactive account and emails
    /// an activation link; the password is set when they click it (the existing invite/accept flow).
    /// Never reveals whether the email already exists (uniform response) — an already-active account is
    /// silently ignored, a pending one is re-sent a fresh link.</summary>
    public async Task RegisterAsync(RegisterRequest request, CancellationToken ct = default)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var now = clock.UtcNow;
        var user = await db.Users.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.Email == email, ct);

        if (user is { IsActive: true })
            return; // account exists and is usable — say nothing (anti-enumeration); they can log in

        if (user is null)
        {
            user = new User(email, request.FirstName, request.LastName);
            user.Deactivate(); // activated when they set a password via the emailed link
            db.Users.Add(user);
        }

        var raw = TokenHasher.NewRawToken();
        db.Invitations.Add(new Invitation(user.Id, TokenHasher.Hash(raw), now.AddDays(_auth.InviteTokenDays), invitedById: null));
        InviteEmail.Enqueue(db, _app.PublicBaseUrl, email, "account_verify", raw,
            new Dictionary<string, string> { ["name"] = $"{request.FirstName} {request.LastName}".Trim() });
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Self-service password reset (spec §1.12). Emails a one-time link that sets a new password
    /// (reuses the invite/accept flow — <see cref="InvitationService.AcceptInviteAsync"/>, which also revokes
    /// existing sessions). Only an active account with a password can reset; inactive/unknown accounts get
    /// nothing. Always returns without revealing whether the email exists (anti-enumeration).</summary>
    public async Task ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken ct = default)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await db.Users.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.Email == email, ct);

        // Only a usable account can reset. Unknown/inactive/never-set-password accounts fall through
        // silently (an inactive account activates via the invite link, not here).
        if (user is not { IsActive: true } || user.PasswordHash is null)
            return;

        var now = clock.UtcNow;
        var raw = TokenHasher.NewRawToken();
        db.Invitations.Add(new Invitation(user.Id, TokenHasher.Hash(raw), now.AddDays(_auth.InviteTokenDays), invitedById: null));
        InviteEmail.Enqueue(db, _app.PublicBaseUrl, email, "password_reset", raw,
            new Dictionary<string, string> { ["name"] = $"{user.FirstName} {user.LastName}".Trim() });
        await db.SaveChangesAsync(ct);
    }

    public async Task<AuthResult> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await db.Users.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.Email == email, ct);

        // Uniform failure — never reveal whether the email exists.
        if (user is null || !user.IsActive || user.PasswordHash is null ||
            !passwordHasher.Verify(user, user.PasswordHash, request.Password))
            throw new UnauthorizedException("auth.invalid_credentials", "Invalid email or password.");

        return await IssueAsync(user, ct);
    }

    public async Task<AuthResult> RefreshAsync(RefreshRequest request, CancellationToken ct = default)
    {
        var hash = jwt.HashRefreshToken(request.RefreshToken);
        var token = await db.RefreshTokens.IgnoreQueryFilters().FirstOrDefaultAsync(t => t.TokenHash == hash, ct);
        if (token is null)
            throw new UnauthorizedException("auth.invalid_refresh", "Invalid refresh token.");

        var now = clock.UtcNow;
        if (!token.IsActive(now))
        {
            // Reuse of a revoked/expired token → likely theft. Revoke the whole chain for this user.
            if (token.RevokedAt is not null)
            {
                await RevokeAllAsync(token.UserId, now, ct);
                await db.SaveChangesAsync(ct);
            }
            throw new UnauthorizedException("auth.invalid_refresh", "Invalid refresh token.");
        }

        var user = await db.Users.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.Id == token.UserId, ct);
        if (user is null || !user.IsActive)
            throw new UnauthorizedException("auth.invalid_refresh", "Invalid refresh token.");

        var result = await IssueAsync(user, ct, rotatedFrom: token);
        return result;
    }

    /// <summary>Super-admin impersonation (support/debug): mint a normal session for another user without
    /// their password. SuperAdmin-only, never targets another super admin, and is audit-logged with the
    /// real actor. The issued token is an ordinary target session — accountability lives in the audit log,
    /// not the token (ponytail: no impersonator claim; add one if refresh must preserve the link).</summary>
    public async Task<AuthResult> ImpersonateAsync(Guid targetUserId, CancellationToken ct = default)
    {
        if (!currentUser.IsSuperAdmin)
            throw new ForbiddenException("auth.impersonate_forbidden", "Only a super admin can impersonate a user.");
        var actorId = currentUser.UserId ?? throw new UnauthorizedException("auth.required", "Authentication required.");

        var target = await db.Users.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.Id == targetUserId, ct)
            ?? throw new NotFoundException("user.not_found", "User not found.");
        if (target.IsSuperAdmin)
            throw new ForbiddenException("auth.impersonate_superadmin", "Cannot impersonate another super admin.");
        if (!target.IsActive)
            throw new BadRequestException("auth.impersonate_inactive", "Cannot impersonate an inactive account.");

        db.AuditLogs.Add(new AuditLog(actorId, "auth.impersonate", $"target={targetUserId}"));
        return await IssueAsync(target, ct); // persists the audit row too
    }

    public async Task<UserInfo> GetMeAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await db.Users.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.Id == userId, ct)
            ?? throw new NotFoundException("user.not_found", "User not found.");
        var memberships = await db.Memberships.IgnoreQueryFilters()
            .Where(m => m.UserId == userId)
            .Select(m => new CompanyMembershipInfo(m.CompanyId, m.Role))
            .ToListAsync(ct);
        return new UserInfo(user.Id, user.Email, $"{user.FirstName} {user.LastName}".Trim(),
            user.IsSuperAdmin, user.MustChangePassword, memberships);
    }

    public async Task LogoutAsync(RefreshRequest request, CancellationToken ct = default)
    {
        var hash = jwt.HashRefreshToken(request.RefreshToken);
        var token = await db.RefreshTokens.IgnoreQueryFilters().FirstOrDefaultAsync(t => t.TokenHash == hash, ct);
        token?.Revoke(clock.UtcNow);
        await db.SaveChangesAsync(ct);
    }

    public async Task ChangePasswordAsync(Guid userId, ChangePasswordRequest request, CancellationToken ct = default)
    {
        var user = await db.Users.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.Id == userId, ct)
            ?? throw new NotFoundException("user.not_found", "User not found.");

        if (user.PasswordHash is null || !passwordHasher.Verify(user, user.PasswordHash, request.CurrentPassword))
            throw new UnauthorizedException("auth.invalid_credentials", "Current password is incorrect.");

        user.SetPasswordHash(passwordHasher.Hash(user, request.NewPassword));
        await RevokeAllAsync(userId, clock.UtcNow, ct); // force other sessions to re-authenticate
        await db.SaveChangesAsync(ct);
    }

    private async Task<AuthResult> IssueAsync(User user, CancellationToken ct, RefreshToken? rotatedFrom = null)
    {
        var now = clock.UtcNow;
        var memberships = await db.Memberships.IgnoreQueryFilters()
            .Where(m => m.UserId == user.Id)
            .Select(m => new CompanyMembershipInfo(m.CompanyId, m.Role))
            .ToListAsync(ct);
        var companyIds = memberships.Select(m => m.CompanyId).ToList();

        var access = jwt.CreateAccessToken(new AccessTokenClaims(user, user.IsSuperAdmin, companyIds));

        var rawRefresh = jwt.CreateRefreshTokenValue();
        var refresh = new RefreshToken(user.Id, jwt.HashRefreshToken(rawRefresh), now.AddDays(_auth.RefreshTokenDays));
        db.RefreshTokens.Add(refresh);
        rotatedFrom?.Revoke(now, replacedBy: refresh.Id);

        await db.SaveChangesAsync(ct);

        var info = new UserInfo(user.Id, user.Email, $"{user.FirstName} {user.LastName}".Trim(),
            user.IsSuperAdmin, user.MustChangePassword, memberships);
        return new AuthResult(access.Token, access.ExpiresAt, rawRefresh, info);
    }

    private async Task RevokeAllAsync(Guid userId, DateTime now, CancellationToken ct)
    {
        var tokens = await db.RefreshTokens.IgnoreQueryFilters()
            .Where(t => t.UserId == userId && t.RevokedAt == null)
            .ToListAsync(ct);
        foreach (var t in tokens)
            t.Revoke(now);
    }
}
