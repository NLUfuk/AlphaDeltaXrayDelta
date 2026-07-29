using CrmKanban.Domain.Common;
using CrmKanban.Domain.Entities;
using CrmKanban.Domain.Enums;
using CrmKanban.Domain.Tickets;
using FluentAssertions;

namespace CrmKanban.Domain.Tests.Tickets;

public class TicketBehaviorTests
{
    private static readonly DateTime Now = new(2026, 7, 29, 10, 0, 0, DateTimeKind.Utc);

    private static TicketStatus Status(string name, StatusCategory category, bool terminal = false) =>
        new(name, category, "#000", 0, terminal);

    private static Ticket NewTicket(TicketStatus initial) =>
        new(Guid.NewGuid(), "ACME-1", Guid.NewGuid(), initial.Id, "Title", "Body");

    private static StatusChangeActor Staff() => StatusChangeActor.Staff(RoleType.Personel, ["ticket.status.change"]);

    [Fact]
    public void First_move_to_answered_stamps_first_response_once()
    {
        var open = Status("New", StatusCategory.Open);
        var answered = Status("Answered", StatusCategory.Answered);
        var pending = Status("Pending", StatusCategory.Pending);
        var answered2 = Status("Answered again", StatusCategory.Answered);
        var ticket = NewTicket(open);
        var edges = new[]
        {
            new StatusTransition(open.Id, answered.Id, "ticket.status.change"),
            new StatusTransition(answered.Id, pending.Id, "ticket.status.change"),
            new StatusTransition(pending.Id, answered2.Id, "ticket.status.change"),
        };

        ticket.ChangeStatus(open, answered, edges, Staff(), Now);
        var first = ticket.FirstResponseAt;

        ticket.ChangeStatus(answered, pending, edges, Staff(), Now.AddHours(1));
        ticket.ChangeStatus(pending, answered2, edges, Staff(), Now.AddHours(2));

        first.Should().Be(Now);
        ticket.FirstResponseAt.Should().Be(Now, "first response time is stamped once and never overwritten");
    }

    [Fact]
    public void Move_to_closed_stamps_resolved_and_closed()
    {
        var open = Status("New", StatusCategory.Open);
        var closed = Status("Closed", StatusCategory.Closed, terminal: true);
        var ticket = NewTicket(open);
        var edges = new[] { new StatusTransition(open.Id, closed.Id, "ticket.status.change") };

        ticket.ChangeStatus(open, closed, edges, Staff(), Now);

        ticket.ResolvedAt.Should().Be(Now);
        ticket.ClosedAt.Should().Be(Now);
    }

    [Fact]
    public void Stale_from_status_is_rejected()
    {
        var open = Status("New", StatusCategory.Open);
        var other = Status("Other", StatusCategory.Pending);
        var closed = Status("Closed", StatusCategory.Closed, terminal: true);
        var ticket = NewTicket(open); // current = open

        var act = () => ticket.ChangeStatus(other, closed, [], Staff(), Now);

        act.Should().Throw<DomainException>().Which.Code.Should().Be("ticket.status.stale");
    }

    [Fact]
    public void Reopen_within_window_clears_closed_and_keeps_resolved()
    {
        var open = Status("New", StatusCategory.Open);
        var closed = Status("Closed", StatusCategory.Closed, terminal: true);
        var reopened = Status("Reopened", StatusCategory.Open);
        var ticket = NewTicket(open);
        var edges = new[] { new StatusTransition(open.Id, closed.Id, "ticket.status.change") };
        ticket.ChangeStatus(open, closed, edges, Staff(), Now);

        ticket.Reopen(closed, reopened, Now.AddDays(3), reopenWindowDays: 7);

        ticket.StatusId.Should().Be(reopened.Id);
        ticket.ClosedAt.Should().BeNull();
        ticket.ResolvedAt.Should().Be(Now, "the original resolution time is preserved for reporting");
    }

    [Fact]
    public void Reopen_after_window_is_rejected()
    {
        var open = Status("New", StatusCategory.Open);
        var closed = Status("Closed", StatusCategory.Closed, terminal: true);
        var reopened = Status("Reopened", StatusCategory.Open);
        var ticket = NewTicket(open);
        var edges = new[] { new StatusTransition(open.Id, closed.Id, "ticket.status.change") };
        ticket.ChangeStatus(open, closed, edges, Staff(), Now);

        var act = () => ticket.Reopen(closed, reopened, Now.AddDays(8), reopenWindowDays: 7);

        act.Should().Throw<DomainException>().Which.Code.Should().Be("ticket.reopen.window_expired");
    }

    [Fact]
    public void Reopen_into_terminal_status_is_rejected()
    {
        var open = Status("New", StatusCategory.Open);
        var closed = Status("Closed", StatusCategory.Closed, terminal: true);
        var alsoTerminal = Status("Cancelled", StatusCategory.Cancelled, terminal: true);
        var ticket = NewTicket(open);
        var edges = new[] { new StatusTransition(open.Id, closed.Id, "ticket.status.change") };
        ticket.ChangeStatus(open, closed, edges, Staff(), Now);

        var act = () => ticket.Reopen(closed, alsoTerminal, Now.AddDays(1), reopenWindowDays: 7);

        act.Should().Throw<DomainException>().Which.Code.Should().Be("ticket.reopen.target_terminal");
    }

    [Fact]
    public void Reopen_non_closed_ticket_is_rejected()
    {
        var open = Status("New", StatusCategory.Open);
        var pending = Status("Pending", StatusCategory.Pending);
        var ticket = NewTicket(open);

        var act = () => ticket.Reopen(open, pending, Now, reopenWindowDays: 7);

        act.Should().Throw<DomainException>().Which.Code.Should().Be("ticket.reopen.not_closed");
    }
}
