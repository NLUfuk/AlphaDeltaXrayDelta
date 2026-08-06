using CrmKanban.Domain.Enums;

namespace CrmKanban.Application.Reports;

/// <summary>
/// A ticket report (spec §15). Company-scoped for admins, global for super admin. Metrics branch on the
/// status <see cref="StatusCategory"/>, never on the display name (spec §4.3). Times are in hours.
/// </summary>
public sealed record TicketReport(
    Guid? CompanyId,
    DateTime? From,
    DateTime? To,
    int TotalTickets,
    IReadOnlyList<StatusCategoryCount> ByStatusCategory,
    double? AvgFirstResponseHours,
    double? AvgResolutionHours,
    IReadOnlyList<StaffLoadItem> StaffLoad,
    IReadOnlyList<CategoryCount> ByCategory,
    IReadOnlyList<TrendPoint> Trend,
    RevenueSummary? Revenue);

/// <summary>
/// The money view of the same tickets (Faz 39). Null on the report when the caller lacks
/// <c>ticket.value</c> — the figures are withheld server-side, not hidden in the UI.
/// <para>
/// Won/lost is read off the status <see cref="StatusCategory"/>, never stored separately: Closed =
/// won, Cancelled = lost, everything else is still open pipeline. A second "sales outcome" field
/// would be a second truth that drifts the moment someone reopens or re-closes a ticket.
/// </para>
/// </summary>
public sealed record RevenueSummary(
    /// <summary>The currency every amount here is in (Settings <c>finance.currency</c>). Travels with
    /// the figures rather than being fetched separately: /settings is super-admin only, and a money
    /// number without its unit is not a number.</summary>
    string Currency,

    decimal WonTotal, int WonCount,
    decimal LostTotal, int LostCount,
    decimal OpenTotal, int OpenCount,

    /// <summary>Tickets with no amount at all. Reported rather than counted as zero: "not priced yet"
    /// and "worth nothing" are different facts, and averaging the second into forecasts is a lie.</summary>
    int UnpricedCount,

    /// <summary>Won ÷ (won + lost). By count and by value, because they tell different stories — many
    /// small wins and one lost giant is a good month by count and a bad one by value. Null when
    /// nothing has closed yet (0/0 is undefined, not zero).</summary>
    double? WinRateByCount,
    double? WinRateByValue,

    /// <summary>Realised ÷ estimated across won tickets that carry both figures. 1.0 = estimates land;
    /// below 1 = habitual over-promising. Null when no won ticket has both numbers.</summary>
    decimal? ForecastAccuracy,

    IReadOnlyList<RevenueTrendPoint> Trend);

/// <summary>Won and lost amounts per month, keyed by the first day of the month.</summary>
public sealed record RevenueTrendPoint(DateOnly Month, decimal Won, decimal Lost);

public sealed record StatusCategoryCount(StatusCategory Category, int Count);

/// <summary>Currently-open tickets per assignee (null = unassigned).</summary>
public sealed record StaffLoadItem(Guid? AssignedToId, int OpenCount);

public sealed record CategoryCount(Guid? CategoryId, int Count);

public sealed record TrendPoint(DateOnly Date, int Opened, int Closed);
