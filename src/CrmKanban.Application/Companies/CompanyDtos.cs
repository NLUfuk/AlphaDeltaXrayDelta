namespace CrmKanban.Application.Companies;

/// <summary>Create a company (spec §8/§18.8). OwnerAdminId is honored only for a super admin creating on
/// an admin's behalf; an admin caller always owns the company they create.</summary>
public sealed record CreateCompanyRequest(string Name, string Slug, Guid? OwnerAdminId = null);

public sealed record CompanyDto(Guid Id, string Name, string Slug, Guid OwnerAdminId, bool IsActive, bool IsArchived, string TicketNumberPrefix);

/// <summary>A company member (staff), for assignment and permission-target pickers.</summary>
public sealed record MemberDto(Guid UserId, string Email, string Name, int Role);
