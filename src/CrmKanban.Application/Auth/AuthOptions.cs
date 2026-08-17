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

    /// <summary>How long a just-rotated refresh token still answers, in seconds. The access token now
    /// lives only in the SPA's memory, so every tab asks for a refresh when it boots — two tabs opened
    /// together present the same cookie within milliseconds of each other and the second one would look
    /// like token theft. Inside this window a replaced token is treated as that race instead
    /// (see <see cref="AuthService.RefreshAsync"/>); outside it, reuse is still theft.</summary>
    public int RefreshRotationGraceSeconds { get; init; } = 30;
}
