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

/// <summary>
/// The three branding values every visitor may see (name, accent colour, logo). Separate controller
/// because it is anonymous: the sign-in screen renders before there is a session, and the app shell
/// must not need <c>settings.manage</c> just to print the system's own name. The public form has
/// returned the same triple since Faz 4 — this only drops the company-slug requirement.
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("api/public/brand")]
public sealed class BrandController(SettingsService settings) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<BrandDto>> Get(CancellationToken ct) => Ok(await settings.GetBrandAsync(ct));
}
