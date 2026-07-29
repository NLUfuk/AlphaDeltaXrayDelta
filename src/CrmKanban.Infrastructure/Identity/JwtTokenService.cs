using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using CrmKanban.Application.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace CrmKanban.Infrastructure.Identity;

/// <summary>
/// Issues short-lived signed JWT access tokens and opaque refresh tokens (spec §3, §9). Access
/// tokens carry identity + tenant scope (company_ids, is_super_admin) so the request pipeline can
/// build ICurrentUserService without a DB hit; permissions stay out (resolved per company).
/// </summary>
public sealed class JwtTokenService(IOptions<JwtOptions> options) : IJwtTokenService
{
    public const string CompanyClaim = "company_id";
    public const string SuperAdminClaim = "is_super_admin";

    private readonly JwtOptions _o = options.Value;

    public IssuedToken CreateAccessToken(AccessTokenClaims claims)
    {
        var now = DateTime.UtcNow;
        var expires = now.AddMinutes(_o.AccessTokenMinutes);

        var identity = new ClaimsIdentity();
        identity.AddClaim(new Claim(JwtRegisteredClaimNames.Sub, claims.User.Id.ToString()));
        identity.AddClaim(new Claim(JwtRegisteredClaimNames.Email, claims.User.Email));
        identity.AddClaim(new Claim("name", $"{claims.User.FirstName} {claims.User.LastName}".Trim()));
        identity.AddClaim(new Claim(SuperAdminClaim, claims.IsSuperAdmin ? "true" : "false"));
        foreach (var companyId in claims.CompanyIds)
            identity.AddClaim(new Claim(CompanyClaim, companyId.ToString()));

        var creds = new SigningCredentials(SigningKey(), SecurityAlgorithms.HmacSha256);
        var descriptor = new SecurityTokenDescriptor
        {
            Subject = identity,
            Issuer = _o.Issuer,
            Audience = _o.Audience,
            IssuedAt = now,
            NotBefore = now,
            Expires = expires,
            SigningCredentials = creds,
        };

        var token = new JsonWebTokenHandler().CreateToken(descriptor);
        return new IssuedToken(token, expires);
    }

    public string CreateRefreshTokenValue()
    {
        Span<byte> bytes = stackalloc byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Base64UrlEncoder.Encode(bytes.ToArray());
    }

    public string HashRefreshToken(string rawValue)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(rawValue));
        return Convert.ToHexStringLower(hash);
    }

    private SymmetricSecurityKey SigningKey()
    {
        if (string.IsNullOrWhiteSpace(_o.SigningKey) || Encoding.UTF8.GetByteCount(_o.SigningKey) < 32)
            throw new InvalidOperationException("Jwt:SigningKey is missing or too short (need >= 32 bytes). Set it via env/user-secrets.");
        return new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_o.SigningKey));
    }
}
