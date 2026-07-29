namespace CrmKanban.Application.Auth;

/// <summary>Non-secret auth lifetimes (spec §3, §9). Registered from config in Infrastructure DI.</summary>
public sealed class AuthOptions
{
    public int RefreshTokenDays { get; init; } = 14;
    public int InviteTokenDays { get; init; } = 7;
}
