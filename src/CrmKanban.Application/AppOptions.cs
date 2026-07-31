namespace CrmKanban.Application;

/// <summary>App-level, non-secret settings. <see cref="PublicBaseUrl"/> is the SPA origin used to
/// build absolute links in outbound email (account activation / staff invite). Bound from the "App"
/// config section in Infrastructure DI; overridable per environment (e.g. App__PublicBaseUrl).</summary>
public sealed class AppOptions
{
    public string PublicBaseUrl { get; init; } = "http://localhost:8080";
}
