using CrmKanban.Application.Abstractions;
using CrmKanban.Application.Auth;
using CrmKanban.Application.Common;
using CrmKanban.Domain.Entities;
using CrmKanban.Domain.Enums;
using CrmKanban.Infrastructure.Persistence;
using CrmKanban.Infrastructure.Persistence.Interceptors;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Options;

namespace CrmKanban.Application.Tests.Auth;

/// <summary>
/// Revoking a membership must actually revoke access (spec §6, §7). Membership rows are SOFT deleted —
/// removing a member, and deleting a whole company, only stamp DeletedAt — so every read that turns
/// memberships into authority has to exclude them. The token/session path did not: it queried
/// <c>Memberships.IgnoreQueryFilters()</c> with no DeletedAt guard, so a removed member kept full staff
/// scope after a fresh login, and a deleted company stayed in the session. Tested here because this is
/// the boundary the whole tenant filter is built on: the company_id claims it reads.
/// </summary>
public class MembershipRevocationTests
{
    private sealed class SuperAdmin : ICurrentUserService
    {
        public Guid? UserId { get; } = Guid.NewGuid();
        public bool IsAuthenticated => true;
        public bool IsSuperAdmin => true;
        public IReadOnlyCollection<Guid> CompanyIds => [];
    }

    private sealed class FakeJwt : IJwtTokenService
    {
        public AccessTokenClaims? Last { get; private set; }
        public IssuedToken CreateAccessToken(AccessTokenClaims claims)
        {
            Last = claims;
            return new($"access-{claims.User.Id}", DateTime.UtcNow.AddMinutes(15));
        }
        public string CreateRefreshTokenValue() => Guid.NewGuid().ToString("N");
        public string HashRefreshToken(string rawValue) => "h:" + rawValue;
    }

    private sealed class FakeHasher : IPasswordHasher
    {
        public string Hash(User user, string password) => "hash:" + password;
        public bool Verify(User user, string hash, string password) => hash == "hash:" + password;
    }

    private sealed class FixedClock : IClock { public DateTime UtcNow => new(2026, 8, 5, 0, 0, 0, DateTimeKind.Utc); }

    private static DbContextOptions<CrmDbContext> Store() =>
        new DbContextOptionsBuilder<CrmDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString(), new InMemoryDatabaseRoot())
            .AddInterceptors(new AuditableEntityInterceptor(new FixedClock())).Options;

    /// <summary>A staff user in two companies; the membership in <paramref name="revoked"/> is then
    /// removed the way the app removes it (Remove → interceptor soft-deletes), not with a hand-written flag.</summary>
    private static async Task<(User User, Guid Kept, Guid Revoked)> SeedAsync(DbContextOptions<CrmDbContext> options)
    {
        await using var db = new CrmDbContext(options, new SuperAdmin());
        var hasher = new FakeHasher();
        var user = new User("staff@x.io", "S", "X");
        user.SetPasswordHash(hasher.Hash(user, "Passw0rd!"));
        db.Users.Add(user);

        var kept = new Company("Kalan", "kalan", Guid.NewGuid());
        var revoked = new Company("Çıkarıldı", "cikarildi", Guid.NewGuid());
        db.Companies.AddRange(kept, revoked);
        db.Memberships.Add(new Membership(user.Id, kept.Id, RoleType.Personel));
        var gone = new Membership(user.Id, revoked.Id, RoleType.Personel);
        db.Memberships.Add(gone);
        await db.SaveChangesAsync();

        db.Memberships.Remove(gone);
        await db.SaveChangesAsync();

        return (user, kept.Id, revoked.Id);
    }

    private static (AuthService Service, FakeJwt Jwt) Build(DbContextOptions<CrmDbContext> options)
    {
        var db = new CrmDbContext(options, new SuperAdmin());
        var jwt = new FakeJwt();
        return (new AuthService(db, jwt, new FakeHasher(), new FixedClock(),
            Options.Create(new AuthOptions()), Options.Create(new AppOptions()), new SuperAdmin()), jwt);
    }

    [Fact]
    public async Task A_revoked_membership_is_not_in_the_issued_tokens_company_scope()
    {
        var options = Store();
        var (_, kept, revoked) = await SeedAsync(options);
        var (svc, jwt) = Build(options);

        await svc.LoginAsync(new LoginRequest("staff@x.io", "Passw0rd!"));

        jwt.Last!.CompanyIds.Should().BeEquivalentTo([kept],
            "the company_id claims are what the tenant filter trusts — a removed member must lose scope on the next login");
        jwt.Last.CompanyIds.Should().NotContain(revoked);
    }

    [Fact]
    public async Task A_revoked_membership_is_not_reported_by_me()
    {
        var options = Store();
        var (user, kept, revoked) = await SeedAsync(options);
        var (svc, _) = Build(options);

        var me = await svc.GetMeAsync(user.Id);

        me.Companies.Select(c => c.CompanyId).Should().BeEquivalentTo([kept]);
        me.Companies.Select(c => c.CompanyId).Should().NotContain(revoked,
            "a deleted company must disappear from the session, which is what the UI's post-delete refresh relies on");
    }

    [Fact]
    public async Task A_revoked_member_is_no_longer_staff_on_that_companys_tickets()
    {
        var options = Store();
        var (user, _, revoked) = await SeedAsync(options);

        var caller = new Actor(user.Id);
        await using var db = new CrmDbContext(options, caller);
        var authz = new CrmKanban.Application.Tickets.TicketAuthorizationService(caller, new AllowAll(), db);

        // Not a member any more and not the opener → no relationship at all, so resolution must refuse.
        var ticket = new CrmKanban.Domain.Entities.Ticket(revoked, "REV-1", Guid.NewGuid(), Guid.NewGuid(), "t", "b");
        var act = () => authz.ResolveAsync(ticket);

        await act.Should().ThrowAsync<ForbiddenException>()
            .Where(e => e.Code == "ticket.forbidden", "per-record authorization must not read a soft-deleted membership as authority");
    }

    private sealed class Actor(Guid id) : ICurrentUserService
    {
        public Guid? UserId => id;
        public bool IsAuthenticated => true;
        public bool IsSuperAdmin => false;
        public IReadOnlyCollection<Guid> CompanyIds => [];
    }

    private sealed class AllowAll : IPermissionService
    {
        public Task<IReadOnlySet<string>> GetPermissionsAsync(Guid u, Guid c, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlySet<string>>(new HashSet<string>());
        public Task<bool> HasPermissionAsync(Guid u, Guid c, string k, CancellationToken ct = default) => Task.FromResult(true);
    }
}
