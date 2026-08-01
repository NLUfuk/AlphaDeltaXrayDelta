using CrmKanban.Domain.Enums;

namespace CrmKanban.Application.Notifications;

/// <summary>Who a ticket event notifies, as role slots resolved against the ticket.</summary>
public enum RecipientSlot { Opener, Assignee, CompanyAdmin }

/// <summary>
/// The default event × recipient matrix (spec §14). This is the v1 default set; in Faz 6 it becomes a
/// super-admin-editable DB setting. Two rules from §14 are baked in and enforced later in the pipeline,
/// not just encoded here:
/// <list type="bullet">
///   <item><b>Internal notes never reach the customer</b> — Opener is absent from InternalNoteAdded,
///   AND the pipeline hard-excludes the opener for that event (defense in depth, spec §20).</item>
///   <item><b>Never mail someone their own action</b> — the actor is removed, except the Created
///   confirmation which the opener always receives (ticket number receipt).</item>
/// </list>
/// A comment's recipients are the same whoever authored it: the "remove actor" rule turns
/// {Opener, Assignee} into just the Assignee when the customer is the author, and into
/// {Opener, other-assignee} when staff authors — matching both §14 rows with one entry.
/// </summary>
public sealed record MatrixEntry(RecipientSlot[] Slots, string TemplateKey, bool NotifyOpenerEvenIfActor, bool Critical);

public static class NotificationMatrix
{
    public static readonly IReadOnlyDictionary<TicketEventType, MatrixEntry> Default =
        new Dictionary<TicketEventType, MatrixEntry>
        {
            [TicketEventType.Created] = new(
                [RecipientSlot.Opener, RecipientSlot.CompanyAdmin], "ticket_created",
                NotifyOpenerEvenIfActor: true, Critical: true), // opener gets the ticket-number receipt

            [TicketEventType.StatusChanged] = new(
                [RecipientSlot.Opener, RecipientSlot.Assignee], "ticket_status_changed",
                NotifyOpenerEvenIfActor: false, Critical: false),

            [TicketEventType.Reopened] = new(
                [RecipientSlot.Opener, RecipientSlot.Assignee], "ticket_reopened",
                NotifyOpenerEvenIfActor: false, Critical: false),

            [TicketEventType.CommentAdded] = new(
                [RecipientSlot.Opener, RecipientSlot.Assignee], "ticket_comment_added",
                NotifyOpenerEvenIfActor: false, Critical: false),

            [TicketEventType.InternalNoteAdded] = new(
                [RecipientSlot.Assignee, RecipientSlot.CompanyAdmin], "ticket_internal_note_added",
                NotifyOpenerEvenIfActor: false, Critical: false), // NEVER the opener/customer

            [TicketEventType.Assigned] = new(
                [RecipientSlot.Assignee], "ticket_assigned",
                NotifyOpenerEvenIfActor: false, Critical: false),

            // Moderation outcomes for public submissions (spec §10, tech debt #27). The actor is staff,
            // so the opener (customer) is always the recipient. Rejection is critical — the customer must
            // learn their request was not accepted.
            [TicketEventType.Approved] = new(
                [RecipientSlot.Opener], "ticket_approved",
                NotifyOpenerEvenIfActor: false, Critical: false),

            [TicketEventType.Rejected] = new(
                [RecipientSlot.Opener], "ticket_rejected",
                NotifyOpenerEvenIfActor: false, Critical: true),

            // A file added to the ticket (spec §7). Same recipients as a comment; the actor is removed, so
            // whoever uploaded it isn't mailed their own upload.
            [TicketEventType.AttachmentAdded] = new(
                [RecipientSlot.Opener, RecipientSlot.Assignee], "ticket_attachment_added",
                NotifyOpenerEvenIfActor: false, Critical: false),

            // Title/body edit (checklist §7). Spec §14's default was "nobody"; the product owner chose to
            // notify the opener + assignee so the customer sees content changes. Actor is removed.
            [TicketEventType.Edited] = new(
                [RecipientSlot.Opener, RecipientSlot.Assignee], "ticket_edited",
                NotifyOpenerEvenIfActor: false, Critical: false),
        };

    // PriorityChanged, CategoryChanged, Deleted, Unassigned → nobody (spec §14).
    public static MatrixEntry? For(TicketEventType type) => Default.GetValueOrDefault(type);
}
