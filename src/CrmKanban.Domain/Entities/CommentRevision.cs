using CrmKanban.Domain.Common;

namespace CrmKanban.Domain.Entities;

/// <summary>Edit history for a comment (spec §11, §18.2): the old body is preserved so a dispute has
/// evidence. Append-only.</summary>
public sealed class CommentRevision : Entity
{
    private CommentRevision() { } // EF

    public CommentRevision(Guid companyId, Guid commentId, string oldBody, Guid editedById)
    {
        CompanyId = companyId;
        CommentId = commentId;
        OldBody = oldBody;
        EditedById = editedById;
    }

    public Guid CompanyId { get; private set; }
    public Guid CommentId { get; private set; }
    public string OldBody { get; private set; } = null!;
    public Guid EditedById { get; private set; }
}
