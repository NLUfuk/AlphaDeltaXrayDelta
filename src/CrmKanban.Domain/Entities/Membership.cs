using CrmKanban.Domain.Common;
using CrmKanban.Domain.Enums;

namespace CrmKanban.Domain.Entities;

/// <summary>
/// User↔Company link with a role (spec §6, §11). Many-to-many: an admin may own several
/// companies; a user's company access is proven by Membership, never taken from the request
/// (spec §6). Only Admin/Personel are scoped to a company; SuperAdmin/Customer are not members.
/// </summary>
public sealed class Membership : Entity
{
    private Membership() { } // EF

    public Membership(Guid userId, Guid companyId, RoleType role)
    {
        if (role is not (RoleType.Admin or RoleType.Personel))
            throw new ArgumentException("Only Admin or Personel can be company members.", nameof(role));

        UserId = userId;
        CompanyId = companyId;
        Role = role;
    }

    public Guid UserId { get; private set; }
    public Guid CompanyId { get; private set; }
    public RoleType Role { get; private set; }

    public void ChangeRole(RoleType role)
    {
        if (role is not (RoleType.Admin or RoleType.Personel))
            throw new ArgumentException("Only Admin or Personel can be company members.", nameof(role));
        Role = role;
    }
}
