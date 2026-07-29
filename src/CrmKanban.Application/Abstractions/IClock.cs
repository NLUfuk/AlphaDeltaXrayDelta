namespace CrmKanban.Application.Abstractions;

/// <summary>Abstracts "now" so time-dependent logic and the audit interceptor stay testable.</summary>
public interface IClock
{
    DateTime UtcNow { get; }
}
