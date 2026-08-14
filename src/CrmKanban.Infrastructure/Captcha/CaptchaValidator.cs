using System.Net.Http.Json;
using System.Text.Json.Serialization;
using CrmKanban.Application.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CrmKanban.Infrastructure.Captcha;

/// <summary>
/// <see cref="ICaptchaValidator"/> (spec §10/§13), Cloudflare Turnstile. Disabled → pass (dev).
/// Enabled → the browser's token is verified server-side against Turnstile's siteverify endpoint;
/// anything that is not an explicit success (no token, wrong provider, missing secret, network
/// failure, non-200) REJECTS the submission. The gate is worthless if it opens when it breaks.
/// </summary>
public sealed class CaptchaValidator(HttpClient http, IOptions<CaptchaOptions> options, ILogger<CaptchaValidator> logger) : ICaptchaValidator
{
    private const string TurnstileVerifyUrl = "https://challenges.cloudflare.com/turnstile/v0/siteverify";
    private readonly CaptchaOptions _opt = options.Value;

    public string? SiteKey => _opt.Enabled ? _opt.SiteKey : null;

    public async Task<bool> ValidateAsync(string? token, CancellationToken ct = default)
    {
        if (!_opt.Enabled)
            return true;

        if (!string.Equals(_opt.Provider, "turnstile", StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(_opt.SecretKey))
        {
            logger.LogWarning("CAPTCHA is enabled but provider '{Provider}' is not wired (or has no secret); rejecting submission (fail-closed).", _opt.Provider);
            return false;
        }

        if (string.IsNullOrWhiteSpace(token))
            return false;

        try
        {
            using var content = new FormUrlEncodedContent(
            [
                new KeyValuePair<string, string>("secret", _opt.SecretKey),
                new KeyValuePair<string, string>("response", token),
            ]);
            using var response = await http.PostAsync(_opt.VerifyUrl ?? TurnstileVerifyUrl, content, ct);
            response.EnsureSuccessStatusCode();

            var body = await response.Content.ReadFromJsonAsync<TurnstileResponse>(ct);
            if (body?.Success != true)
                logger.LogWarning("Turnstile rejected a submission: {Codes}", string.Join(',', body?.ErrorCodes ?? []));
            return body?.Success == true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Turnstile verification failed; rejecting submission (fail-closed).");
            return false;
        }
    }

    private sealed record TurnstileResponse(
        [property: JsonPropertyName("success")] bool Success,
        [property: JsonPropertyName("error-codes")] string[]? ErrorCodes);
}
