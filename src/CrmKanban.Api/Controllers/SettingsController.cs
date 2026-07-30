using CrmKanban.Application.Settings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CrmKanban.Api.Controllers;

/// <summary>
/// Super-admin management of global business parameters (spec §13). The service enforces the
/// SuperAdmin gate; secrets/infra are never exposed here (they live in file/env).
/// </summary>
[ApiController]
[Authorize]
[Route("api/settings")]
public sealed class SettingsController(SettingsService settings) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<SettingDto>>> List([FromQuery] string? group, CancellationToken ct) =>
        Ok(await settings.ListAsync(group, ct));

    [HttpPut("{key}")]
    public async Task<IActionResult> Update(string key, UpdateSettingRequest request, CancellationToken ct)
    {
        await settings.UpdateAsync(key, request.Value, ct);
        return NoContent();
    }
}
