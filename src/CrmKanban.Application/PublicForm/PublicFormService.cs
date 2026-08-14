using CrmKanban.Application.Abstractions;
using CrmKanban.Application.Auth;
using CrmKanban.Application.Common;
using CrmKanban.Application.Files;
using CrmKanban.Domain.Entities;
using CrmKanban.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CrmKanban.Application.PublicForm;

/// <summary>
/// The anonymous public form (spec §10): a customer opens a ticket from a company's slug link without
/// logging in. Runs unauthenticated, so it reads tenant data with IgnoreQueryFilters and writes into
/// the resolved company only. A matching email links to the existing user; otherwise a pending
/// customer + a "set your password" invitation are created (spec §9 — classic register + invite link,
/// not a magic link). CAPTCHA and rate limiting (the endpoint) guard against bots (spec §10).
/// </summary>
public sealed class PublicFormService(
    IAppDbContext db,
    ICaptchaValidator captcha,
    AttachmentService attachments,
    Forms.FormFieldService formFields,
    IClock clock,
    Settings.SettingsService settings,
    ICurrentUserService currentUser,
    IntakeTrustService intakeTrust,
    IOptions<AuthOptions> authOptions,
    IOptions<AppOptions> appOptions)
{
    private readonly AuthOptions _auth = authOptions.Value;
    private readonly AppOptions _app = appOptions.Value;

    /// <summary>Config for rendering the anonymous form: company name + super-admin-editable KVKK text
    /// and branding from the Settings store (spec §13, §16), plus the company's configurable extra fields
    /// (spec §4.6). No auth — the form is public.</summary>
    public async Task<PublicFormConfig> GetConfigAsync(string slug, CancellationToken ct = default)
    {
        var company = await OpenCompanyBySlugAsync(slug, ct);
        var brand = await settings.GetBrandAsync(ct); // same triple GET /api/public/brand returns
        return new PublicFormConfig(
            company.Name,
            await settings.GetValueAsync("form.kvkk_text", ct) ?? "",
            brand.SystemName,
            brand.PrimaryColor,
            brand.LogoUrl,
            await formFields.ListActiveAsync(company.Id, ct),
            captcha.SiteKey);
    }

    public async Task<PublicFormResult> SubmitAsync(string slug, PublicFormSubmitRequest request, CancellationToken ct = default)
    {
        if (!await captcha.ValidateAsync(request.CaptchaToken, ct))
            throw new BadRequestException("captcha.failed", "CAPTCHA verification failed.");

        if (!request.KvkkConsent) // defense in depth; the validator already gates this
            throw new BadRequestException("kvkk.consent_required", "KVKK consent is required.");

        var company = await OpenCompanyBySlugAsync(slug, ct);

        var now = clock.UtcNow;
        var email = request.Email.Trim().ToLowerInvariant();
        var (user, inviteToken) = await ResolveCustomerAsync(email, request.FirstName, request.LastName, now, ct);

        // Zero-trust intake (spec §10, Faz 35): held for approval unless staff vouched for this
        // customer or issued them an invitation. Recognising the email is NOT enough — that was the
        // old rule and it let any past submitter skip moderation forever.
        var hold = await intakeTrust.ShouldHoldForApprovalAsync(company.Id, user.Id, request.InviteToken, ct);
        var ticket = await AddTicketAsync(company, user.Id, request.Title, request.Body, request.CustomFields,
            pendingApproval: hold, request.Attachments, ct);

        // First-time customer: email the account-activation link. Clicking it verifies the address and
        // lets them set a password (spec §9). Known customers already have an account — no email.
        if (inviteToken is not null)
            InviteEmail.Enqueue(db, _app.PublicBaseUrl, email, "account_invite", inviteToken,
                new Dictionary<string, string>
                {
                    ["name"] = $"{request.FirstName} {request.LastName}".Trim(),
                    ["companyName"] = company.Name,
                });

        await db.SaveChangesAsync(ct);
        return new PublicFormResult(ticket.Number, inviteToken is not null, hold);
    }

    /// <summary>A signed-in customer sends a request through the company's own link (/c/{slug}) — the
    /// first-contact channel that creates the relationship the portal then scopes to (Faz 17). No CAPTCHA
    /// or KVKK re-consent: they consented and proved their address when they registered.
    /// <para>Being signed in is NOT a reason to skip moderation (changed in Faz 35). Anyone can register
    /// on a public company page, so "has an account" only proves they own a mailbox — it says nothing
    /// about whether this company wants their tickets. Moderation is decided by the same gate as the
    /// anonymous form: a staff invitation or standing trust.</para></summary>
    public async Task<PublicFormResult> SubmitAsCustomerAsync(
        string slug, CustomerFormSubmitRequest request, CancellationToken ct = default)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedException("auth.required", "Authentication required.");
        var company = await CompanyLookup.OpenBySlugAsync(db, slug, ct);
        var hold = await intakeTrust.ShouldHoldForApprovalAsync(company.Id, userId, request.InviteToken, ct);
        var ticket = await AddTicketAsync(company, userId, request.Title, request.Body, request.CustomFields,
            pendingApproval: hold, request.Attachments, ct);
        await db.SaveChangesAsync(ct);
        return new PublicFormResult(ticket.Number, NewAccount: false, PendingApproval: hold);
    }

    /// <summary>Shared ticket construction for both intake paths (anonymous form / signed-in customer):
    /// number allocation, custom fields, the Created event and attachments. Does not save.</summary>
    private async Task<Ticket> AddTicketAsync(
        Company company, Guid openedById, string title, string body,
        IReadOnlyDictionary<string, string>? customFields, bool pendingApproval,
        IReadOnlyList<AttachmentDescriptor>? files, CancellationToken ct)
    {
        var status = await InitialStatusAsync(company.Id, ct);
        var ticket = new Ticket(company.Id, company.AllocateTicketNumber(), openedById, status.Id, title, body,
            await settings.DefaultTicketPriorityAsync(ct));
        ticket.SetCustomFields(await BuildCustomFieldsJsonAsync(company.Id, customFields, ct));
        if (pendingApproval)
            ticket.MarkPendingApproval();
        db.Tickets.Add(ticket);
        db.TicketEvents.Add(new TicketEvent(company.Id, ticket.Id, openedById, TicketEventType.Created, null, ticket.Number));

        if (files is { Count: > 0 })
        {
            foreach (var a in await attachments.BuildPublicAttachmentsAsync(company.Id, ticket.Id, files, openedById, ct))
                db.Attachments.Add(a);
        }
        return ticket;
    }

    /// <summary>Zero-trust upload of a file for the first form, before the ticket exists (spec §10). The
    /// bytes stream through the API and are inspected (extension + content-type + magic bytes, pdf/txt/
    /// doc/docx only) before storage. Anonymous; the slug must resolve to an open company so keys can't
    /// be spammed under arbitrary prefixes.</summary>
    public async Task<AttachmentDescriptor> UploadAsync(
        string slug, string fileName, string contentType, Stream content, CancellationToken ct = default)
    {
        var company = await OpenCompanyBySlugAsync(slug, ct);
        return await attachments.StorePublicUploadAsync($"public/{company.Id:N}", fileName, contentType, content, ct);
    }

    private Task<Company> OpenCompanyBySlugAsync(string slug, CancellationToken ct) =>
        CompanyLookup.OpenBySlugAsync(db, slug, ct);

    /// <summary>Links to an existing user by email, or creates a pending customer + one-time invite token.</summary>
    private async Task<(User User, string? InviteToken)> ResolveCustomerAsync(
        string email, string firstName, string lastName, DateTime now, CancellationToken ct)
    {
        var user = await db.Users.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.Email == email, ct);
        if (user is not null)
            return (user, null); // known user — the ticket links to them, no new invite

        user = new User(email, firstName, lastName);
        user.Deactivate(); // activated when they set a password via the invite link
        db.Users.Add(user);

        var raw = Auth.TokenHasher.NewRawToken();
        db.Invitations.Add(new Invitation(user.Id, Auth.TokenHasher.Hash(raw), now.AddDays(_auth.InviteTokenDays), invitedById: null));
        return (user, raw);
    }

    /// <summary>Validate the configurable form fields (spec §4.6) and return the captured values as a
    /// denormalized JSON array of {label, value} to store on the ticket. Required active fields must have a
    /// non-empty value; a select value must be one of the field's options. Returns null when there are no
    /// values to store.</summary>
    private async Task<string?> BuildCustomFieldsJsonAsync(
        Guid companyId, IReadOnlyDictionary<string, string>? submitted, CancellationToken ct)
    {
        var fields = await formFields.ListActiveAsync(companyId, ct);
        if (fields.Count == 0)
            return null;

        var captured = new List<CapturedField>();
        foreach (var field in fields)
        {
            string? raw = null;
            submitted?.TryGetValue(field.Id.ToString(), out raw);
            var value = (raw ?? "").Trim();

            if (value.Length == 0)
            {
                if (field.Required)
                    throw new BadRequestException("formfield.required", $"'{field.Label}' is required.");
                continue;
            }
            if (field.Type == (int)Domain.Enums.FormFieldType.Select && field.Options.Count > 0 && !field.Options.Contains(value))
                throw new BadRequestException("formfield.invalid_option", $"'{value}' is not a valid option for '{field.Label}'.");

            captured.Add(new CapturedField(field.Label, value));
        }

        return captured.Count == 0 ? null : System.Text.Json.JsonSerializer.Serialize(captured);
    }

    private sealed record CapturedField(string Label, string Value);

    private async Task<TicketStatus> InitialStatusAsync(Guid companyId, CancellationToken ct) =>
        (await Tickets.StatusSet.EffectiveAsync(db, companyId, ct)).FirstOrDefault(s => s.Category == StatusCategory.Open)
        ?? throw new NotFoundException("status.no_initial", "No initial (Open) status is configured.");
}
