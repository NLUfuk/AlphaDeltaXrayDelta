using CrmKanban.Application.Abstractions;
using CrmKanban.Application.Common;
using CrmKanban.Domain.Authorization;
using CrmKanban.Domain.Entities;
using CrmKanban.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CrmKanban.Application.Tickets;

public enum TicketActorKind { SuperAdmin, Staff, Customer }

/// <summary>The caller's relationship to one ticket, resolved once and reused across an operation.</summary>
public sealed record TicketActor(Guid UserId, TicketActorKind Kind, RoleType? Role, IReadOnlySet<string> Permissions)
{
    public bool IsStaff => Kind is TicketActorKind.SuperAdmin or TicketActorKind.Staff;
    public bool Has(string permission) => Kind == TicketActorKind.SuperAdmin || Permissions.Contains(permission);
}

/// <summary>
/// Ticket-level authorization (spec §7, §8) — the "who may do what to THIS ticket" checks, separate
/// from the ticket command/query logic (fat-service split, spec §4.1). Tenant scope is already
/// guaranteed by the DbContext filter; this adds the per-op permission + role rules. Customers are not
/// company members, so they reach a ticket only as its opener.
/// </summary>
public sealed class TicketAuthorizationService(
    ICurrentUserService currentUser,
    IPermissionService permissions,
    IAppDbContext db)
{
    public async Task<TicketActor> ResolveAsync(Guid ticketCompanyId, Guid ticketOpenedById, CancellationToken ct = default)
    {
        var userId = currentUser.UserId
            ?? throw new UnauthorizedException("auth.required", "Authentication required.");

        if (currentUser.IsSuperAdmin)
            return new TicketActor(userId, TicketActorKind.SuperAdmin, null, PermissionKeys.All.ToHashSet(StringComparer.Ordinal));

        var membership = await db.ActiveMemberships()
            .FirstOrDefaultAsync(m => m.UserId == userId && m.CompanyId == ticketCompanyId, ct);
        if (membership is not null)
        {
            var perms = await permissions.GetPermissionsAsync(userId, ticketCompanyId, ct);
            return new TicketActor(userId, TicketActorKind.Staff, membership.Role, perms);
        }

        if (ticketOpenedById == userId)
            return new TicketActor(userId, TicketActorKind.Customer, RoleType.Customer, new HashSet<string>());

        // Not a member and not the opener → no relationship to this ticket.
        throw new ForbiddenException("ticket.forbidden", "You do not have access to this ticket.");
    }

    public static void EnsurePermission(TicketActor actor, string permissionKey)
    {
        if (!actor.Has(permissionKey))
            throw new ForbiddenException("ticket.permission_denied", $"You lack the '{permissionKey}' permission.");
    }

    /// <summary>Internal notes are staff-only and require comment.internal; customers can never write them.</summary>
    public static void EnsureCanComment(TicketActor actor, bool isInternal)
    {
        if (isInternal)
        {
            if (!actor.IsStaff)
                throw new ForbiddenException("comment.internal_forbidden", "Only staff can write internal notes.");
            EnsurePermission(actor, PermissionKeys.CommentInternal);
        }
        // Public comments: staff always; customer only as the opener (already ensured by resolution).
    }

    /// <summary>Status change: staff need ticket.status.change; a Personel may change status only on a
    /// ticket assigned to them (spec §8). Customers go through the state machine's customer branch.</summary>
    public static void EnsureCanChangeStatus(TicketActor actor, Ticket ticket)
    {
        if (actor.Kind == TicketActorKind.Customer)
            return; // the state machine enforces cancel/complete-only for customers

        EnsurePermission(actor, PermissionKeys.TicketStatusChange);
        if (actor.Role == RoleType.Personel && ticket.AssignedToId != actor.UserId)
            throw new ForbiddenException("ticket.status.not_assignee", "A staff member can only change the status of a ticket assigned to them.");
    }
}
