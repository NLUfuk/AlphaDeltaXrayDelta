namespace CrmKanban.Application.Notifications;

/// <summary>
/// Notification pipeline knobs (spec §14). Business-ish params; config-bound ("Notifications"), move
/// to the DB Settings table in Faz 6. PollSeconds is how often the worker drains events + queue;
/// MaxAttempts before an email dead-letters.
/// </summary>
public sealed class NotificationOptions
{
    public int PollSeconds { get; init; } = 10;
    public int BatchSize { get; init; } = 100;
    public int MaxAttempts { get; init; } = 5;
}
