using CrmKanban.Application.Kvkk;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CrmKanban.Api.Controllers;

/// <summary>
/// KVKK data-subject operations (spec §16). Anonymization (erasure-as-masking) is super-admin only;
/// the service enforces the gate and audits the action.
/// </summary>
[ApiController]
[Authorize]
[Route("api/kvkk")]
public sealed class KvkkController(KvkkService kvkk) : ControllerBase
{
    [HttpPost("anonymize/{userId:guid}")]
    public async Task<IActionResult> Anonymize(Guid userId, CancellationToken ct)
    {
        await kvkk.AnonymizeUserAsync(userId, ct);
        return NoContent();
    }
}
