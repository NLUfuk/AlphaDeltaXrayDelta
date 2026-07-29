using CrmKanban.Domain.Common;

namespace CrmKanban.Domain.Entities;

/// <summary>
/// A named email template (spec §11, §14). Body/Subject carry {{placeholder}} tokens filled from the
/// queued message payload. Super-admin editable (Faz 6); seeded with v1 defaults. Keyed by a stable
/// Key so the notification pipeline references templates by meaning, not by display text.
/// </summary>
public sealed class EmailTemplate : Entity
{
    private EmailTemplate() { } // EF

    public EmailTemplate(string key, string subject, string body, bool isActive = true, Guid? id = null)
    {
        if (id is { } fixedId) Id = fixedId; // deterministic Id for idempotent seeding
        Key = key;
        Subject = subject;
        Body = body;
        IsActive = isActive;
    }

    public string Key { get; private set; } = null!;
    public string Subject { get; private set; } = null!;
    public string Body { get; private set; } = null!;
    public bool IsActive { get; private set; }

    public void Update(string subject, string body)
    {
        Subject = subject;
        Body = body;
    }
}
