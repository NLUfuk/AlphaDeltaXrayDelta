using CrmKanban.Application.Abstractions;
using CrmKanban.Application.Common;
using CrmKanban.Application.Companies;
using CrmKanban.Domain.Entities;
using CrmKanban.Domain.Enums;
using CrmKanban.Infrastructure.Persistence;
using CrmKanban.Infrastructure.Persistence.Interceptors;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace CrmKanban.Application.Tests.Companies;

/// <summary>
/// Company onboarding (spec §8/§9/§18.8). The invariants: only a flagged admin (or super admin) may
/// create a company, the creator becomes its Admin, and slugs are globally unique. Runs over the real
/// DbContext (InMemory).
/// </summary>
public class CompanyServiceTests
{
    private sealed class FakeUser(bool isSuperAdmin, Guid userId) : ICurrentUserService
    {
        public Guid? UserId => userId;
        public bool IsAuthenticated => true;
        public bool IsSuperAdmin { get; } = isSuperAdmin;
        public IReadOnlyCollection<Guid> CompanyIds => [];
    }

    private sealed class FixedClock : IClock { public DateTime UtcNow => new(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc); }

    // The audit interceptor is wired in because the delete path depends on it: Remove must become a
    // soft delete, and "nothing is physically lost" is exactly what these tests have to prove.
    private static DbContextOptions<CrmDbContext> Store() =>
        new DbContextOptionsBuilder<CrmDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString(),
                new Microsoft.EntityFrameworkCore.Storage.InMemoryDatabaseRoot())
            .AddInterceptors(new AuditableEntityInterceptor(new FixedClock())).Options;

    private static async Task<Guid> SeedAdminAsync(DbContextOptions<CrmDbContext> options, bool canCreate)
    {
        await using var db = new CrmDbContext(options, new FakeUser(true, Guid.NewGuid()));
        var u = new User("admin@x.com", "Ada", "Admin");
        if (canCreate) u.AllowCompanyCreation();
        db.Users.Add(u);
        await db.SaveChangesAsync();
        return u.Id;
    }

    private static CompanyService Service(CrmDbContext db, ICurrentUserService user) => new(db, user, new FixedClock());

    [Fact]
    public async Task Admin_with_flag_creates_company_and_becomes_its_admin()
    {
        var options = Store();
        var adminId = await SeedAdminAsync(options, canCreate: true);
        var admin = new FakeUser(false, adminId);

        await using var db = new CrmDbContext(options, admin);
        var dto = await Service(db, admin).CreateAsync(new CreateCompanyRequest("Acme", "acme"));

        dto.Slug.Should().Be("acme");
        dto.OwnerAdminId.Should().Be(adminId);
        (await db.Memberships.IgnoreQueryFilters().SingleAsync(m => m.CompanyId == dto.Id))
            .Should().BeEquivalentTo(new { UserId = adminId, Role = RoleType.Admin }, o => o.ExcludingMissingMembers());
    }

    [Fact]
    public async Task A_user_without_the_flag_cannot_create_a_company()
    {
        var options = Store();
        var userId = await SeedAdminAsync(options, canCreate: false);
        var caller = new FakeUser(false, userId);

        await using var db = new CrmDbContext(options, caller);
        var act = () => Service(db, caller).CreateAsync(new CreateCompanyRequest("Acme", "acme"));
        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task Duplicate_slug_is_rejected()
    {
        var options = Store();
        var adminId = await SeedAdminAsync(options, canCreate: true);
        var admin = new FakeUser(false, adminId);

        await using var db = new CrmDbContext(options, admin);
        await Service(db, admin).CreateAsync(new CreateCompanyRequest("Acme", "acme"));
        var act = () => Service(db, admin).CreateAsync(new CreateCompanyRequest("Acme2", "acme"));
        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task Non_member_cannot_list_members()
    {
        var options = Store();
        var adminId = await SeedAdminAsync(options, canCreate: true);
        Guid companyId;
        await using (var db = new CrmDbContext(options, new FakeUser(false, adminId)))
            companyId = (await Service(db, new FakeUser(false, adminId)).CreateAsync(new CreateCompanyRequest("Acme", "acme"))).Id;

        var stranger = new FakeUser(false, Guid.NewGuid());
        await using var read = new CrmDbContext(options, stranger);
        var act = () => Service(read, stranger).ListMembersAsync(companyId);
        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task Super_admin_is_never_listed_as_a_member()
    {
        var options = Store();
        var adminId = await SeedAdminAsync(options, canCreate: true);
        var admin = new FakeUser(false, adminId);

        Guid companyId;
        await using (var db = new CrmDbContext(options, admin))
        {
            companyId = (await Service(db, admin).CreateAsync(new CreateCompanyRequest("Acme", "acme"))).Id;
            // A super admin sitting inside the company's memberships must stay hidden from staff pickers.
            var su = new User("root@x.com", "Super", "Admin");
            su.PromoteToSuperAdmin();
            db.Users.Add(su);
            db.Memberships.Add(new Membership(su.Id, companyId, RoleType.Admin));
            await db.SaveChangesAsync();
        }

        await using (var db = new CrmDbContext(options, admin))
        {
            var members = await Service(db, admin).ListMembersAsync(companyId);
            members.Should().OnlyContain(m => m.Email != "root@x.com");
            members.Should().ContainSingle(m => m.UserId == adminId);
        }
    }

    [Fact]
    public async Task Admin_can_remove_a_member_but_not_the_owner()
    {
        var options = Store();
        var adminId = await SeedAdminAsync(options, canCreate: true);
        var admin = new FakeUser(false, adminId);

        Guid companyId;
        var staffId = Guid.NewGuid();
        await using (var db = new CrmDbContext(options, admin))
        {
            companyId = (await Service(db, admin).CreateAsync(new CreateCompanyRequest("Acme", "acme"))).Id;
            db.Memberships.Add(new Membership(staffId, companyId, RoleType.Personel));
            await db.SaveChangesAsync();
        }

        await using (var db = new CrmDbContext(options, admin))
            await Service(db, admin).RemoveMemberAsync(companyId, staffId);

        await using (var db = new CrmDbContext(options, admin))
        {
            (await db.Memberships.IgnoreQueryFilters().CountAsync(m => m.UserId == staffId && m.DeletedAt == null))
                .Should().Be(0, "the member was removed (soft-deleted)");
            // The owner cannot be removed.
            var act = () => Service(db, admin).RemoveMemberAsync(companyId, adminId);
            await act.Should().ThrowAsync<BadRequestException>();
        }
    }

    [Fact]
    public async Task An_admin_can_own_several_companies()
    {
        var options = Store();
        var adminId = await SeedAdminAsync(options, canCreate: true);
        var admin = new FakeUser(false, adminId);

        await using var db = new CrmDbContext(options, admin);
        var svc = Service(db, admin);
        await svc.CreateAsync(new CreateCompanyRequest("Acme", "acme"));
        await svc.CreateAsync(new CreateCompanyRequest("Globex", "globex"));

        var mine = await svc.ListAsync();
        mine.Should().HaveCount(2).And.OnlyContain(c => c.OwnerAdminId == adminId);
        (await db.Memberships.IgnoreQueryFilters().CountAsync(m => m.UserId == adminId && m.Role == RoleType.Admin))
            .Should().Be(2, "each company gets its own Admin membership");
    }

    [Fact]
    public async Task Delete_needs_the_matching_company_name()
    {
        var options = Store();
        var adminId = await SeedAdminAsync(options, canCreate: true);
        var admin = new FakeUser(false, adminId);

        await using var db = new CrmDbContext(options, admin);
        var svc = Service(db, admin);
        var company = await svc.CreateAsync(new CreateCompanyRequest("Acme", "acme"));

        var act = () => svc.DeleteAsync(company.Id, new DeleteCompanyRequest("Acme Ltd"));
        await act.Should().ThrowAsync<BadRequestException>();
        (await svc.ListAsync()).Should().ContainSingle();
    }

    [Fact]
    public async Task Only_the_owner_or_a_super_admin_deletes_a_company()
    {
        var options = Store();
        var ownerId = await SeedAdminAsync(options, canCreate: true);
        var owner = new FakeUser(false, ownerId);

        Guid companyId;
        var otherAdminId = Guid.NewGuid();
        await using (var db = new CrmDbContext(options, owner))
        {
            companyId = (await Service(db, owner).CreateAsync(new CreateCompanyRequest("Acme", "acme"))).Id;
            // A second Admin of the same company: may manage members, must NOT be able to delete the tenant.
            db.Memberships.Add(new Membership(otherAdminId, companyId, RoleType.Admin));
            await db.SaveChangesAsync();
        }

        var otherAdmin = new FakeUser(false, otherAdminId);
        await using (var db = new CrmDbContext(options, otherAdmin))
        {
            var act = () => Service(db, otherAdmin).DeleteAsync(companyId, new DeleteCompanyRequest("Acme"));
            await act.Should().ThrowAsync<ForbiddenException>();
        }
    }

    [Fact]
    public async Task Deleting_hides_the_company_closes_it_and_frees_the_slug()
    {
        var options = Store();
        var adminId = await SeedAdminAsync(options, canCreate: true);
        var admin = new FakeUser(false, adminId);

        await using var db = new CrmDbContext(options, admin);
        var svc = Service(db, admin);
        var company = await svc.CreateAsync(new CreateCompanyRequest("Acme", "acme"));

        await svc.DeleteAsync(company.Id, new DeleteCompanyRequest(" acme ")); // trimmed + case-insensitive

        (await svc.ListAsync()).Should().BeEmpty();
        var row = await db.Companies.IgnoreQueryFilters().SingleAsync(c => c.Id == company.Id);
        row.DeletedAt.Should().NotBeNull("the row stays for history");
        row.IsArchived.Should().BeTrue("public intake must close with the delete");
        row.Slug.Should().NotBe("acme");
        (await db.Memberships.IgnoreQueryFilters().CountAsync(m => m.CompanyId == company.Id && m.DeletedAt == null))
            .Should().Be(0, "the tenant disappears from every member's session");

        // The freed slug can be used again.
        var reborn = await svc.CreateAsync(new CreateCompanyRequest("Acme", "acme"));
        reborn.Id.Should().NotBe(company.Id);
    }

    [Fact]
    public async Task An_admin_of_the_company_edits_the_contact_card_but_never_the_slug()
    {
        var options = Store();
        var adminId = await SeedAdminAsync(options, canCreate: true);
        var admin = new FakeUser(false, adminId);

        await using var db = new CrmDbContext(options, admin);
        var svc = Service(db, admin);
        var created = await svc.CreateAsync(new CreateCompanyRequest("Acme", "acme", Phone: " 0212 555 00 00 "));
        created.Phone.Should().Be("0212 555 00 00", "contact values are trimmed on the way in");

        var updated = await svc.UpdateAsync(created.Id, new UpdateCompanyRequest(
            "Acme A.Ş.", Phone: "0850 111 22 33", Email: "  Info@ACME.com ", Website: "www.acme.com", Address: "   "));

        updated.Name.Should().Be("Acme A.Ş.");
        updated.Email.Should().Be("info@acme.com", "the address is normalised like every other email in the system");
        updated.Address.Should().BeNull("blank means 'not filled in', not an empty string");
        updated.Slug.Should().Be("acme", "customers already hold /c/{slug} links — editing must not break them");
    }

    [Fact]
    public async Task Only_an_admin_of_that_company_can_edit_it()
    {
        var options = Store();
        var ownerId = await SeedAdminAsync(options, canCreate: true);
        var owner = new FakeUser(false, ownerId);

        Guid companyId;
        var staffId = Guid.NewGuid();
        await using (var db = new CrmDbContext(options, owner))
        {
            companyId = (await Service(db, owner).CreateAsync(new CreateCompanyRequest("Acme", "acme"))).Id;
            db.Memberships.Add(new Membership(staffId, companyId, RoleType.Personel));
            await db.SaveChangesAsync();
        }

        // Personel of the company: a member, but not an admin.
        var staff = new FakeUser(false, staffId);
        await using (var db = new CrmDbContext(options, staff))
        {
            var act = () => Service(db, staff).UpdateAsync(companyId, new UpdateCompanyRequest("Ele geçirildi"));
            await act.Should().ThrowAsync<ForbiddenException>();
        }

        // An admin of ANOTHER company must not reach this tenant's row either.
        var stranger = new FakeUser(false, Guid.NewGuid());
        await using (var db = new CrmDbContext(options, stranger))
        {
            var act = () => Service(db, stranger).UpdateAsync(companyId, new UpdateCompanyRequest("Ele geçirildi"));
            await act.Should().ThrowAsync<ForbiddenException>();
        }

        await using (var read = new CrmDbContext(options, owner))
            (await read.Companies.IgnoreQueryFilters().SingleAsync(c => c.Id == companyId)).Name.Should().Be("Acme");
    }

    [Fact]
    public async Task A_stranger_cannot_remove_members()
    {
        var options = Store();
        var adminId = await SeedAdminAsync(options, canCreate: true);
        var admin = new FakeUser(false, adminId);

        Guid companyId;
        var staffId = Guid.NewGuid();
        await using (var db = new CrmDbContext(options, admin))
        {
            companyId = (await Service(db, admin).CreateAsync(new CreateCompanyRequest("Acme", "acme"))).Id;
            db.Memberships.Add(new Membership(staffId, companyId, RoleType.Personel));
            await db.SaveChangesAsync();
        }

        var stranger = new FakeUser(false, Guid.NewGuid());
        await using var read = new CrmDbContext(options, stranger);
        var act = () => Service(read, stranger).RemoveMemberAsync(companyId, staffId);
        await act.Should().ThrowAsync<ForbiddenException>();
    }
}
