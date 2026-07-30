namespace CrmKanban.Application.Users;

/// <summary>Super admin creates an admin *account* (spec §9): the person sets a password via the invite
/// link, then opens their own company. No company is assigned here.</summary>
public sealed record CreateAdminRequest(string Email, string FirstName, string LastName);

public sealed record UserDto(Guid Id, string Email, string Name, bool IsSuperAdmin, bool CanCreateCompany, bool IsActive);
