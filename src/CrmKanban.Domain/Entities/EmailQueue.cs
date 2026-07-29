using CrmKanban.Domain.Common;
using CrmKanban.Domain.Enums;

namespace CrmKanban.Domain.Entities;

/// <summary>
/// A queued outbound email (spec §11, §14). Mail is NEVER sent in the request loop: the notification
/// pipeline enqueues rows, a background worker sends them with retry and a dead-letter after too many
/// attempts. Payload is JSON that fills the template's {{placeholders}}.
/// </summary>
public sealed class EmailQueue : Entity
{
    private EmailQueue() { } // EF

    public EmailQueue(string toEmail, string templateKey, string payloadJson)
    {
        ToEmail = toEmail;
        TemplateKey = templateKey;
        Payload = payloadJson;
        Status = EmailStatus.Pending;
    }

    public string ToEmail { get; private set; } = null!;
    public string TemplateKey { get; private set; } = null!;
    public string Payload { get; private set; } = null!;
    public EmailStatus Status { get; private set; }
    public int Attempts { get; private set; }
    public DateTime? SentAt { get; private set; }
    public string? LastError { get; private set; }

    public void MarkSent(DateTime now)
    {
        Status = EmailStatus.Sent;
        SentAt = now;
        LastError = null;
    }

    /// <summary>Record a send failure; dead-letter once attempts reach <paramref name="maxAttempts"/>.</summary>
    public void MarkFailed(string error, int maxAttempts)
    {
        Attempts++;
        LastError = error;
        Status = Attempts >= maxAttempts ? EmailStatus.DeadLetter : EmailStatus.Failed;
    }
}
