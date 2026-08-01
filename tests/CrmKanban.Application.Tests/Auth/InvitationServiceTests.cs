using CrmKanban.Application.Abstractions;
using CrmKanban.Application.Auth;
using CrmKanban.Domain.Entities;
using CrmKanban.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CrmKanban.Application.Tests.Auth;

public class InvitationServiceTests
{
    private sealed class FakeCurrentUser : ICurrentUserService
    {
        public Guid? UserId => null;
        public bool IsAuthenticated => false;
        public bool IsSuperAdmin => true;
        public IReadOnlyCollection<Guid> CompanyIds => [];
    }

    private sealed class FakeHasher : IPasswordHasher
    {
        public string Hash(User user, string password) => "hash:" + password;
        public bool Verify(User user, string hash, string password) => hash == "hash:" + password;
    }

    private sealed class FakeClock(DateTime now) : IClock { public DateTime UtcNow => now; }

    private sealed class FakePermissions : IPermissionService
    {
        public Task<IReadOnlySet<string>> GetPermissionsAsync(Guid u, Guid c, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlySet<string>>(new HashSet<string>());
        public Task<bool> HasPermissionAsync(Guid u, Guid c, string k, CancellationToken ct = default) =>
            Task.FromResult(true);
    }

    private static CrmDbContext NewDb() =>
        new(new DbContextOptionsBuilder<CrmDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options, new FakeCurrentUser());

    // A password reset reuses AcceptInvite; it must set the new password AND kill every existing session,
    // otherwise a reset would leave a stolen refresh token alive. (spec §1.12 security invariant)
    [Fact]
    public async Task Accepting_a_reset_token_sets_the_password_and_revokes_existing_sessions()
    {
        var db = NewDb();
        var hasher = new FakeHasher();
        var now = new DateTime(2026, 8, 1, 12, 0, 0, DateTimeKind.Utc);

        var user = new User("u@x.io", "U", "X");
        user.SetPasswordHash(hasher.Hash(user, "OldPassw0rd!"));
        db.Users.Add(user);
        // Two live sessions the user (or an attacker) already holds.
        db.RefreshTokens.Add(new RefreshToken(user.Id, "h:a", now.AddDays(30)));
        db.RefreshTokens.Add(new RefreshToken(user.Id, "h:b", now.AddDays(30)));
        var raw = TokenHasher.NewRawToken();
        db.Invitations.Add(new Invitation(user.Id, TokenHasher.Hash(raw), now.AddDays(7), invitedById: null));
        await db.SaveChangesAsync();

        var svc = new InvitationService(db, hasher, new FakeClock(now), new FakeCurrentUser(),
            new FakePermissions(), Options.Create(new AuthOptions()), Options.Create(new AppOptions()));

        await svc.AcceptInviteAsync(new AcceptInviteRequest(raw, "NewPassw0rd!"));

        var reloaded = await db.Users.IgnoreQueryFilters().SingleAsync(u => u.Id == user.Id);
        reloaded.PasswordHash.Should().Be("hash:NewPassw0rd!", "the new password is set");
        (await db.RefreshTokens.IgnoreQueryFilters().CountAsync(t => t.RevokedAt == null))
            .Should().Be(0, "setting a password kills every existing session");
    }
}
