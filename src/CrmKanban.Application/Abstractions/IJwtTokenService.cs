using CrmKanban.Domain.Entities;

namespace CrmKanban.Application.Abstractions;

/// <summary>Identity claims baked into an access token (spec §9). Company-scoped permissions are NOT
/// here — they are resolved per request against the record's company.</summary>
public sealed record AccessTokenClaims(User User, bool IsSuperAdmin, IReadOnlyCollection<Guid> CompanyIds);

public sealed record IssuedToken(string Token, DateTime ExpiresAt);

/// <summary>Creates signed JWT access tokens and opaque refresh tokens (spec §3, §9).</summary>
public interface IJwtTokenService
{
    IssuedToken CreateAccessToken(AccessTokenClaims claims);

    /// <summary>A cryptographically random opaque refresh token (the raw value returned to the client).</summary>
    string CreateRefreshTokenValue();

    /// <summary>Hash used to store a refresh token at rest (never store the raw value) — spec §9.</summary>
    string HashRefreshToken(string rawValue);
}
