using CrmKanban.Domain.Common;

namespace CrmKanban.Domain.Entities;

/// <summary>
/// Tenant unit (spec §6). Companies are never hard-deleted — they are archived: the public
/// form link closes, data stays readable, no cascade delete (spec §18.20).
/// </summary>
public sealed class Company : Entity
{
    private Company() { } // EF

    public Company(string name, string slug, Guid ownerAdminId, string? ticketPrefix = null)
    {
        Name = name.Trim();
        Slug = slug.Trim().ToLowerInvariant();
        OwnerAdminId = ownerAdminId;
        TicketNumberPrefix = (ticketPrefix ?? Slug).Trim().ToUpperInvariant();
        NextTicketNumber = 1;
        IsActive = true;
    }

    public string Name { get; private set; } = null!;
    public string Slug { get; private set; } = null!;
    public Guid OwnerAdminId { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime? ArchivedAt { get; private set; }

    /// <summary>Human-readable ticket number prefix (spec §11/§18.16, e.g. "ACME" → ACME-1042).</summary>
    public string TicketNumberPrefix { get; private set; } = null!;
    public int NextTicketNumber { get; private set; }

    public bool IsArchived => ArchivedAt is not null;

    /// <summary>
    /// Allocates the next per-company ticket number and advances the counter (spec §11).
    /// ponytail: naive read-increment; the unique (CompanyId, Number) index is the backstop and the
    /// caller retries on collision. Swap for a DB sequence per company if creation throughput demands it.
    /// </summary>
    public string AllocateTicketNumber()
    {
        var number = $"{TicketNumberPrefix}-{NextTicketNumber}";
        NextTicketNumber++;
        return number;
    }

    public void Rename(string name) => Name = name.Trim();

    public void Archive(DateTime now)
    {
        ArchivedAt = now;
        IsActive = false;
    }

    /// <summary>
    /// Retires the company on the delete path: closes it exactly like <see cref="Archive"/> (so every
    /// write guard that checks IsArchived/IsActive shuts at once) and releases the public slug, whose
    /// unique index is global — without this the same slug could never be used again.
    /// </summary>
    public void Retire(DateTime now)
    {
        Archive(now);
        var stem = Slug.Length > 100 ? Slug[..100] : Slug;
        Slug = $"{stem}-deleted-{Id.ToString("N")[..8]}"; // ≤ 117 chars, fits the 120 column
    }
}
