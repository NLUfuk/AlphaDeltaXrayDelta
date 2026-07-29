using CrmKanban.Application.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CrmKanban.Api.Controllers;

[ApiController]
[Route("api/invitations")]
public sealed class InvitationsController(InvitationService invitations) : ControllerBase
{
    /// <summary>Invite a Personel or second Admin into a company. Authorization (user.invite in the
    /// target company) is enforced inside the service. Raw token is returned until email ships (Faz 5).</summary>
    [Authorize]
    [HttpPost]
    public async Task<ActionResult<InviteResult>> Invite(InviteUserRequest request, CancellationToken ct) =>
        Ok(await invitations.InviteUserAsync(request, ct));

    [AllowAnonymous]
    [HttpPost("accept")]
    public async Task<IActionResult> Accept(AcceptInviteRequest request, CancellationToken ct)
    {
        await invitations.AcceptInviteAsync(request, ct);
        return NoContent();
    }
}
