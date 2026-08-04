using CrmKanban.Application.Auth;
using CrmKanban.Application.Files;
using CrmKanban.Application.PublicForm;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace CrmKanban.Api.Controllers;

/// <summary>
/// The anonymous public ticket form (spec §10). Unauthenticated by design; protected by CAPTCHA
/// (in the service) and rate limiting (the "public-form" policy). A customer opens a ticket via the
/// company's slug link and may attach files uploaded through a presigned PUT.
/// </summary>
[ApiController]
[EnableRateLimiting("public-form")]
[Route("api/public/form/{slug}")]
// [AllowAnonymous] sits on each action, not the controller: a controller-level one would override the
// [Authorize] on the customer-ticket action (ASP0026) and silently open it to anonymous callers.
public sealed class PublicFormController(PublicFormService publicForm, AuthService auth) : ControllerBase
{
    [AllowAnonymous]
    [HttpGet]
    public async Task<ActionResult<PublicFormConfig>> Config(string slug, CancellationToken ct) =>
        Ok(await publicForm.GetConfigAsync(slug, ct));

    [AllowAnonymous]
    [HttpPost]
    public async Task<ActionResult<PublicFormResult>> Submit(string slug, PublicFormSubmitRequest request, CancellationToken ct) =>
        Ok(await publicForm.SubmitAsync(slug, request, ct));

    /// <summary>Customer sign-up on the company's own sign-in page. Emails a 6-digit code; always 204,
    /// so an existing account is never revealed. Rate-limited by the controller policy.</summary>
    [AllowAnonymous]
    [HttpPost("register")]
    public async Task<IActionResult> Register(string slug, CustomerRegisterRequest request, CancellationToken ct)
    {
        await auth.RegisterCustomerAsync(slug, request, ct);
        return NoContent();
    }

    /// <summary>Types the emailed code back: activates the account and returns a session.</summary>
    [AllowAnonymous]
    [HttpPost("verify")]
    public async Task<ActionResult<AuthResult>> Verify(string slug, VerifyCodeRequest request, CancellationToken ct) =>
        Ok(await auth.VerifyCodeAsync(slug, request, ct));

    /// <summary>A signed-in customer sends a request to this company — the first one also creates the
    /// relationship the portal scopes to. Authenticated, unlike the rest of this controller.</summary>
    [Authorize]
    [HttpPost("ticket")]
    public async Task<ActionResult<PublicFormResult>> CustomerSubmit(string slug, CustomerFormSubmitRequest request, CancellationToken ct) =>
        Ok(await publicForm.SubmitAsCustomerAsync(slug, request, ct));

    /// <summary>Zero-trust file upload (spec §10): the bytes pass through the API and are inspected
    /// (extension + content-type + magic bytes; pdf/txt/doc/docx only) before storage. The returned
    /// descriptor is what the submit call links. Capped at 11 MB (10 MB limit + envelope).</summary>
    [AllowAnonymous]
    [HttpPost("upload")]
    [RequestSizeLimit(11 * 1024 * 1024)]
    public async Task<ActionResult<AttachmentDescriptor>> Upload(string slug, IFormFile file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { code = "attachment.empty", message = "No file was provided." });
        await using var stream = file.OpenReadStream();
        return Ok(await publicForm.UploadAsync(slug, file.FileName, file.ContentType, stream, ct));
    }
}
