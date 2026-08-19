using CrmKanban.Application.Abstractions;
using CrmKanban.Domain.Entities;
using CrmKanban.Domain.Enums;
using CrmKanban.Infrastructure.Identity;
using CrmKanban.Infrastructure.Persistence;
using CrmKanban.Infrastructure.Persistence.Seed;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CrmKanban.Application.Tests.Persistence;

/// <summary>
/// The demo environment's two safety rules, after the shared demo password turned out to be readable in
/// a public repository (tech debt #64):
/// <list type="number">
///   <item>No configured password → no demo seeding at all (fail closed).</item>
///   <item>Changing the password actually changes the EXISTING accounts. The seeder skips a tenant whose
///   slug already exists, so without an explicit rotation pass a re-configured password would leave every
///   old hash working and the fix would be imaginary.</item>
/// </list>
/// The rotation must not touch a super admin (their credentials come from the real seeder) or an account
/// that has no password yet (a pending invitation would be silently activated).
/// </summary>
public class DemoSeedSecurityTests
{
    private sealed class FakeStorage : IFileStorage
    {
        public Task PutAsync(string key, Stream content, string contentType, CancellationToken ct = default) => Task.CompletedTask;
        public Task<Stream> GetAsync(string key, CancellationToken ct = default) => Task.FromResult<Stream>(new MemoryStream());
        public Task DeleteAsync(string key, CancellationToken ct = default) => Task.CompletedTask;
        public string PresignPut(string key, string contentType, TimeSpan expiry) => "http://test/put";
        public string PresignGet(string key, TimeSpan expiry) => "http://test/get";
    }

    private static DbContextOptions<CrmDbContext> Store() =>
        new DbContextOptionsBuilder<CrmDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString(),
                new Microsoft.EntityFrameworkCore.Storage.InMemoryDatabaseRoot()).Options;

    /// <summary>The demo seeder builds on the real seed (global statuses, permissions), so tests have to
    /// lay that down first — exactly as startup does.</summary>
    private static async Task BaseSeedAsync(DbContextOptions<CrmDbContext> options) =>
        await new DatabaseSeeder(options, new PasswordHasher<User>(),
            new SeederOptions(null, null), NullLogger<DatabaseSeeder>.Instance).SeedAsync();

    private static DevSeeder SeederWith(DbContextOptions<CrmDbContext> options, string? password) =>
        new(options, new PasswordHasher<User>(), new FakeStorage(),
            Options.Create(new DemoOptions { Demo = true, DemoPassword = password }),
            NullLogger<DevSeeder>.Instance);

    [Fact]
    public async Task No_configured_password_means_no_demo_data_at_all()
    {
        var options = Store();

        await SeederWith(options, null).SeedAsync();

        await using var db = new CrmDbContext(options, SystemCurrentUserService.System);
        (await db.Companies.IgnoreQueryFilters().CountAsync()).Should().Be(0);
        (await db.Users.IgnoreQueryFilters().CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task A_password_shorter_than_the_minimum_counts_as_unset()
    {
        var options = Store();

        await SeederWith(options, "kisa").SeedAsync();

        await using var db = new CrmDbContext(options, SystemCurrentUserService.System);
        (await db.Companies.IgnoreQueryFilters().CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Reconfiguring_the_password_rotates_the_accounts_that_were_already_seeded()
    {
        const string oldPassword = "EskiDemo!2026x";
        const string newPassword = "YeniDemo!2026x";
        var options = Store();
        var hasher = new PasswordHasher<User>();

        await BaseSeedAsync(options);
        await SeederWith(options, oldPassword).SeedAsync();

        // A super admin and a never-activated invitee, both of which the rotation must leave alone.
        Guid superAdminId, pendingId;
        await using (var arrange = new CrmDbContext(options, SystemCurrentUserService.System))
        {
            var superAdmin = new User("super@crmkanban.local", "Super", "Admin");
            superAdmin.PromoteToSuperAdmin();
            superAdmin.SetPasswordHash(hasher.HashPassword(superAdmin, "SuperAdmin!Secret1"));
            var tekstil = await arrange.Companies.IgnoreQueryFilters().FirstAsync(c => c.Slug == DemoTenants.Tekstil);
            // Both are attached to the demo tenant, so only the exclusions can save them.
            arrange.Memberships.Add(new Membership(superAdmin.Id, tekstil.Id, RoleType.Admin));
            var pending = new User("davetli@tekstil.local", "Davet", "Bekliyor"); // no password set yet
            arrange.Memberships.Add(new Membership(pending.Id, tekstil.Id, RoleType.Personel));
            arrange.Users.AddRange(superAdmin, pending);
            await arrange.SaveChangesAsync();
            (superAdminId, pendingId) = (superAdmin.Id, pending.Id);
        }

        // Second run with a different password: the tenants already exist, so seeding bails out and only
        // the rotation does anything.
        await SeederWith(options, newPassword).SeedAsync();

        await using var db = new CrmDbContext(options, SystemCurrentUserService.System);
        var admin = await db.Users.IgnoreQueryFilters().FirstAsync(u => u.Email == "admin@tekstil.local");
        var customer = await db.Users.IgnoreQueryFilters().FirstAsync(u => u.Email == "alici1@tekstil-musteri.local");
        var superAdminAfter = await db.Users.IgnoreQueryFilters().FirstAsync(u => u.Id == superAdminId);
        var pendingAfter = await db.Users.IgnoreQueryFilters().FirstAsync(u => u.Id == pendingId);

        hasher.VerifyHashedPassword(admin, admin.PasswordHash!, newPassword)
            .Should().NotBe(PasswordVerificationResult.Failed, "the reconfigured password must actually take effect");
        hasher.VerifyHashedPassword(admin, admin.PasswordHash!, oldPassword)
            .Should().Be(PasswordVerificationResult.Failed, "the password published in the repository must stop working");
        hasher.VerifyHashedPassword(customer, customer.PasswordHash!, newPassword)
            .Should().NotBe(PasswordVerificationResult.Failed, "customers are demo accounts too");

        hasher.VerifyHashedPassword(superAdminAfter, superAdminAfter.PasswordHash!, "SuperAdmin!Secret1")
            .Should().NotBe(PasswordVerificationResult.Failed, "a super admin's real credentials are never rewritten");
        pendingAfter.PasswordHash.Should().BeNull("a pending invitation must not be activated by the rotation");
    }
}
