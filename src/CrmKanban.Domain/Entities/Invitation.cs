using CrmKanban.Domain.Common;

namespace CrmKanban.Domain.Entities;

/// <summary>
/// A one-time "set your password" token (spec §9). Created when a user is invited (admin/personel)
/// or when a customer opens a ticket without an account. The membership/role is already set on the
/// User/Membership at invite time; this only carries the token to activate the account. Stored as a
/// hash; the raw value goes only in the emailed link.
/// </summary>
public sealed class Invitation : Entity
{
    private Invitation() { } // EF

    public Invitation(Guid userId, string tokenHash, DateTime expiresAt, Guid? invitedById)
    {
        UserId = userId;
        TokenHash = tokenHash;
        ExpiresAt = expiresAt;
        InvitedById = invitedById;
    }

    public Guid UserId { get; private set; }
    public string TokenHash { get; private set; } = null!;
    public DateTime ExpiresAt { get; private set; }
    public DateTime? AcceptedAt { get; private set; }
    public Guid? InvitedById { get; private set; }

    public bool IsPending(DateTime now) => AcceptedAt is null && now < ExpiresAt;

    public void Accept(DateTime now) => AcceptedAt = now;
}
