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
    RevenueSummary? Revenue,
    IReadOnlyList<CustomerBreakdownItem> Customers);

/// <summary>
/// Per-customer view: who was dealt with, how much of it, for how long, and what it was worth
/// (Faz 41 — the PDF report's core table).
/// <para>
/// "Customer" is the ticket's opener WITHOUT a membership in that ticket's company — the same
/// definition <c>TicketAuthorizationService</c> uses to decide access. Membership is per company, so
/// the same person can be staff at one company and a customer at another; the exclusion is applied
/// per (company, opener) pair, never per user. A staff-opened ticket therefore never invents a
/// customer out of a colleague.
/// </para>
/// </summary>
public sealed record CustomerBreakdownItem(
    Guid CustomerId,
    string Name,
    string Email,

    int TicketCount, int OpenCount, int WonCount, int LostCount,

    /// <summary>Money for this customer, both null when the caller lacks <c>ticket.value</c> — the
    /// figures are withheld here exactly as they are on <see cref="RevenueSummary"/>, so a report
    /// export can never become the back door to the order book.</summary>
    decimal? WonTotal, decimal? OpenTotal,

    /// <summary>ELAPSED hours from open to resolution, averaged and summed over this customer's
    /// resolved tickets. This is calendar time, not worked time — the system tracks no timesheets,
    /// and calling it "worked hours" would invent a number nobody measured. Null when nothing of
    /// theirs has been resolved yet.</summary>
    double? AvgResolutionHours, double? TotalResolutionHours,

    /// <summary>Hours to the first staff reply, averaged. How fast this customer gets answered.</summary>
    double? AvgFirstResponseHours,

    DateTime FirstTicketAt, DateTime LastTicketAt);

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
