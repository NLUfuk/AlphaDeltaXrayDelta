using CrmKanban.Domain.Common;
using CrmKanban.Domain.Enums;

namespace CrmKanban.Domain.Entities;

/// <summary>
/// A one-time account token (spec §9). Two kinds share this table:
///  - <see cref="InvitationKind.Link"/>: a high-entropy token carried in an emailed "set your password"
///    link (staff invite, public-form customer, password reset). Looked up BY TOKEN.
///  - <see cref="InvitationKind.Code"/>: a short numeric code the customer types on the company sign-in
///    page. Low entropy, so it is looked up BY USER (never by token) and <see cref="Attempts"/> caps
///    guessing. The two kinds must never be interchangeable — a code must not be accepted by the link
///    flow, or its 6-digit space would be brute-forceable there.
/// Both are stored hashed; the raw value only ever leaves the server by email.
/// </summary>
public sealed class Invitation : Entity
{
    private Invitation() { } // EF

    public Invitation(Guid userId, string tokenHash, DateTime expiresAt, Guid? invitedById,
        InvitationKind kind = InvitationKind.Link)
    {
        UserId = userId;
        TokenHash = tokenHash;
        ExpiresAt = expiresAt;
        InvitedById = invitedById;
        Kind = kind;
    }

    public Guid UserId { get; private set; }
    public string TokenHash { get; private set; } = null!;
    public DateTime ExpiresAt { get; private set; }
    public DateTime? AcceptedAt { get; private set; }
    public Guid? InvitedById { get; private set; }
    public InvitationKind Kind { get; private set; }

    /// <summary>Failed verification attempts (code kind). Caps online guessing of the short code.</summary>
    public int Attempts { get; private set; }

    public bool IsPending(DateTime now) => AcceptedAt is null && now < ExpiresAt;

    public void Accept(DateTime now) => AcceptedAt = now;

    public void RecordFailedAttempt() => Attempts++;
}
