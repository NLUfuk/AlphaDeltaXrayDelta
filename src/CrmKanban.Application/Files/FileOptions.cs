namespace CrmKanban.Application.Files;

/// <summary>
/// File upload limits (spec §12/§18.13). v1 defaults: image + PDF + Office, 10 MB, 5 per comment.
/// Config-bound ("Files"); moves into the DB Settings table in Faz 6. The allow-list is a
/// server-side gate — the client is never trusted to enforce type or size.
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
