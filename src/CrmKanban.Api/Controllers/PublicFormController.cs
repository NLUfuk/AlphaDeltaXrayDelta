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
[AllowAnonymous]
[EnableRateLimiting("public-form")]
[Route("api/public/form/{slug}")]
public sealed class PublicFormController(PublicFormService publicForm) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<PublicFormResult>> Submit(string slug, PublicFormSubmitRequest request, CancellationToken ct) =>
        Ok(await publicForm.SubmitAsync(slug, request, ct));

    [HttpPost("upload-url")]
    public async Task<ActionResult<UploadUrlResult>> UploadUrl(string slug, UploadUrlRequest request, CancellationToken ct) =>
        Ok(await publicForm.CreateUploadUrlAsync(slug, request, ct));
}
