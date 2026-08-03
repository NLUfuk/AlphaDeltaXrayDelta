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
        return new PublicFormConfig(
            company.Name,
            await settings.GetValueAsync("form.kvkk_text", ct) ?? "",
            await settings.GetValueAsync("brand.system_name", ct) ?? "",
            await settings.GetValueAsync("brand.primary_color", ct) ?? "#2563eb",
            await settings.GetValueAsync("brand.logo_url", ct) is { Length: > 0 } logo ? logo : null,
            await formFields.ListActiveAsync(company.Id, ct));
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

        var status = await InitialStatusAsync(company.Id, ct);
        var ticket = new Ticket(company.Id, company.AllocateTicketNumber(), user.Id, status.Id, request.Title, request.Body);
        ticket.SetCustomFields(await BuildCustomFieldsJsonAsync(company.Id, request.CustomFields, ct));
        // Zero-trust intake (spec §10): a first-time (unknown) customer's ticket is held for manual
        // approval before it enters the pool; a known customer (existing user) goes straight in.
        if (inviteToken is not null)
            ticket.MarkPendingApproval();
        db.Tickets.Add(ticket);
        db.TicketEvents.Add(new TicketEvent(company.Id, ticket.Id, user.Id, TicketEventType.Created, null, ticket.Number));

        if (request.Attachments is { Count: > 0 } files)
        {
            foreach (var a in attachments.BuildPublicAttachments(company.Id, ticket.Id, files, user.Id))
                db.Attachments.Add(a);
        }

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
        return new PublicFormResult(ticket.Number, inviteToken is not null);
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

    private async Task<Company> OpenCompanyBySlugAsync(string slug, CancellationToken ct)
    {
        var normalizedSlug = slug.Trim().ToLowerInvariant();
        var company = await db.Companies.IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.Slug == normalizedSlug, ct)
            ?? throw new NotFoundException("company.not_found", "No company matches this form link.");
        if (company.IsArchived || !company.IsActive)
            throw new ConflictException("company.form_closed", "This form is no longer accepting submissions.");
        return company;
    }

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
