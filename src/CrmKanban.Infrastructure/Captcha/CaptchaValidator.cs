using CrmKanban.Application.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CrmKanban.Infrastructure.Captcha;

/// <summary>
/// <see cref="ICaptchaValidator"/> (spec §10/§13). Disabled → pass (dev). Enabled → verify with the
/// configured provider; no provider is wired in v1, so an enabled-but-unconfigured gate FAILS CLOSED
/// rather than silently letting bots through. Wiring a provider (Turnstile/reCAPTCHA) is one branch here.
/// </summary>
public sealed class CaptchaValidator(IOptions<CaptchaOptions> options, ILogger<CaptchaValidator> logger) : ICaptchaValidator
{
    private readonly CaptchaOptions _opt = options.Value;

    public Task<bool> ValidateAsync(string? token, CancellationToken ct = default)
    {
        if (!_opt.Enabled)
            return Task.FromResult(true);

        // ponytail: no provider integration shipped in v1. Add a `switch (_opt.Provider)` branch that
        // POSTs the token to the provider's verify endpoint when a provider is chosen (spec §18.24).
        logger.LogWarning("CAPTCHA is enabled but no provider is wired; rejecting submission (fail-closed).");
        return Task.FromResult(false);
    }
}
