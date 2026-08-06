using CrmKanban.Application.Abstractions;
using CrmKanban.Application.Common;
using CrmKanban.Domain.Authorization;
using CrmKanban.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CrmKanban.Application.Reports;

/// <summary>
/// Ticket reports (spec §15) sourced from tickets + their status category. Two entry points with two
/// authorization gates: a company report needs report.company AND scope over that company; the global
/// report is super-admin only (report.global). Record scope for the company path is still the DbContext
/// tenant filter — the permission check is stage (a), the filter is stage (b) (spec §7).
/// ponytail: aggregates a projected row set in memory (dates diffed here, not in SQL). Fine at v1 scale;
/// push GROUP BY into SQL if ticket volumes make the row pull expensive.
/// </summary>
public sealed class ReportService(
    IAppDbContext db,
    ICurrentUserService currentUser,
    IPermissionService permissions,
    Settings.SettingsService settings)
{
    /// <summary>Fallback when the seed row is missing (older database); never blocks a report.</summary>
    private const string DefaultCurrency = "TRY";
    private sealed record Row(
        DateTime CreatedAt, DateTime? FirstResponseAt, DateTime? ResolvedAt, DateTime? ClosedAt,
        StatusCategory Category, Guid? AssignedToId, Guid? CategoryId,
        decimal? EstimatedValue, decimal? ActualValue)
    {
        /// <summary>What reporting counts: the realised amount once known, otherwise the estimate.</summary>
        public decimal? Value => ActualValue ?? EstimatedValue;

        /// <summary>When the outcome landed. Falls back so a terminal ticket always lands in some month.</summary>
        public DateTime OutcomeAt => ClosedAt ?? ResolvedAt ?? CreatedAt;
    }

    public async Task<TicketReport> CompanyReportAsync(Guid companyId, DateTime? from, DateTime? to, CancellationToken ct = default)
    {
        await EnsureCompanyAccessAsync(companyId, ct);
        // Tenant filter still applies for a non-super-admin: an out-of-scope companyId yields no rows.
        var rows = await LoadRowsAsync(db.Tickets.Where(t => t.CompanyId == companyId), from, to, ct);
        // Money is a second gate on top of report access: seeing how many tickets closed is not the
        // same as seeing what they were worth. Withheld here, so the figures never reach the client.
        var revenue = await CanSeeValueAsync(companyId, ct) ? BuildRevenue(rows, await CurrencyAsync(ct)) : null;
        return Build(companyId, from, to, rows, revenue);
    }

    public async Task<TicketReport> GlobalReportAsync(DateTime? from, DateTime? to, CancellationToken ct = default)
    {
        EnsureGlobalAccess();
        // SuperAdmin bypasses the tenant filter → all companies (and holds every permission).
        var rows = await LoadRowsAsync(db.Tickets.AsQueryable(), from, to, ct);
        return Build(null, from, to, rows, BuildRevenue(rows, await CurrencyAsync(ct)));
    }

    /// <summary>Ticket-level CSV of the same scope (spec §15 "rapor almak" = export). CSV opens in Excel;
    /// a native .xlsx would need a dependency (ClosedXML/EPPlus) — deferred until asked (ponytail).</summary>
    public async Task<string> CompanyExportCsvAsync(Guid companyId, DateTime? from, DateTime? to, CancellationToken ct = default)
    {
        await EnsureCompanyAccessAsync(companyId, ct);
        return ToCsv(await LoadExportRowsAsync(db.Tickets.Where(t => t.CompanyId == companyId), from, to, ct));
    }

    public async Task<string> GlobalExportCsvAsync(DateTime? from, DateTime? to, CancellationToken ct = default)
    {
        EnsureGlobalAccess();
        return ToCsv(await LoadExportRowsAsync(db.Tickets.AsQueryable(), from, to, ct));
    }

    private async Task EnsureCompanyAccessAsync(Guid companyId, CancellationToken ct)
    {
        var userId = currentUser.UserId
            ?? throw new UnauthorizedException("auth.required", "Authentication required.");
        if (!currentUser.IsSuperAdmin &&
            !await permissions.HasPermissionAsync(userId, companyId, PermissionKeys.ReportCompany, ct))
            throw new ForbiddenException("report.forbidden", "You lack company report access.");
    }

    /// <summary>Whether this caller may see money for this company (Faz 39).</summary>
    private async Task<bool> CanSeeValueAsync(Guid companyId, CancellationToken ct)
    {
        if (currentUser.IsSuperAdmin) return true;
        var userId = currentUser.UserId;
        return userId is not null
            && await permissions.HasPermissionAsync(userId.Value, companyId, PermissionKeys.TicketValue, ct);
    }

    private void EnsureGlobalAccess()
    {
        if (!currentUser.IsSuperAdmin)
            throw new ForbiddenException("report.forbidden", "The global report is super-admin only.");
    }

    private async Task<List<Row>> LoadRowsAsync(IQueryable<Domain.Entities.Ticket> tickets, DateTime? from, DateTime? to, CancellationToken ct)
    {
        if (from is { } f) tickets = tickets.Where(t => t.CreatedAt >= f);
        if (to is { } t2) tickets = tickets.Where(t => t.CreatedAt < t2);

        // Join statuses (ignore their filter — global statuses have null CompanyId) to read the category.
        var joined = from t in tickets
                     join s in db.TicketStatuses.IgnoreQueryFilters() on t.StatusId equals s.Id
                     select new Row(t.CreatedAt, t.FirstResponseAt, t.ResolvedAt, t.ClosedAt,
                         s.Category, t.AssignedToId, t.CategoryId, t.EstimatedValue, t.ActualValue);
        return await joined.ToListAsync(ct);
    }

    private static TicketReport Build(
        Guid? companyId, DateTime? from, DateTime? to, List<Row> rows, RevenueSummary? revenue)
    {
        var byCategory = rows.GroupBy(r => r.Category)
            .Select(g => new StatusCategoryCount(g.Key, g.Count()))
            .OrderBy(c => c.Category).ToList();

        var firstResponse = rows.Where(r => r.FirstResponseAt is not null)
            .Select(r => (r.FirstResponseAt!.Value - r.CreatedAt).TotalHours).ToList();
        var resolution = rows.Where(r => r.ResolvedAt is not null)
            .Select(r => (r.ResolvedAt!.Value - r.CreatedAt).TotalHours).ToList();

        // Open = not in a terminal category (Closed/Cancelled).
        var staffLoad = rows.Where(r => r.Category is not (StatusCategory.Closed or StatusCategory.Cancelled))
            .GroupBy(r => r.AssignedToId)
            .Select(g => new StaffLoadItem(g.Key, g.Count()))
            .OrderByDescending(s => s.OpenCount).ToList();

        var categoryBreakdown = rows.GroupBy(r => r.CategoryId)
            .Select(g => new CategoryCount(g.Key, g.Count()))
            .OrderByDescending(c => c.Count).ToList();

        var opened = rows.GroupBy(r => DateOnly.FromDateTime(r.CreatedAt))
            .ToDictionary(g => g.Key, g => g.Count());
        var closed = rows.Where(r => r.ClosedAt is not null)
            .GroupBy(r => DateOnly.FromDateTime(r.ClosedAt!.Value))
            .ToDictionary(g => g.Key, g => g.Count());
        var trend = opened.Keys.Union(closed.Keys).OrderBy(d => d)
            .Select(d => new TrendPoint(d, opened.GetValueOrDefault(d), closed.GetValueOrDefault(d)))
            .ToList();

        return new TicketReport(companyId, from, to, rows.Count, byCategory,
            firstResponse.Count > 0 ? Math.Round(firstResponse.Average(), 2) : null,
            resolution.Count > 0 ? Math.Round(resolution.Average(), 2) : null,
            staffLoad, categoryBreakdown, trend, revenue);
    }

    /// <summary>
    /// The money view (Faz 39). Won = Closed category, lost = Cancelled, everything else is open
    /// pipeline — branching on the CATEGORY, never the status name, so a company that renames
    /// "Tamamlandı" to "Teslim edildi" keeps correct totals (spec §4.3).
    /// </summary>
    private async Task<string> CurrencyAsync(CancellationToken ct) =>
        await settings.GetValueAsync("finance.currency", ct) is { Length: > 0 } c ? c : DefaultCurrency;

    private static RevenueSummary BuildRevenue(List<Row> rows, string currency)
    {
        var priced = rows.Where(r => r.Value is not null).ToList();
        var won = priced.Where(r => r.Category == StatusCategory.Closed).ToList();
        var lost = priced.Where(r => r.Category == StatusCategory.Cancelled).ToList();
        var open = priced.Where(r => r.Category is not (StatusCategory.Closed or StatusCategory.Cancelled)).ToList();

        var wonTotal = won.Sum(r => r.Value!.Value);
        var lostTotal = lost.Sum(r => r.Value!.Value);
        var decided = won.Count + lost.Count;
        var decidedTotal = wonTotal + lostTotal;

        // Forecast accuracy only means something where BOTH numbers exist: a won ticket whose actual
        // was never entered would otherwise report a perfect 1.0 and flatter the average.
        var estimated = won.Where(r => r.ActualValue is not null && r.EstimatedValue is > 0).ToList();
        decimal? accuracy = estimated.Count > 0
            ? Math.Round(estimated.Sum(r => r.ActualValue!.Value) / estimated.Sum(r => r.EstimatedValue!.Value), 4)
            : null;

        var byMonth = won.Concat(lost)
            .GroupBy(r => new DateOnly(r.OutcomeAt.Year, r.OutcomeAt.Month, 1))
            .OrderBy(g => g.Key)
            .Select(g => new RevenueTrendPoint(
                g.Key,
                g.Where(r => r.Category == StatusCategory.Closed).Sum(r => r.Value!.Value),
                g.Where(r => r.Category == StatusCategory.Cancelled).Sum(r => r.Value!.Value)))
            .ToList();

        return new RevenueSummary(
            currency,
            wonTotal, won.Count,
            lostTotal, lost.Count,
            open.Sum(r => r.Value!.Value), open.Count,
            rows.Count - priced.Count,
            // 0/0 is undefined, not zero — a company that has closed nothing has no win rate yet.
            decided > 0 ? Math.Round((double)won.Count / decided, 4) : null,
            decidedTotal > 0 ? Math.Round((double)(wonTotal / decidedTotal), 4) : null,
            accuracy,
            byMonth);
    }

    private sealed record ExportRow(string Number, string Title, string Status, StatusCategory Category,
        Priority Priority, Guid? AssignedToId, Guid? CategoryId,
        DateTime CreatedAt, DateTime? FirstResponseAt, DateTime? ResolvedAt, DateTime? ClosedAt);

    private async Task<List<ExportRow>> LoadExportRowsAsync(IQueryable<Domain.Entities.Ticket> tickets, DateTime? from, DateTime? to, CancellationToken ct)
    {
        if (from is { } f) tickets = tickets.Where(t => t.CreatedAt >= f);
        if (to is { } t2) tickets = tickets.Where(t => t.CreatedAt < t2);
        var joined = from t in tickets
                     join s in db.TicketStatuses.IgnoreQueryFilters() on t.StatusId equals s.Id
                     orderby t.CreatedAt
                     select new ExportRow(t.Number, t.Title, s.Name, s.Category, t.Priority,
                         t.AssignedToId, t.CategoryId, t.CreatedAt, t.FirstResponseAt, t.ResolvedAt, t.ClosedAt);
        return await joined.ToListAsync(ct);
    }

    private static string ToCsv(List<ExportRow> rows)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Number,Title,Status,Category,Priority,AssignedToId,CategoryId,CreatedAt,FirstResponseAt,ResolvedAt,ClosedAt");
        foreach (var r in rows)
            sb.AppendLine(string.Join(',', new[]
            {
                Csv(r.Number), Csv(r.Title), Csv(r.Status), Csv(r.Category.ToString()), Csv(r.Priority.ToString()),
                Csv(r.AssignedToId?.ToString()), Csv(r.CategoryId?.ToString()),
                Csv(r.CreatedAt.ToString("O")), Csv(r.FirstResponseAt?.ToString("O")),
                Csv(r.ResolvedAt?.ToString("O")), Csv(r.ClosedAt?.ToString("O")),
            }));
        return sb.ToString();
    }

    // RFC 4180 escaping: wrap in quotes and double internal quotes when the field holds , " or a newline.
    private static string Csv(string? field)
    {
        if (string.IsNullOrEmpty(field)) return "";
        if (field.IndexOfAny([',', '"', '\n', '\r']) < 0) return field;
        return $"\"{field.Replace("\"", "\"\"")}\"";
    }
}
