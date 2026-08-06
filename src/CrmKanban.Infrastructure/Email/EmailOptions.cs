namespace CrmKanban.Infrastructure.Email;

/// <summary>
/// Email transport config (spec §14). Secrets/infra — env/user-secrets, never DB/UI (config split,
/// spec §13). Provider selects the sender: "log" (dev, writes to the log) or "smtp" (prod). SMTP
/// host/credentials are supplied when a provider is chosen (spec §18.24).
/// </summary>
public sealed class EmailOptions
{
    public const string SectionName = "Email";

    public string Provider { get; init; } = "log"; // "log" | "smtp"
    public string From { get; init; } = "no-reply@crm-kanban.local";

    /// <summary>Display name shown instead of the raw address ("Anadolu Tekstil Destek"). Optional —
    /// relays (Brevo, Resend) send the bare address when it is empty.</summary>
    public string? FromName { get; init; }

    /// <summary>
    /// Reply-To header. Set this whenever <see cref="From"/> is a freemail address (gmail, outlook,
    /// yahoo) that the relay is not authorized to sign for. Brevo rewrites such a From to its own
    /// <c>&lt;id&gt;.brevosend.com</c> domain and, if no Reply-To was supplied, injects the original
    /// freemail address as one — which trips SpamAssassin FREEMAIL_FORGED_REPLYTO (+2.5) and
    /// FREEMAIL_REPLYTO_END_DIGIT (+0.25) on every message. Supplying our own Reply-To suppresses
    /// that injection. Measured on mail-tester.com against the live relay: 4.9/10 without, 7.8/10
    /// with (SpamAssassin 4.1 → 1.2 against a 5.0 threshold). Use a non-freemail address; the
    /// templates already tell recipients not to reply. Empty = send no Reply-To at all.
    /// </summary>
    public string? ReplyTo { get; init; }

    public string? Host { get; init; }
    public int Port { get; init; } = 587;
    public bool UseSsl { get; init; } = true;
    public string? Username { get; init; }
    public string? Password { get; init; }
}
