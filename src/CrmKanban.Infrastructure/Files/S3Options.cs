namespace CrmKanban.Infrastructure.Files;

/// <summary>
/// S3-compatible storage connection (spec §3, §12). Secrets/infra — lives in env/user-secrets, never
/// the DB or UI (config split, spec §13). ServiceUrl + ForcePathStyle target MinIO in dev; leave
/// ServiceUrl empty and set Region for AWS S3. Same code, different config.
/// </summary>
public sealed class S3Options
{
    public const string SectionName = "S3";

    public string? ServiceUrl { get; init; }   // e.g. http://localhost:9000 for MinIO; null = AWS
    public string Region { get; init; } = "us-east-1";
    public string AccessKey { get; init; } = "";
    public string SecretKey { get; init; } = "";
    public string BucketName { get; init; } = "crm-kanban";
    public bool ForcePathStyle { get; init; } = true; // MinIO requires path-style
}
