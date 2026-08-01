using CrmKanban.Application.Notifications;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CrmKanban.Api.Controllers;

/// <summary>Email template management (spec §14/§9). Super-admin gate enforced in the service.</summary>
[ApiController]
[Authorize]
[Route("api/email-templates")]
public sealed class EmailTemplatesController(EmailTemplateService templates) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<EmailTemplateDto>>> List(CancellationToken ct) =>
        Ok(await templates.ListAsync(ct));

    [HttpPut("{key}")]
    public async Task<IActionResult> Update(string key, UpdateEmailTemplateRequest request, CancellationToken ct)
    {
        await templates.UpdateAsync(key, request, ct);
        return NoContent();
    }
}
