namespace CrmKanban.Infrastructure.Files;

/// <summary>
/// Local-disk storage location (used when Files:Provider is "local"). Infra config — env, not DB/UI.
/// RootPath defaults to App_Data/uploads under the content root: a non-served folder (IIS denies
/// App_Data), so files are reachable only through the authorized API proxy, never a public URL.
/// </summary>
public sealed class LocalStorageOptions
{
    public const string SectionName = "LocalStorage";

    public string RootPath { get; init; } = "";
}
