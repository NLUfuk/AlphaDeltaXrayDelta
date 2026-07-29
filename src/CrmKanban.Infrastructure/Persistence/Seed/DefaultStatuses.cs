using CrmKanban.Domain.Enums;

namespace CrmKanban.Infrastructure.Persistence.Seed;

/// <summary>
/// The global default status set and transition graph (spec §12, §18.9). Fixed GUIDs make the
/// seed idempotent (upsert by Id) and let StatusTransitions reference statuses deterministically.
/// v1 ships only this global set (CompanyId null); per-company overrides are schema-ready but off.
/// </summary>
public static class DefaultStatuses
{
    public sealed record StatusDef(Guid Id, string Name, StatusCategory Category, string Color, int Order, bool IsTerminal);

    public static readonly StatusDef New = new(
        Guid.Parse("11111111-0000-0000-0000-000000000001"), "Yeni", StatusCategory.Open, "#3b82f6", 0, false);
    public static readonly StatusDef InProgress = new(
        Guid.Parse("11111111-0000-0000-0000-000000000002"), "İşlemde", StatusCategory.Pending, "#f59e0b", 1, false);
    public static readonly StatusDef Answered = new(
        Guid.Parse("11111111-0000-0000-0000-000000000003"), "Yanıtlandı", StatusCategory.Answered, "#8b5cf6", 2, false);
    public static readonly StatusDef Waiting = new(
        Guid.Parse("11111111-0000-0000-0000-000000000004"), "Müşteri Bekleniyor", StatusCategory.Waiting, "#6b7280", 3, false);
    public static readonly StatusDef Completed = new(
        Guid.Parse("11111111-0000-0000-0000-000000000005"), "Tamamlandı", StatusCategory.Closed, "#10b981", 4, true);
    public static readonly StatusDef Cancelled = new(
        Guid.Parse("11111111-0000-0000-0000-000000000006"), "İptal", StatusCategory.Cancelled, "#ef4444", 5, true);

    public static readonly IReadOnlyList<StatusDef> All = [New, InProgress, Answered, Waiting, Completed, Cancelled];

    /// <summary>Non-terminal statuses can move to any other non-terminal one plus the two terminals.</summary>
    public static IEnumerable<(Guid From, Guid To)> Transitions()
    {
        var nonTerminal = new[] { New, InProgress, Answered, Waiting };
        var targets = new[] { InProgress, Answered, Waiting, Completed, Cancelled };
        foreach (var from in nonTerminal)
            foreach (var to in targets)
                if (from.Id != to.Id)
                    yield return (from.Id, to.Id);
    }
}
