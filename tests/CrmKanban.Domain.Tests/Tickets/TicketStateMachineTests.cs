using CrmKanban.Domain.Common;
using CrmKanban.Domain.Entities;
using CrmKanban.Domain.Enums;
using CrmKanban.Domain.Tickets;
using FluentAssertions;

namespace CrmKanban.Domain.Tests.Tickets;

public class TicketStateMachineTests
{
    private static TicketStatus Status(string name, StatusCategory category, bool terminal = false) =>
        new(name, category, "#000", order: 0, isTerminal: terminal);

    private static StatusTransition Edge(TicketStatus from, TicketStatus to, string? perm = null) =>
        new(from.Id, to.Id, perm);

    private static StatusChangeActor Staff(params string[] perms) =>
        StatusChangeActor.Staff(RoleType.Personel, perms);

    // ---- staff path ----

    [Fact]
    public void Staff_valid_edge_without_permission_requirement_is_allowed()
    {
        var open = Status("New", StatusCategory.Open);
        var pending = Status("In Progress", StatusCategory.Pending);
        var act = () => TicketStateMachine.EnsureCanTransition(open, pending, [Edge(open, pending)], Staff());
        act.Should().NotThrow();
    }

    [Fact]
    public void Staff_undefined_edge_is_rejected()
    {
        var open = Status("New", StatusCategory.Open);
        var closed = Status("Closed", StatusCategory.Closed, terminal: true);
        var act = () => TicketStateMachine.EnsureCanTransition(open, closed, [], Staff());
        act.Should().Throw<DomainException>().Which.Code.Should().Be("ticket.status.transition_invalid");
    }

    [Fact]
    public void Staff_missing_required_permission_is_rejected()
    {
        var open = Status("New", StatusCategory.Open);
        var closed = Status("Closed", StatusCategory.Closed, terminal: true);
        var edge = Edge(open, closed, "ticket.status.change");
        var act = () => TicketStateMachine.EnsureCanTransition(open, closed, [edge], Staff(/* no perms */));
        act.Should().Throw<DomainException>().Which.Code.Should().Be("ticket.status.forbidden");
    }

    [Fact]
    public void Staff_with_required_permission_is_allowed()
    {
        var open = Status("New", StatusCategory.Open);
        var closed = Status("Closed", StatusCategory.Closed, terminal: true);
        var edge = Edge(open, closed, "ticket.status.change");
        var act = () => TicketStateMachine.EnsureCanTransition(open, closed, [edge], Staff("ticket.status.change"));
        act.Should().NotThrow();
    }

    [Fact]
    public void Same_status_is_rejected()
    {
        var open = Status("New", StatusCategory.Open);
        var act = () => TicketStateMachine.EnsureCanTransition(open, open, [Edge(open, open)], Staff());
        act.Should().Throw<DomainException>().Which.Code.Should().Be("ticket.status.unchanged");
    }

    // ---- customer path (spec §12/§18.10) ----

    [Fact]
    public void Customer_can_cancel_a_non_terminal_ticket()
    {
        var open = Status("New", StatusCategory.Open);
        var cancelled = Status("Cancelled", StatusCategory.Cancelled, terminal: true);
        var act = () => TicketStateMachine.EnsureCanTransition(open, cancelled, [], StatusChangeActor.Customer());
        act.Should().NotThrow();
    }

    [Fact]
    public void Customer_can_complete_a_non_terminal_ticket()
    {
        var open = Status("New", StatusCategory.Open);
        var done = Status("Done", StatusCategory.Closed, terminal: true);
        var act = () => TicketStateMachine.EnsureCanTransition(open, done, [], StatusChangeActor.Customer());
        act.Should().NotThrow();
    }

    [Fact]
    public void Customer_cannot_move_to_a_non_terminal_status()
    {
        var open = Status("New", StatusCategory.Open);
        var pending = Status("In Progress", StatusCategory.Pending);
        var act = () => TicketStateMachine.EnsureCanTransition(open, pending, [], StatusChangeActor.Customer());
        act.Should().Throw<DomainException>().Which.Code.Should().Be("ticket.status.customer_forbidden");
    }

    [Fact]
    public void Customer_cannot_touch_a_ticket_already_terminal()
    {
        // Admin closed it; customer may not reopen/cancel — spec §18.10.
        var closed = Status("Closed", StatusCategory.Closed, terminal: true);
        var cancelled = Status("Cancelled", StatusCategory.Cancelled, terminal: true);
        var act = () => TicketStateMachine.EnsureCanTransition(closed, cancelled, [], StatusChangeActor.Customer());
        act.Should().Throw<DomainException>().Which.Code.Should().Be("ticket.status.terminal");
    }
}
