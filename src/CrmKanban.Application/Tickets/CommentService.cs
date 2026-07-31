using CrmKanban.Application.Abstractions;
using CrmKanban.Application.Common;
using CrmKanban.Domain.Authorization;
using CrmKanban.Domain.Entities;
using CrmKanban.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CrmKanban.Application.Tickets;

/// <summary>
/// Ticket comments and internal notes (spec §12, §14). Internal notes never reach the customer;
/// customers cannot edit or delete comments (§18.12). Staff edits/deletes are tracked: an edit writes
/// a CommentRevision and flags the comment as edited; a delete is soft (§18.2).
/// </summary>
public sealed class CommentService(IAppDbContext db, TicketAuthorizationService authz, Files.AttachmentService attachments, IClock clock)
{
    public async Task<Guid> AddAsync(Guid ticketId, AddCommentRequest request, CancellationToken ct = default)
    {
        var ticket = await LoadTicketAsync(ticketId, ct);
        var actor = await authz.ResolveAsync(ticket.CompanyId, ticket.OpenedById, ct);
        TicketAuthorizationService.EnsureCanComment(actor, request.IsInternal);

        var comment = new Comment(ticket.CompanyId, ticketId, actor.UserId, request.Body, request.IsInternal);
        db.Comments.Add(comment);
        db.TicketEvents.Add(new TicketEvent(ticket.CompanyId, ticketId, actor.UserId,
            request.IsInternal ? TicketEventType.InternalNoteAdded : TicketEventType.CommentAdded, null, null));

        if (request.Attachments is { Count: > 0 } files)
        {
            foreach (var a in attachments.BuildAttachments(ticket.CompanyId, ticketId, comment.Id, files, actor.UserId))
                db.Attachments.Add(a);
        }

        await db.SaveChangesAsync(ct);
        return comment.Id;
    }

    public async Task EditAsync(Guid commentId, EditCommentRequest request, CancellationToken ct = default)
    {
        var (comment, ticket) = await LoadCommentAsync(commentId, ct);
        var actor = await authz.ResolveAsync(ticket.CompanyId, ticket.OpenedById, ct);
        // Editing a comment (incl. a customer's) is an admin power gated by ticket.edit — spec §8/§18.2.
        TicketAuthorizationService.EnsurePermission(actor, PermissionKeys.TicketEdit);

        var oldBody = comment.Edit(request.Body, clock.UtcNow);
        db.CommentRevisions.Add(new CommentRevision(ticket.CompanyId, comment.Id, oldBody, actor.UserId));
        db.TicketEvents.Add(new TicketEvent(ticket.CompanyId, ticket.Id, actor.UserId, TicketEventType.Edited, null, null));
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid commentId, CancellationToken ct = default)
    {
        var (comment, ticket) = await LoadCommentAsync(commentId, ct);
        var actor = await authz.ResolveAsync(ticket.CompanyId, ticket.OpenedById, ct);
        TicketAuthorizationService.EnsurePermission(actor, PermissionKeys.TicketEdit);

        db.Comments.Remove(comment); // soft delete via interceptor
        db.TicketEvents.Add(new TicketEvent(ticket.CompanyId, ticket.Id, actor.UserId, TicketEventType.Deleted, null, null));
        await db.SaveChangesAsync(ct);
    }

    // Ignore the tenant filter and gate via authz.ResolveAsync (opener or in-company) — a customer has
    // no company scope, so the filter would 404 their own ticket. Matches the read path (GetDetailAsync).
    private async Task<Ticket> LoadTicketAsync(Guid ticketId, CancellationToken ct) =>
        await db.Tickets.IgnoreQueryFilters().FirstOrDefaultAsync(t => t.Id == ticketId && t.DeletedAt == null, ct)
        ?? throw new NotFoundException("ticket.not_found", "Ticket not found.");

    private async Task<(Comment Comment, Ticket Ticket)> LoadCommentAsync(Guid commentId, CancellationToken ct)
    {
        var comment = await db.Comments.IgnoreQueryFilters().FirstOrDefaultAsync(c => c.Id == commentId && c.DeletedAt == null, ct)
            ?? throw new NotFoundException("comment.not_found", "Comment not found.");
        var ticket = await LoadTicketAsync(comment.TicketId, ct);
        return (comment, ticket);
    }
}
