using System.Net;
using System.Net.Mail;
using CrmKanban.Application.Abstractions;
using Microsoft.Extensions.Options;

namespace CrmKanban.Infrastructure.Email;

/// <summary>
/// SMTP <see cref="IEmailSender"/> over the built-in <see cref="SmtpClient"/> (no extra dependency).
/// Selected when Email:Provider is "smtp". A send exception propagates so the worker retries and
/// eventually dead-letters (spec §14).
/// </summary>
public sealed class SmtpEmailSender(IOptions<EmailOptions> options) : IEmailSender
{
    private readonly EmailOptions _opt = options.Value;

    public async Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken ct = default)
    {
        using var client = new SmtpClient(_opt.Host, _opt.Port) { EnableSsl = _opt.UseSsl };
        if (!string.IsNullOrEmpty(_opt.Username))
            client.Credentials = new NetworkCredential(_opt.Username, _opt.Password);

        using var message = new MailMessage(_opt.From, toEmail, subject, htmlBody) { IsBodyHtml = true };
        await client.SendMailAsync(message, ct);
    }
}
