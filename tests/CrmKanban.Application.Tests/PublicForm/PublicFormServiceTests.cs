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
        public Task PutAsync(string key, Stream content, string contentType, CancellationToken ct = default) => Task.CompletedTask;
        public Task<Stream> GetAsync(string key, CancellationToken ct = default) => Task.FromResult<Stream>(new MemoryStream());
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

    /// <summary>A signed-in customer (no company membership) — the /c/{slug} caller.</summary>
    private sealed class SignedIn(Guid id) : ICurrentUserService
    {
        public Guid? UserId => id;
        public bool IsAuthenticated => true;
        public bool IsSuperAdmin => false;
        public IReadOnlyCollection<Guid> CompanyIds => [];
    }

    private static PublicFormService ServiceFor(
        DbContextOptions<CrmDbContext> options, bool captchaOk = true, ICurrentUserService? caller = null)
    {
        var db = new CrmDbContext(options, new Anonymous());
        // The public form never calls authz, but AttachmentService needs one; a real instance over the
        // anonymous context is enough (no attachment paths are exercised in these tests).
        var authz = new TicketAuthorizationService(new Anonymous(), new FakePermissionService(), db);
        var attachments = new AttachmentService(db, new FakeFileStorage(), authz, new FixedClock(),
            Options.Create(new Application.Files.FileOptions()));
        var settings = new Application.Settings.SettingsService(db, new Anonymous());
        var formFields = new Application.Forms.FormFieldService(db, new Anonymous());
        return new PublicFormService(db, new FakeCaptcha(captchaOk), attachments, formFields, new FixedClock(),
            settings, caller ?? new Anonymous(), Options.Create(new AuthOptions()), Options.Create(new AppOptions()));
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
        result.NewAccount.Should().BeTrue("a brand-new customer gets an activation email");

        await using var read = new CrmDbContext(options, new SuperAdmin());
        var ticket = await read.Tickets.SingleAsync();
        ticket.CompanyId.Should().Be(companyId);
        ticket.StatusId.Should().Be(StatusId);
        var user = await read.Users.SingleAsync(u => u.Email == "jane@example.com");
        ticket.OpenedById.Should().Be(user.Id);
        user.IsInvitedPending.Should().BeTrue();
        (await read.Invitations.CountAsync()).Should().Be(1);
        // The activation link is emailed (not returned to the client): exactly one queued invite.
        var mail = await read.EmailQueue.SingleAsync();
        mail.TemplateKey.Should().Be("account_invite");
        mail.ToEmail.Should().Be("jane@example.com");
        mail.Payload.Should().Contain("/invite?token=");
    }

    [Fact]
    public async Task Submit_stores_custom_field_values_on_the_ticket()
    {
        var options = Store();
        var companyId = await SeedCompanyAsync(options);
        Guid fieldId;
        await using (var seed = new CrmDbContext(options, new SuperAdmin()))
        {
            var field = new FormField(companyId, "Telefon", FormFieldType.Text, required: true, sortOrder: 0);
            seed.FormFields.Add(field);
            await seed.SaveChangesAsync();
            fieldId = field.Id;
        }

        var request = Request() with { CustomFields = new Dictionary<string, string> { [fieldId.ToString()] = "555-1234" } };
        await ServiceFor(options).SubmitAsync(Slug, request);

        await using var read = new CrmDbContext(options, new SuperAdmin());
        var ticket = await read.Tickets.SingleAsync();
        ticket.CustomFieldsJson.Should().NotBeNull();
        ticket.CustomFieldsJson.Should().Contain("Telefon").And.Contain("555-1234");
    }

    [Fact]
    public async Task Submit_rejects_a_missing_required_custom_field()
    {
        var options = Store();
        var companyId = await SeedCompanyAsync(options);
        await using (var seed = new CrmDbContext(options, new SuperAdmin()))
        {
            seed.FormFields.Add(new FormField(companyId, "Telefon", FormFieldType.Text, required: true, sortOrder: 0));
            await seed.SaveChangesAsync();
        }

        var act = () => ServiceFor(options).SubmitAsync(Slug, Request()); // no CustomFields
        await act.Should().ThrowAsync<BadRequestException>().Where(e => e.Code == "formfield.required");
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

        result.NewAccount.Should().BeFalse("a known user already has an account");
        await using var read = new CrmDbContext(options, new SuperAdmin());
        (await read.Tickets.SingleAsync()).OpenedById.Should().Be(existingUserId);
        (await read.Invitations.CountAsync()).Should().Be(0);
        (await read.EmailQueue.CountAsync()).Should().Be(0, "no activation email for an existing account");
    }

    [Fact]
    public async Task Submit_by_a_first_time_customer_is_held_for_approval()
    {
        var options = Store();
        await SeedCompanyAsync(options);

        await ServiceFor(options).SubmitAsync(Slug, Request());

        await using var read = new CrmDbContext(options, new SuperAdmin());
        (await read.Tickets.SingleAsync()).ApprovalState.Should().Be(TicketApprovalState.Pending);
    }

    [Fact]
    public async Task Submit_by_a_known_customer_enters_the_pool_directly()
    {
        var options = Store();
        await SeedCompanyAsync(options);
        await using (var seed = new CrmDbContext(options, new SuperAdmin()))
        {
            var u = new User("jane@example.com", "Jane", "Doe");
            u.SetPasswordHash("hash");
            seed.Users.Add(u);
            await seed.SaveChangesAsync();
        }

        await ServiceFor(options).SubmitAsync(Slug, Request());

        await using var read = new CrmDbContext(options, new SuperAdmin());
        (await read.Tickets.SingleAsync()).ApprovalState.Should().Be(TicketApprovalState.Approved);
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

    // ---- signed-in customer path (/c/{slug}), the first contact that creates the relationship ----

    [Fact]
    public async Task A_signed_in_customer_request_enters_the_pool_directly_and_creates_the_relationship()
    {
        var options = Store();
        var companyId = await SeedCompanyAsync(options);
        var customer = new User("jane@example.com", "Jane", "Doe");
        await using (var seed = new CrmDbContext(options, new SuperAdmin()))
        {
            seed.Users.Add(customer);
            await seed.SaveChangesAsync();
        }

        var result = await ServiceFor(options, caller: new SignedIn(customer.Id))
            .SubmitAsCustomerAsync(Slug, new CustomerFormSubmitRequest("Teklif", "Fiyat alabilir miyim?"));

        result.TicketNumber.Should().StartWith("ACME-");
        await using var read = new CrmDbContext(options, new SuperAdmin());
        var ticket = await read.Tickets.SingleAsync();
        ticket.CompanyId.Should().Be(companyId);
        ticket.OpenedById.Should().Be(customer.Id);
        ticket.ApprovalState.Should().Be(TicketApprovalState.Approved,
            "the address was already proven by the emailed code — no moderation hold");
        (await read.EmailQueue.CountAsync()).Should().Be(0, "the account already exists; nothing to activate");
    }

    [Fact]
    public async Task An_anonymous_caller_cannot_use_the_signed_in_request_path()
    {
        var options = Store();
        await SeedCompanyAsync(options);

        var act = () => ServiceFor(options).SubmitAsCustomerAsync(Slug, new CustomerFormSubmitRequest("t", "b"));

        await act.Should().ThrowAsync<UnauthorizedException>();
    }
}
