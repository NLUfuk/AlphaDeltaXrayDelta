using CrmKanban.Application.Abstractions;
using CrmKanban.Application.Notifications;
using CrmKanban.Domain.Entities;
using CrmKanban.Domain.Enums;
using CrmKanban.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace CrmKanban.Application.Tests.Notifications;

/// <summary>
/// The in-app notification feed. It reads the same TicketEvent outbox the e-mail fan-out reads, but
/// through a query that bypasses the tenant filter (a customer holds no company claim), so the rules
/// that filter protect elsewhere have to hold here on their own:
/// an internal note never reaches the customer (§14/§20), your own action is not news except the
/// Created receipt, another company's events are invisible, a deleted ticket is gone, and the unread
/// count tracks the single seen-timestamp.
/// </summary>
public class NotificationFeedServiceTests
{
    private sealed class Caller(Guid userId, params Guid[] companies) : ICurrentUserService
    {
        public Guid? UserId => userId;
        public bool IsAuthenticated => true;
        public bool IsSuperAdmin => false;
        public IReadOnlyCollection<Guid> CompanyIds => companies;
    }

    private sealed class SuperAdmin : ICurrentUserService
    {
        public Guid? UserId => null;
        public bool IsAuthenticated => false;
        public bool IsSuperAdmin => true;
        public IReadOnlyCollection<Guid> CompanyIds => [];
    }

    private sealed class FixedClock : IClock
    {
        public DateTime UtcNow => new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    }

    private static readonly DateTime Earlier = new(2025, 12, 30, 9, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Later = new(2025, 12, 31, 9, 0, 0, DateTimeKind.Utc);

    private static readonly Guid CompanyA = Guid.NewGuid();
    private static readonly Guid CompanyB = Guid.NewGuid();

    private sealed record World(Guid TicketId, Guid CustomerId, Guid StaffId, Guid AdminId, Guid OtherAdminId);

    private static DbContextOptions<CrmDbContext> Store() =>
        new DbContextOptionsBuilder<CrmDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString(),
                new Microsoft.EntityFrameworkCore.Storage.InMemoryDatabaseRoot()).Options;

    /// <summary>One ticket at company A opened by a customer and assigned to staff, plus an admin at A
    /// and an unrelated admin at company B.</summary>
    private static async Task<World> SeedAsync(DbContextOptions<CrmDbContext> options)
    {
        await using var db = new CrmDbContext(options, new SuperAdmin());
        var customer = new User("customer@x.com", "Cust", "Omer");
        var staff = new User("staff@x.com", "Sam", "Staff");
        var admin = new User("admin@x.com", "Ada", "Admin");
        var otherAdmin = new User("other@y.com", "Ozan", "Other");
        db.Users.AddRange(customer, staff, admin, otherAdmin);
        db.Memberships.Add(new Membership(staff.Id, CompanyA, RoleType.Personel));
        db.Memberships.Add(new Membership(admin.Id, CompanyA, RoleType.Admin));
        db.Memberships.Add(new Membership(otherAdmin.Id, CompanyB, RoleType.Admin));
        var ticket = new Ticket(CompanyA, "ACME-1", customer.Id, Guid.NewGuid(), "Broken", "body");
        ticket.Assign(staff.Id);
        db.Tickets.Add(ticket);
        await db.SaveChangesAsync();
        return new World(ticket.Id, customer.Id, staff.Id, admin.Id, otherAdmin.Id);
    }

    /// <summary>CreatedAt is stamped by an interceptor that only exists in the composed app, so tests
    /// set it by hand — the unread cutoff is a comparison against exactly this value.</summary>
    private static async Task AddEventAsync(
        DbContextOptions<CrmDbContext> options, Guid companyId, Guid ticketId, Guid actorId,
        TicketEventType type, DateTime at)
    {
        await using var db = new CrmDbContext(options, new SuperAdmin());
        var ev = new TicketEvent(companyId, ticketId, actorId, type, null, null) { CreatedAt = at };
        db.TicketEvents.Add(ev);
        await db.SaveChangesAsync();
    }

    private static NotificationFeedService FeedFor(DbContextOptions<CrmDbContext> options, ICurrentUserService caller) =>
        new(new CrmDbContext(options, caller), caller, new FixedClock());

    [Fact]
    public async Task Internal_note_never_reaches_the_customer_but_does_reach_the_admin()
    {
        var options = Store();
        var w = await SeedAsync(options);
        await AddEventAsync(options, CompanyA, w.TicketId, w.StaffId, TicketEventType.InternalNoteAdded, Earlier);

        var customerFeed = await FeedFor(options, new Caller(w.CustomerId)).GetAsync(20);
        var adminFeed = await FeedFor(options, new Caller(w.AdminId, CompanyA)).GetAsync(20);

        customerFeed.Items.Should().BeEmpty();
        customerFeed.UnreadCount.Should().Be(0);
        adminFeed.Items.Should().ContainSingle()
            .Which.EventType.Should().Be(TicketEventType.InternalNoteAdded);
    }

    [Fact]
    public async Task Own_action_is_not_news_except_the_created_receipt()
    {
        var options = Store();
        var w = await SeedAsync(options);
        // The customer opened the ticket and then commented on it themselves.
        await AddEventAsync(options, CompanyA, w.TicketId, w.CustomerId, TicketEventType.Created, Earlier);
        await AddEventAsync(options, CompanyA, w.TicketId, w.CustomerId, TicketEventType.CommentAdded, Later);

        var customerFeed = await FeedFor(options, new Caller(w.CustomerId)).GetAsync(20);
        var staffFeed = await FeedFor(options, new Caller(w.StaffId, CompanyA)).GetAsync(20);

        customerFeed.Items.Select(i => i.EventType).Should().Equal(TicketEventType.Created);
        // The assignee hears the comment they did not write; they did not open the ticket, so no receipt.
        staffFeed.Items.Select(i => i.EventType).Should().Equal(TicketEventType.CommentAdded);
    }

    [Fact]
    public async Task Another_companys_events_are_invisible()
    {
        var options = Store();
        var w = await SeedAsync(options);
        await AddEventAsync(options, CompanyA, w.TicketId, w.StaffId, TicketEventType.CommentAdded, Earlier);

        var otherAdminFeed = await FeedFor(options, new Caller(w.OtherAdminId, CompanyB)).GetAsync(20);

        otherAdminFeed.Items.Should().BeEmpty();
        otherAdminFeed.UnreadCount.Should().Be(0);
    }

    [Fact]
    public async Task A_deleted_ticket_leaves_the_feed()
    {
        var options = Store();
        var w = await SeedAsync(options);
        await AddEventAsync(options, CompanyA, w.TicketId, w.StaffId, TicketEventType.CommentAdded, Earlier);

        await using (var db = new CrmDbContext(options, new SuperAdmin()))
        {
            var ticket = await db.Tickets.FirstAsync(t => t.Id == w.TicketId);
            ticket.SoftDelete(new DateTime(2025, 12, 31, 0, 0, 0, DateTimeKind.Utc));
            await db.SaveChangesAsync();
        }

        var customerFeed = await FeedFor(options, new Caller(w.CustomerId)).GetAsync(20);
        customerFeed.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Unread_count_follows_the_seen_timestamp_and_mark_seen_clears_it()
    {
        var options = Store();
        var w = await SeedAsync(options);
        await AddEventAsync(options, CompanyA, w.TicketId, w.StaffId, TicketEventType.CommentAdded, Earlier);
        await AddEventAsync(options, CompanyA, w.TicketId, w.StaffId, TicketEventType.StatusChanged, Later);

        var caller = new Caller(w.CustomerId);
        var beforeReading = await FeedFor(options, caller).GetAsync(20);
        beforeReading.UnreadCount.Should().Be(2);
        beforeReading.Items.Should().OnlyContain(i => i.IsUnread);
        // Newest first, so the list opens on what just happened.
        beforeReading.Items.Select(i => i.EventType)
            .Should().Equal(TicketEventType.StatusChanged, TicketEventType.CommentAdded);

        await FeedFor(options, caller).MarkSeenAsync();

        var afterReading = await FeedFor(options, caller).GetAsync(20);
        afterReading.UnreadCount.Should().Be(0);
        afterReading.Items.Should().HaveCount(2).And.OnlyContain(i => !i.IsUnread);
    }

    [Fact]
    public async Task An_event_arriving_after_the_read_click_stays_unread()
    {
        var options = Store();
        var w = await SeedAsync(options);
        await AddEventAsync(options, CompanyA, w.TicketId, w.StaffId, TicketEventType.CommentAdded, Earlier);

        var caller = new Caller(w.CustomerId);
        await FeedFor(options, caller).MarkSeenAsync(); // clock: 2026-01-01

        await AddEventAsync(options, CompanyA, w.TicketId, w.StaffId, TicketEventType.StatusChanged,
            new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc));

        var feed = await FeedFor(options, caller).GetAsync(20);
        feed.UnreadCount.Should().Be(1);
        feed.Items.Should().HaveCount(2);
        feed.Items.Single(i => i.EventType == TicketEventType.StatusChanged).IsUnread.Should().BeTrue();
    }
}
