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
    IReadOnlyList<TrendPoint> Trend);

public sealed record StatusCategoryCount(StatusCategory Category, int Count);

/// <summary>Currently-open tickets per assignee (null = unassigned).</summary>
public sealed record StaffLoadItem(Guid? AssignedToId, int OpenCount);

public sealed record CategoryCount(Guid? CategoryId, int Count);

public sealed record TrendPoint(DateOnly Date, int Opened, int Closed);
