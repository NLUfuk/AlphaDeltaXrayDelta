using CrmKanban.Application.Files;
using CrmKanban.Domain.Enums;

namespace CrmKanban.Application.Tickets;

public sealed record CreateTicketRequest(
    Guid CompanyId, string Title, string Body, Priority Priority = Priority.Normal, Guid? CategoryId = null);

/// <summary>A logged-in customer opening a request to a company they picked from the portal (spec §18.5).
/// No priority/category — those are staff-set; the customer only chooses the company and writes.</summary>
public sealed record CustomerCreateTicketRequest(Guid CompanyId, string Title, string Body);

public sealed record EditTicketRequest(string Title, string Body);

public sealed record AssignTicketRequest(Guid? AssigneeUserId);

public sealed record ChangeStatusRequest(Guid TargetStatusId);

public sealed record SetPriorityRequest(Priority Priority);

public sealed record AddCommentRequest(string Body, bool IsInternal, IReadOnlyList<AttachmentDescriptor>? Attachments = null);

public sealed record EditCommentRequest(string Body);

public sealed record TicketListQuery(
    int Page = 1,
    int PageSize = 20,
    string? Search = null,
    Guid? StatusId = null,
    Guid? CategoryId = null,
    Guid? AssignedToId = null,
    Priority? Priority = null,
    StatusCategory? Category = null);

public sealed record TicketListItem(
    Guid Id, string Number, string Title, Guid StatusId, string StatusName, StatusCategory Category,
    string StatusColor, Priority Priority, Guid? AssignedToId, Guid? CategoryId, DateTime CreatedAt);

public sealed record CommentDto(
    Guid Id, Guid AuthorId, string Body, bool IsInternal, bool IsEdited, DateTime CreatedAt, DateTime? EditedAt);

public sealed record TicketDetail(
    Guid Id, string Number, Guid CompanyId, string Title, string Body,
    Guid StatusId, string StatusName, StatusCategory Category, Priority Priority,
    Guid OpenedById, Guid? AssignedToId, Guid? CategoryId,
    DateTime? FirstResponseAt, DateTime? ResolvedAt, DateTime? ClosedAt,
    DateTime CreatedAt, IReadOnlyList<CommentDto> Comments, IReadOnlyList<AttachmentDto> Attachments,
    IReadOnlyList<CustomFieldValue> CustomFields);

/// <summary>A value captured from a configurable public-form field (spec §4.6), denormalized as label+value.</summary>
public sealed record CustomFieldValue(string Label, string Value);

public sealed record StatusDto(Guid Id, string Name, StatusCategory Category, string Color, int Order, bool IsTerminal);

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);

public sealed record KanbanColumn(Guid StatusId, string StatusName, StatusCategory Category, string Color, int Order, IReadOnlyList<TicketListItem> Tickets);
