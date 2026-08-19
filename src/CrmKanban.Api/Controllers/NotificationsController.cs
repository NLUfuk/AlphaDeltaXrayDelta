using CrmKanban.Application.Notifications;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CrmKanban.Api.Controllers;

/// <summary>
/// The caller's own in-app notifications (spec §14). No company parameter and no permission key: the
/// feed is defined by who you are on each ticket (opener / assignee / company admin), and the service
/// resolves that with the same matrix the notification e-mails use.
/// </summary>
[ApiController]
[Authorize]
[Route("api/notifications")]
public sealed class NotificationsController(NotificationFeedService feed) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<NotificationFeed>> Get([FromQuery] int take = 20, CancellationToken ct = default) =>
        Ok(await feed.GetAsync(take, ct));

    /// <summary>Marks everything up to now as read (the unread badge goes to zero).</summary>
    [HttpPost("seen")]
    public async Task<IActionResult> MarkSeen(CancellationToken ct)
    {
        await feed.MarkSeenAsync(ct);
        return NoContent();
    }
}
