using CrmKanban.Domain.Entities;
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
/// <param name="TemplateKey">Customer-voice template ("talebiniz…"), used for the opener when the opener
/// is a customer (not a member of the ticket's company).</param>
/// <param name="StaffTemplateKey">Template for recipients who work at the company. Null = use
/// <paramref name="TemplateKey"/> (for events that are staff-only anyway, e.g. assignment/internal note).
/// Staff get one generic "this ticket changed" mail rather than a customer-worded one.</param>
public sealed record MatrixEntry(
    RecipientSlot[] Slots, string TemplateKey, bool NotifyOpenerEvenIfActor, bool Critical,
    string? StaffTemplateKey = null);

/// <summary>The generic staff-facing update mail: "X nolu talepte güncelleme var" + what changed.</summary>
public static class StaffTemplate
{
    public const string Update = "ticket_staff_update";
}

public static class NotificationMatrix
{
    public static readonly IReadOnlyDictionary<TicketEventType, MatrixEntry> Default =
        new Dictionary<TicketEventType, MatrixEntry>
        {
            [TicketEventType.Created] = new(
                [RecipientSlot.Opener, RecipientSlot.CompanyAdmin], "ticket_created",
                NotifyOpenerEvenIfActor: true, Critical: true, StaffTemplate.Update), // opener gets the ticket-number receipt

            [TicketEventType.StatusChanged] = new(
                [RecipientSlot.Opener, RecipientSlot.Assignee], "ticket_status_changed",
                NotifyOpenerEvenIfActor: false, Critical: false, StaffTemplate.Update),

            [TicketEventType.Reopened] = new(
                [RecipientSlot.Opener, RecipientSlot.Assignee], "ticket_reopened",
                NotifyOpenerEvenIfActor: false, Critical: false, StaffTemplate.Update),

            [TicketEventType.CommentAdded] = new(
                [RecipientSlot.Opener, RecipientSlot.Assignee], "ticket_comment_added",
                NotifyOpenerEvenIfActor: false, Critical: false, StaffTemplate.Update),

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
                [RecipientSlot.Opener, RecipientSlot.Assignee], "ticket_approved",
                NotifyOpenerEvenIfActor: false, Critical: false, StaffTemplate.Update),

            [TicketEventType.Rejected] = new(
                [RecipientSlot.Opener, RecipientSlot.Assignee], "ticket_rejected",
                NotifyOpenerEvenIfActor: false, Critical: true, StaffTemplate.Update),

            // A file added to the ticket (spec §7). Same recipients as a comment; the actor is removed, so
            // whoever uploaded it isn't mailed their own upload.
            [TicketEventType.AttachmentAdded] = new(
                [RecipientSlot.Opener, RecipientSlot.Assignee], "ticket_attachment_added",
                NotifyOpenerEvenIfActor: false, Critical: false, StaffTemplate.Update),

            // Title/body edit (checklist §7). Spec §14's default was "nobody"; the product owner chose to
            // notify the opener + assignee so the customer sees content changes. Actor is removed.
            [TicketEventType.Edited] = new(
                [RecipientSlot.Opener, RecipientSlot.Assignee], "ticket_edited",
                NotifyOpenerEvenIfActor: false, Critical: false, StaffTemplate.Update),
            // Internal bookkeeping: the assignee needs to know, the customer does not (priority is a
            // staff concept — mailing it would leak internal triage and add noise).
            [TicketEventType.PriorityChanged] = new(
                [RecipientSlot.Assignee], StaffTemplate.Update,
                NotifyOpenerEvenIfActor: false, Critical: false),

            // Money (Faz 39). Added in Faz 42: the event type existed since Faz 40 but had no entry
            // here, so pricing a deal notified nobody — silence by omission rather than by decision.
            // The opener is DELIBERATELY absent: the amount is the company's commercial position, it
            // sits behind `ticket.value`, and mailing "tahmini tutarınız 100.000 → 90.000 oldu" to a
            // customer would hand out through the mail exactly what the permission withholds in the
            // API. The assignee gets the generic staff update, which names the event, not the figure.
            [TicketEventType.ValueChanged] = new(
                [RecipientSlot.Assignee], StaffTemplate.Update,
                NotifyOpenerEvenIfActor: false, Critical: false),
        };

    /// <summary>
    /// Who this event notifies, as user ids. The two §14 rules that must never be re-implemented at a
    /// call site live HERE, next to the matrix they qualify: the actor never hears about their own
    /// action (except the Created receipt, which the opener always gets), and the opener is hard-excluded
    /// from an internal note even if some other slot pulled them in (spec §20 — defense in depth on top
    /// of the matrix entry that already omits them).
    /// Shared by the email fan-out (<c>NotificationService</c>) and the in-app feed
    /// (<c>NotificationFeedService</c>): one rule, two surfaces.
    /// </summary>
    public static HashSet<Guid> Recipients(
        MatrixEntry entry, TicketEvent ev, Ticket ticket, IEnumerable<Guid> companyAdmins)
    {
        var set = new HashSet<Guid>();
        foreach (var slot in entry.Slots)
        {
            switch (slot)
            {
                case RecipientSlot.Opener: set.Add(ticket.OpenedById); break;
                case RecipientSlot.Assignee: if (ticket.AssignedToId is { } a) set.Add(a); break;
                case RecipientSlot.CompanyAdmin: foreach (var admin in companyAdmins) set.Add(admin); break;
            }
        }

        set.Remove(ev.ActorId); // never notify someone of their own action (§14 golden rule)
        if (entry.NotifyOpenerEvenIfActor) set.Add(ticket.OpenedById); // …except the Created receipt
        if (ev.EventType == TicketEventType.InternalNoteAdded) set.Remove(ticket.OpenedById); // hard privacy rule (§20)
        return set;
    }

    /// <summary>Event types whose recipients include the given slot. Lets a database query pre-filter
    /// on "could this event ever reach me?" without restating the matrix in SQL.</summary>
    public static TicketEventType[] TypesFor(RecipientSlot slot) =>
        [.. Default.Where(kv => kv.Value.Slots.Contains(slot)).Select(kv => kv.Key)];

    // Deleted, Unassigned → nobody. (Unassigned would have to mail the *previous* assignee; the event
    // carries their id in OldValue but no recipient slot resolves to it — add one if it's ever wanted.)
    public static MatrixEntry? For(TicketEventType type) => Default.GetValueOrDefault(type);
}
