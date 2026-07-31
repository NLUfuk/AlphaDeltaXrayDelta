using CrmKanban.Application.Tickets;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CrmKanban.Api.Controllers;

/// <summary>
/// Per-company kanban column management (spec §12, §18.9). An admin with status.manage adds a column
/// at a chosen position in the chain, renames/recolors it, reorders the board, or removes one. All
/// operations are company-scoped and gated inside the service.
/// </summary>
[ApiController]
[Authorize]
[Route("api/companies/{companyId:guid}/statuses")]
public sealed class StatusesController(StatusManagementService statuses) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<StatusColumnDto>>> List(Guid companyId, CancellationToken ct) =>
        Ok(await statuses.ListAsync(companyId, ct));

    [HttpPost]
    public async Task<ActionResult<object>> Create(Guid companyId, CreateStatusRequest request, CancellationToken ct)
    {
        var id = await statuses.CreateAsync(companyId, request, ct);
        return Ok(new { id });
    }

    [HttpPut("{statusId:guid}")]
    public async Task<IActionResult> Update(Guid companyId, Guid statusId, UpdateStatusRequest request, CancellationToken ct)
    {
        await statuses.UpdateAsync(companyId, statusId, request, ct);
        return NoContent();
    }

    [HttpPost("reorder")]
    public async Task<IActionResult> Reorder(Guid companyId, ReorderStatusesRequest request, CancellationToken ct)
    {
        await statuses.ReorderAsync(companyId, request, ct);
        return NoContent();
    }

    [HttpDelete("{statusId:guid}")]
    public async Task<IActionResult> Delete(Guid companyId, Guid statusId, CancellationToken ct)
    {
        await statuses.DeleteAsync(companyId, statusId, ct);
        return NoContent();
    }
}
