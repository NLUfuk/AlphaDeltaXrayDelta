namespace CrmKanban.Application.Auth;

/// <summary>Non-secret auth lifetimes (spec §3, §9). Registered from config in Infrastructure DI.</summary>
public sealed class AuthOptions
{
    public int RefreshTokenDays { get; init; } = 14;
    public int InviteTokenDays { get; init; } = 7;

    /// <summary>Lifetime of the emailed 6-digit sign-in code, and how many wrong tries it survives
    /// before a new one must be requested (short code → short life + hard cap).</summary>
    public int VerificationCodeMinutes { get; init; } = 15;
    public int MaxCodeAttempts { get; init; } = 5;
}
