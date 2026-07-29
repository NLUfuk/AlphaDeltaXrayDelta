namespace CrmKanban.Domain.Common;

/// <summary>
/// Thrown when a domain invariant or state-machine rule is violated (spec §12).
/// Carries a stable <see cref="Code"/> so the API can map it to the shared error
/// envelope and the frontend to a catalog message (spec §4.3) — never a raw string.
/// </summary>
public sealed class DomainException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}
