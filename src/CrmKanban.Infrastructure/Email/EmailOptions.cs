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
    public string? Host { get; init; }
    public int Port { get; init; } = 587;
    public bool UseSsl { get; init; } = true;
    public string? Username { get; init; }
    public string? Password { get; init; }
}
