using CrmKanban.Domain.Enums;

namespace CrmKanban.Domain.Tickets;

/// <summary>
/// Who is attempting a status change and what they may do. The state machine judges the
/// transition's legality by <b>category</b> and <b>permission</b> only; record ownership and
/// tenant scope are enforced at the data layer (spec §7), not here.
/// </summary>
public readonly record struct StatusChangeActor(RoleType Role, IReadOnlySet<string> Permissions)
{
    private static readonly IReadOnlySet<string> None = new HashSet<string>();

    public static StatusChangeActor Customer() => new(RoleType.Customer, None);

    public static StatusChangeActor Staff(RoleType role, IEnumerable<string> permissions) =>
        new(role, permissions as IReadOnlySet<string> ?? permissions.ToHashSet());
}
