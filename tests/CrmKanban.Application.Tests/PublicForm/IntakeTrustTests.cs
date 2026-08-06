using CrmKanban.Application.Abstractions;
using CrmKanban.Application.Auth;
using CrmKanban.Application.PublicForm;
using CrmKanban.Domain.Entities;
using CrmKanban.Domain.Enums;
using CrmKanban.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace CrmKanban.Application.Tests.PublicForm;

/// <summary>
/// The moderation gate (Faz 35). This is the executable spec for the intake rule the operator asked
/// for: a ticket skips the approval queue ONLY for a staff-issued invitation (one-shot) or standing
/// trust. Everything else waits — a stranger who found the company link, and a known customer's later
/// tickets.
/// <para>
/// The token tests are the security core. A customer-access token is handed out over WhatsApp, so it
/// must be worthless to anyone it was not issued for, worthless at any other company, worthless after
/// one use, and worthless once expired. Each of those is one <c>&amp;&amp;</c> in
/// <see cref="IntakeTrustService"/>; each has a test here because dropping any one of them silently
/// turns the link into a skeleton key.
/// </para>
/// </summary>
public class IntakeTrustTests
{
    private sealed class SuperAdmin : ICurrentUserService
    {
        public Guid? UserId => Guid.NewGuid();
        public bool IsAuthenticated => true;
        public bool IsSuperAdmin => true;
        public IReadOnlyCollection<Guid> CompanyIds => [];
    }

    private sealed class Anonymous : ICurrentUserService
    {
        public Guid? UserId => null;
        public bool IsAuthenticated => false;
        public bool IsSuperAdmin => false;
        public IReadOnlyCollection<Guid> CompanyIds => [];
    }

    private sealed class FixedClock(DateTime now) : IClock
    {
        public DateTime UtcNow { get; } = now;
    }

    private static readonly DateTime Now = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private static DbContextOptions<CrmDbContext> Store() =>
        new DbContextOptionsBuilder<CrmDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString(),
                new Microsoft.EntityFrameworkCore.Storage.InMemoryDatabaseRoot()).Options;

    /// <summary>Seeds a company + a customer, and returns both ids.</summary>
    private static async Task<(Guid CompanyId, Guid CustomerId)> SeedAsync(DbContextOptions<CrmDbContext> options)
    {
        await using var db = new CrmDbContext(options, new SuperAdmin());
        var company = new Company("Acme", $"acme-{Guid.NewGuid():N}", Guid.NewGuid());
        var customer = new User($"c{Guid.NewGuid():N}@example.com", "Cust", "Omer");
        db.Companies.Add(company);
        db.Users.Add(customer);
        await db.SaveChangesAsync();
        return (company.Id, customer.Id);
    }

    /// <summary>Writes a customer-access invitation and returns the raw token staff would hand over.</summary>
    private static async Task<string> IssueInviteAsync(
        DbContextOptions<CrmDbContext> options, Guid companyId, Guid customerId, DateTime? expiresAt = null)
    {
        await using var db = new CrmDbContext(options, new SuperAdmin());
        var raw = TokenHasher.NewRawToken();
        db.Invitations.Add(new Invitation(customerId, TokenHasher.Hash(raw), expiresAt ?? Now.AddDays(7),
            invitedById: Guid.NewGuid(), InvitationKind.CustomerAccess, companyId));
        await db.SaveChangesAsync();
        return raw;
    }

    private static IntakeTrustService ServiceFor(DbContextOptions<CrmDbContext> options, DateTime? now = null) =>
        new(new CrmDbContext(options, new Anonymous()), new FixedClock(now ?? Now));

    // ---- the rule the operator asked for ----

    [Fact]
    public async Task A_stranger_who_found_the_company_link_is_held_for_approval()
    {
        var options = Store();
        var (companyId, customerId) = await SeedAsync(options);

        var hold = await ServiceFor(options).ShouldHoldForApprovalAsync(companyId, customerId, rawInviteToken: null);

        hold.Should().BeTrue("no invitation and no standing trust — this is the whole point of the queue");
    }

    [Fact]
    public async Task An_invited_customers_first_ticket_goes_straight_to_the_board()
    {
        var options = Store();
        var (companyId, customerId) = await SeedAsync(options);
        var token = await IssueInviteAsync(options, companyId, customerId);

        var hold = await ServiceFor(options).ShouldHoldForApprovalAsync(companyId, customerId, token);

        hold.Should().BeFalse();
    }

    [Fact]
    public async Task The_invitation_is_one_shot_so_the_next_ticket_queues_again()
    {
        var options = Store();
        var (companyId, customerId) = await SeedAsync(options);
        var token = await IssueInviteAsync(options, companyId, customerId);

        // First submission consumes it. The service does not save (the caller commits), so persist here
        // exactly like PublicFormService does after building the ticket.
        var db = new CrmDbContext(options, new Anonymous());
        var service = new IntakeTrustService(db, new FixedClock(Now));
        (await service.ShouldHoldForApprovalAsync(companyId, customerId, token)).Should().BeFalse();
        await db.SaveChangesAsync();

        var second = await ServiceFor(options).ShouldHoldForApprovalAsync(companyId, customerId, token);

        second.Should().BeTrue("trust is attached to the invitation, not to the person");
    }

    [Fact]
    public async Task A_trusted_customer_skips_the_queue_every_time()
    {
        var options = Store();
        var (companyId, customerId) = await SeedAsync(options);
        await using (var db = new CrmDbContext(options, new SuperAdmin()))
        {
            db.CustomerTrusts.Add(new CustomerTrust(companyId, customerId, Guid.NewGuid()));
            await db.SaveChangesAsync();
        }

        var first = await ServiceFor(options).ShouldHoldForApprovalAsync(companyId, customerId, null);
        var second = await ServiceFor(options).ShouldHoldForApprovalAsync(companyId, customerId, null);

        first.Should().BeFalse();
        second.Should().BeFalse("standing trust is not consumed");
    }

    [Fact]
    public async Task Trust_does_not_burn_an_invitation_the_customer_also_holds()
    {
        var options = Store();
        var (companyId, customerId) = await SeedAsync(options);
        var token = await IssueInviteAsync(options, companyId, customerId);
        await using (var db = new CrmDbContext(options, new SuperAdmin()))
        {
            db.CustomerTrusts.Add(new CustomerTrust(companyId, customerId, Guid.NewGuid()));
            await db.SaveChangesAsync();
        }

        var scoped = new CrmDbContext(options, new Anonymous());
        await new IntakeTrustService(scoped, new FixedClock(Now)).ShouldHoldForApprovalAsync(companyId, customerId, token);
        await scoped.SaveChangesAsync();

        await using var read = new CrmDbContext(options, new SuperAdmin());
        var invite = await read.Invitations.IgnoreQueryFilters().SingleAsync();
        invite.AcceptedAt.Should().BeNull("trust was enough; the token stays available");
    }

    // ---- the security core: what the token must NOT do ----

    [Fact]
    public async Task A_token_issued_for_someone_else_does_not_work()
    {
        var options = Store();
        var (companyId, invitedCustomer) = await SeedAsync(options);
        Guid strangerId;
        await using (var db = new CrmDbContext(options, new SuperAdmin()))
        {
            var stranger = new User("stranger@example.com", "Str", "Anger");
            db.Users.Add(stranger);
            await db.SaveChangesAsync();
            strangerId = stranger.Id;
        }
        var token = await IssueInviteAsync(options, companyId, invitedCustomer);

        // The link was forwarded: someone else registered and pasted the same ?davet= value.
        var hold = await ServiceFor(options).ShouldHoldForApprovalAsync(companyId, strangerId, token);

        hold.Should().BeTrue("the token is bound to the address it was issued for");
    }

    [Fact]
    public async Task A_token_from_another_company_does_not_work()
    {
        var options = Store();
        var (companyA, customerId) = await SeedAsync(options);
        Guid companyB;
        await using (var db = new CrmDbContext(options, new SuperAdmin()))
        {
            var other = new Company("Other", $"other-{Guid.NewGuid():N}", Guid.NewGuid());
            db.Companies.Add(other);
            await db.SaveChangesAsync();
            companyB = other.Id;
        }
        var tokenForA = await IssueInviteAsync(options, companyA, customerId);

        var hold = await ServiceFor(options).ShouldHoldForApprovalAsync(companyB, customerId, tokenForA);

        hold.Should().BeTrue("an invitation from one company must never be a skeleton key at another");
    }

    [Fact]
    public async Task An_expired_token_does_not_work()
    {
        var options = Store();
        var (companyId, customerId) = await SeedAsync(options);
        var token = await IssueInviteAsync(options, companyId, customerId, expiresAt: Now.AddDays(7));

        var hold = await ServiceFor(options, now: Now.AddDays(8))
            .ShouldHoldForApprovalAsync(companyId, customerId, token);

        hold.Should().BeTrue();
    }

    [Fact]
    public async Task An_account_token_cannot_be_replayed_as_a_customer_access_token()
    {
        var options = Store();
        var (companyId, customerId) = await SeedAsync(options);
        var raw = TokenHasher.NewRawToken();
        await using (var db = new CrmDbContext(options, new SuperAdmin()))
        {
            // A password-set/activation token: same table, same shape, different purpose.
            db.Invitations.Add(new Invitation(customerId, TokenHasher.Hash(raw), Now.AddDays(7),
                invitedById: null, InvitationKind.Link));
            await db.SaveChangesAsync();
        }

        var hold = await ServiceFor(options).ShouldHoldForApprovalAsync(companyId, customerId, raw);

        hold.Should().BeTrue("kinds must never be interchangeable");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-real-token")]
    public async Task A_missing_or_bogus_token_is_held(string? token)
    {
        var options = Store();
        var (companyId, customerId) = await SeedAsync(options);

        var hold = await ServiceFor(options).ShouldHoldForApprovalAsync(companyId, customerId, token);

        hold.Should().BeTrue();
    }

    [Fact]
    public void A_customer_access_invitation_cannot_be_created_without_a_company()
    {
        // Guarded in the constructor, not just at the call site: an unscoped customer-access row would
        // satisfy `i.CompanyId == companyId` for nobody, but the guard documents why it must not exist.
        var create = () => new Invitation(Guid.NewGuid(), "hash", Now.AddDays(7), null,
            InvitationKind.CustomerAccess, companyId: null);

        create.Should().Throw<ArgumentNullException>();
    }
}
