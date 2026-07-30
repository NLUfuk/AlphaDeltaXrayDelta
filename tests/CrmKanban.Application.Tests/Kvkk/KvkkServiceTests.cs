using CrmKanban.Application.Abstractions;
using CrmKanban.Application.Common;
using CrmKanban.Application.Kvkk;
using CrmKanban.Domain.Entities;
using CrmKanban.Domain.Enums;
using CrmKanban.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace CrmKanban.Application.Tests.Kvkk;

/// <summary>
/// KVKK erasure-as-anonymization (spec §16): personal fields are masked while ticket history and the
/// audit chain survive (hard delete would break both). The gate is super-admin only, and a super admin
/// can't be anonymized. Runs over the real DbContext (InMemory).
/// </summary>
public class KvkkServiceTests
{
    private sealed class FakeUser(bool isSuperAdmin, Guid? userId) : ICurrentUserService
    {
        public Guid? UserId { get; } = userId;
        public bool IsAuthenticated => true;
        public bool IsSuperAdmin { get; } = isSuperAdmin;
        public IReadOnlyCollection<Guid> CompanyIds => [];
    }

    private sealed class FixedClock : IClock
    {
        public DateTime UtcNow => new(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
    }

    private static readonly Guid Company = Guid.NewGuid();

    private static DbContextOptions<CrmDbContext> Store() =>
        new DbContextOptionsBuilder<CrmDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString(),
                new Microsoft.EntityFrameworkCore.Storage.InMemoryDatabaseRoot()).Options;

    private static KvkkService Service(CrmDbContext db, bool isSuperAdmin) =>
        new(db, new FakeUser(isSuperAdmin, Guid.NewGuid()), new FixedClock());

    [Fact]
    public async Task Anonymize_masks_personal_data_but_keeps_ticket_history()
    {
        var options = Store();
        Guid customerId, ticketId;
        await using (var db = new CrmDbContext(options, new FakeUser(true, null)))
        {
            var customer = new User("jane@real.com", "Jane", "Doe");
            customer.SetPasswordHash("hash");
            db.Users.Add(customer);
            var seedTicket = new Ticket(Company, "A-1", customer.Id, Guid.NewGuid(), "t", "b");
            db.Tickets.Add(seedTicket);
            db.TicketEvents.Add(new TicketEvent(Company, seedTicket.Id, customer.Id, TicketEventType.Created, null, "A-1"));
            db.RefreshTokens.Add(new RefreshToken(customer.Id, "tok", new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc)));
            await db.SaveChangesAsync();
            customerId = customer.Id;
            ticketId = seedTicket.Id;

            await Service(db, isSuperAdmin: true).AnonymizeUserAsync(customerId);
        }

        await using var read = new CrmDbContext(options, new FakeUser(true, null));
        var user = await read.Users.SingleAsync(u => u.Id == customerId);
        user.Email.Should().NotContain("jane@real.com").And.Contain("anonymized");
        user.FirstName.Should().Be("Anonim");
        user.PasswordHash.Should().BeNull("an anonymized user can no longer log in");
        user.IsActive.Should().BeFalse();

        // Ticket + event survive (statistics preserved), still linked to the same (now masked) id.
        var ticket = await read.Tickets.SingleAsync(t => t.Id == ticketId);
        ticket.OpenedById.Should().Be(customerId);
        (await read.TicketEvents.CountAsync(e => e.TicketId == ticketId)).Should().Be(1);

        // Refresh token revoked; anonymization audited.
        (await read.RefreshTokens.SingleAsync(r => r.UserId == customerId)).RevokedAt.Should().NotBeNull();
        (await read.AuditLogs.CountAsync(a => a.Action == "kvkk.anonymize")).Should().Be(1);
    }

    [Fact]
    public async Task Anonymize_is_forbidden_for_non_super_admin()
    {
        var options = Store();
        Guid customerId;
        await using var db = new CrmDbContext(options, new FakeUser(true, null));
        var customer = new User("jane@real.com", "Jane", "Doe");
        db.Users.Add(customer);
        await db.SaveChangesAsync();
        customerId = customer.Id;

        var act = () => Service(db, isSuperAdmin: false).AnonymizeUserAsync(customerId);
        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task A_super_admin_cannot_be_anonymized()
    {
        var options = Store();
        Guid adminId;
        await using var db = new CrmDbContext(options, new FakeUser(true, null));
        var admin = new User("root@x.com", "Super", "Admin");
        admin.PromoteToSuperAdmin();
        db.Users.Add(admin);
        await db.SaveChangesAsync();
        adminId = admin.Id;

        var act = () => Service(db, isSuperAdmin: true).AnonymizeUserAsync(adminId);
        await act.Should().ThrowAsync<ConflictException>();
    }
}
