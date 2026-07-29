using CrmKanban.Domain.Enums;

namespace CrmKanban.Application.Authorization;

/// <summary>A user-level permission override, already loaded from UserPermissions (spec §7).</summary>
public readonly record struct PermissionOverride(string Key, UserPermissionType Type, Guid? CompanyId);

/// <summary>
/// Resolves a user's effective permissions within one company (spec §7): the role baseline plus
/// user Grants minus user Denies, where <b>Deny always wins</b> over Grant. Only overrides scoped
/// to this company (or global, CompanyId null) apply. Pure and side-effect free — the tested core.
/// </summary>
public static class PermissionResolver
{
    public static IReadOnlySet<string> Resolve(
        IEnumerable<string> roleBaseline,
        IEnumerable<PermissionOverride> overrides,
        Guid companyId)
    {
        var effective = new HashSet<string>(roleBaseline, StringComparer.Ordinal);

        var applicable = overrides.Where(o => o.CompanyId is null || o.CompanyId == companyId).ToList();
        foreach (var grant in applicable.Where(o => o.Type == UserPermissionType.Grant))
            effective.Add(grant.Key);

        // Deny wins: apply after grants so a Grant+Deny on the same key resolves to denied.
        foreach (var deny in applicable.Where(o => o.Type == UserPermissionType.Deny))
            effective.Remove(deny.Key);

        return effective;
    }
}
