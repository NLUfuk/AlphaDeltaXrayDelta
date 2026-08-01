using CrmKanban.Application.Abstractions;
using CrmKanban.Application.Common;
using CrmKanban.Domain.Authorization;
using CrmKanban.Domain.Entities;
using CrmKanban.Domain.Enums;
using CrmKanban.Domain.Tickets;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CrmKanban.Application.Tickets;

/// <summary>
/// Ticket write operations (spec §17.4). Orchestration only — each method does one job (spec §4.1):
/// authorize, mutate through the domain, record a TicketEvent, save. Status validity lives in the
/// domain state machine; permission/role rules in <see cref="TicketAuthorizationService"/>.
/// </summary>
public sealed class TicketCommandService(
    IAppDbContext db,
    TicketAuthorizationService authz,
    ICurrentUserService currentUser,
    IClock clock,
    IOptions<TicketOptions> options)
{
    private readonly TicketOptions _opt = options.Value;

    public async Task<Guid> CreateAsync(CreateTicketRequest request, CancellationToken ct = default)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedException("auth.required", "Authentication required.");
        var isMember = currentUser.IsSuperAdmin ||
            await db.Memberships.IgnoreQueryFilters().AnyAsync(m => m.UserId == userId && m.CompanyId == request.CompanyId, ct);
        if (!isMember)
            throw new ForbiddenException("ticket.create_forbidden", "You cannot create tickets in this company.");

        var company = await db.Companies.IgnoreQueryFilters().FirstOrDefaultAsync(c => c.Id == request.CompanyId, ct)
            ?? throw new NotFoundException("company.not_found", "Company not found.");
        if (company.IsArchived)
            throw new ConflictException("company.archived", "This company is archived.");

        var initialStatus = await InitialStatusAsync(request.CompanyId, ct);
        var ticket = new Ticket(request.CompanyId, company.AllocateTicketNumber(), userId,
            initialStatus.Id, request.Title, request.Body, request.Priority, request.CategoryId);

        db.Tickets.Add(ticket);
        db.TicketEvents.Add(new TicketEvent(request.CompanyId, ticket.Id, userId, TicketEventType.Created, null, ticket.Number));
        await db.SaveChangesAsync(ct);
        return ticket.Id;
    }

    /// <summary>A logged-in customer opens a request to a company they picked from the portal (spec §18.5).
    /// Any authenticated user may do this (they become that company's customer for the ticket) — unlike
    /// <see cref="CreateAsync"/>, no membership is required. The company must be active/not archived. A
    /// verified account is trusted, so the ticket enters the pool directly (no zero-trust hold).</summary>
    public async Task<Guid> CreateAsCustomerAsync(CustomerCreateTicketRequest request, CancellationToken ct = default)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedException("auth.required", "Authentication required.");
        var company = await db.Companies.IgnoreQueryFilters().FirstOrDefaultAsync(c => c.Id == request.CompanyId, ct)
            ?? throw new NotFoundException("company.not_found", "Company not found.");
        if (company.IsArchived || !company.IsActive)
            throw new ConflictException("company.form_closed", "This company is not accepting requests.");

        var initialStatus = await InitialStatusAsync(request.CompanyId, ct);
        var ticket = new Ticket(request.CompanyId, company.AllocateTicketNumber(), userId,
            initialStatus.Id, request.Title, request.Body);
        db.Tickets.Add(ticket);
        db.TicketEvents.Add(new TicketEvent(request.CompanyId, ticket.Id, userId, TicketEventType.Created, null, ticket.Number));
        await db.SaveChangesAsync(ct);
        return ticket.Id;
    }

    public async Task EditAsync(Guid ticketId, EditTicketRequest request, CancellationToken ct = default)
    {
        var ticket = await LoadAsync(ticketId, ct);
        var actor = await authz.ResolveAsync(ticket.CompanyId, ticket.OpenedById, ct);
        TicketAuthorizationService.EnsurePermission(actor, PermissionKeys.TicketEdit);

        ticket.Edit(request.Title, request.Body);
        db.TicketEvents.Add(Event(ticket, actor.UserId, TicketEventType.Edited, null, null));
        await db.SaveChangesAsync(ct);
    }

    public async Task AssignAsync(Guid ticketId, AssignTicketRequest request, CancellationToken ct = default)
    {
        var ticket = await LoadAsync(ticketId, ct);
        var actor = await authz.ResolveAsync(ticket.CompanyId, ticket.OpenedById, ct);
        TicketAuthorizationService.EnsurePermission(actor, PermissionKeys.TicketAssign);

        var old = ticket.AssignedToId;
        if (request.AssigneeUserId is { } assignee)
        {
            var isMember = await db.Memberships.IgnoreQueryFilters()
                .AnyAsync(m => m.UserId == assignee && m.CompanyId == ticket.CompanyId, ct);
            if (!isMember)
                throw new ConflictException("ticket.assignee_not_member", "The assignee is not a member of this company.");
            ticket.Assign(assignee);
            db.TicketEvents.Add(Event(ticket, actor.UserId, TicketEventType.Assigned, old?.ToString(), assignee.ToString()));
        }
        else
        {
            ticket.Unassign();
            db.TicketEvents.Add(Event(ticket, actor.UserId, TicketEventType.Unassigned, old?.ToString(), null));
        }

        await db.SaveChangesAsync(ct);
    }

    public async Task ChangeStatusAsync(Guid ticketId, ChangeStatusRequest request, CancellationToken ct = default)
    {
        var ticket = await LoadAsync(ticketId, ct);
        var actor = await authz.ResolveAsync(ticket.CompanyId, ticket.OpenedById, ct);
        TicketAuthorizationService.EnsureCanChangeStatus(actor, ticket);

        var from = await StatusAsync(ticket.StatusId, ct);
        var to = await StatusAsync(request.TargetStatusId, ct);
        var transitions = await db.StatusTransitions.IgnoreQueryFilters()
            .Where(t => t.FromStatusId == from.Id).ToListAsync(ct);

        var stateActor = actor.Kind == TicketActorKind.Customer
            ? StatusChangeActor.Customer()
            : StatusChangeActor.Staff(actor.Role ?? RoleType.Personel, actor.Permissions);

        ticket.ChangeStatus(from, to, transitions, stateActor, clock.UtcNow);
        db.TicketEvents.Add(Event(ticket, actor.UserId, TicketEventType.StatusChanged, from.Name, to.Name));
        await db.SaveChangesAsync(ct);
    }

    public async Task ReopenAsync(Guid ticketId, ChangeStatusRequest request, CancellationToken ct = default)
    {
        var ticket = await LoadAsync(ticketId, ct);
        var actor = await authz.ResolveAsync(ticket.CompanyId, ticket.OpenedById, ct);
        if (actor.Kind != TicketActorKind.Customer)
            TicketAuthorizationService.EnsurePermission(actor, PermissionKeys.TicketStatusChange);

        var from = await StatusAsync(ticket.StatusId, ct);
        var to = await StatusAsync(request.TargetStatusId, ct);
        ticket.Reopen(from, to, clock.UtcNow, _opt.ReopenWindowDays);
        db.TicketEvents.Add(Event(ticket, actor.UserId, TicketEventType.Reopened, from.Name, to.Name));
        await db.SaveChangesAsync(ct);
    }

    public async Task SetPriorityAsync(Guid ticketId, SetPriorityRequest request, CancellationToken ct = default)
    {
        var ticket = await LoadAsync(ticketId, ct);
        var actor = await authz.ResolveAsync(ticket.CompanyId, ticket.OpenedById, ct);
        // Priority is set by staff, not the customer (spec §18.17).
        if (actor.Kind == TicketActorKind.Customer)
            throw new ForbiddenException("ticket.priority_forbidden", "The customer cannot set priority.");
        TicketAuthorizationService.EnsurePermission(actor, PermissionKeys.TicketEdit);

        var old = ticket.Priority;
        ticket.SetPriority(request.Priority);
        db.TicketEvents.Add(Event(ticket, actor.UserId, TicketEventType.PriorityChanged, old.ToString(), request.Priority.ToString()));
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Approve a pending public submission into the pool (spec §10). Staff-only; requires
    /// ticket.edit (Admin/SuperAdmin by default), so triage stays with people who can act on it.</summary>
    public async Task ApproveAsync(Guid ticketId, CancellationToken ct = default)
    {
        var ticket = await LoadPendingAsync(ticketId, ct);
        var actor = await authz.ResolveAsync(ticket.CompanyId, ticket.OpenedById, ct);
        TicketAuthorizationService.EnsurePermission(actor, PermissionKeys.TicketEdit);

        ticket.Approve();
        db.TicketEvents.Add(Event(ticket, actor.UserId, TicketEventType.Approved, null, ticket.Number));
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Reject a pending submission (spec §10): it stays out of every pool. Staff-only, ticket.edit.</summary>
    public async Task RejectAsync(Guid ticketId, CancellationToken ct = default)
    {
        var ticket = await LoadPendingAsync(ticketId, ct);
        var actor = await authz.ResolveAsync(ticket.CompanyId, ticket.OpenedById, ct);
        TicketAuthorizationService.EnsurePermission(actor, PermissionKeys.TicketEdit);

        ticket.Reject();
        db.TicketEvents.Add(Event(ticket, actor.UserId, TicketEventType.Rejected, null, ticket.Number));
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid ticketId, CancellationToken ct = default)
    {
        var ticket = await LoadAsync(ticketId, ct);
        var actor = await authz.ResolveAsync(ticket.CompanyId, ticket.OpenedById, ct);
        TicketAuthorizationService.EnsurePermission(actor, PermissionKeys.TicketDelete);

        db.TicketEvents.Add(Event(ticket, actor.UserId, TicketEventType.Deleted, ticket.Number, null));
        db.Tickets.Remove(ticket); // interceptor turns this into a soft delete
        await db.SaveChangesAsync(ct);
    }

    // ---- helpers ----

    // Load ignoring the tenant filter, then let authz.ResolveAsync enforce the caller's relationship.
    // A customer has no company scope, so the filter would hide their own ticket — the read path
    // (GetDetailAsync) already loads this way; ResolveAsync is the real gate (opener or in-company).
    private async Task<Ticket> LoadAsync(Guid ticketId, CancellationToken ct) =>
        await db.Tickets.IgnoreQueryFilters().FirstOrDefaultAsync(t => t.Id == ticketId && t.DeletedAt == null, ct)
        ?? throw new NotFoundException("ticket.not_found", "Ticket not found.");

    private async Task<Ticket> LoadPendingAsync(Guid ticketId, CancellationToken ct) =>
        await db.Tickets.FirstOrDefaultAsync(t => t.Id == ticketId && t.ApprovalState == TicketApprovalState.Pending, ct)
        ?? throw new NotFoundException("ticket.not_found", "No pending ticket with this id.");

    private async Task<TicketStatus> StatusAsync(Guid statusId, CancellationToken ct) =>
        await db.TicketStatuses.IgnoreQueryFilters().FirstOrDefaultAsync(s => s.Id == statusId, ct)
        ?? throw new NotFoundException("status.not_found", "Status not found.");

    private async Task<TicketStatus> InitialStatusAsync(Guid companyId, CancellationToken ct) =>
        (await StatusSet.EffectiveAsync(db, companyId, ct)).FirstOrDefault(s => s.Category == StatusCategory.Open)
        ?? throw new NotFoundException("status.no_initial", "No initial (Open) status is configured.");

    private static TicketEvent Event(Ticket t, Guid actorId, TicketEventType type, string? oldV, string? newV) =>
        new(t.CompanyId, t.Id, actorId, type, oldV, newV);
}
