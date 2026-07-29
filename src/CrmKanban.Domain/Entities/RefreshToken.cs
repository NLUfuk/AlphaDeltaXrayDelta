using CrmKanban.Domain.Common;

namespace CrmKanban.Domain.Entities;

/// <summary>
/// Refresh token stored as a hash only, rotational (spec §3, §9). On refresh the presented
/// token is revoked and a new one issued (<see cref="ReplacedByTokenId"/> links the chain).
/// Presenting an already-revoked token signals reuse/theft → the service revokes the whole chain.
/// </summary>
public sealed class RefreshToken : Entity
{
    private RefreshToken() { } // EF

    public RefreshToken(Guid userId, string tokenHash, DateTime expiresAt)
    {
        UserId = userId;
        TokenHash = tokenHash;
        ExpiresAt = expiresAt;
    }

    public Guid UserId { get; private set; }
    public string TokenHash { get; private set; } = null!;
    public DateTime ExpiresAt { get; private set; }
    public DateTime? RevokedAt { get; private set; }
    public Guid? ReplacedByTokenId { get; private set; }

    public bool IsActive(DateTime now) => RevokedAt is null && now < ExpiresAt;

    public void Revoke(DateTime now, Guid? replacedBy = null)
    {
        if (RevokedAt is not null) return;
        RevokedAt = now;
        ReplacedByTokenId = replacedBy;
    }
}
