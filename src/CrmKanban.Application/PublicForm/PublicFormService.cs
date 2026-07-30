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
    IClock clock,
    Settings.SettingsService settings,
    IOptions<AuthOptions> authOptions)
{
    private readonly AuthOptions _auth = authOptions.Value;

    /// <summary>Config for rendering the anonymous form: company name + super-admin-editable KVKK text
    /// and branding from the Settings store (spec §13, §16). No auth — the form is public.</summary>
    public async Task<PublicFormConfig> GetConfigAsync(string slug, CancellationToken ct = default)
    {
        var company = await OpenCompanyBySlugAsync(slug, ct);
        return new PublicFormConfig(
            company.Name,
            await settings.GetValueAsync("form.kvkk_text", ct) ?? "",
            await settings.GetValueAsync("brand.system_name", ct) ?? "",
            await settings.GetValueAsync("brand.primary_color", ct) ?? "#2563eb",
            await settings.GetValueAsync("brand.logo_url", ct) is { Length: > 0 } logo ? logo : null);
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
        db.Tickets.Add(ticket);
        db.TicketEvents.Add(new TicketEvent(company.Id, ticket.Id, user.Id, TicketEventType.Created, null, ticket.Number));

        if (request.Attachments is { Count: > 0 } files)
        {
            foreach (var a in attachments.BuildAttachments(company.Id, ticket.Id, commentId: null, files, user.Id))
                db.Attachments.Add(a);
        }

        await db.SaveChangesAsync(ct);
        return new PublicFormResult(ticket.Number, inviteToken);
    }

    /// <summary>Presigned PUT for a file attached to the first form, before the ticket exists. Anonymous;
    /// the slug must resolve to an open company so keys can't be spammed under arbitrary prefixes.</summary>
    public async Task<UploadUrlResult> CreateUploadUrlAsync(string slug, UploadUrlRequest request, CancellationToken ct = default)
    {
        var company = await OpenCompanyBySlugAsync(slug, ct);
        return attachments.CreateUploadUrl($"public/{company.Id:N}", request);
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

        var raw = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
        // Mirror of InvitationService token hashing (opaque, single-use, stored hashed — spec §9).
        // ponytail: 3rd copy of SHA256 token hashing (refresh/invite/here); extract a TokenHasher when auth is next touched.
        db.Invitations.Add(new Invitation(user.Id, HashToken(raw), now.AddDays(_auth.InviteTokenDays), invitedById: null));
        return (user, raw);
    }

    private async Task<TicketStatus> InitialStatusAsync(Guid companyId, CancellationToken ct) =>
        await db.TicketStatuses.IgnoreQueryFilters()
            .Where(s => (s.CompanyId == companyId || s.CompanyId == null) && s.Category == StatusCategory.Open)
            .OrderBy(s => s.Order).FirstOrDefaultAsync(ct)
        ?? throw new NotFoundException("status.no_initial", "No initial (Open) status is configured.");

    private static string HashToken(string raw)
    {
        var bytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexStringLower(bytes);
    }
}
