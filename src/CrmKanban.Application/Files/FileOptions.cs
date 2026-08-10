namespace CrmKanban.Application.Files;

/// <summary>
/// File upload limits (spec §12/§18.13). The live values come from the DB Settings store
/// (file.max_size_mb / file.max_per_comment / file.allowed_types) — these are the FALLBACKS
/// <see cref="AttachmentService"/> uses when a row is missing or malformed, so a broken settings table
/// degrades to documented behaviour instead of blocking every upload. Keep them equal to what
/// DefaultSettings seeds. PresignExpiryMinutes stays config-only: it is a storage detail, not a
/// business parameter. The allow-list is a server-side gate — the client is never trusted.
/// </summary>
public sealed class FileOptions
{
    public long MaxSizeBytes { get; init; } = 10 * 1024 * 1024; // 10 MB
    public int MaxPerAttachTarget { get; init; } = 5;           // per comment / per form (spec §18.13)
    public int PresignExpiryMinutes { get; init; } = 10;

    public string[] AllowedContentTypes { get; init; } =
    [
        "image/png", "image/jpeg", "image/gif", "image/webp",
        "application/pdf",
        "application/msword",
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        "application/vnd.ms-excel",
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
    ];
}
