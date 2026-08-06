using CrmKanban.Application.Files;
using CrmKanban.Application.Forms;

namespace CrmKanban.Application.PublicForm;

/// <summary>
/// An anonymous public-form submission (spec §10). CaptchaToken feeds the bot gate; KvkkConsent must
/// be true (spec §16). Attachments are objects already uploaded via a presigned PUT (spec §12).
/// CustomFields carries the company's configurable extra fields (spec §4.6), keyed by field id.
/// </summary>
public sealed record PublicFormSubmitRequest(
    string FirstName,
    string LastName,
    string Email,
    string Title,
    string Body,
    bool KvkkConsent,
    string? CaptchaToken = null,
    IReadOnlyList<AttachmentDescriptor>? Attachments = null,
    IReadOnlyDictionary<string, string>? CustomFields = null,
    string? InviteToken = null);

/// <summary>A signed-in customer's request through the company link (/c/{slug}). No identity fields —
/// the account is the identity — and no KVKK/CAPTCHA: both were settled at registration.
/// <see cref="InviteToken"/> is the <c>?davet=</c> value the staff-issued link carried, if any: it is
/// what tells this submission apart from a stranger who found the same page (Faz 35). Untrusted input
/// — it only ever grants what <see cref="IntakeTrustService"/> verifies it grants.</summary>
public sealed record CustomerFormSubmitRequest(
    string Title,
    string Body,
    IReadOnlyList<AttachmentDescriptor>? Attachments = null,
    IReadOnlyDictionary<string, string>? CustomFields = null,
    string? InviteToken = null);

/// <summary>Result of a submission. <see cref="NewAccount"/> is true when this was a first-time
/// customer: the activation link was emailed to them (the raw token never leaves the server via the
/// API — spec §9). Known customers get their ticket linked to the existing account, no email.
/// <see cref="PendingApproval"/> reports what the moderation gate actually decided, so the screen can
/// say "waiting for approval" only when that is true — before Faz 35 the UI inferred it from
/// <see cref="NewAccount"/>, which stopped being the same question.</summary>
public sealed record PublicFormResult(string TicketNumber, bool NewAccount, bool PendingApproval);

/// <summary>What the anonymous form needs to render (spec §10, §13, §16): the company name plus the
/// super-admin-editable KVKK text and branding, read from the DB Settings store.</summary>
public sealed record PublicFormConfig(
    string CompanyName,
    string KvkkText,
    string BrandName,
    string PrimaryColor,
    string? LogoUrl,
    IReadOnlyList<PublicFormFieldDto> Fields);
