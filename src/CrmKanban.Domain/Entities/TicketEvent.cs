using CrmKanban.Domain.Common;
using CrmKanban.Domain.Enums;

namespace CrmKanban.Domain.Entities;

/// <summary>
/// An append-only ticket activity record (spec §11): the single source for audit, reports (first
/// response / resolution times, §15), and notification triggers (§14). Business logic records events;
/// mail is derived from them later, never called inline.
/// </summary>
public sealed class TicketEvent : Entity
{
    private TicketEvent() { } // EF

    public TicketEvent(Guid companyId, Guid ticketId, Guid actorId, TicketEventType eventType, string? oldValue, string? newValue)
    {
        CompanyId = companyId;
        TicketId = ticketId;
        ActorId = actorId;
        EventType = eventType;
        OldValue = oldValue;
        NewValue = newValue;
    }

    public Guid CompanyId { get; private set; }
    public Guid TicketId { get; private set; }
    public Guid ActorId { get; private set; }
    public TicketEventType EventType { get; private set; }
    public string? OldValue { get; private set; }
    public string? NewValue { get; private set; }
}
