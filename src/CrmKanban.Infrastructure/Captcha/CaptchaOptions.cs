namespace CrmKanban.Infrastructure.Captcha;

/// <summary>
/// CAPTCHA gate config (spec §10/§13). On/off is a business setting; Provider/SecretKey are the
/// concrete integration (env/user-secrets). Disabled in dev. When enabled with no provider wired,
/// the validator fails closed — you enable this only after wiring a provider.
/// </summary>
public sealed class CaptchaOptions
{
    public const string SectionName = "Captcha";

    public bool Enabled { get; init; }
    public string? Provider { get; init; }   // e.g. "turnstile", "recaptcha" — none wired in v1
    public string? SecretKey { get; init; }
    public string? VerifyUrl { get; init; }
}
