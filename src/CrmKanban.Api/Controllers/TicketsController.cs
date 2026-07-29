using CrmKanban.Application.Tickets;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CrmKanban.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/tickets")]
public sealed class TicketsController(
    TicketCommandService commands,
    TicketQueryService queries,
    CommentService comments) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<TicketListItem>>> List([FromQuery] TicketListQuery query, CancellationToken ct) =>
        Ok(await queries.ListAsync(query, ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<TicketDetail>> Get(Guid id, CancellationToken ct) =>
        Ok(await queries.GetDetailAsync(id, ct));

    [HttpGet("kanban/{companyId:guid}")]
    public async Task<ActionResult<IReadOnlyList<KanbanColumn>>> Kanban(Guid companyId, [FromQuery] TicketListQuery query, CancellationToken ct) =>
        Ok(await queries.KanbanAsync(companyId, query, ct));

    [HttpPost]
    public async Task<ActionResult<object>> Create(CreateTicketRequest request, CancellationToken ct)
    {
        var id = await commands.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id }, new { id });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Edit(Guid id, EditTicketRequest request, CancellationToken ct)
    {
        await commands.EditAsync(id, request, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/assign")]
    public async Task<IActionResult> Assign(Guid id, AssignTicketRequest request, CancellationToken ct)
    {
        await commands.AssignAsync(id, request, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/status")]
    public async Task<IActionResult> ChangeStatus(Guid id, ChangeStatusRequest request, CancellationToken ct)
    {
        await commands.ChangeStatusAsync(id, request, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/reopen")]
    public async Task<IActionResult> Reopen(Guid id, ChangeStatusRequest request, CancellationToken ct)
    {
        await commands.ReopenAsync(id, request, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/priority")]
    public async Task<IActionResult> SetPriority(Guid id, SetPriorityRequest request, CancellationToken ct)
    {
        await commands.SetPriorityAsync(id, request, ct);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await commands.DeleteAsync(id, ct);
        return NoContent();
    }

    // ---- comments ----

    [HttpPost("{id:guid}/comments")]
    public async Task<ActionResult<object>> AddComment(Guid id, AddCommentRequest request, CancellationToken ct)
    {
        var commentId = await comments.AddAsync(id, request, ct);
        return Ok(new { id = commentId });
    }

    [HttpPut("comments/{commentId:guid}")]
    public async Task<IActionResult> EditComment(Guid commentId, EditCommentRequest request, CancellationToken ct)
    {
        await comments.EditAsync(commentId, request, ct);
        return NoContent();
    }

    [HttpDelete("comments/{commentId:guid}")]
    public async Task<IActionResult> DeleteComment(Guid commentId, CancellationToken ct)
    {
        await comments.DeleteAsync(commentId, ct);
        return NoContent();
    }
}
