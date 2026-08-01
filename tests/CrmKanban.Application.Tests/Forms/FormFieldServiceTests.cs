using CrmKanban.Application.Abstractions;
using CrmKanban.Application.Common;
using CrmKanban.Application.Forms;
using CrmKanban.Domain.Entities;
using CrmKanban.Domain.Enums;
using CrmKanban.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace CrmKanban.Application.Tests.Forms;

/// <summary>
/// Configurable public-form fields (spec §4.6). Invariants: only the company admin (or super admin) may
/// manage a company's fields; active fields are exposed for public rendering.
/// </summary>
public class FormFieldServiceTests
{
    private sealed class User(Guid? id, bool super) : ICurrentUserService
    {
        public Guid? UserId => id;
        public bool IsAuthenticated => id is not null;
        public bool IsSuperAdmin => super;
        public IReadOnlyCollection<Guid> CompanyIds => [];
    }

    private static DbContextOptions<CrmDbContext> Store() =>
        new DbContextOptionsBuilder<CrmDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString(),
                new Microsoft.EntityFrameworkCore.Storage.InMemoryDatabaseRoot()).Options;

    private static async Task<(Guid CompanyId, Guid AdminId)> SeedAsync(DbContextOptions<CrmDbContext> options)
    {
        await using var db = new CrmDbContext(options, new User(null, true));
        var adminId = Guid.NewGuid();
        var company = new Company("Acme", "acme", adminId);
        db.Companies.Add(company);
        db.Memberships.Add(new Membership(adminId, company.Id, RoleType.Admin));
        await db.SaveChangesAsync();
        return (company.Id, adminId);
    }

    [Fact]
    public async Task Company_admin_creates_a_field_and_it_is_exposed_for_the_public_form()
    {
        var options = Store();
        var (companyId, adminId) = await SeedAsync(options);

        await using var db = new CrmDbContext(options, new User(adminId, false));
        var svc = new FormFieldService(db, new User(adminId, false));

        await svc.CreateAsync(companyId, new CreateFormFieldRequest("Telefon", (int)FormFieldType.Text, Required: true, Options: null));

        (await svc.ListForCompanyAsync(companyId)).Should().ContainSingle().Which.Label.Should().Be("Telefon");
        var publicFields = await svc.ListActiveAsync(companyId);
        publicFields.Should().ContainSingle().Which.Required.Should().BeTrue();
    }

    [Fact]
    public async Task A_non_admin_cannot_manage_fields()
    {
        var options = Store();
        var (companyId, _) = await SeedAsync(options);
        var stranger = new User(Guid.NewGuid(), false);

        await using var db = new CrmDbContext(options, stranger);
        var svc = new FormFieldService(db, stranger);

        var act = () => svc.CreateAsync(companyId, new CreateFormFieldRequest("X", 0, false, null));
        await act.Should().ThrowAsync<ForbiddenException>();
    }
}
