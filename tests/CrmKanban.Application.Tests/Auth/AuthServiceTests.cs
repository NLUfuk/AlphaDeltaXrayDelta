using CrmKanban.Application.Abstractions;
using CrmKanban.Application.Auth;
using CrmKanban.Application.Common;
using CrmKanban.Domain.Entities;
using CrmKanban.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CrmKanban.Application.Tests.Auth;

public class AuthServiceTests
{
    // ---- fakes ----
    private sealed class FakeCurrentUser : ICurrentUserService
    {
        public Guid? UserId => null;
        public bool IsAuthenticated => false;
        public bool IsSuperAdmin => true; // auth queries use IgnoreQueryFilters anyway
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

    private static CrmDbContext NewDb() =>
        new(new DbContextOptionsBuilder<CrmDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options, new FakeCurrentUser());

    private static (AuthService Service, CrmDbContext Db, FakeClock Clock) Build(out User user)
    {
        var db = NewDb();
        var hasher = new FakeHasher();
        user = new User("u@x.io", "U", "X");
        user.SetPasswordHash(hasher.Hash(user, "Passw0rd!"));
        db.Users.Add(user);
        db.SaveChanges();

        var clock = new FakeClock(new DateTime(2026, 7, 29, 12, 0, 0, DateTimeKind.Utc));
        var svc = new AuthService(db, new FakeJwt(), hasher, clock,
            Options.Create(new AuthOptions()), Options.Create(new AppOptions()));
        return (svc, db, clock);
    }

    [Fact]
    public async Task Login_with_correct_password_issues_tokens_and_stores_refresh_hash()
    {
        var (svc, db, _) = Build(out _);

        var result = await svc.LoginAsync(new LoginRequest("u@x.io", "Passw0rd!"));

        result.AccessToken.Should().NotBeNullOrEmpty();
        result.RefreshToken.Should().NotBeNullOrEmpty();
        var stored = await db.RefreshTokens.SingleAsync();
        stored.TokenHash.Should().Be("h:" + result.RefreshToken, "the raw token is never stored, only its hash");
    }

    [Fact]
    public async Task Login_with_wrong_password_is_rejected()
    {
        var (svc, _, _) = Build(out _);
        var act = () => svc.LoginAsync(new LoginRequest("u@x.io", "wrong"));
        await act.Should().ThrowAsync<UnauthorizedException>().Where(e => e.Code == "auth.invalid_credentials");
    }

    [Fact]
    public async Task Refresh_rotates_the_token_revoking_the_old_one()
    {
        var (svc, db, _) = Build(out _);
        var login = await svc.LoginAsync(new LoginRequest("u@x.io", "Passw0rd!"));

        var refreshed = await svc.RefreshAsync(new RefreshRequest(login.RefreshToken));

        refreshed.RefreshToken.Should().NotBe(login.RefreshToken, "a new token is issued on every refresh");
        var oldToken = await db.RefreshTokens.IgnoreQueryFilters().FirstAsync(t => t.TokenHash == "h:" + login.RefreshToken);
        oldToken.RevokedAt.Should().NotBeNull("the old token is revoked when rotated");
    }

    [Fact]
    public async Task Reusing_a_rotated_token_revokes_the_whole_chain()
    {
        var (svc, db, _) = Build(out _);
        var login = await svc.LoginAsync(new LoginRequest("u@x.io", "Passw0rd!"));
        await svc.RefreshAsync(new RefreshRequest(login.RefreshToken)); // rotates; old now revoked

        // Attacker replays the old, already-rotated token.
        var act = () => svc.RefreshAsync(new RefreshRequest(login.RefreshToken));
        await act.Should().ThrowAsync<UnauthorizedException>();

        var active = await db.RefreshTokens.IgnoreQueryFilters().CountAsync(t => t.RevokedAt == null);
        active.Should().Be(0, "detecting reuse of a revoked token revokes every token in the chain");
    }

    [Fact]
    public async Task Change_password_revokes_all_refresh_tokens()
    {
        var (svc, db, _) = Build(out var user);
        await svc.LoginAsync(new LoginRequest("u@x.io", "Passw0rd!"));

        await svc.ChangePasswordAsync(user.Id, new ChangePasswordRequest("Passw0rd!", "NewPassw0rd!"));

        (await db.RefreshTokens.IgnoreQueryFilters().CountAsync(t => t.RevokedAt == null)).Should().Be(0);
    }

    [Fact]
    public async Task Register_new_email_creates_a_pending_account_and_queues_a_verification_email()
    {
        var (svc, db, _) = Build(out _);

        await svc.RegisterAsync(new RegisterRequest("new@x.io", "New", "Person"));

        var user = await db.Users.IgnoreQueryFilters().SingleAsync(u => u.Email == "new@x.io");
        user.IsActive.Should().BeFalse("account is inactive until the emailed link is used");
        user.IsInvitedPending.Should().BeTrue("no password set yet — set on the activation link");
        (await db.Invitations.IgnoreQueryFilters().CountAsync(i => i.UserId == user.Id)).Should().Be(1);
        var mail = await db.EmailQueue.IgnoreQueryFilters().SingleAsync();
        mail.TemplateKey.Should().Be("account_verify");
        mail.ToEmail.Should().Be("new@x.io");
        mail.Payload.Should().Contain("/invite?token=");
    }

    [Fact]
    public async Task Register_with_an_existing_active_email_is_a_silent_noop_no_enumeration()
    {
        var (svc, db, _) = Build(out _); // "u@x.io" already exists and is active

        await svc.RegisterAsync(new RegisterRequest("u@x.io", "U", "X"));

        (await db.Users.IgnoreQueryFilters().CountAsync(u => u.Email == "u@x.io")).Should().Be(1, "no duplicate account");
        (await db.Invitations.IgnoreQueryFilters().CountAsync()).Should().Be(0, "no token issued");
        (await db.EmailQueue.IgnoreQueryFilters().CountAsync()).Should().Be(0, "no email — nothing leaks that the account exists");
    }
}
