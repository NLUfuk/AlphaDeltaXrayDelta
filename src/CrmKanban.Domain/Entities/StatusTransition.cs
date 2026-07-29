using CrmKanban.Domain.Common;

namespace CrmKanban.Domain.Entities;

/// <summary>
/// A legal status edge for staff (spec §12). A move from→to is only allowed if a matching
/// transition exists; if <see cref="AllowedByPermissionKey"/> is set the actor must hold it.
/// Customer moves bypass this table and follow the dedicated customer rule (spec §12/§18.10).
/// </summary>
public sealed class StatusTransition : Entity
{
    private StatusTransition() { } // EF

    public StatusTransition(Guid fromStatusId, Guid toStatusId, string? allowedByPermissionKey)
    {
        FromStatusId = fromStatusId;
        ToStatusId = toStatusId;
        AllowedByPermissionKey = allowedByPermissionKey;
    }

    public Guid FromStatusId { get; private set; }
    public Guid ToStatusId { get; private set; }
    public string? AllowedByPermissionKey { get; private set; }
}
