using CrmKanban.Application.Abstractions;
using CrmKanban.Application.Authorization;
using CrmKanban.Application.Common;
using CrmKanban.Domain.Authorization;
using CrmKanban.Domain.Entities;
using CrmKanban.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CrmKanban.Application.Tickets;

/// <summary>
/// Ticket reads (spec §17.4): server-side search/filter/pagination (not optional), detail with
/// visibility-filtered comments, the kanban board, and the customer's own flat list. Tenant scope
/// for staff is the DbContext filter; customers (not company members) are scoped by OpenedById.
/// </summary>
public sealed class TicketQueryService(
    IAppDbContext db,
    ICurrentUserService currentUser,
    TicketAuthorizationService authz,
    IPermissionService permissions,
    IOptions<TicketOptions> options)
{
    private readonly TicketOptions _opt = options.Value;

    private bool IsStaff => currentUser.IsSuperAdmin || currentUser.CompanyIds.Count > 0;

    /// <summary>
    /// Companies whose tickets this staff caller may READ (`ticket.view`). Until Faz 30 the key was
    /// offered in the admin UI and checked nowhere, so revoking it changed nothing — the switch lied.
    /// Membership alone was treated as permission to read. Super admin short-circuits to "everything".
    /// </summary>
    private async Task<HashSet<Guid>> ViewableCompaniesAsync(CancellationToken ct)
    {
        var userId = RequireUserId();
        var allowed = new HashSet<Guid>();
        foreach (var companyId in currentUser.CompanyIds)
            if (await permissions.HasPermissionAsync(userId, companyId, PermissionKeys.TicketView, ct))
                allowed.Add(companyId);
        return allowed;
    }

    /// <summary>
    /// Companies where this caller may see money (`ticket.value`, Faz 39). Mirrors
    /// <see cref="ViewableCompaniesAsync"/> because the list paths span companies: a customer's own
    /// tickets, or a staff list across memberships. Per company, never global — an admin at one
    /// company must not read another's order book through a shared list.
    /// </summary>
    private async Task<HashSet<Guid>> ValueVisibleCompaniesAsync(CancellationToken ct)
    {
        var userId = currentUser.UserId;
        var allowed = new HashSet<Guid>();
        if (userId is null) return allowed;
        foreach (var companyId in currentUser.CompanyIds)
            if (await permissions.HasPermissionAsync(userId.Value, companyId, PermissionKeys.TicketValue, ct))
                allowed.Add(companyId);
        return allowed;
    }

    private async Task EnsureCanViewAsync(Guid companyId, CancellationToken ct)
    {
        if (currentUser.IsSuperAdmin) return;
        if (!await permissions.HasPermissionAsync(RequireUserId(), companyId, PermissionKeys.TicketView, ct))
            throw new ForbiddenException("ticket.view_forbidden", "You cannot view this company's tickets.");
    }

    public async Task<PagedResult<TicketListItem>> ListAsync(TicketListQuery query, CancellationToken ct = default)
    {
        // Staff: the tenant filter limits to their companies, and `ticket.view` narrows that to the ones
        // they may actually read. Customer: their own tickets, any state (they are not members, so they
        // hold no permissions at all — gating them on ticket.view would lock them out of their own requests).
        IQueryable<Ticket> baseQuery;
        if (IsStaff)
        {
            baseQuery = db.Tickets.Where(t => t.ApprovalState == TicketApprovalState.Approved);
            if (!currentUser.IsSuperAdmin)
            {
                var allowed = await ViewableCompaniesAsync(ct);
                baseQuery = baseQuery.Where(t => allowed.Contains(t.CompanyId));
            }
        }
        else
        {
            baseQuery = db.Tickets.IgnoreQueryFilters().Where(t => t.OpenedById == RequireUserId() && t.DeletedAt == null);
        }

        baseQuery = ApplyFilters(baseQuery, query);
        return await PaginateAsync(baseQuery, query, ct);
    }

    /// <summary>Companies the customer already has a relationship with = the distinct companies of their
    /// own tickets (customers aren't members, so relationship is established by opening a ticket, first
    /// via the company's public form). Feeds the portal's "new message" picker so a customer only ever
    /// sees the companies they actually work with — not every company in the system (spec §7, §18.5).</summary>
    public async Task<IReadOnlyList<CustomerCompanyDto>> ListMyCompaniesAsync(CancellationToken ct = default)
    {
        var userId = RequireUserId();
        var companyIds = await db.Tickets.IgnoreQueryFilters()
            .Where(t => t.OpenedById == userId && t.DeletedAt == null)
            .Select(t => t.CompanyId).Distinct().ToListAsync(ct);
        return await db.Companies.IgnoreQueryFilters()
            .Where(c => companyIds.Contains(c.Id) && c.IsActive && c.ArchivedAt == null)
            .OrderBy(c => c.Name)
            .Select(c => new CustomerCompanyDto(c.Id, c.Name))
            .ToListAsync(ct);
    }

    public async Task<TicketDetail> GetDetailAsync(Guid ticketId, CancellationToken ct = default)
    {
        // Load ignoring the tenant filter, then let authz enforce the caller's relationship — this is
        // the single gate that also lets a customer (no company scope) reach their own ticket.
        var ticket = await db.Tickets.IgnoreQueryFilters().FirstOrDefaultAsync(t => t.Id == ticketId && t.DeletedAt == null, ct)
            ?? throw new NotFoundException("ticket.not_found", "Ticket not found.");
        var actor = await authz.ResolveAsync(ticket.CompanyId, ticket.OpenedById, ct);
        // Staff read the detail only with ticket.view; the opener reaches their own ticket regardless
        // (a customer holds no permissions, and locking them out of their own request would be absurd).
        if (actor.IsStaff && ticket.OpenedById != actor.UserId)
            TicketAuthorizationService.EnsurePermission(actor, PermissionKeys.TicketView);

        var status = await db.TicketStatuses.IgnoreQueryFilters().FirstAsync(s => s.Id == ticket.StatusId, ct);

        var commentsQuery = db.Comments.IgnoreQueryFilters()
            .Where(c => c.TicketId == ticketId && c.DeletedAt == null);
        if (!actor.IsStaff)
            commentsQuery = commentsQuery.Where(c => !c.IsInternal); // customers never see internal notes

        var comments = await commentsQuery.OrderBy(c => c.CreatedAt)
            .Select(c => new CommentDto(
                c.Id, c.AuthorId,
                db.Users.IgnoreQueryFilters().Where(u => u.Id == c.AuthorId)
                    .Select(u => u.FirstName + " " + u.LastName).FirstOrDefault() ?? "—",
                c.Body, c.IsInternal, c.EditedAt != null, c.CreatedAt, c.EditedAt))
            .ToListAsync(ct);

        // Attachments visible to this caller: ticket-level (CommentId == null) plus those on comments the
        // caller can see. A customer never gets a file on an internal note (spec §14/§20).
        var visibleCommentIds = comments.Select(c => (Guid?)c.Id).ToHashSet();
        var attachmentRows = await db.Attachments.IgnoreQueryFilters()
            .Where(a => a.TicketId == ticketId && a.DeletedAt == null).ToListAsync(ct);
        var attachmentDtos = attachmentRows
            .Where(a => a.CommentId == null || visibleCommentIds.Contains(a.CommentId))
            .Select(Files.AttachmentService.ToDto).ToList();

        var customFields = ParseCustomFields(ticket.CustomFieldsJson);

        var canSeeValue = await permissions.CanSeeValueAsync(currentUser, ticket.CompanyId, ct);
        return new TicketDetail(ticket.Id, ticket.Number, ticket.CompanyId, ticket.Title, ticket.Body,
            ticket.StatusId, status.Name, status.Category, ticket.Priority,
            ticket.OpenedById, ticket.AssignedToId,
            ticket.FirstResponseAt, ticket.ResolvedAt, ticket.ClosedAt, ticket.CreatedAt, comments, attachmentDtos,
            customFields,
            canSeeValue ? ticket.EstimatedValue : null,
            canSeeValue ? ticket.ActualValue : null,
            canSeeValue);
    }

    public async Task<IReadOnlyList<KanbanColumn>> KanbanAsync(Guid companyId, TicketListQuery query, CancellationToken ct = default)
    {
        if (!IsStaff)
            throw new ForbiddenException("kanban.forbidden", "The kanban board is staff-only.");
        await EnsureCanViewAsync(companyId, ct);

        var statuses = await StatusSet.EffectiveAsync(db, companyId, ct);

        var tickets = await ApplyFilters(
                db.Tickets.Where(t => t.CompanyId == companyId && t.ApprovalState == TicketApprovalState.Approved), query)
            .OrderByDescending(t => t.CreatedAt).ToListAsync(ct);

        var byStatus = tickets.ToLookup(t => t.StatusId);
        var statusById = statuses.ToDictionary(s => s.Id);
        var withValue = await permissions.CanSeeValueAsync(currentUser, companyId, ct);
        return statuses.Select(s => new KanbanColumn(s.Id, s.Name, s.Category, s.Color, s.Order,
            byStatus[s.Id].Select(t => ToListItem(t, statusById, withValue)).ToList())).ToList();
    }

    /// <summary>Tickets awaiting moderation (spec §10 zero-trust intake): first-time public submissions
    /// held out of the pool. Staff-only; tenant filter scopes to the caller's company.</summary>
    public async Task<IReadOnlyList<TicketListItem>> ModerationQueueAsync(Guid companyId, CancellationToken ct = default)
    {
        if (!IsStaff)
            throw new ForbiddenException("moderation.forbidden", "The moderation queue is staff-only.");
        await EnsureCanViewAsync(companyId, ct);

        // companyId comes from the caller, so the tenant filter on db.Tickets — not this predicate — is
        // what actually scopes the read. Statuses are resolved separately (see LoadStatusesAsync).
        var tickets = await db.Tickets
            .Where(t => t.CompanyId == companyId && t.ApprovalState == TicketApprovalState.Pending)
            .OrderBy(t => t.CreatedAt).ToListAsync(ct);
        var statusById = await LoadStatusesAsync(ct);
        var withValue = await permissions.CanSeeValueAsync(currentUser, companyId, ct);
        return tickets.Select(t => ToListItem(t, statusById, withValue)).ToList();
    }

    /// <summary>The status catalog for dropdowns and the customer cancel/complete actions. With a
    /// companyId it returns that company's effective set (its own columns if customized, else global);
    /// without one, the global defaults. Any authenticated caller may read.</summary>
    public async Task<IReadOnlyList<StatusDto>> ListStatusesAsync(Guid? companyId = null, CancellationToken ct = default)
    {
        var set = companyId is { } cid
            ? await StatusSet.EffectiveAsync(db, cid, ct)
            : await db.TicketStatuses.IgnoreQueryFilters()
                .Where(s => s.CompanyId == null && s.DeletedAt == null).OrderBy(s => s.Order).ToListAsync(ct);
        return set.Select(s => new StatusDto(s.Id, s.Name, s.Category, s.Color, s.Order, s.IsTerminal)).ToList();
    }

    // ---- helpers ----

    // Custom fields are stored denormalized as a JSON array of {Label, Value} (spec §4.6). Parse leniently:
    // a malformed blob just yields no fields rather than failing the whole detail read.
    private static IReadOnlyList<CustomFieldValue> ParseCustomFields(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return [];
        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<List<CustomFieldValue>>(json) ?? [];
        }
        catch (System.Text.Json.JsonException)
        {
            return [];
        }
    }

    /// <summary>
    /// Status metadata (name/category/color) for the list projections, read as its OWN query.
    /// It must not be joined into the ticket query: reaching a company's private status column needs
    /// IgnoreQueryFilters (a customer has no company scope at all), and EF applies IgnoreQueryFilters to
    /// the WHOLE query — joining it in silently switched the TENANT filter off for the tickets too and
    /// leaked every tenant's list to every staff user. Two queries, one of them over a tiny table, keep
    /// the tenant filter the single thing that decides which tickets are readable.
    /// </summary>
    private async Task<Dictionary<Guid, TicketStatus>> LoadStatusesAsync(CancellationToken ct) =>
        await db.TicketStatuses.IgnoreQueryFilters()
            .Where(s => s.DeletedAt == null)
            .ToDictionaryAsync(s => s.Id, ct);

    private static IQueryable<Ticket> ApplyFilters(IQueryable<Ticket> q, TicketListQuery query)
    {
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var s = query.Search.Trim();
            q = q.Where(t => EF.Functions.Like(t.Number, $"%{s}%") || EF.Functions.Like(t.Title, $"%{s}%"));
        }
        if (query.StatusId is { } statusId) q = q.Where(t => t.StatusId == statusId);
        if (query.AssignedToId is { } assignee) q = q.Where(t => t.AssignedToId == assignee);
        if (query.Priority is { } priority) q = q.Where(t => t.Priority == priority);
        return q;
    }

    private async Task<PagedResult<TicketListItem>> PaginateAsync(IQueryable<Ticket> q, TicketListQuery query, CancellationToken ct)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, _opt.MaxPageSize);

        var statusById = await LoadStatusesAsync(ct);
        if (query.Category is { } category)
        {
            var ids = statusById.Values.Where(s => s.Category == category).Select(s => s.Id).ToList();
            q = q.Where(t => ids.Contains(t.StatusId));
        }

        var total = await q.CountAsync(ct);
        var tickets = await q
            .OrderByDescending(t => t.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync(ct);

        // This path can span companies (a customer's tickets, a staff list across memberships), so the
        // decision is per ticket, not once for the page.
        var valueCompanies = await ValueVisibleCompaniesAsync(ct);
        var superAdmin = currentUser.IsSuperAdmin;
        return new PagedResult<TicketListItem>(
            tickets.Select(t => ToListItem(t, statusById, superAdmin || valueCompanies.Contains(t.CompanyId)))
                .ToList(), total, page, pageSize);
    }

    private static TicketListItem ToListItem(Ticket t, Dictionary<Guid, TicketStatus> statusById, bool withValue)
    {
        var s = statusById[t.StatusId];
        return new TicketListItem(t.Id, t.Number, t.Title, t.StatusId, s.Name, s.Category, s.Color,
            t.Priority, t.AssignedToId, t.CreatedAt,
            withValue ? t.ActualValue ?? t.EstimatedValue : null);
    }

    // Money visibility (ticket.value) is decided by ValueVisibility.CanSeeValueAsync — shared with
    // ReportService. Amounts are stripped from the DTO when false: hiding a field in the UI while
    // shipping it in the JSON is not access control.

    private Guid RequireUserId() =>
        currentUser.UserId ?? throw new UnauthorizedException("auth.required", "Authentication required.");
}
