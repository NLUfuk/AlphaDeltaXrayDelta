using CrmKanban.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace CrmKanban.Infrastructure.Email;

/// <summary>
/// Dev/default <see cref="IEmailSender"/>: writes the email to the log instead of sending it, so the
/// whole notification pipeline is exercisable without an SMTP server (spec §18.24 — provider TBD).
/// </summary>
public sealed class DevLogEmailSender(ILogger<DevLogEmailSender> logger) : IEmailSender
{
    public Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken ct = default)
    {
        logger.LogInformation("[DEV EMAIL] To: {To} | Subject: {Subject}\n{Body}", toEmail, subject, htmlBody);
        return Task.CompletedTask;
    }
}
