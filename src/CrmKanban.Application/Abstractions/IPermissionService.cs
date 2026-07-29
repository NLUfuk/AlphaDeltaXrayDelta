namespace CrmKanban.Application.Abstractions;

/// <summary>
/// Resolves a user's effective permissions within a company (spec §7): role baseline (RolePermissions)
/// ± user overrides (UserPermissions), Deny wins. This is stage (a) of the two-stage check — "does the
/// caller hold this permission?"; stage (b), record scope, is the DbContext tenant filter. SuperAdmin
/// short-circuits to "has everything".
/// </summary>
public interface IPermissionService
{
    Task<IReadOnlySet<string>> GetPermissionsAsync(Guid userId, Guid companyId, CancellationToken ct = default);

    Task<bool> HasPermissionAsync(Guid userId, Guid companyId, string permissionKey, CancellationToken ct = default);
}
