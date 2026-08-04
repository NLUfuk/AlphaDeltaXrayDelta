using CrmKanban.Application.Abstractions;
using CrmKanban.Application.Auth;
using CrmKanban.Application.Common;
using CrmKanban.Application.Users;
using CrmKanban.Domain.Entities;
using CrmKanban.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CrmKanban.Application.Tests.Users;

/// <summary>
/// Super-admin admin-account creation (spec §9): only a super admin creates admins; the account is
/// invited-pending with the company-creation flag, and duplicate emails are rejected. This is the top of
/// the onboarding chain (no self-service admin signup, §18.5).
/// </summary>
public class UserServiceTests
{
    private sealed class FakeUser(bool isSuperAdmin, Guid userId) : ICurrentUserService
    {
        public Guid? UserId => userId;
        public bool IsAuthenticated => true;
        public bool IsSuperAdmin { get; } = isSuperAdmin;
        public IReadOnlyCollection<Guid> CompanyIds => [];
    }

    private sealed class FixedClock : IClock { public DateTime UtcNow => new(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc); }

    private static DbContextOptions<CrmDbContext> Store() =>
        new DbContextOptionsBuilder<CrmDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString(),
                new Microsoft.EntityFrameworkCore.Storage.InMemoryDatabaseRoot()).Options;

    private static UserService Service(CrmDbContext db, ICurrentUserService user) =>
        new(db, user, new FixedClock(), Options.Create(new AuthOptions()));

    [Fact]
    public async Task Super_admin_creates_an_invited_admin_with_company_creation_rights()
    {
        var options = Store();
        var superAdmin = new FakeUser(true, Guid.NewGuid());
        await using var db = new CrmDbContext(options, superAdmin);

        var result = await Service(db, superAdmin).CreateAdminAsync(new CreateAdminRequest("new@x.com", "New", "Admin"));

        result.RawToken.Should().NotBeNullOrEmpty("the invite link needs a token");
        var user = await db.Users.IgnoreQueryFilters().SingleAsync(u => u.Email == "new@x.com");
        user.CanCreateCompany.Should().BeTrue();
        user.IsInvitedPending.Should().BeTrue("the admin sets their password via the invite");
        user.IsActive.Should().BeFalse();
        (await db.Invitations.IgnoreQueryFilters().CountAsync(i => i.UserId == user.Id)).Should().Be(1);
    }

    [Fact]
    public async Task A_non_super_admin_cannot_create_an_admin()
    {
        var options = Store();
        var caller = new FakeUser(false, Guid.NewGuid());
        await using var db = new CrmDbContext(options, caller);

        var act = () => Service(db, caller).CreateAdminAsync(new CreateAdminRequest("new@x.com", "New", "Admin"));
        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task The_user_list_shows_staff_by_membership_and_customers_by_the_companies_they_wrote_to()
    {
        var options = Store();
        var superAdmin = new FakeUser(true, Guid.NewGuid());
        await using var db = new CrmDbContext(options, superAdmin);
        var company = new Company("Acme", "acme", Guid.NewGuid());
        var staff = new User("staff@acme.com", "Staff", "Member");
        var customer = new User("buyer@x.com", "Buyer", "Person");
        var statusId = Guid.NewGuid();
        db.TicketStatuses.Add(new TicketStatus("Open", Domain.Enums.StatusCategory.Open, "#000", 1, false, id: statusId));
        db.Companies.Add(company);
        db.Users.AddRange(staff, customer);
        db.Memberships.Add(new Membership(staff.Id, company.Id, Domain.Enums.RoleType.Personel));
        db.Tickets.Add(new Ticket(company.Id, company.AllocateTicketNumber(), customer.Id, statusId, "Teklif", "…"));
        await db.SaveChangesAsync();

        var list = await Service(db, superAdmin).ListAsync(null);

        var staffRow = list.Single(u => u.Email == "staff@acme.com");
        staffRow.Companies.Should().ContainSingle(c => c.CompanyName == "Acme");
        var customerRow = list.Single(u => u.Email == "buyer@x.com");
        customerRow.Companies.Should().BeEmpty("a customer has no membership");
        customerRow.CustomerOf.Should().ContainSingle()
            .Which.Should().Be("Acme", "the relationship comes from their ticket");
    }

    [Fact]
    public async Task Duplicate_email_is_rejected()
    {
        var options = Store();
        var superAdmin = new FakeUser(true, Guid.NewGuid());
        await using var db = new CrmDbContext(options, superAdmin);
        db.Users.Add(new User("dup@x.com", "Existing", "User"));
        await db.SaveChangesAsync();

        var act = () => Service(db, superAdmin).CreateAdminAsync(new CreateAdminRequest("dup@x.com", "New", "Admin"));
        await act.Should().ThrowAsync<ConflictException>();
    }
}
