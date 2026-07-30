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

/// <summary>Result of a submission. InviteToken is returned only until email ships (Faz 5), like invites.</summary>
public sealed record PublicFormResult(string TicketNumber, string? InviteToken);

/// <summary>What the anonymous form needs to render (spec §10, §13, §16): the company name plus the
/// super-admin-editable KVKK text and branding, read from the DB Settings store.</summary>
public sealed record PublicFormConfig(
    string CompanyName,
    string KvkkText,
    string BrandName,
    string PrimaryColor,
    string? LogoUrl);
