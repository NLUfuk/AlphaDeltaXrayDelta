using System.Text.Json;
using CrmKanban.Application.Abstractions;
using CrmKanban.Domain.Entities;
using CrmKanban.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CrmKanban.Application.Notifications;

/// <summary>
/// The notification pipeline (spec §14). Two passes, both idempotent and driven off data, never off
/// inline mail calls sprinkled through business logic:
/// <list type="number">
///   <item><b>Fan-out</b>: unprocessed <see cref="TicketEvent"/> rows (the outbox) → recipients via
///   the <see cref="NotificationMatrix"/> with the §14 rules (no self-notify; internal note never to
///   the customer; Created confirms the opener), coalesced per (recipient, ticket, event) within a
///   tick (debounce), honoring per-user opt-outs → <see cref="EmailQueue"/> rows.</item>
///   <item><b>Send</b>: pending/failed queue rows → <see cref="IEmailSender"/>, retry, dead-letter.</item>
/// </list>
/// Runs SYSTEM-scoped (the caller supplies a super-admin context) so it sees every tenant's events.
/// ponytail: debounce is per-tick coalescing of identical (recipient, ticket, event); a cross-type
/// "one summary mail" upgrade (spec §14) is a larger change — do it if mail volume proves noisy.
/// </summary>
public sealed class NotificationService(
    IAppDbContext db,
    IEmailSender sender,
    IClock clock,
    IOptions<NotificationOptions> options)
{
    private readonly NotificationOptions _opt = options.Value;

    public async Task RunOnceAsync(CancellationToken ct = default)
    {
        await FanOutEventsAsync(ct);
        await SendQueuedAsync(ct);
    }

    /// <summary>Turn newly-recorded ticket events into queued emails. Returns the number of events processed.</summary>
    public async Task<int> FanOutEventsAsync(CancellationToken ct = default)
    {
        var events = await db.TicketEvents.IgnoreQueryFilters()
            .Where(e => e.NotifiedAt == null)
            .OrderBy(e => e.CreatedAt).Take(_opt.BatchSize).ToListAsync(ct);
        if (events.Count == 0) return 0;

        var now = clock.UtcNow;
        var ticketIds = events.Select(e => e.TicketId).Distinct().ToList();
        var tickets = await db.Tickets.IgnoreQueryFilters()
            .Where(t => ticketIds.Contains(t.Id)).ToDictionaryAsync(t => t.Id, ct);

        var companyIds = events.Select(e => e.CompanyId).Distinct().ToList();
        var adminsByCompany = (await db.Memberships.IgnoreQueryFilters()
                .Where(m => companyIds.Contains(m.CompanyId) && m.Role == RoleType.Admin)
                .Select(m => new { m.CompanyId, m.UserId }).ToListAsync(ct))
            .ToLookup(x => x.CompanyId, x => x.UserId);

        // (recipient, ticket, eventType) dedupe within this tick = debounce.
        var seen = new HashSet<(Guid, Guid, TicketEventType)>();
        var planned = new List<(Guid UserId, MatrixEntry Entry, TicketEvent Event, Ticket Ticket)>();

        foreach (var ev in events)
        {
            var entry = NotificationMatrix.For(ev.EventType);
            if (entry is not null && tickets.TryGetValue(ev.TicketId, out var ticket))
                foreach (var uid in ResolveRecipients(entry, ev, ticket, adminsByCompany[ev.CompanyId]))
                    if (seen.Add((uid, ev.TicketId, ev.EventType)))
                        planned.Add((uid, entry, ev, ticket));

            ev.MarkNotified(now); // outbox row consumed exactly once, whether or not it had recipients
        }

        var userIds = planned.Select(p => p.UserId).Distinct().ToList();
        var emailById = await db.Users.IgnoreQueryFilters()
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.Email }).ToDictionaryAsync(x => x.Id, x => x.Email, ct);
        var optedOut = (await db.UserNotificationPrefs.IgnoreQueryFilters()
                .Where(p => userIds.Contains(p.UserId) && !p.Enabled)
                .Select(p => new { p.UserId, p.EventType }).ToListAsync(ct))
            .Select(x => (x.UserId, x.EventType)).ToHashSet();

        foreach (var (userId, entry, ev, ticket) in planned)
        {
            if (!entry.Critical && optedOut.Contains((userId, ev.EventType))) continue; // per-user opt-out
            if (!emailById.TryGetValue(userId, out var email) || string.IsNullOrWhiteSpace(email)) continue;
            db.EmailQueue.Add(new EmailQueue(email, entry.TemplateKey, BuildPayload(ev, ticket)));
        }

        await db.SaveChangesAsync(ct);
        return events.Count;
    }

    /// <summary>Send pending/failed queue rows. Returns the number attempted.</summary>
    public async Task<int> SendQueuedAsync(CancellationToken ct = default)
    {
        var pending = await db.EmailQueue.IgnoreQueryFilters()
            .Where(e => e.Status == EmailStatus.Pending || e.Status == EmailStatus.Failed)
            .OrderBy(e => e.CreatedAt).Take(_opt.BatchSize).ToListAsync(ct);
        if (pending.Count == 0) return 0;

        var templates = await db.EmailTemplates.IgnoreQueryFilters()
            .Where(t => t.IsActive).ToDictionaryAsync(t => t.Key, ct);
        var now = clock.UtcNow;

        foreach (var msg in pending)
        {
            try
            {
                if (!templates.TryGetValue(msg.TemplateKey, out var tpl))
                {
                    msg.MarkFailed($"Template '{msg.TemplateKey}' not found.", _opt.MaxAttempts);
                    continue;
                }
                var data = Deserialize(msg.Payload);
                await sender.SendAsync(msg.ToEmail, Render(tpl.Subject, data), Render(tpl.Body, data), ct);
                msg.MarkSent(now);
            }
            catch (Exception ex)
            {
                msg.MarkFailed(ex.Message, _opt.MaxAttempts);
            }
        }

        await db.SaveChangesAsync(ct);
        return pending.Count;
    }

    private static HashSet<Guid> ResolveRecipients(
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

        set.Remove(ev.ActorId); // never mail someone their own action (§14 golden rule)
        if (entry.NotifyOpenerEvenIfActor) set.Add(ticket.OpenedById); // …except the Created receipt
        if (ev.EventType == TicketEventType.InternalNoteAdded) set.Remove(ticket.OpenedById); // hard privacy rule (§20)
        return set;
    }

    private static string BuildPayload(TicketEvent ev, Ticket ticket) =>
        JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["ticketNumber"] = ticket.Number,
            ["title"] = ticket.Title,
            ["event"] = ev.EventType.ToString(),
            ["oldValue"] = ev.OldValue ?? "",
            ["newValue"] = ev.NewValue ?? "",
        });

    private static Dictionary<string, string> Deserialize(string json) =>
        JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? [];

    private static string Render(string template, Dictionary<string, string> data)
    {
        foreach (var (key, value) in data)
            template = template.Replace($"{{{{{key}}}}}", value, StringComparison.Ordinal);
        return template;
    }
}
