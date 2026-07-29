namespace CrmKanban.Application.Tickets;

/// <summary>Ticket business params (spec §12/§18.11). v1 default; moves into the DB Settings table in Faz 6.</summary>
public sealed class TicketOptions
{
    public int ReopenWindowDays { get; init; } = 7;
    public int MaxPageSize { get; init; } = 100;
}
