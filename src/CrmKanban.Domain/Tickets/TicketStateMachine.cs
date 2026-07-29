using CrmKanban.Domain.Common;
using CrmKanban.Domain.Entities;
using CrmKanban.Domain.Enums;

namespace CrmKanban.Domain.Tickets;

/// <summary>
/// The ticket status transition rules (spec §12) — one of the tested cores. Pure and clock-free:
/// given the current status, the target, the legal edges, and the actor, it either returns or
/// throws a <see cref="DomainException"/> with a stable code. Kanban card drag = status change,
/// so it routes through here too (spec §12).
/// </summary>
public static class TicketStateMachine
{
    public static void EnsureCanTransition(
        TicketStatus from,
        TicketStatus to,
        IReadOnlyCollection<StatusTransition> transitions,
        in StatusChangeActor actor)
    {
        ArgumentNullException.ThrowIfNull(from);
        ArgumentNullException.ThrowIfNull(to);
        ArgumentNullException.ThrowIfNull(transitions);

        if (from.Id == to.Id)
            throw new DomainException("ticket.status.unchanged", "Ticket is already in this status.");

        if (actor.Role == RoleType.Customer)
        {
            // Customer: only Cancel/Complete, and only while the ticket is not yet terminal
            // (an admin-closed ticket is out of the customer's hands) — spec §12/§18.10.
            if (from.IsTerminal)
                throw new DomainException("ticket.status.terminal", "The ticket is closed and cannot be changed by the customer.");
            if (to.Category is not (StatusCategory.Closed or StatusCategory.Cancelled))
                throw new DomainException("ticket.status.customer_forbidden", "A customer may only cancel or complete a ticket.");
            return;
        }

        // Staff (SuperAdmin/Admin/Personel): the edge must exist and the actor must hold its permission.
        var edge = transitions.FirstOrDefault(t => t.FromStatusId == from.Id && t.ToStatusId == to.Id);
        if (edge is null)
            throw new DomainException("ticket.status.transition_invalid", "This status transition is not allowed.");
        if (edge.AllowedByPermissionKey is not null && !actor.Permissions.Contains(edge.AllowedByPermissionKey))
            throw new DomainException("ticket.status.forbidden", "You lack permission for this status transition.");
    }
}
