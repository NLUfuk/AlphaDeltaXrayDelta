using CrmKanban.Domain.Enums;

namespace CrmKanban.Application.Tickets;

/// <summary>A kanban column as the admin manager sees it. Editable is false for the shared global
/// defaults (a company must fork them before renaming/reordering).</summary>
public sealed record StatusColumnDto(
    Guid Id, string Name, StatusCategory Category, string Color, int Order, bool IsTerminal, bool Editable);

/// <summary>Add a column at a position in the chain. IsTerminal is derived from the category
/// (Closed/Cancelled = terminal), never trusted from the client.</summary>
public sealed record CreateStatusRequest(string Name, StatusCategory Category, string Color, int Position);

public sealed record UpdateStatusRequest(string? Name, string? Color);

public sealed record ReorderStatusesRequest(IReadOnlyList<Guid> OrderedStatusIds);
