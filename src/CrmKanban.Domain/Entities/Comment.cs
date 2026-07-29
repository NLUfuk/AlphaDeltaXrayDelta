using CrmKanban.Domain.Common;
using CrmKanban.Domain.Enums;

namespace CrmKanban.Domain.Entities;

/// <summary>
/// A ticket comment (spec §11, §12). <see cref="IsInternal"/> notes are staff-only and must NEVER
/// surface to the customer — the number-one privacy risk (spec §14, §20). Comments are a record: a
/// customer cannot edit/delete their own (§18.12); staff edits are tracked in CommentRevisions and
/// flagged with an "edited" marker (<see cref="EditedAt"/>), deletes are soft (§18.2).
/// </summary>
public sealed class Comment : Entity
{
    private Comment() { } // EF

    public Comment(Guid companyId, Guid ticketId, Guid authorId, string body, bool isInternal, CommentSource source = CommentSource.Web)
    {
        CompanyId = companyId;
        TicketId = ticketId;
        AuthorId = authorId;
        Body = body;
        IsInternal = isInternal;
        Source = source;
    }

    // Denormalized from the ticket so the tenant query filter is a direct predicate (spec §6).
    public Guid CompanyId { get; private set; }
    public Guid TicketId { get; private set; }
    public Guid AuthorId { get; private set; }
    public string Body { get; private set; } = null!;
    public bool IsInternal { get; private set; }
    public DateTime? EditedAt { get; private set; }
    public CommentSource Source { get; private set; }

    public bool IsEdited => EditedAt is not null;

    /// <summary>Returns the pre-edit body so the caller can persist a CommentRevision (spec §18.2).</summary>
    public string Edit(string newBody, DateTime now)
    {
        var old = Body;
        Body = newBody;
        EditedAt = now;
        return old;
    }
}
