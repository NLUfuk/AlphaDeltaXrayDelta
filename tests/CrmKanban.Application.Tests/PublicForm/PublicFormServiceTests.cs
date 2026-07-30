using CrmKanban.Application.Abstractions;
using CrmKanban.Application.Auth;
using CrmKanban.Application.Common;
using CrmKanban.Application.Files;
using CrmKanban.Application.PublicForm;
using CrmKanban.Application.Tickets;
using CrmKanban.Domain.Entities;
using CrmKanban.Domain.Enums;
using CrmKanban.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CrmKanban.Application.Tests.PublicForm;

/// <summary>
/// The anonymous public form (spec §10). Core-adjacent: it writes tenant data unauthenticated, so the
/// gates matter — CAPTCHA fails closed, KVKK consent is required, an archived company's form is shut,
/// and a matching email links instead of duplicating. Uses the real DbContext (InMemory) end to end.
/// </summary>
public class PublicFormServiceTests
{
    private sealed class Anonymous : ICurrentUserService
    {
        public Guid? UserId => null;
        public bool IsAuthenticated => false;
        public bool IsSuperAdmin => false;
        public IReadOnlyCollection<Guid> CompanyIds => [];
    }

    private sealed class SuperAdmin : ICurrentUserService
    {
        public Guid? UserId => Guid.NewGuid();
        public bool IsAuthenticated => true;
        public bool IsSuperAdmin => true;
        public IReadOnlyCollection<Guid> CompanyIds => [];
    }

    private sealed class FakeCaptcha(bool result) : ICaptchaValidator
    {
        public Task<bool> ValidateAsync(string? token, CancellationToken ct = default) => Task.FromResult(result);
    }

    private sealed class FakeFileStorage : IFileStorage
    {
        public string PresignPut(string key, string contentType, TimeSpan expiry) => "https://storage.test/put";
        public string PresignGet(string key, TimeSpan expiry) => "https://storage.test/get";
    }

    private sealed class FakePermissionService : IPermissionService
    {
        public Task<IReadOnlySet<string>> GetPermissionsAsync(Guid u, Guid c, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlySet<string>>(new HashSet<string>());
        public Task<bool> HasPermissionAsync(Guid u, Guid c, string k, CancellationToken ct = default) => Task.FromResult(false);
    }

    private sealed class FixedClock : IClock
    {
        public DateTime UtcNow => new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    }

    private const string Slug = "acme";
    private static readonly Guid StatusId = Guid.NewGuid();

    private static DbContextOptions<CrmDbContext> Store() =>
        new DbContextOptionsBuilder<CrmDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString(),
                new Microsoft.EntityFrameworkCore.Storage.InMemoryDatabaseRoot()).Options;

    private static async Task<Guid> SeedCompanyAsync(DbContextOptions<CrmDbContext> options, bool archived = false)
    {
        await using var db = new CrmDbContext(options, new SuperAdmin());
        db.TicketStatuses.Add(new TicketStatus("Open", StatusCategory.Open, "#000", 1, isTerminal: false, id: StatusId));
        var company = new Company("Acme", Slug, Guid.NewGuid());
        if (archived) company.Archive(DateTime.UtcNow);
        db.Companies.Add(company);
        await db.SaveChangesAsync();
        return company.Id;
    }

    private static PublicFormService ServiceFor(DbContextOptions<CrmDbContext> options, bool captchaOk = true)
    {
        var db = new CrmDbContext(options, new Anonymous());
        // The public form never calls authz, but AttachmentService needs one; a real instance over the
        // anonymous context is enough (no attachment paths are exercised in these tests).
        var authz = new TicketAuthorizationService(new Anonymous(), new FakePermissionService(), db);
        var attachments = new AttachmentService(db, new FakeFileStorage(), authz, new FixedClock(),
            Options.Create(new Application.Files.FileOptions()));
        var settings = new Application.Settings.SettingsService(db, new Anonymous());
        return new PublicFormService(db, new FakeCaptcha(captchaOk), attachments, new FixedClock(),
            settings, Options.Create(new AuthOptions()));
    }

    private static PublicFormSubmitRequest Request(bool consent = true) =>
        new("Jane", "Doe", "jane@example.com", "Printer broken", "It won't print.", consent);

    [Fact]
    public async Task Submit_creates_a_ticket_in_the_company_and_a_pending_customer_with_an_invite_token()
    {
        var options = Store();
        var companyId = await SeedCompanyAsync(options);

        var result = await ServiceFor(options).SubmitAsync(Slug, Request());

        result.TicketNumber.Should().StartWith("ACME-");
        result.InviteToken.Should().NotBeNullOrEmpty("a brand-new customer must get a set-password link");

        await using var read = new CrmDbContext(options, new SuperAdmin());
        var ticket = await read.Tickets.SingleAsync();
        ticket.CompanyId.Should().Be(companyId);
        ticket.StatusId.Should().Be(StatusId);
        var user = await read.Users.SingleAsync(u => u.Email == "jane@example.com");
        ticket.OpenedById.Should().Be(user.Id);
        user.IsInvitedPending.Should().BeTrue();
        (await read.Invitations.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Submit_links_an_existing_user_and_issues_no_new_invite()
    {
        var options = Store();
        await SeedCompanyAsync(options);
        Guid existingUserId;
        await using (var seed = new CrmDbContext(options, new SuperAdmin()))
        {
            var u = new User("jane@example.com", "Jane", "Doe");
            u.SetPasswordHash("hash"); // already active
            seed.Users.Add(u);
            await seed.SaveChangesAsync();
            existingUserId = u.Id;
        }

        var result = await ServiceFor(options).SubmitAsync(Slug, Request());

        result.InviteToken.Should().BeNull("a known user already has an account");
        await using var read = new CrmDbContext(options, new SuperAdmin());
        (await read.Tickets.SingleAsync()).OpenedById.Should().Be(existingUserId);
        (await read.Invitations.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Submit_fails_closed_when_captcha_rejects()
    {
        var options = Store();
        await SeedCompanyAsync(options);

        var act = () => ServiceFor(options, captchaOk: false).SubmitAsync(Slug, Request());

        await act.Should().ThrowAsync<BadRequestException>().Where(e => e.Code == "captcha.failed");
    }

    [Fact]
    public async Task Submit_requires_kvkk_consent()
    {
        var options = Store();
        await SeedCompanyAsync(options);

        var act = () => ServiceFor(options).SubmitAsync(Slug, Request(consent: false));

        await act.Should().ThrowAsync<BadRequestException>().Where(e => e.Code == "kvkk.consent_required");
    }

    [Fact]
    public async Task Submit_to_an_archived_company_is_closed()
    {
        var options = Store();
        await SeedCompanyAsync(options, archived: true);

        var act = () => ServiceFor(options).SubmitAsync(Slug, Request());

        await act.Should().ThrowAsync<ConflictException>().Where(e => e.Code == "company.form_closed");
    }

    [Fact]
    public async Task Submit_to_an_unknown_slug_is_not_found()
    {
        var options = Store();
        await SeedCompanyAsync(options);

        var act = () => ServiceFor(options).SubmitAsync("nope", Request());

        await act.Should().ThrowAsync<NotFoundException>();
    }
}
