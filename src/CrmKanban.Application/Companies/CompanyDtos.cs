namespace CrmKanban.Application.Companies;

/// <summary>Create a company (spec §8/§18.8). OwnerAdminId is honored only for a super admin creating on
/// an admin's behalf; an admin caller always owns the company they create. The contact fields are
/// optional — a company only needs a name and a slug to work.</summary>
public sealed record CreateCompanyRequest(
    string Name, string Slug, Guid? OwnerAdminId = null,
    string? Phone = null, string? Email = null, string? Website = null, string? Address = null);

/// <summary>Edit a company's name and contact card. The slug is absent on purpose: customers already
/// hold <c>/c/{slug}</c> links, so it is fixed at creation (see <c>Company.UpdateInfo</c>).</summary>
public sealed record UpdateCompanyRequest(
    string Name, string? Phone = null, string? Email = null, string? Website = null, string? Address = null);

/// <summary>Delete confirmation (spec §18.20 keeps the rows — this is a soft delete). The caller must echo
/// the company name back; it is the second gate after the UI's own confirm step.</summary>
public sealed record DeleteCompanyRequest(string ConfirmName);

public sealed record CompanyDto(
    Guid Id, string Name, string Slug, Guid OwnerAdminId, bool IsActive, bool IsArchived, string TicketNumberPrefix,
    string? Phone, string? Email, string? Website, string? Address);

/// <summary>A company member (staff), for assignment and permission-target pickers.</summary>
public sealed record MemberDto(Guid UserId, string Email, string Name, int Role);
