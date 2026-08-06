using CrmKanban.Domain.Common;

namespace CrmKanban.Domain.Entities;

/// <summary>
/// "This company trusts this customer" — their submissions skip the moderation queue and enter the
/// pool directly (spec §10 zero-trust intake, Faz 35).
/// <para>
/// Intake is trusted for exactly two reasons: a valid staff-issued invitation (one-shot, consumed by
/// the first ticket) or a standing trust row here. Everything else is held for approval — including a
/// known customer's second ticket, which is the point: recognising an email address is not the same
/// as vouching for it, and the old rule ("known email → straight in") let anyone who had once
/// submitted a form bypass moderation forever.
/// </para>
/// <para>
/// Per (company, customer), never global: trust granted by Anadolu Tekstil says nothing about Ege
/// Mermer. <see cref="GrantedById"/> keeps the decision auditable — someone chose this.
/// </para>
/// </summary>
public sealed class CustomerTrust : Entity
{
    private CustomerTrust() { } // EF

    public CustomerTrust(Guid companyId, Guid userId, Guid grantedById)
    {
        CompanyId = companyId;
        UserId = userId;
        GrantedById = grantedById;
    }

    public Guid CompanyId { get; private set; }
    public Guid UserId { get; private set; }

    /// <summary>The staff member who vouched for this customer.</summary>
    public Guid GrantedById { get; private set; }
}
