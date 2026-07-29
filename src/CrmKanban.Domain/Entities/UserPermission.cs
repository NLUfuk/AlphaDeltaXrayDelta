using CrmKanban.Domain.Common;
using CrmKanban.Domain.Enums;

namespace CrmKanban.Domain.Entities;

/// <summary>
/// User-level permission override on top of the role baseline (spec §7): a Grant adds a
/// permission the role lacks, a Deny removes one the role has. <see cref="CompanyId"/> scopes
/// the override to one company; null = global. Deny wins over Grant when resolving (see
/// permission resolution in Application, Faz 2).
/// </summary>
public sealed class UserPermission : Entity
{
    private UserPermission() { } // EF

    public UserPermission(Guid userId, Guid permissionId, UserPermissionType type, Guid? companyId)
    {
        UserId = userId;
        PermissionId = permissionId;
        Type = type;
        CompanyId = companyId;
    }

    public Guid UserId { get; private set; }
    public Guid PermissionId { get; private set; }
    public UserPermissionType Type { get; private set; }

    /// <summary>Company scope of this override; null = applies globally to the user.</summary>
    public Guid? CompanyId { get; private set; }
}
