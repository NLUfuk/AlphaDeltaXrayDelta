using CrmKanban.Domain.Common;

namespace CrmKanban.Domain.Entities;

/// <summary>Audit trail for permission/settings changes (spec §11). Append-only.</summary>
public sealed class AuditLog : Entity
{
    private AuditLog() { } // EF

    public AuditLog(Guid actorId, string action, string? detail)
    {
        ActorId = actorId;
        Action = action;
        Detail = detail;
    }

    public Guid ActorId { get; private set; }
    public string Action { get; private set; } = null!;
    public string? Detail { get; private set; }
}
