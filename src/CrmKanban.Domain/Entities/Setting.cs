using CrmKanban.Domain.Common;

namespace CrmKanban.Domain.Entities;

/// <summary>
/// A single business parameter (spec §11, §13). The store is generic key/value/type/group so adding
/// a new setting is a DB row, not a code change (spec §18.3). Secrets and infra (DB/S3/SMTP/JWT) never
/// live here — those stay in file/env (§13). Value is a string; complex values are JSON. Type/Group are
/// hints for the super-admin UI, not logic — code reads a value by its stable Key.
/// </summary>
public sealed class Setting : Entity
{
    private Setting() { } // EF

    public Setting(string key, string value, string type, string group, Guid? updatedById = null, Guid? id = null)
    {
        if (id is { } fixedId) Id = fixedId; // deterministic Id for idempotent seeding
        Key = key;
        Value = value;
        Type = type;
        Group = group;
        UpdatedById = updatedById;
    }

    public string Key { get; private set; } = null!;
    public string Value { get; private set; } = null!;
    public string Type { get; private set; } = null!;
    public string Group { get; private set; } = null!;
    public Guid? UpdatedById { get; private set; }

    public void Update(string value, Guid? updatedById)
    {
        Value = value;
        UpdatedById = updatedById;
    }
}
