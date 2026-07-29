namespace CrmKanban.Infrastructure.Identity;

/// <summary>JWT settings. SigningKey is a SECRET — env/user-secrets only, never appsettings (spec §13).</summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; init; } = "CrmKanban";
    public string Audience { get; init; } = "CrmKanban";
    public int AccessTokenMinutes { get; init; } = 15;
    public int RefreshTokenDays { get; init; } = 14;
    public string SigningKey { get; init; } = "";
}
