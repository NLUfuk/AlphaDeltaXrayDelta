namespace CrmKanban.Application.Companies;

/// <summary>Create a company (spec §8/§18.8). OwnerAdminId is honored only for a super admin creating on
/// an admin's behalf; an admin caller always owns the company they create.</summary>
public sealed record CreateCompanyRequest(string Name, string Slug, Guid? OwnerAdminId = null);

/// <summary>Delete confirmation (spec §18.20 keeps the rows — this is a soft delete). The caller must echo
/// the company name back; it is the second gate after the UI's own confirm step.</summary>
public sealed record DeleteCompanyRequest(string ConfirmName);

public sealed record CompanyDto(Guid Id, string Name, string Slug, Guid OwnerAdminId, bool IsActive, bool IsArchived, string TicketNumberPrefix);

/// <summary>A company member (staff), for assignment and permission-target pickers.</summary>
public sealed record MemberDto(Guid UserId, string Email, string Name, int Role);
