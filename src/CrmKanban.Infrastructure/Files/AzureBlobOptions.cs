namespace CrmKanban.Infrastructure.Files;

/// <summary>
/// Azure Blob Storage connection (Microsoft object storage). Secret/infra — the connection string
/// lives in env/user-secrets, never the DB or UI (config split, spec §13). Bound when
/// Files:Provider is "azure"; the container is created private on first use.
/// </summary>
public sealed class AzureBlobOptions
{
    public const string SectionName = "AzureBlob";

    public string ConnectionString { get; init; } = "";
    public string ContainerName { get; init; } = "attachments";
}
