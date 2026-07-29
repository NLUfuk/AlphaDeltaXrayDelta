using CrmKanban.Application.Abstractions;
using CrmKanban.Application.Common;
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
    Files.AttachmentService attachments,
    IOptions<TicketOptions> options)
{
    private readonly TicketOptions _opt = options.Value;

    private bool IsStaff => currentUser.IsSuperAdmin || currentUser.CompanyIds.Count > 0;

    public async Task<PagedResult<TicketListItem>> ListAsync(TicketListQuery query, CancellationToken ct = default)
    {
        // Staff: the tenant filter already limits to their companies. Customer: their own tickets only.
        var baseQuery = IsStaff
            ? db.Tickets.AsQueryable()
            : db.Tickets.IgnoreQueryFilters().Where(t => t.OpenedById == RequireUserId() && t.DeletedAt == null);

        baseQuery = ApplyFilters(baseQuery, query);
        return await PaginateAsync(baseQuery, query, ct);
    }

    public async Task<TicketDetail> GetDetailAsync(Guid ticketId, CancellationToken ct = default)
    {
        // Load ignoring the tenant filter, then let authz enforce the caller's relationship — this is
        // the single gate that also lets a customer (no company scope) reach their own ticket.
        var ticket = await db.Tickets.IgnoreQueryFilters().FirstOrDefaultAsync(t => t.Id == ticketId && t.DeletedAt == null, ct)
            ?? throw new NotFoundException("ticket.not_found", "Ticket not found.");
        var actor = await authz.ResolveAsync(ticket.CompanyId, ticket.OpenedById, ct);

        var status = await db.TicketStatuses.IgnoreQueryFilters().FirstAsync(s => s.Id == ticket.StatusId, ct);

        var commentsQuery = db.Comments.IgnoreQueryFilters()
            .Where(c => c.TicketId == ticketId && c.DeletedAt == null);
        if (!actor.IsStaff)
            commentsQuery = commentsQuery.Where(c => !c.IsInternal); // customers never see internal notes

        var comments = await commentsQuery.OrderBy(c => c.CreatedAt)
            .Select(c => new CommentDto(c.Id, c.AuthorId, c.Body, c.IsInternal, c.EditedAt != null, c.CreatedAt, c.EditedAt))
            .ToListAsync(ct);

        // Attachments visible to this caller: ticket-level (CommentId == null) plus those on comments the
        // caller can see. A customer never gets a file on an internal note (spec §14/§20).
        var visibleCommentIds = comments.Select(c => (Guid?)c.Id).ToHashSet();
        var attachmentRows = await db.Attachments.IgnoreQueryFilters()
            .Where(a => a.TicketId == ticketId && a.DeletedAt == null).ToListAsync(ct);
        var attachmentDtos = attachmentRows
            .Where(a => a.CommentId == null || visibleCommentIds.Contains(a.CommentId))
            .Select(attachments.ToDto).ToList();

        return new TicketDetail(ticket.Id, ticket.Number, ticket.CompanyId, ticket.Title, ticket.Body,
            ticket.StatusId, status.Name, status.Category, ticket.Priority,
            ticket.OpenedById, ticket.AssignedToId, ticket.CategoryId,
            ticket.FirstResponseAt, ticket.ResolvedAt, ticket.ClosedAt, ticket.CreatedAt, comments, attachmentDtos);
    }

    public async Task<IReadOnlyList<KanbanColumn>> KanbanAsync(Guid companyId, TicketListQuery query, CancellationToken ct = default)
    {
        if (!IsStaff)
            throw new ForbiddenException("kanban.forbidden", "The kanban board is staff-only.");

        var statuses = await db.TicketStatuses.IgnoreQueryFilters()
            .Where(s => (s.CompanyId == companyId || s.CompanyId == null) && s.DeletedAt == null)
            .OrderBy(s => s.Order).ToListAsync(ct);

        var tickets = await ApplyFilters(db.Tickets.Where(t => t.CompanyId == companyId), query)
            .OrderByDescending(t => t.CreatedAt).ToListAsync(ct);

        var byStatus = tickets.ToLookup(t => t.StatusId);
        var statusById = statuses.ToDictionary(s => s.Id);
        return statuses.Select(s => new KanbanColumn(s.Id, s.Name, s.Category, s.Color, s.Order,
            byStatus[s.Id].Select(t => ToListItem(t, statusById)).ToList())).ToList();
    }

    // ---- helpers ----

    private static IQueryable<Ticket> ApplyFilters(IQueryable<Ticket> q, TicketListQuery query)
    {
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var s = query.Search.Trim();
            q = q.Where(t => EF.Functions.Like(t.Number, $"%{s}%") || EF.Functions.Like(t.Title, $"%{s}%"));
        }
        if (query.StatusId is { } statusId) q = q.Where(t => t.StatusId == statusId);
        if (query.CategoryId is { } categoryId) q = q.Where(t => t.CategoryId == categoryId);
        if (query.AssignedToId is { } assignee) q = q.Where(t => t.AssignedToId == assignee);
        if (query.Priority is { } priority) q = q.Where(t => t.Priority == priority);
        return q;
    }

    private async Task<PagedResult<TicketListItem>> PaginateAsync(IQueryable<Ticket> q, TicketListQuery query, CancellationToken ct)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, _opt.MaxPageSize);
        var statusFilterByCategory = query.Category;

        // Join statuses for name/category/color and optional category filter.
        var joined = from t in q
                     join s in db.TicketStatuses.IgnoreQueryFilters() on t.StatusId equals s.Id
                     where statusFilterByCategory == null || s.Category == statusFilterByCategory
                     select new { t, s };

        var total = await joined.CountAsync(ct);
        var items = await joined
            .OrderByDescending(x => x.t.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new TicketListItem(x.t.Id, x.t.Number, x.t.Title, x.t.StatusId, x.s.Name, x.s.Category,
                x.s.Color, x.t.Priority, x.t.AssignedToId, x.t.CategoryId, x.t.CreatedAt))
            .ToListAsync(ct);

        return new PagedResult<TicketListItem>(items, total, page, pageSize);
    }

    private static TicketListItem ToListItem(Ticket t, Dictionary<Guid, TicketStatus> statusById)
    {
        var s = statusById[t.StatusId];
        return new TicketListItem(t.Id, t.Number, t.Title, t.StatusId, s.Name, s.Category, s.Color,
            t.Priority, t.AssignedToId, t.CategoryId, t.CreatedAt);
    }

    private Guid RequireUserId() =>
        currentUser.UserId ?? throw new UnauthorizedException("auth.required", "Authentication required.");
}
