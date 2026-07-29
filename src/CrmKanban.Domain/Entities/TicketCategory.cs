using CrmKanban.Domain.Common;

namespace CrmKanban.Domain.Entities;

/// <summary>Per-company ticket category for report breakdown (spec §11, §18.18).</summary>
public sealed class TicketCategory : Entity
{
    private TicketCategory() { } // EF

    public TicketCategory(Guid companyId, string name)
    {
        CompanyId = companyId;
        Name = name.Trim();
        IsActive = true;
    }

    public Guid CompanyId { get; private set; }
    public string Name { get; private set; } = null!;
    public bool IsActive { get; private set; }

    public void Rename(string name) => Name = name.Trim();
    public void Deactivate() => IsActive = false;
}
