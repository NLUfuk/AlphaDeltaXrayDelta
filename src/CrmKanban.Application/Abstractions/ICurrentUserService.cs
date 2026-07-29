namespace CrmKanban.Application.Abstractions;

/// <summary>
/// The authenticated caller, sourced from the JWT (spec §6). The DbContext's global query filter
/// reads <see cref="IsSuperAdmin"/> and <see cref="CompanyIds"/> to enforce tenant isolation at the
/// DATA layer — the number-one leak in these systems (spec §7, §20). SuperAdmin bypasses the filter.
/// Permissions are NOT here: they are company-dependent, resolved per company via
/// <see cref="IPermissionService"/>.
/// </summary>
public interface ICurrentUserService
{
    Guid? UserId { get; }
    bool IsAuthenticated { get; }
    bool IsSuperAdmin { get; }

    /// <summary>Companies the caller may access, proven by Membership — never taken from the request.</summary>
    IReadOnlyCollection<Guid> CompanyIds { get; }
}
