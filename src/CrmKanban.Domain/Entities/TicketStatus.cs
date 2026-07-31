using CrmKanban.Domain.Common;
using CrmKanban.Domain.Enums;

namespace CrmKanban.Domain.Entities;

/// <summary>
/// A data-driven ticket status (spec §12). Name/Color/Order are super-admin editable; code and
/// reports branch on <see cref="Category"/>, never on Name (spec §4.3). CompanyId null = global
/// default set; the schema is ready for per-company overrides but v1 ships only the global set
/// (seam present, feature off — spec §18.9).
/// </summary>
public sealed class TicketStatus : Entity
{
    private TicketStatus() { } // EF

    public TicketStatus(string name, StatusCategory category, string color, int order, bool isTerminal, Guid? companyId = null, Guid? id = null)
    {
        if (id is { } fixedId) Id = fixedId; // deterministic Id for idempotent seeding
        Name = name.Trim();
        Category = category;
        Color = color;
        Order = order;
        IsTerminal = isTerminal;
        CompanyId = companyId;
    }

    public Guid? CompanyId { get; private set; }
    public string Name { get; private set; } = null!;
    public StatusCategory Category { get; private set; }
    public string Color { get; private set; } = null!;
    public int Order { get; private set; }
    public bool IsTerminal { get; private set; }

    // Admin-editable presentation (spec §12): a company can rename/recolor and reorder its own
    // columns. Category/IsTerminal are set at creation and not mutated here — changing a status's
    // category mid-life would rewrite reporting semantics and transition legality (out of v1 scope).
    public void Rename(string name) => Name = name.Trim();
    public void Recolor(string color) => Color = color.Trim();
    public void MoveTo(int order) => Order = order;
}
