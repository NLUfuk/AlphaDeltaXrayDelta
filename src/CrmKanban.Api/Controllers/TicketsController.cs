using CrmKanban.Application.Files;
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
    CommentService comments,
    AttachmentService attachments) : ControllerBase
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

    [HttpGet("statuses")]
    public async Task<ActionResult<IReadOnlyList<StatusDto>>> Statuses([FromQuery] Guid? companyId, CancellationToken ct) =>
        Ok(await queries.ListStatusesAsync(companyId, ct));

    /// <summary>Companies the logged-in customer already works with — feeds the portal's "new message"
    /// picker so they only see companies they've contacted (spec §18.5).</summary>
    [HttpGet("my-companies")]
    public async Task<ActionResult<IReadOnlyList<CustomerCompanyDto>>> MyCompanies(CancellationToken ct) =>
        Ok(await queries.ListMyCompaniesAsync(ct));

    // ---- moderation (zero-trust public intake, spec §10) ----

    [HttpGet("moderation/{companyId:guid}")]
    public async Task<ActionResult<IReadOnlyList<TicketListItem>>> Moderation(Guid companyId, CancellationToken ct) =>
        Ok(await queries.ModerationQueueAsync(companyId, ct));

    [HttpPost("{id:guid}/approve")]
    public async Task<IActionResult> Approve(Guid id, CancellationToken ct)
    {
        await commands.ApproveAsync(id, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/reject")]
    public async Task<IActionResult> Reject(Guid id, CancellationToken ct)
    {
        await commands.RejectAsync(id, ct);
        return NoContent();
    }

    [HttpPost]
    public async Task<ActionResult<object>> Create(CreateTicketRequest request, CancellationToken ct)
    {
        var id = await commands.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id }, new { id });
    }

    /// <summary>A logged-in customer opens a request to a company they picked from the portal (spec §18.5).
    /// Any authenticated user may call it; no company membership required (unlike <see cref="Create"/>).</summary>
    [HttpPost("customer")]
    public async Task<ActionResult<object>> CreateAsCustomer(CustomerCreateTicketRequest request, CancellationToken ct)
    {
        var id = await commands.CreateAsCustomerAsync(request, ct);
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

    // ---- attachments (spec §12) ----

    /// <summary>Attach a file to a ticket. The bytes proxy through the API (like the public path) so the
    /// browser never reaches the storage host and the real size is measured server-side. Authorized
    /// against the ticket. Capped at 11 MB (10 MB limit + envelope).</summary>
    [HttpPost("{id:guid}/attachments")]
    [RequestSizeLimit(11 * 1024 * 1024)]
    public async Task<ActionResult<AttachmentDto>> UploadAttachment(Guid id, IFormFile file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { code = "attachment.empty", message = "No file was provided." });
        await using var stream = file.OpenReadStream();
        return Ok(await attachments.StoreTicketUploadAsync(id, file.FileName, file.ContentType, stream, ct));
    }

    /// <summary>Stream a private attachment back through the API; authorized against the ticket (a
    /// customer never gets a file on an internal note).</summary>
    [HttpGet("attachments/{attachmentId:guid}/download")]
    public async Task<IActionResult> DownloadAttachment(Guid attachmentId, CancellationToken ct)
    {
        var content = await attachments.OpenAttachmentAsync(attachmentId, ct);
        return File(content.Content, content.ContentType, content.FileName);
    }
}
