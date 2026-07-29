using CrmKanban.Domain.Common;
using CrmKanban.Domain.Enums;

namespace CrmKanban.Domain.Entities;

/// <summary>
/// Role→permission baseline (spec §7). Roles are a fixed set (RoleType enum), so there is no
/// separate Roles table; the editable seam is the matrix of RolePermission rows — the "rol-permission
/// matrisi" the super-admin edits from Settings (spec §13). Adding a role IS a code change (enum);
/// changing what a role can do is data.
/// </summary>
public sealed class RolePermission : Entity
{
    private RolePermission() { } // EF

    public RolePermission(RoleType role, Guid permissionId)
    {
        Role = role;
        PermissionId = permissionId;
    }

    public RoleType Role { get; private set; }
    public Guid PermissionId { get; private set; }
}
