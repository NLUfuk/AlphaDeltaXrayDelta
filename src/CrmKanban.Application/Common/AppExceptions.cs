namespace CrmKanban.Application.Common;

/// <summary>Base for application-level failures that carry a stable code for the API error envelope (spec §4.3).</summary>
public abstract class AppException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}

/// <summary>Caller is authenticated but not allowed to do this / see this record (spec §7 scope).</summary>
public sealed class ForbiddenException(string code, string message) : AppException(code, message);

/// <summary>Requested resource does not exist or is outside the caller's scope (looks the same to the caller).</summary>
public sealed class NotFoundException(string code, string message) : AppException(code, message);

/// <summary>Request conflicts with current state (duplicate, already used, etc.).</summary>
public sealed class ConflictException(string code, string message) : AppException(code, message);

/// <summary>Authentication failed (bad credentials, expired/invalid token).</summary>
public sealed class UnauthorizedException(string code, string message) : AppException(code, message);

/// <summary>Input violates a server-side rule (bad file type/size, missing consent, …) → 400.</summary>
public sealed class BadRequestException(string code, string message) : AppException(code, message);
