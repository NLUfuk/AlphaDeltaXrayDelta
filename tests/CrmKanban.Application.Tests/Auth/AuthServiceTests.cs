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

    // Configurable caller for the impersonation gate (identity + super-admin flag matter there).
    private sealed class Caller(Guid? id, bool super) : ICurrentUserService
    {
        public Guid? UserId => id;
        public bool IsAuthenticated => id is not null;
        public bool IsSuperAdmin => super;
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

    private static (AuthService Service, CrmDbContext Db, FakeClock Clock) Build(out User user, ICurrentUserService? caller = null)
    {
        var db = NewDb();
        var hasher = new FakeHasher();
        user = new User("u@x.io", "U", "X");
        user.SetPasswordHash(hasher.Hash(user, "Passw0rd!"));
        db.Users.Add(user);
        db.SaveChanges();

        var clock = new FakeClock(new DateTime(2026, 7, 29, 12, 0, 0, DateTimeKind.Utc));
        var svc = new AuthService(db, new FakeJwt(), hasher, clock,
            Options.Create(new AuthOptions()), Options.Create(new AppOptions()), caller ?? new FakeCurrentUser());
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
        var (svc, db, clock) = Build(out _);
        var login = await svc.LoginAsync(new LoginRequest("u@x.io", "Passw0rd!"));
        await svc.RefreshAsync(new RefreshRequest(login.RefreshToken)); // rotates; old now revoked

        // Past the rotation grace window, so this is a genuine replay and not the multi-tab race below.
        clock.UtcNow = clock.UtcNow.AddSeconds(new AuthOptions().RefreshRotationGraceSeconds + 1);

        // Attacker replays the old, already-rotated token.
        var act = () => svc.RefreshAsync(new RefreshRequest(login.RefreshToken));
        await act.Should().ThrowAsync<UnauthorizedException>();

        var active = await db.RefreshTokens.IgnoreQueryFilters().CountAsync(t => t.RevokedAt == null);
        active.Should().Be(0, "detecting reuse of a revoked token revokes every token in the chain");
    }

    /// <summary>
    /// The access token is no longer persisted anywhere, so every tab refreshes when it boots and two
    /// tabs opened together present the same cookie microseconds apart. Without a grace window the
    /// second one looked exactly like theft and logged the user out of both — a self-inflicted denial
    /// of service on the most ordinary user action there is.
    /// </summary>
    [Fact]
    public async Task Replaying_a_just_rotated_token_is_served_not_treated_as_theft()
    {
        var (svc, db, _) = Build(out _);
        var login = await svc.LoginAsync(new LoginRequest("u@x.io", "Passw0rd!"));
        await svc.RefreshAsync(new RefreshRequest(login.RefreshToken)); // tab A rotates

        // Tab B, same cookie, same instant.
        var second = await svc.RefreshAsync(new RefreshRequest(login.RefreshToken));

        second.AccessToken.Should().NotBeNullOrEmpty("the racing tab gets a working session");
        second.RefreshToken.Should().NotBe(login.RefreshToken, "it is still a rotation, not a reissue of the old token");
        var active = await db.RefreshTokens.IgnoreQueryFilters().CountAsync(t => t.RevokedAt == null);
        active.Should().Be(1, "the chain survives — exactly one token is live");
    }

    /// <summary>The window is not a blanket amnesty: a token revoked with no replacement (logout,
    /// password change) is reuse whenever it is presented, including one second later.</summary>
    [Fact]
    public async Task Replaying_a_logged_out_token_is_still_theft_even_immediately()
    {
        var (svc, db, _) = Build(out _);
        var login = await svc.LoginAsync(new LoginRequest("u@x.io", "Passw0rd!"));
        await svc.LogoutAsync(new RefreshRequest(login.RefreshToken)); // revoked, ReplacedByTokenId null

        var act = () => svc.RefreshAsync(new RefreshRequest(login.RefreshToken));
        await act.Should().ThrowAsync<UnauthorizedException>();

        (await db.RefreshTokens.IgnoreQueryFilters().CountAsync(t => t.RevokedAt == null)).Should().Be(0);
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
    public async Task Delete_own_account_anonymizes_deactivates_and_revokes_sessions()
    {
        var (svc, db, _) = Build(out var user);
        await svc.LoginAsync(new LoginRequest("u@x.io", "Passw0rd!"));

        await svc.DeleteOwnAccountAsync(user.Id, new DeleteAccountRequest("Passw0rd!"));

        var deleted = await db.Users.IgnoreQueryFilters().SingleAsync(u => u.Id == user.Id);
        deleted.IsActive.Should().BeFalse();
        deleted.Email.Should().NotBe("u@x.io", "personal fields are masked (anonymized)");
        deleted.PasswordHash.Should().BeNull("the account can no longer log in");
        (await db.RefreshTokens.IgnoreQueryFilters().CountAsync(t => t.RevokedAt == null)).Should().Be(0);
    }

    [Fact]
    public async Task Delete_own_account_with_wrong_password_is_rejected()
    {
        var (svc, _, _) = Build(out var user);
        var act = () => svc.DeleteOwnAccountAsync(user.Id, new DeleteAccountRequest("wrong"));
        // Not `auth.invalid_credentials` / 401: the caller is authenticated and merely mistyped the
        // password they are re-confirming. A 401 sent the SPA into refresh-and-retry and could log them
        // out over a typo; the distinct code also lets the UI say "mevcut parolanız hatalı".
        await act.Should().ThrowAsync<BadRequestException>().Where(e => e.Code == "auth.wrong_password");
    }

    [Fact]
    public async Task A_super_admin_cannot_self_delete()
    {
        var (svc, db, _) = Build(out _);
        var boss = new User("boss@x.io", "Boss", "X");
        boss.SetPasswordHash(new FakeHasher().Hash(boss, "Passw0rd!"));
        boss.PromoteToSuperAdmin();
        db.Users.Add(boss);
        await db.SaveChangesAsync();

        var act = () => svc.DeleteOwnAccountAsync(boss.Id, new DeleteAccountRequest("Passw0rd!"));
        await act.Should().ThrowAsync<ConflictException>().Where(e => e.Code == "auth.superadmin_delete");
    }

    [Fact]
    public async Task Super_admin_can_impersonate_a_normal_user_and_it_is_audit_logged()
    {
        var superAdminId = Guid.NewGuid();
        var (svc, db, _) = Build(out var target, new Caller(superAdminId, super: true));

        var result = await svc.ImpersonateAsync(target.Id);

        result.User.Id.Should().Be(target.Id, "the session is minted for the target, not the super admin");
        result.AccessToken.Should().NotBeNullOrEmpty();
        var audit = await db.AuditLogs.IgnoreQueryFilters().SingleAsync(a => a.Action == "auth.impersonate");
        audit.ActorId.Should().Be(superAdminId, "the real actor is recorded for accountability");
        audit.Detail.Should().Contain(target.Id.ToString());
    }

    [Fact]
    public async Task A_non_super_admin_cannot_impersonate()
    {
        var (svc, _, _) = Build(out var target, new Caller(Guid.NewGuid(), super: false));
        var act = () => svc.ImpersonateAsync(target.Id);
        await act.Should().ThrowAsync<ForbiddenException>().Where(e => e.Code == "auth.impersonate_forbidden");
    }

    [Fact]
    public async Task Impersonating_another_super_admin_is_refused()
    {
        var (svc, db, _) = Build(out _, new Caller(Guid.NewGuid(), super: true));
        var targetSuper = new User("boss@x.io", "Boss", "X");
        targetSuper.PromoteToSuperAdmin();
        db.Users.Add(targetSuper);
        await db.SaveChangesAsync();

        var act = () => svc.ImpersonateAsync(targetSuper.Id);
        await act.Should().ThrowAsync<ForbiddenException>().Where(e => e.Code == "auth.impersonate_superadmin");
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

    [Fact]
    public async Task Forgot_password_for_an_active_account_queues_a_reset_link()
    {
        var (svc, db, _) = Build(out var user); // "u@x.io" is active with a password

        await svc.ForgotPasswordAsync(new ForgotPasswordRequest("u@x.io"));

        (await db.Invitations.IgnoreQueryFilters().CountAsync(i => i.UserId == user.Id)).Should().Be(1);
        var mail = await db.EmailQueue.IgnoreQueryFilters().SingleAsync();
        mail.TemplateKey.Should().Be("password_reset");
        mail.ToEmail.Should().Be("u@x.io");
        mail.Payload.Should().Contain("/invite?token=");
    }

    [Fact]
    public async Task Forgot_password_for_unknown_email_is_a_silent_noop_no_enumeration()
    {
        var (svc, db, _) = Build(out _);

        await svc.ForgotPasswordAsync(new ForgotPasswordRequest("nobody@x.io"));

        (await db.Invitations.IgnoreQueryFilters().CountAsync()).Should().Be(0);
        (await db.EmailQueue.IgnoreQueryFilters().CountAsync()).Should().Be(0, "nothing leaks that the account is absent");
    }

    [Fact]
    public async Task Forgot_password_for_an_inactive_account_sends_nothing()
    {
        var (svc, db, _) = Build(out var user);
        user.Deactivate();
        await db.SaveChangesAsync();

        await svc.ForgotPasswordAsync(new ForgotPasswordRequest("u@x.io"));

        (await db.EmailQueue.IgnoreQueryFilters().CountAsync()).Should().Be(0, "inactive accounts activate via invite, not reset");
    }
}
