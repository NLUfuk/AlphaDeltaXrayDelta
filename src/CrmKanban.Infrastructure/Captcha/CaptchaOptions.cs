namespace CrmKanban.Infrastructure.Captcha;

/// <summary>
/// CAPTCHA gate config (spec §10/§13). On/off plus the concrete integration; infra + secret, so it
/// lives in config/env, never in the DB settings table (CLAUDE.md config split). Disabled in dev.
/// Enabled with no wired provider or no secret → the validator fails closed.
/// </summary>
public sealed class CaptchaOptions
{
    public const string SectionName = "Captcha";

    public bool Enabled { get; init; }
    public string? Provider { get; init; }   // "turnstile" — the only wired provider
    public string? SiteKey { get; init; }    // public, served to the browser
    public string? SecretKey { get; init; }  // secret, server-side only
    public string? VerifyUrl { get; init; }  // override for tests; defaults to Cloudflare's endpoint
}
