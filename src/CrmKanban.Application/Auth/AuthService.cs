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
    IOptions<AuthOptions> authOptions)
{
    private readonly AuthOptions _auth = authOptions.Value;

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
