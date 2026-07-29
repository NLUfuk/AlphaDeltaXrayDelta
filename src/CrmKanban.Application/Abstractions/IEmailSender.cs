namespace CrmKanban.Application.Abstractions;

/// <summary>
/// Sends one email (spec §14). Only ever called by the background worker, never in the request loop.
/// One implementation logs (dev), another talks SMTP (prod) — selected by config, same pipeline.
/// </summary>
public interface IEmailSender
{
    Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken ct = default);
}
