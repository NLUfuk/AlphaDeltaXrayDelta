using CrmKanban.Domain.Common;
using CrmKanban.Domain.Enums;

namespace CrmKanban.Domain.Entities;

/// <summary>
/// A per-user opt-out for a non-critical notification type (spec §11, §14). Absence means the default
/// (enabled): the pipeline only skips a recipient with an explicit row where Enabled is false.
/// </summary>
public sealed class UserNotificationPref : Entity
{
    private UserNotificationPref() { } // EF

    public UserNotificationPref(Guid userId, TicketEventType eventType, bool enabled)
    {
        UserId = userId;
        EventType = eventType;
        Enabled = enabled;
    }

    public Guid UserId { get; private set; }
    public TicketEventType EventType { get; private set; }
    public bool Enabled { get; private set; }

    public void Set(bool enabled) => Enabled = enabled;
}
