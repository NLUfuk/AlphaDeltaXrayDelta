using CrmKanban.Application.Common;
using CrmKanban.Application.Tickets;
using CrmKanban.Domain.Authorization;
using CrmKanban.Domain.Entities;
using CrmKanban.Domain.Enums;
using FluentAssertions;

namespace CrmKanban.Application.Tests.Tickets;

/// <summary>
/// The per-ticket authorization rules (spec §8): a Personel may change status only on a ticket
/// assigned to them; internal notes are staff-only and require comment.internal; a customer can
/// never write one. Pure decisions, so tested directly. Core, test-first (spec §17).
/// </summary>
public class TicketAuthorizationTests
{
    private static readonly Guid StaffId = Guid.NewGuid();

    private static TicketActor Staff(RoleType role, params string[] perms) =>
        new(StaffId, TicketActorKind.Staff, role, perms.ToHashSet(StringComparer.Ordinal));

    private static TicketActor Customer() =>
        new(Guid.NewGuid(), TicketActorKind.Customer, RoleType.Customer, new HashSet<string>());

    private static Ticket TicketAssignedTo(Guid? assignee)
    {
        var t = new Ticket(Guid.NewGuid(), "T-1", Guid.NewGuid(), Guid.NewGuid(), "t", "b");
        if (assignee is { } a) t.Assign(a);
        return t;
    }

    [Fact]
    public void Personel_cannot_change_status_of_a_ticket_not_assigned_to_them()
    {
        var actor = Staff(RoleType.Personel, PermissionKeys.TicketStatusChange);
        var ticket = TicketAssignedTo(Guid.NewGuid()); // assigned to someone else

        var act = () => TicketAuthorizationService.EnsureCanChangeStatus(actor, ticket);

        act.Should().Throw<ForbiddenException>().Which.Code.Should().Be("ticket.status.not_assignee");
    }

    [Fact]
    public void Personel_can_change_status_of_their_assigned_ticket()
    {
        var actor = Staff(RoleType.Personel, PermissionKeys.TicketStatusChange);
        var ticket = TicketAssignedTo(StaffId);

        var act = () => TicketAuthorizationService.EnsureCanChangeStatus(actor, ticket);

        act.Should().NotThrow();
    }

    [Fact]
    public void Staff_without_status_change_permission_is_denied()
    {
        var actor = Staff(RoleType.Admin); // no permissions
        var ticket = TicketAssignedTo(StaffId);

        var act = () => TicketAuthorizationService.EnsureCanChangeStatus(actor, ticket);

        act.Should().Throw<ForbiddenException>();
    }

    [Fact]
    public void Customer_status_change_defers_to_the_state_machine()
    {
        var act = () => TicketAuthorizationService.EnsureCanChangeStatus(Customer(), TicketAssignedTo(null));
        act.Should().NotThrow(); // the domain state machine enforces cancel/complete-only
    }

    [Fact]
    public void Customer_cannot_write_an_internal_note()
    {
        var act = () => TicketAuthorizationService.EnsureCanComment(Customer(), isInternal: true);
        act.Should().Throw<ForbiddenException>().Which.Code.Should().Be("comment.internal_forbidden");
    }

    [Fact]
    public void Staff_needs_comment_internal_permission_for_an_internal_note()
    {
        var without = () => TicketAuthorizationService.EnsureCanComment(Staff(RoleType.Personel), isInternal: true);
        without.Should().Throw<ForbiddenException>();

        var with = () => TicketAuthorizationService.EnsureCanComment(Staff(RoleType.Personel, PermissionKeys.CommentInternal), isInternal: true);
        with.Should().NotThrow();
    }

    [Fact]
    public void Customer_may_write_a_public_comment()
    {
        var act = () => TicketAuthorizationService.EnsureCanComment(Customer(), isInternal: false);
        act.Should().NotThrow();
    }
}
