using CrmKanban.Application.Auth;
using CrmKanban.Application.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CrmKanban.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/permissions")]
public sealed class PermissionsController(PermissionAssignmentService assignments, PermissionQueryService queries) : ControllerBase
{
    /// <summary>The catalog of assignable permission keys (grouped) — feeds the RBAC UI.</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<PermissionInfo>>> Catalog(CancellationToken ct) =>
        Ok(await queries.ListCatalogAsync(ct));

    /// <summary>A user's effective permissions in a company (to pre-check the boxes). Gated like assign.</summary>
    [HttpGet("effective")]
    public async Task<ActionResult<EffectivePermissions>> Effective([FromQuery] Guid userId, [FromQuery] Guid companyId, CancellationToken ct) =>
        Ok(await queries.GetEffectiveAsync(userId, companyId, ct));

    /// <summary>Grant or deny a permission to a user in a company. The escalation guard (assign only
    /// what you hold, only in your own company) is enforced inside the service — spec §7/§18.4.</summary>
    [HttpPost("assign")]
    public async Task<IActionResult> Assign(AssignPermissionRequest request, CancellationToken ct)
    {
        await assignments.AssignAsync(request, ct);
        return NoContent();
    }

    /// <summary>Drop the user-level override so the key falls back to the ROLE default — the third state
    /// the Grant/Deny pair cannot express. Same escalation guard as assigning.</summary>
    [HttpDelete("assign")]
    public async Task<IActionResult> Clear([FromQuery] Guid userId, [FromQuery] Guid companyId,
        [FromQuery] string permissionKey, CancellationToken ct)
    {
        await assignments.ClearAsync(userId, companyId, permissionKey, ct);
        return NoContent();
    }
}
