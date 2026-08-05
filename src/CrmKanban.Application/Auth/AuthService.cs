using CrmKanban.Application.Abstractions;
using CrmKanban.Application.Common;
using CrmKanban.Domain.Entities;
using CrmKanban.Domain.Enums;
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

    /// <summary>Customer registration from a company's sign-in link (/c/{slug}). Creates (or reuses) the
    /// account with the chosen password, holds it inactive, and emails a 6-digit confirmation code — the
    /// customer types it back to prove the address (spec §9; the emailed-link variant is
    /// <see cref="RegisterAsync"/>). Uniform 204 to the caller: an existing active account is not
    /// revealed and keeps its own password — the code then acts as a one-time login instead.</summary>
    public async Task RegisterCustomerAsync(string slug, CustomerRegisterRequest request, CancellationToken ct = default)
    {
        var company = await CompanyLookup.OpenBySlugAsync(db, slug, ct);
        var email = request.Email.Trim().ToLowerInvariant();
        var now = clock.UtcNow;

        var user = await db.Users.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.Email == email, ct);
        if (user is null)
        {
            user = new User(email, request.FirstName, request.LastName);
            user.Deactivate(); // activated by the code
            db.Users.Add(user);
        }
        // An account that can already log in keeps its password (this is not a reset path — the code only
        // proves the mailbox). A pending/passwordless one takes the password chosen here.
        if (!user.IsActive || user.PasswordHash is null)
            user.SetPasswordHash(passwordHasher.Hash(user, request.Password));

        // Only the newest code is live: consume any earlier pending ones so a re-send invalidates them.
        var earlier = await db.Invitations.IgnoreQueryFilters()
            .Where(i => i.UserId == user.Id && i.Kind == InvitationKind.Code && i.AcceptedAt == null)
            .ToListAsync(ct);
        foreach (var old in earlier)
            old.Accept(now);

        var code = NewCode();
        db.Invitations.Add(new Invitation(user.Id, HashCode(user.Id, code),
            now.AddMinutes(_auth.VerificationCodeMinutes), invitedById: null, InvitationKind.Code));
        db.EmailQueue.Add(new EmailQueue(email, "account_code", System.Text.Json.JsonSerializer.Serialize(
            new Dictionary<string, string>
            {
                ["name"] = $"{request.FirstName} {request.LastName}".Trim(),
                ["companyName"] = company.Name,
                ["code"] = code,
                ["minutes"] = _auth.VerificationCodeMinutes.ToString(System.Globalization.CultureInfo.InvariantCulture),
            })));
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Verifies the emailed 6-digit code and logs the customer in. The code is looked up BY USER
    /// (never by token — 6 digits are too few to be a lookup key) and capped at
    /// <see cref="AuthOptions.MaxCodeAttempts"/> failed tries, after which a new code must be requested.</summary>
    public async Task<AuthResult> VerifyCodeAsync(string slug, VerifyCodeRequest request, CancellationToken ct = default)
    {
        await CompanyLookup.OpenBySlugAsync(db, slug, ct); // the company link must still be open
        var email = request.Email.Trim().ToLowerInvariant();
        var now = clock.UtcNow;

        var user = await db.Users.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.Email == email, ct);
        var invitation = user is null ? null : await db.Invitations.IgnoreQueryFilters()
            .Where(i => i.UserId == user.Id && i.Kind == InvitationKind.Code && i.AcceptedAt == null)
            .OrderByDescending(i => i.CreatedAt)
            .FirstOrDefaultAsync(ct);

        if (user is null || invitation is null || !invitation.IsPending(now))
            throw new UnauthorizedException("auth.invalid_code", "The code is invalid or has expired.");
        if (invitation.Attempts >= _auth.MaxCodeAttempts)
            throw new UnauthorizedException("auth.code_locked", "Too many attempts. Request a new code.");

        if (invitation.TokenHash != HashCode(user.Id, request.Code.Trim()))
        {
            invitation.RecordFailedAttempt();
            await db.SaveChangesAsync(ct);
            throw new UnauthorizedException("auth.invalid_code", "The code is invalid or has expired.");
        }

        invitation.Accept(now);
        user.Activate();
        return await IssueAsync(user, ct); // saves the accepted invitation too
    }

    /// <summary>A 6-digit code, uniformly random (no modulo bias: 000000-999999 drawn directly).</summary>
    private static string NewCode() => System.Security.Cryptography.RandomNumberGenerator
        .GetInt32(0, 1_000_000).ToString("D6", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>Codes are salted with the user id before hashing — a 6-digit value alone would be
    /// trivially reversible from a stolen database row.</summary>
    private static string HashCode(Guid userId, string code) => TokenHasher.Hash($"{userId:N}:{code}");

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
        var memberships = await db.ActiveMemberships()
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

    /// <summary>Self-service account deletion (KVKK §16). Re-confirms the password (destructive action),
    /// then anonymizes the account — masks personal fields, clears the password, deactivates — keeping
    /// ticket history and the audit chain intact (a hard delete would break both). All sessions are
    /// revoked. A super admin cannot self-delete (would orphan the system) — same rule as KVKK anonymize.</summary>
    public async Task DeleteOwnAccountAsync(Guid userId, DeleteAccountRequest request, CancellationToken ct = default)
    {
        var user = await db.Users.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.Id == userId, ct)
            ?? throw new NotFoundException("user.not_found", "User not found.");

        if (user.IsSuperAdmin)
            throw new ConflictException("auth.superadmin_delete", "A super admin account cannot be self-deleted.");
        if (user.PasswordHash is null || !passwordHasher.Verify(user, user.PasswordHash, request.Password))
            throw new UnauthorizedException("auth.invalid_credentials", "Password is incorrect.");

        user.Anonymize();
        await RevokeAllAsync(userId, clock.UtcNow, ct);
        await db.SaveChangesAsync(ct);
    }

    private async Task<AuthResult> IssueAsync(User user, CancellationToken ct, RefreshToken? rotatedFrom = null)
    {
        var now = clock.UtcNow;
        // The company_id claims the tenant filter trusts for the whole session — revoked memberships
        // must never reach them (see MembershipQueries).
        var memberships = await db.ActiveMemberships()
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
