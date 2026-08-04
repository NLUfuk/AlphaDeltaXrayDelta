using CrmKanban.Application.Abstractions;
using CrmKanban.Application.Auth;
using CrmKanban.Application.Common;
using CrmKanban.Domain.Entities;
using CrmKanban.Domain.Enums;
using CrmKanban.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CrmKanban.Application.Tests.Auth;

/// <summary>
/// Customer sign-up on a company's own page (/c/{slug}) with an emailed 6-digit code. Core: a short
/// code is only safe with a hard attempt cap, a short life, single-use, and strict separation from the
/// high-entropy link tokens that share the Invitations table. Each of those is a test here.
/// </summary>
public class CustomerSignInTests
{
    private sealed class SystemUser : ICurrentUserService
    {
        public Guid? UserId => null;
        public bool IsAuthenticated => false;
        public bool IsSuperAdmin => true; // auth paths use IgnoreQueryFilters anyway
        public IReadOnlyCollection<Guid> CompanyIds => [];
    }

    private sealed class FakeJwt : IJwtTokenService
    {
        public IssuedToken CreateAccessToken(AccessTokenClaims claims) =>
            new($"access-{claims.User.Id}", DateTime.UtcNow.AddMinutes(15));
        public string CreateRefreshTokenValue() => Guid.NewGuid().ToString("N");
        public string HashRefreshToken(string rawValue) => "h:" + rawValue;
    }

    private sealed class FakeHasher : IPasswordHasher
    {
        public string Hash(User user, string password) => "hash:" + password;
        public bool Verify(User user, string hash, string password) => hash == "hash:" + password;
    }

    private sealed class FakeClock(DateTime now) : IClock
    {
        public DateTime UtcNow { get; set; } = now;
    }

    private const string Slug = "acme";
    private static readonly CustomerRegisterRequest Signup = new("jane@example.com", "Jane", "Doe", "Passw0rd!");

    private static (AuthService Service, CrmDbContext Db, FakeClock Clock) Build(bool archived = false)
    {
        var db = new CrmDbContext(new DbContextOptionsBuilder<CrmDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options, new SystemUser());
        var company = new Company("Acme", Slug, Guid.NewGuid());
        if (archived) company.Archive(DateTime.UtcNow);
        db.Companies.Add(company);
        db.SaveChanges();

        var clock = new FakeClock(new DateTime(2026, 8, 4, 12, 0, 0, DateTimeKind.Utc));
        var svc = new AuthService(db, new FakeJwt(), new FakeHasher(), clock,
            Options.Create(new AuthOptions()), Options.Create(new AppOptions()), new SystemUser());
        return (svc, db, clock);
    }

    /// <summary>The code only leaves the server by email, so the test reads it out of the queued payload
    /// the same way the customer reads it out of their inbox.</summary>
    private static string EmailedCode(CrmDbContext db)
    {
        // Insertion order, not CreatedAt: the fake clock stamps both mails with the same instant.
        var payload = db.EmailQueue.IgnoreQueryFilters().AsEnumerable().Last().Payload;
        return System.Text.Json.JsonDocument.Parse(payload).RootElement.GetProperty("code").GetString()!;
    }

    [Fact]
    public async Task Register_holds_the_account_inactive_and_emails_a_six_digit_code()
    {
        var (svc, db, _) = Build();

        await svc.RegisterCustomerAsync(Slug, Signup);

        var user = await db.Users.IgnoreQueryFilters().SingleAsync();
        user.IsActive.Should().BeFalse("the address is not proven until the code comes back");
        var mail = await db.EmailQueue.IgnoreQueryFilters().SingleAsync();
        mail.TemplateKey.Should().Be("account_code");
        mail.ToEmail.Should().Be("jane@example.com");
        EmailedCode(db).Should().MatchRegex("^[0-9]{6}$");
        var invitation = await db.Invitations.IgnoreQueryFilters().SingleAsync();
        invitation.Kind.Should().Be(InvitationKind.Code);
        invitation.TokenHash.Should().NotContain(EmailedCode(db), "the code is stored hashed, never in the clear");
    }

    [Fact]
    public async Task Verifying_the_emailed_code_activates_the_account_and_issues_a_session()
    {
        var (svc, db, _) = Build();
        await svc.RegisterCustomerAsync(Slug, Signup);

        var result = await svc.VerifyCodeAsync(Slug, new VerifyCodeRequest("jane@example.com", EmailedCode(db)));

        result.AccessToken.Should().NotBeNullOrEmpty();
        (await db.Users.IgnoreQueryFilters().SingleAsync()).IsActive.Should().BeTrue();
        (await db.Invitations.IgnoreQueryFilters().SingleAsync()).AcceptedAt.Should().NotBeNull("single use");
    }

    [Fact]
    public async Task A_used_code_cannot_be_replayed()
    {
        var (svc, db, _) = Build();
        await svc.RegisterCustomerAsync(Slug, Signup);
        var code = EmailedCode(db);
        await svc.VerifyCodeAsync(Slug, new VerifyCodeRequest("jane@example.com", code));

        var act = () => svc.VerifyCodeAsync(Slug, new VerifyCodeRequest("jane@example.com", code));

        await act.Should().ThrowAsync<UnauthorizedException>().Where(e => e.Code == "auth.invalid_code");
    }

    [Fact]
    public async Task Guessing_is_capped_the_code_locks_after_max_attempts_even_for_the_right_code()
    {
        var (svc, db, _) = Build();
        await svc.RegisterCustomerAsync(Slug, Signup);
        var code = EmailedCode(db);
        var wrong = code == "000000" ? "111111" : "000000";

        for (var i = 0; i < new AuthOptions().MaxCodeAttempts; i++)
        {
            var bad = () => svc.VerifyCodeAsync(Slug, new VerifyCodeRequest("jane@example.com", wrong));
            await bad.Should().ThrowAsync<UnauthorizedException>().Where(e => e.Code == "auth.invalid_code");
        }

        var act = () => svc.VerifyCodeAsync(Slug, new VerifyCodeRequest("jane@example.com", code));
        await act.Should().ThrowAsync<UnauthorizedException>().Where(e => e.Code == "auth.code_locked");
        (await db.Users.IgnoreQueryFilters().SingleAsync()).IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task An_expired_code_is_rejected()
    {
        var (svc, db, clock) = Build();
        await svc.RegisterCustomerAsync(Slug, Signup);
        var code = EmailedCode(db);
        clock.UtcNow = clock.UtcNow.AddMinutes(new AuthOptions().VerificationCodeMinutes + 1);

        var act = () => svc.VerifyCodeAsync(Slug, new VerifyCodeRequest("jane@example.com", code));

        await act.Should().ThrowAsync<UnauthorizedException>().Where(e => e.Code == "auth.invalid_code");
    }

    [Fact]
    public async Task Requesting_a_new_code_invalidates_the_previous_one()
    {
        var (svc, db, _) = Build();
        await svc.RegisterCustomerAsync(Slug, Signup);
        var first = EmailedCode(db);
        await svc.RegisterCustomerAsync(Slug, Signup);

        var act = () => svc.VerifyCodeAsync(Slug, new VerifyCodeRequest("jane@example.com", first));

        await act.Should().ThrowAsync<UnauthorizedException>();
        await svc.VerifyCodeAsync(Slug, new VerifyCodeRequest("jane@example.com", EmailedCode(db))); // newest works
    }

    [Fact]
    public async Task A_sign_in_code_is_not_redeemable_through_the_invite_link_flow()
    {
        var (_, db, clock) = Build();
        var user = new User("jane@example.com", "Jane", "Doe");
        user.Deactivate();
        db.Users.Add(user);
        // Worst case: a code stored unsalted, so its hash is exactly what the link flow would compute.
        db.Invitations.Add(new Invitation(user.Id, TokenHasher.Hash("123456"),
            clock.UtcNow.AddMinutes(15), invitedById: null, InvitationKind.Code));
        await db.SaveChangesAsync();
        var invitations = new InvitationService(db, new FakeHasher(), clock, new SystemUser(),
            new AllowAllPermissions(), Options.Create(new AuthOptions()), Options.Create(new AppOptions()));

        var act = () => invitations.AcceptInviteAsync(new AcceptInviteRequest("123456", "NewPassw0rd!"));

        await act.Should().ThrowAsync<UnauthorizedException>().Where(e => e.Code == "invite.invalid");
        (await db.Users.IgnoreQueryFilters().SingleAsync()).IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task An_archived_company_link_accepts_no_sign_ups()
    {
        var (svc, _, _) = Build(archived: true);

        var act = () => svc.RegisterCustomerAsync(Slug, Signup);

        await act.Should().ThrowAsync<ConflictException>().Where(e => e.Code == "company.form_closed");
    }

    private sealed class AllowAllPermissions : IPermissionService
    {
        public Task<IReadOnlySet<string>> GetPermissionsAsync(Guid u, Guid c, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlySet<string>>(new HashSet<string>());
        public Task<bool> HasPermissionAsync(Guid u, Guid c, string k, CancellationToken ct = default) => Task.FromResult(true);
    }
}
