using CrmKanban.Application.Auth;
using CrmKanban.Application.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CrmKanban.Api.Controllers;

[ApiController]
[Route("api/permissions")]
public sealed class PermissionsController(PermissionAssignmentService assignments) : ControllerBase
{
    /// <summary>Grant or deny a permission to a user in a company. The escalation guard (assign only
    /// what you hold, only in your own company) is enforced inside the service — spec §7/§18.4.</summary>
    [Authorize]
    [HttpPost("assign")]
    public async Task<IActionResult> Assign(AssignPermissionRequest request, CancellationToken ct)
    {
        await assignments.AssignAsync(request, ct);
        return NoContent();
    }
}
