using CrmKanban.Domain.Enums;

namespace CrmKanban.Application.Auth;

public sealed record LoginRequest(string Email, string Password);

/// <summary>Self-service customer registration (spec §18.5). The password is set later, on the emailed
/// activation link (reuses the invite/accept flow) — so only identity + name are collected here.</summary>
public sealed record RegisterRequest(string Email, string FirstName, string LastName);

public sealed record RefreshRequest(string RefreshToken);

public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);

public sealed record AcceptInviteRequest(string Token, string NewPassword);

public sealed record InviteUserRequest(string Email, string FirstName, string LastName, Guid CompanyId, RoleType Role);

public sealed record AssignPermissionRequest(Guid UserId, Guid CompanyId, string PermissionKey, UserPermissionType Type);

public sealed record CompanyMembershipInfo(Guid CompanyId, RoleType Role);

public sealed record UserInfo(
    Guid Id,
    string Email,
    string Name,
    bool IsSuperAdmin,
    bool MustChangePassword,
    IReadOnlyCollection<CompanyMembershipInfo> Companies);

public sealed record AuthResult(
    string AccessToken,
    DateTime AccessTokenExpiresAt,
    string RefreshToken,
    UserInfo User);

/// <summary>Result of inviting a user. RawToken feeds the email link (Faz 5); until then it is returned
/// to the caller/logged so the invite flow is testable end-to-end.</summary>
public sealed record InviteResult(Guid UserId, string RawToken, DateTime ExpiresAt);
