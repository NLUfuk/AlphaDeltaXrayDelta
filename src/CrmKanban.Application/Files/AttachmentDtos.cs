namespace CrmKanban.Application.Files;

/// <summary>What the client wants to upload; the server validates type/size before presigning.</summary>
public sealed record UploadUrlRequest(string FileName, string ContentType, long Size);

/// <summary>A presigned PUT: the client uploads bytes straight to storage, then submits the Key.</summary>
public sealed record UploadUrlResult(string Key, string Url, DateTime ExpiresAt);

/// <summary>A previously-uploaded object the caller now links to a ticket/comment. Re-validated on link.</summary>
public sealed record AttachmentDescriptor(string Key, string FileName, string ContentType, long Size);

/// <summary>An attachment as shown to a caller — Url is the API's proxy download path (spec §12).
/// <para><paramref name="CommentId"/>, <paramref name="UploadedByName"/> and <paramref name="CreatedAt"/>
/// exist so the ticket screen can show a file where it actually happened — inside the message it was
/// sent with, in the order it arrived — instead of in a separate "Ekler" box that says nothing about
/// who added it or when. Null CommentId = attached to the ticket itself, not to a message.</para></summary>
public sealed record AttachmentDto(
    Guid Id, string FileName, string ContentType, long Size, string Url,
    Guid? CommentId = null, string? UploadedByName = null, DateTime CreatedAt = default);

/// <summary>A file streamed back through the API for download. The caller owns and disposes Content.</summary>
public sealed record AttachmentContent(Stream Content, string ContentType, string FileName);
