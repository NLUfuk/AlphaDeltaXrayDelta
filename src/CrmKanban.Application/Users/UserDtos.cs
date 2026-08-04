namespace CrmKanban.Application.Users;

/// <summary>Super admin creates an admin *account* (spec §9): the person sets a password via the invite
/// link, then opens their own company. No company is assigned here.</summary>
public sealed record CreateAdminRequest(string Email, string FirstName, string LastName);

/// <summary><see cref="Companies"/> = staff memberships (role-bearing). <see cref="CustomerOf"/> = the
/// companies a customer actually works with, derived from their tickets — customers have no membership,
/// so without this they would all pile into one anonymous "no company" bucket in the UI.</summary>
public sealed record UserDto(
    Guid Id, string Email, string Name, bool IsSuperAdmin, bool CanCreateCompany, bool IsActive,
    IReadOnlyList<UserCompanyDto> Companies,
    IReadOnlyList<string> CustomerOf);

/// <summary>A company a user belongs to (for grouping the user list), with their role in it.</summary>
public sealed record UserCompanyDto(Guid CompanyId, string CompanyName, int Role);
