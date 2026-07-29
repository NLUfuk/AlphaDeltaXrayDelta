namespace CrmKanban.Application.Abstractions;

/// <summary>
/// Bot/spam gate for the anonymous public form (spec §10). On/off is a setting (spec §13); the
/// concrete provider (Turnstile/reCAPTCHA/…) is a single implementation swapped by config. Fails
/// CLOSED: if verification is enabled but not answerable, the submission is rejected.
/// </summary>
public interface ICaptchaValidator
{
    Task<bool> ValidateAsync(string? token, CancellationToken ct = default);
}
