namespace CrmKanban.Application.Settings;

/// <summary>A settings row as shown in the super-admin UI (spec §13). Value is a string; complex values are JSON.</summary>
public sealed record SettingDto(string Key, string Value, string Type, string Group, DateTime? UpdatedAt);

/// <summary>Edit an existing setting's value (spec §13). Key comes from the route; the v1 set is seeded,
/// so this updates a known key rather than creating arbitrary ones (§18.3 — new settings ship as seed rows).</summary>
public sealed record UpdateSettingRequest(string Value);

/// <summary>The branding an unauthenticated visitor is allowed to see: system name, accent colour, logo.
/// Exactly the three values the public form has always returned — this is not a new disclosure, it is the
/// same triple behind a URL that does not need a company slug, so the sign-in screen and the app shell
/// can render the operator's brand without opening the whole <c>/settings</c> surface to every user.</summary>
public sealed record BrandDto(string SystemName, string PrimaryColor, string? LogoUrl);
