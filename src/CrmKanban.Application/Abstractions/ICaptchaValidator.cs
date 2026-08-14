namespace CrmKanban.Application.Abstractions;

/// <summary>
/// Bot/spam gate for the anonymous public form (spec §10). On/off is a setting (spec §13); the
/// concrete provider (Turnstile/reCAPTCHA/…) is a single implementation swapped by config. Fails
/// CLOSED: if verification is enabled but not answerable, the submission is rejected.
/// </summary>
public interface ICaptchaValidator
{
    /// <summary>The provider's PUBLIC site key when the gate is on, else null. The anonymous form
    /// config endpoint hands it to the SPA so the widget is keyed at runtime — rotating the key is a
    /// server config change, not a frontend rebuild. Never the secret key.</summary>
    string? SiteKey { get; }

    Task<bool> ValidateAsync(string? token, CancellationToken ct = default);
}
