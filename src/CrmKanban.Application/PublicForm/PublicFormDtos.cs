using CrmKanban.Application.Files;

namespace CrmKanban.Application.PublicForm;

/// <summary>
/// An anonymous public-form submission (spec §10). CaptchaToken feeds the bot gate; KvkkConsent must
/// be true (spec §16). Attachments are objects already uploaded via a presigned PUT (spec §12).
/// </summary>
public sealed record PublicFormSubmitRequest(
    string FirstName,
    string LastName,
    string Email,
    string Title,
    string Body,
    bool KvkkConsent,
    string? CaptchaToken = null,
    IReadOnlyList<AttachmentDescriptor>? Attachments = null);

/// <summary>Result of a submission. <see cref="NewAccount"/> is true when this was a first-time
/// customer: the activation link was emailed to them (the raw token never leaves the server via the
/// API — spec §9). Known customers get their ticket linked to the existing account, no email.</summary>
public sealed record PublicFormResult(string TicketNumber, bool NewAccount);

/// <summary>A selectable company for the public register/portal picker: only id, name, slug are public.</summary>
public sealed record PublicCompanyDto(Guid Id, string Name, string Slug);

/// <summary>What the anonymous form needs to render (spec §10, §13, §16): the company name plus the
/// super-admin-editable KVKK text and branding, read from the DB Settings store.</summary>
public sealed record PublicFormConfig(
    string CompanyName,
    string KvkkText,
    string BrandName,
    string PrimaryColor,
    string? LogoUrl);
