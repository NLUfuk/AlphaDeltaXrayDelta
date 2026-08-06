using System.Net;
using System.Net.Mail;
using System.Text;
using System.Text.RegularExpressions;
using CrmKanban.Application.Abstractions;
using Microsoft.Extensions.Options;

namespace CrmKanban.Infrastructure.Email;

/// <summary>
/// SMTP <see cref="IEmailSender"/> over the built-in <see cref="SmtpClient"/> (no extra dependency).
/// Selected when Email:Provider is "smtp". A send exception propagates so the worker retries and
/// eventually dead-letters (spec §14).
/// </summary>
/// <remarks>
/// Sends multipart/alternative (text + HTML), not HTML alone. Measured on mail-tester.com against the
/// live Brevo relay: an HTML-only body scores SpamAssassin MIME_HTML_ONLY (+0.1) and, once the relay
/// injects its open-tracking pixel, HTML_IMAGE_ONLY_16 (+1.0) — over a third of the way to the 5.0
/// spam threshold before the content says anything. Recipients on the operator's own domain still see
/// the mail; everyone else's provider files it under spam, which reads exactly like "mail only reaches
/// my own address". The plain-text part is derived from the HTML so templates stay single-source.
/// </remarks>
public sealed partial class SmtpEmailSender(IOptions<EmailOptions> options) : IEmailSender
{
    private readonly EmailOptions _opt = options.Value;

    public async Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken ct = default)
    {
        using var client = new SmtpClient(_opt.Host, _opt.Port) { EnableSsl = _opt.UseSsl };
        if (!string.IsNullOrEmpty(_opt.Username))
            client.Credentials = new NetworkCredential(_opt.Username, _opt.Password);

        var from = string.IsNullOrWhiteSpace(_opt.FromName)
            ? new MailAddress(_opt.From)
            : new MailAddress(_opt.From, _opt.FromName);
        using var message = new MailMessage { From = from, Subject = subject };
        message.To.Add(new MailAddress(toEmail));

        // Claiming Reply-To ourselves stops the relay from inventing one — see EmailOptions.ReplyTo.
        if (!string.IsNullOrWhiteSpace(_opt.ReplyTo))
            message.ReplyToList.Add(string.IsNullOrWhiteSpace(_opt.FromName)
                ? new MailAddress(_opt.ReplyTo)
                : new MailAddress(_opt.ReplyTo, _opt.FromName));

        // Order matters: least-preferred part first, so clients that render HTML pick the HTML.
        message.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(
            HtmlToPlainText(htmlBody), Encoding.UTF8, "text/plain"));
        message.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(
            htmlBody, Encoding.UTF8, "text/html"));

        await client.SendMailAsync(message, ct);
    }

    /// <summary>
    /// Flattens the template's HTML into a readable text/plain part. The templates are a fixed, known
    /// set of simple markup (paragraphs, bold, one anchor button) seeded by this app, not arbitrary
    /// input — so tag stripping is enough and an HTML parser dependency is not.
    /// ponytail: regex over trusted first-party templates; swap in AngleSharp only if templates ever
    /// become user-authored HTML.
    /// </summary>
    public static string HtmlToPlainText(string html)
    {
        if (string.IsNullOrWhiteSpace(html)) return string.Empty;

        var text = InvisibleElements().Replace(html, " ");
        // Keep a link's destination: a text-only reader cannot follow an <a href>'s markup.
        text = Anchors().Replace(text, m => $"{m.Groups[2].Value.Trim()} ({m.Groups[1].Value.Trim()})");
        text = LineBreaks().Replace(text, "\n");
        text = ParagraphEnds().Replace(text, "\n\n");
        text = Tags().Replace(text, string.Empty);
        text = WebUtility.HtmlDecode(text);
        text = HorizontalWhitespace().Replace(text, " ");
        text = SpaceAroundNewline().Replace(text, "\n");
        text = ExcessNewlines().Replace(text, "\n\n");
        return text.Trim();
    }

    [GeneratedRegex(@"<(script|style|head)\b[^>]*>.*?</\1>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex InvisibleElements();

    [GeneratedRegex(@"<a\b[^>]*href\s*=\s*[""']([^""']*)[""'][^>]*>(.*?)</a>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex Anchors();

    [GeneratedRegex(@"<br\s*/?>", RegexOptions.IgnoreCase)]
    private static partial Regex LineBreaks();

    [GeneratedRegex(@"</(p|div|tr|h[1-6]|li)\s*>", RegexOptions.IgnoreCase)]
    private static partial Regex ParagraphEnds();

    [GeneratedRegex("<[^>]+>", RegexOptions.Singleline)]
    private static partial Regex Tags();

    [GeneratedRegex(@"[^\S\n]+")]
    private static partial Regex HorizontalWhitespace();

    [GeneratedRegex(@"[^\S\n]*\n[^\S\n]*")]
    private static partial Regex SpaceAroundNewline();

    [GeneratedRegex(@"\n{3,}")]
    private static partial Regex ExcessNewlines();
}
