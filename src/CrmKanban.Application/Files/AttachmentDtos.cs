namespace CrmKanban.Application.Files;

/// <summary>What the client wants to upload; the server validates type/size before presigning.</summary>
public sealed record UploadUrlRequest(string FileName, string ContentType, long Size);

/// <summary>A presigned PUT: the client uploads bytes straight to storage, then submits the Key.</summary>
public sealed record UploadUrlResult(string Key, string Url, DateTime ExpiresAt);

/// <summary>A previously-uploaded object the caller now links to a ticket/comment. Re-validated on link.</summary>
public sealed record AttachmentDescriptor(string Key, string FileName, string ContentType, long Size);

/// <summary>An attachment as shown to a caller — Url is a short-lived presigned GET (spec §12).</summary>
public sealed record AttachmentDto(Guid Id, string FileName, string ContentType, long Size, string Url);
