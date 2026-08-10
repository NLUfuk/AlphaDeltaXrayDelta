namespace CrmKanban.Application.Tickets;

/// <summary>
/// Ticket knobs that are NOT business parameters. The page-size cap protects the server from an
/// oversized query — it is a resource guard, so it stays in config where a web form cannot widen it
/// (§13). The business params that used to live here (reopen window, default priority) are read from
/// the DB Settings store by <see cref="TicketCommandService"/>.
/// </summary>
public sealed class TicketOptions
{
    public int MaxPageSize { get; init; } = 100;
}
