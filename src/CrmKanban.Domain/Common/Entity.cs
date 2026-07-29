namespace CrmKanban.Domain.Common;

/// <summary>
/// Base for all aggregate roots/entities. GUID PK, audit timestamps, soft-delete (spec §11).
/// Timestamps are stamped centrally by the DbContext SaveChanges interceptor, not here,
/// so the domain stays clock-free and unit-testable.
/// </summary>
public abstract class Entity
{
    public Guid Id { get; protected set; } = Guid.NewGuid();
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; private set; }

    public bool IsDeleted => DeletedAt is not null;

    public void SoftDelete(DateTime now) => DeletedAt = now;
    public void Restore() => DeletedAt = null;
}
