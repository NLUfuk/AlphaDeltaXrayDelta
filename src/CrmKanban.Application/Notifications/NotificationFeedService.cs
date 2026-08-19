using CrmKanban.Application.Abstractions;
using CrmKanban.Application.Common;
using CrmKanban.Domain.Entities;
using CrmKanban.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CrmKanban.Application.Notifications;

/// <summary>One line of the caller's in-app notification list. Carries the raw event type and values,
/// not a rendered sentence: every other enum on the wire (status category, priority) is turned into
/// Turkish by the SPA's message catalogue, and notifications are no different. The email pipeline
/// renders its own wording because a mail is read outside the app.</summary>
public sealed record NotificationItem(
    Guid EventId,
    Guid TicketId,
    string TicketNumber,
    string TicketTitle,
    TicketEventType EventType,
    string? OldValue,
    string? NewValue,
    DateTime CreatedAt,
    bool IsUnread);

public sealed record NotificationFeed(int UnreadCount, IReadOnlyList<NotificationItem> Items);

/// <summary>
/// The in-app notification feed (spec §14, in-app half). There is no notification table: the feed is
/// <see cref="TicketEvent"/> — the same outbox the email fan-out reads — filtered to the caller by the
/// same <see cref="NotificationMatrix"/> rules, with a single <c>User.NotificationsSeenAt</c> timestamp
/// as the read state. Adding rows per (user, event) would duplicate data the outbox already holds and
/// would need its own backfill, retention and consistency story.
///
/// Tenant scope: the query bypasses the global filter ON PURPOSE and re-states the rules by hand — a
/// customer holds no company claim, so the filter would hide their own tickets (the Faz 9
/// IgnoreQueryFilters + explicit authorization pattern). Soft-delete is therefore also re-applied by
/// hand on both sides of the join (tech debt #42: bypassing the filter drops that too).
/// </summary>
public sealed class NotificationFeedService(IAppDbContext db, ICurrentUserService currentUser, IClock clock)
{
    /// <summary>Which event types can reach which slot — read off the matrix so SQL never restates it.</summary>
    private static readonly TicketEventType[] OpenerTypes = NotificationMatrix.TypesFor(RecipientSlot.Opener);
    private static readonly TicketEventType[] AssigneeTypes = NotificationMatrix.TypesFor(RecipientSlot.Assignee);
    private static readonly TicketEventType[] AdminTypes = NotificationMatrix.TypesFor(RecipientSlot.CompanyAdmin);

    public async Task<NotificationFeed> GetAsync(int take, CancellationToken ct = default)
    {
        take = Math.Clamp(take, 1, 50);
        var userId = RequireUserId();
        var user = await db.Users.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.Id == userId, ct)
            ?? throw new NotFoundException("user.not_found", "User not found.");

        // The CompanyAdmin slot resolves through membership, not through the ticket — a company admin
        // hears about tickets they neither opened nor were assigned.
        var adminCompanies = await db.ActiveMemberships()
            .Where(m => m.UserId == userId && m.Role == RoleType.Admin)
            .Select(m => m.CompanyId)
            .ToListAsync(ct);

        var feed = FeedEvents(userId, adminCompanies);

        // Counted in SQL over the whole feed rather than over the page, so the badge is a real number
        // and not "how many of the last 20". The count uses the query predicate alone while the list
        // below is additionally re-checked against the shared rule; if the two ever drift the badge is
        // off by one, never the list — the privacy-critical side is the strict one.
        var seenAt = user.NotificationsSeenAt;
        var unreadCount = seenAt is { } cutoff
            ? await feed.CountAsync(e => e.CreatedAt > cutoff, ct)
            : await feed.CountAsync(ct);

        var events = await feed.OrderByDescending(e => e.CreatedAt).Take(take).ToListAsync(ct);
        var ticketIds = events.Select(e => e.TicketId).Distinct().ToList();
        var tickets = await db.Tickets.IgnoreQueryFilters()
            .Where(t => ticketIds.Contains(t.Id))
            .ToDictionaryAsync(t => t.Id, ct);

        var items = new List<NotificationItem>(events.Count);
        foreach (var ev in events)
        {
            var entry = NotificationMatrix.For(ev.EventType);
            if (entry is null || !tickets.TryGetValue(ev.TicketId, out var ticket)) continue;
            // Only whether *I* am an admin here matters: the returned set is tested for me alone.
            Guid[] admins = adminCompanies.Contains(ticket.CompanyId) ? [userId] : [];
            if (!NotificationMatrix.Recipients(entry, ev, ticket, admins).Contains(userId)) continue;

            items.Add(new NotificationItem(
                ev.Id, ticket.Id, ticket.Number, ticket.Title,
                ev.EventType, ev.OldValue, ev.NewValue, ev.CreatedAt,
                IsUnread: seenAt is not { } read || ev.CreatedAt > read));
        }

        return new NotificationFeed(unreadCount, items);
    }

    /// <summary>Marks everything up to now as read. A timestamp, so a notification that arrives while
    /// the list is open stays unread instead of being swallowed by the click.</summary>
    public async Task MarkSeenAsync(CancellationToken ct = default)
    {
        var userId = RequireUserId();
        var user = await db.Users.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.Id == userId, ct)
            ?? throw new NotFoundException("user.not_found", "User not found.");
        user.MarkNotificationsSeen(clock.UtcNow);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Events that notify this user, as a query. The slot rules of <see cref="NotificationMatrix"/> are
    /// expressed in SQL here (from its own type lists, not from a second copy of the matrix) so the
    /// database returns the caller's page instead of a scan that is then thrown away in memory. The two
    /// qualifying rules that are not slot-shaped are restated in the same predicate, and every row that
    /// survives is re-checked afterwards by <see cref="NotificationMatrix.Recipients"/>.
    ///
    /// The assignee branch reads the ticket's CURRENT assignee: taking over a ticket hands you its
    /// history, and handing it on takes it out of your feed. Assumed deliberately — the event rows do
    /// not record who was assigned at the time, and "the ticket I own now" is what a feed is for.
    /// </summary>
    private IQueryable<TicketEvent> FeedEvents(Guid userId, List<Guid> adminCompanies) =>
        db.TicketEvents.IgnoreQueryFilters().Where(e => e.DeletedAt == null &&
            db.Tickets.IgnoreQueryFilters().Any(t => t.Id == e.TicketId && t.DeletedAt == null
                && ((t.OpenedById == userId && OpenerTypes.Contains(e.EventType))
                    || (t.AssignedToId == userId && AssigneeTypes.Contains(e.EventType))
                    || (adminCompanies.Contains(t.CompanyId) && AdminTypes.Contains(e.EventType)))
                // Your own action is not news — except the Created receipt the opener always gets.
                && (e.ActorId != userId || (e.EventType == TicketEventType.Created && t.OpenedById == userId))
                // An internal note NEVER reaches the person who opened the ticket (spec §20), even when
                // another slot (assignee, admin) would otherwise have pulled them in.
                && !(e.EventType == TicketEventType.InternalNoteAdded && t.OpenedById == userId)));

    private Guid RequireUserId() =>
        currentUser.UserId ?? throw new UnauthorizedException("auth.required", "Authentication required.");
}
