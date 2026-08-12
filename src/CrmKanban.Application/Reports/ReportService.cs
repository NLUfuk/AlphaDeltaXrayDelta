using CrmKanban.Application.Abstractions;
using CrmKanban.Application.Authorization;
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
    Settings.SettingsService settings,
    IClock clock)
{
    /// <summary>Fallback when the seed row is missing (older database); never blocks a report.</summary>
    private const string DefaultCurrency = "TRY";
    private sealed record Row(
        DateTime CreatedAt, DateTime? FirstResponseAt, DateTime? ResolvedAt, DateTime? ClosedAt,
        StatusCategory Category, Guid? AssignedToId, string? AssignedToName,
        decimal? EstimatedValue, decimal? ActualValue,
        Guid CompanyId, Guid OpenedById, string? OpenedByName, string? OpenedByEmail,
        bool OpenedByIsSuperAdmin)
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
        var withMoney = await permissions.CanSeeValueAsync(currentUser, companyId, ct);
        var revenue = withMoney ? BuildRevenue(rows, await CurrencyAsync(ct)) : null;
        return Build(companyId, from, to, rows, revenue, await BuildCustomersAsync(rows, withMoney, ct));
    }

    public async Task<TicketReport> GlobalReportAsync(DateTime? from, DateTime? to, CancellationToken ct = default)
    {
        EnsureGlobalAccess();
        // SuperAdmin bypasses the tenant filter → all companies (and holds every permission).
        var rows = await LoadRowsAsync(db.Tickets.AsQueryable(), from, to, ct);
        return Build(null, from, to, rows, BuildRevenue(rows, await CurrencyAsync(ct)),
            await BuildCustomersAsync(rows, withMoney: true, ct));
    }

    /// <summary>
    /// The same report as a PDF (spec §15 "rapor almak" = export, Faz 41). Deliberately built from the
    /// very same <see cref="CompanyReportAsync"/> / <see cref="GlobalReportAsync"/> result the screen
    /// shows: one code path, so the export and the dashboard can never disagree, and the money gate is
    /// enforced once instead of twice. The old ticket-level CSV was replaced, not kept alongside — it
    /// dumped raw GUIDs and carried no money, no customer and no totals.
    /// </summary>
    public async Task<byte[]> ExportPdfAsync(Guid? companyId, DateTime? from, DateTime? to, CancellationToken ct = default)
    {
        var report = companyId is { } id
            ? await CompanyReportAsync(id, from, to, ct)
            : await GlobalReportAsync(from, to, ct);

        var scopeName = companyId is { } cid
            ? await db.Companies.IgnoreQueryFilters()
                .Where(c => c.Id == cid).Select(c => c.Name).FirstOrDefaultAsync(ct)
            : null;
        var brand = await settings.GetValueAsync("brand.system_name", ct) is { Length: > 0 } b ? b : "Kanby";

        return ReportPdf.Render(report, scopeName, brand, await LocalNowAsync(ct));
    }

    /// <summary>
    /// "Generated at" in the operator's own timezone (<c>system.timezone</c>), not UTC. A printed
    /// report is read by a person in a place; stamping it three hours off and calling it the time is
    /// the kind of small lie that makes someone distrust every other number on the page.
    /// Falls back to UTC if the configured zone is unknown to the host — never fails the export.
    /// </summary>
    private async Task<DateTime> LocalNowAsync(CancellationToken ct)
    {
        var utc = DateTime.SpecifyKind(clock.UtcNow, DateTimeKind.Utc);
        var id = await settings.GetValueAsync("system.timezone", ct);
        if (string.IsNullOrWhiteSpace(id)) return utc;
        try { return TimeZoneInfo.ConvertTimeFromUtc(utc, TimeZoneInfo.FindSystemTimeZoneById(id)); }
        catch (TimeZoneNotFoundException) { return utc; }
        catch (InvalidTimeZoneException) { return utc; }
    }

    private async Task EnsureCompanyAccessAsync(Guid companyId, CancellationToken ct)
    {
        var userId = currentUser.UserId
            ?? throw new UnauthorizedException("auth.required", "Authentication required.");
        if (!currentUser.IsSuperAdmin &&
            !await permissions.HasPermissionAsync(userId, companyId, PermissionKeys.ReportCompany, ct))
            throw new ForbiddenException("report.forbidden", "You lack company report access.");
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

        // Join statuses (ignore their filter — global statuses have null CompanyId) to read the category,
        // and the opener (customers are not company members, so their User row is out of tenant scope —
        // the ticket set is already scoped, so the name lookup adds no reach).
        // LEFT join on the opener, not inner: an inner join would silently drop a ticket whose opener
        // row is gone (KVKK account deletion), and it would drop it from the TOTALS too — the headline
        // "toplam talep" would quietly shrink because of something that happened to a user record.
        // The assignee is joined the same way and for the same reason as the opener: "Personel yükü"
        // showed the first 8 hex digits of a GUID because the name simply was not in the payload, and
        // the SPA cannot look it up itself — the global report spans companies whose member lists the
        // caller may not be able to enumerate. LEFT join: an unassigned ticket (the biggest bar) and a
        // deleted staff account must both survive into the counts.
        var joined = from t in tickets
                     join s in db.TicketStatuses.IgnoreQueryFilters() on t.StatusId equals s.Id
                     join uj in db.Users.IgnoreQueryFilters() on t.OpenedById equals uj.Id into us
                     from u in us.DefaultIfEmpty()
                     join aj in db.Users.IgnoreQueryFilters() on t.AssignedToId equals aj.Id into asg
                     from a in asg.DefaultIfEmpty()
                     select new Row(t.CreatedAt, t.FirstResponseAt, t.ResolvedAt, t.ClosedAt,
                         s.Category, t.AssignedToId,
                         a == null ? null : a.FirstName + " " + a.LastName,
                         t.EstimatedValue, t.ActualValue,
                         t.CompanyId, t.OpenedById,
                         u == null ? null : u.FirstName + " " + u.LastName,
                         u == null ? null : u.Email,
                         u != null && u.IsSuperAdmin);
        return await joined.ToListAsync(ct);
    }

    /// <summary>
    /// Groups the rows by the person who opened them, keeping only real customers (Faz 41).
    /// <para>
    /// The staff exclusion is the whole point: an opener who holds a membership in that ticket's
    /// company is a colleague, not a customer, and counting them would put the team's own internal
    /// tickets in a report titled "customers". Membership is per company, so the check is on the
    /// (company, opener) PAIR — the same person can legitimately be staff at one company and a
    /// customer at another, and must appear only in the second.
    /// </para>
    /// </summary>
    private async Task<List<CustomerBreakdownItem>> BuildCustomersAsync(
        List<Row> rows, bool withMoney, CancellationToken ct)
    {
        if (rows.Count == 0) return [];

        var openerIds = rows.Select(r => r.OpenedById).Distinct().ToList();
        var companyIds = rows.Select(r => r.CompanyId).Distinct().ToList();
        var staffPairs = await db.Memberships.IgnoreQueryFilters()
            .Where(m => m.DeletedAt == null && openerIds.Contains(m.UserId) && companyIds.Contains(m.CompanyId))
            .Select(m => new { m.UserId, m.CompanyId })
            .ToListAsync(ct);
        var staff = staffPairs.Select(p => (p.UserId, p.CompanyId)).ToHashSet();

        return rows
            // A super admin has NO membership anywhere — the flag is the role (Faz 2) — so the
            // membership test alone would file them as a customer of every company whose ticket they
            // ever touched. Found by reading the first real PDF, not by a test.
            .Where(r => !r.OpenedByIsSuperAdmin)
            .Where(r => !staff.Contains((r.OpenedById, r.CompanyId)))
            .GroupBy(r => r.OpenedById)
            .Select(g =>
            {
                var resolution = g.Where(r => r.ResolvedAt is not null)
                    .Select(r => (r.ResolvedAt!.Value - r.CreatedAt).TotalHours).ToList();
                var firstResponse = g.Where(r => r.FirstResponseAt is not null)
                    .Select(r => (r.FirstResponseAt!.Value - r.CreatedAt).TotalHours).ToList();
                var won = g.Where(r => r.Category == StatusCategory.Closed).ToList();
                var open = g.Where(r => r.Category is not (StatusCategory.Closed or StatusCategory.Cancelled)).ToList();

                return new CustomerBreakdownItem(
                    g.Key,
                    // A deleted account still owned real tickets; naming it plainly keeps its history
                    // in the totals instead of hiding it behind a blank cell.
                    g.First().OpenedByName?.Trim() is { Length: > 0 } n ? n : "(silinmiş kullanıcı)",
                    g.First().OpenedByEmail ?? "—",
                    g.Count(), open.Count, won.Count,
                    g.Count(r => r.Category == StatusCategory.Cancelled),
                    // Unpriced tickets contribute nothing rather than zero — same rule as the revenue
                    // summary, so the two sections of the PDF can never disagree.
                    withMoney ? won.Sum(r => r.Value ?? 0m) : null,
                    withMoney ? open.Sum(r => r.Value ?? 0m) : null,
                    resolution.Count > 0 ? Math.Round(resolution.Average(), 2) : null,
                    resolution.Count > 0 ? Math.Round(resolution.Sum(), 2) : null,
                    firstResponse.Count > 0 ? Math.Round(firstResponse.Average(), 2) : null,
                    g.Min(r => r.CreatedAt), g.Max(r => r.CreatedAt));
            })
            // Busiest first: a report is read top-down and the people you spend most time on matter most.
            .OrderByDescending(c => c.TicketCount).ThenBy(c => c.Name)
            .ToList();
    }

    private static TicketReport Build(
        Guid? companyId, DateTime? from, DateTime? to, List<Row> rows, RevenueSummary? revenue,
        List<CustomerBreakdownItem> customers)
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
            // Name resolution mirrors the customer breakdown: a live account shows its name, an account
            // that was deleted is named as such rather than left blank — its open tickets are still real
            // work sitting on someone's plate. Unassigned (null id) is labelled by the UI.
            .Select(g => new StaffLoadItem(
                g.Key,
                g.Key is null ? null
                    : g.First().AssignedToName?.Trim() is { Length: > 0 } n ? n : "(silinmiş kullanıcı)",
                g.Count()))
            .OrderByDescending(s => s.OpenCount).ToList();

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
            firstResponse.Count, resolution.Count,
            staffLoad, trend, revenue, customers);
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
        //
        // Accuracy is how close the estimate landed, so it is capped at 100% and a miss costs the same
        // in either direction: 1 - (total absolute error / total estimated). Dividing actual by estimate
        // instead reported 110% for a 10% overrun — a worse forecast scoring above a perfect one.
        // Floored at 0 so an overrun past double the quote reads "0% isabet", not a negative percentage.
        var estimated = won.Where(r => r.ActualValue is not null && r.EstimatedValue is > 0).ToList();
        decimal? accuracy = null;
        if (estimated.Count > 0)
        {
            var quoted = estimated.Sum(r => r.EstimatedValue!.Value);
            var error = estimated.Sum(r => Math.Abs(r.ActualValue!.Value - r.EstimatedValue!.Value));
            accuracy = Math.Round(Math.Max(0m, 1m - error / quoted), 4);
        }

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

    // The ticket-level CSV writer lived here until Faz 41. Deleted rather than kept beside the PDF:
    // it exported bare GUIDs for assignee and category, carried no money and no customer, and nothing
    // in the UI pointed at it any more. Bring it back only if someone actually needs Excel input —
    // and then as a deliberate second format, not as the leftover it had become.
}
