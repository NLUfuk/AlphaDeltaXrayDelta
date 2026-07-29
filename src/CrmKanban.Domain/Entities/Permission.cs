using CrmKanban.Domain.Common;

namespace CrmKanban.Domain.Entities;

/// <summary>A permission key (spec §7), e.g. "ticket.assign". Seeded from PermissionKeys.</summary>
public sealed class Permission : Entity
{
    private Permission() { } // EF

    public Permission(string key)
    {
        Key = key;
        Group = key.Contains('.') ? key[..key.IndexOf('.')] : key;
    }

    public string Key { get; private set; } = null!;

    /// <summary>Prefix group ("ticket", "report", ...) for the settings UI grouping.</summary>
    public string Group { get; private set; } = null!;
}
