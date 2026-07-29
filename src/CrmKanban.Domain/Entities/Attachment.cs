using CrmKanban.Domain.Common;

namespace CrmKanban.Domain.Entities;

/// <summary>
/// A file stored in S3-compatible object storage (spec §11, §12). The bucket is private; the row
/// holds only the object key — bytes are reached through short-lived presigned URLs, never a public
/// URL. Attached to a ticket directly (first form) or to a comment (CommentId set). CompanyId is
/// denormalized so the tenant query filter is a direct predicate, like <see cref="Comment"/>.
/// </summary>
public sealed class Attachment : Entity
{
    private Attachment() { } // EF

    public Attachment(Guid companyId, Guid ticketId, Guid? commentId, string s3Key,
        string fileName, string contentType, long size, Guid uploadedById)
    {
        CompanyId = companyId;
        TicketId = ticketId;
        CommentId = commentId;
        S3Key = s3Key;
        FileName = fileName;
        ContentType = contentType;
        Size = size;
        UploadedById = uploadedById;
    }

    public Guid CompanyId { get; private set; }
    public Guid TicketId { get; private set; }
    public Guid? CommentId { get; private set; }
    public string S3Key { get; private set; } = null!;
    public string FileName { get; private set; } = null!;
    public string ContentType { get; private set; } = null!;
    public long Size { get; private set; }
    public Guid UploadedById { get; private set; }
}
