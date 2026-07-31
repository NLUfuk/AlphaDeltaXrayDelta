using CrmKanban.Application.Abstractions;
using CrmKanban.Application.Common;
using CrmKanban.Domain.Authorization;
using CrmKanban.Domain.Entities;
using CrmKanban.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CrmKanban.Application.Tickets;

/// <summary>
/// Per-company kanban column management (spec §12, §18.9). An admin (status.manage) can add a column
/// at any position in the chain, rename/recolor it, reorder the board, and remove a column. The first
/// mutation forks the shared global set into company-owned rows and migrates the company's existing
/// tickets onto the clones, so ordering is fully local and no ticket is orphaned. New non-terminal
/// columns are auto-chained into the transition graph (staff can drag any card to/from them), mirroring
/// the default rule: a non-terminal moves to any other non-terminal plus the terminals.
/// </summary>
public sealed class StatusManagementService(
    IAppDbContext db,
    ICurrentUserService currentUser,
    IPermissionService permissions,
    IClock clock)
{
    public async Task<IReadOnlyList<StatusColumnDto>> ListAsync(Guid companyId, CancellationToken ct = default)
    {
        await EnsureCanManageAsync(companyId, ct);
        var set = await StatusSet.EffectiveAsync(db, companyId, ct);
        return set.Select(s => new StatusColumnDto(s.Id, s.Name, s.Category, s.Color, s.Order, s.IsTerminal,
            Editable: s.CompanyId == companyId)).ToList();
    }

    public async Task<Guid> CreateAsync(Guid companyId, CreateStatusRequest request, CancellationToken ct = default)
    {
        await EnsureCanManageAsync(companyId, ct);
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new BadRequestException("status.name_required", "A column name is required.");

        var set = await EnsureCompanySetAsync(companyId, ct);
        var isTerminal = request.Category is StatusCategory.Closed or StatusCategory.Cancelled;
        var position = Math.Clamp(request.Position, 0, set.Count);

        // Open a gap at the position: shift columns at/after it down by one.
        foreach (var s in set.Where(s => s.Order >= position))
            s.MoveTo(s.Order + 1);

        var created = new TicketStatus(request.Name, request.Category, NormalizeColor(request.Color), position, isTerminal, companyId);
        db.TicketStatuses.Add(created);

        WireTransitions(created, set);
        await db.SaveChangesAsync(ct);
        return created.Id;
    }

    public async Task UpdateAsync(Guid companyId, Guid statusId, UpdateStatusRequest request, CancellationToken ct = default)
    {
        await EnsureCanManageAsync(companyId, ct);
        var status = await OwnedStatusAsync(companyId, statusId, ct);
        if (!string.IsNullOrWhiteSpace(request.Name)) status.Rename(request.Name);
        if (!string.IsNullOrWhiteSpace(request.Color)) status.Recolor(NormalizeColor(request.Color));
        await db.SaveChangesAsync(ct);
    }

    public async Task ReorderAsync(Guid companyId, ReorderStatusesRequest request, CancellationToken ct = default)
    {
        await EnsureCanManageAsync(companyId, ct);
        var set = await EnsureCompanySetAsync(companyId, ct);
        var byId = set.ToDictionary(s => s.Id);

        if (request.OrderedStatusIds.Count != set.Count || request.OrderedStatusIds.Any(id => !byId.ContainsKey(id)))
            throw new BadRequestException("status.reorder_mismatch", "The order must list every column of this company exactly once.");

        for (var i = 0; i < request.OrderedStatusIds.Count; i++)
            byId[request.OrderedStatusIds[i]].MoveTo(i);
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid companyId, Guid statusId, CancellationToken ct = default)
    {
        await EnsureCanManageAsync(companyId, ct);
        var status = await OwnedStatusAsync(companyId, statusId, ct);

        if (await db.Tickets.IgnoreQueryFilters().AnyAsync(t => t.StatusId == statusId && t.DeletedAt == null, ct))
            throw new ConflictException("status.in_use", "Move or close the tickets in this column before removing it.");

        var set = await StatusSet.EffectiveAsync(db, companyId, ct);
        if (status.Category == StatusCategory.Open && set.Count(s => s.Category == StatusCategory.Open) <= 1)
            throw new ConflictException("status.last_open", "A company must keep at least one opening column.");

        var now = clock.UtcNow;
        status.SoftDelete(now);
        var edges = await db.StatusTransitions.IgnoreQueryFilters()
            .Where(t => (t.FromStatusId == statusId || t.ToStatusId == statusId) && t.DeletedAt == null).ToListAsync(ct);
        foreach (var e in edges) e.SoftDelete(now);
        await db.SaveChangesAsync(ct);
    }

    // ---- helpers ----

    /// <summary>Fork the global set into company-owned rows on first customization: clone statuses,
    /// mirror the transition graph, and migrate the company's tickets onto the clones.</summary>
    private async Task<List<TicketStatus>> EnsureCompanySetAsync(Guid companyId, CancellationToken ct)
    {
        var own = await db.TicketStatuses.IgnoreQueryFilters()
            .Where(s => s.CompanyId == companyId && s.DeletedAt == null)
            .OrderBy(s => s.Order).ToListAsync(ct);
        if (own.Count > 0) return own;

        var global = await db.TicketStatuses.IgnoreQueryFilters()
            .Where(s => s.CompanyId == null && s.DeletedAt == null)
            .OrderBy(s => s.Order).ToListAsync(ct);

        var map = new Dictionary<Guid, TicketStatus>();
        foreach (var g in global)
        {
            var clone = new TicketStatus(g.Name, g.Category, g.Color, g.Order, g.IsTerminal, companyId);
            db.TicketStatuses.Add(clone);
            map[g.Id] = clone;
        }

        var globalIds = global.Select(g => g.Id).ToHashSet();
        var globalEdges = await db.StatusTransitions.IgnoreQueryFilters()
            .Where(t => t.DeletedAt == null && globalIds.Contains(t.FromStatusId) && globalIds.Contains(t.ToStatusId))
            .ToListAsync(ct);
        foreach (var e in globalEdges)
            db.StatusTransitions.Add(new StatusTransition(map[e.FromStatusId].Id, map[e.ToStatusId].Id, e.AllowedByPermissionKey));

        var tickets = await db.Tickets.IgnoreQueryFilters()
            .Where(t => t.CompanyId == companyId && globalIds.Contains(t.StatusId)).ToListAsync(ct);
        foreach (var t in tickets)
            t.MigrateStatus(map[t.StatusId].Id);

        await db.SaveChangesAsync(ct);
        return map.Values.OrderBy(s => s.Order).ToList();
    }

    /// <summary>Chain a new column into the graph (spec §12 default rule): a non-terminal reaches every
    /// other non-terminal plus the terminals; a terminal is only a destination. Edges require
    /// ticket.status.change, like the seed.</summary>
    private void WireTransitions(TicketStatus created, IReadOnlyList<TicketStatus> existing)
    {
        // created (if non-terminal) -> every other column; every non-terminal column -> created.
        if (!created.IsTerminal)
            foreach (var to in existing.Where(s => s.Id != created.Id))
                db.StatusTransitions.Add(new StatusTransition(created.Id, to.Id, PermissionKeys.TicketStatusChange));

        foreach (var from in existing.Where(s => !s.IsTerminal))
            db.StatusTransitions.Add(new StatusTransition(from.Id, created.Id, PermissionKeys.TicketStatusChange));
    }

    private async Task<TicketStatus> OwnedStatusAsync(Guid companyId, Guid statusId, CancellationToken ct) =>
        await db.TicketStatuses.IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.Id == statusId && s.CompanyId == companyId && s.DeletedAt == null, ct)
        ?? throw new NotFoundException("status.not_found", "No editable column with this id in this company.");

    private async Task EnsureCanManageAsync(Guid companyId, CancellationToken ct)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedException("auth.required", "Authentication required.");
        if (currentUser.IsSuperAdmin) return;
        if (!await permissions.HasPermissionAsync(userId, companyId, PermissionKeys.StatusManage, ct))
            throw new ForbiddenException("status.manage_forbidden", "You cannot manage columns for this company.");
    }

    private static string NormalizeColor(string? color)
    {
        var c = (color ?? "").Trim();
        return c.Length == 0 ? "#6b7280" : c;
    }
}
