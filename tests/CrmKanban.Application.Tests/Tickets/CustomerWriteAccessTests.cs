using CrmKanban.Application.Abstractions;
using CrmKanban.Application.Common;
using CrmKanban.Application.Files;
using CrmKanban.Application.Tickets;
using CrmKanban.Domain.Entities;
using CrmKanban.Domain.Enums;
using CrmKanban.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CrmKanban.Application.Tests.Tickets;

/// <summary>
/// A customer must be able to communicate on their own ticket (spec §18.12): add a public comment and
/// cancel/complete it. Customers are not company members, so the write path must load the ticket
/// ignoring the tenant filter and gate on authz (opener), exactly like the read path — otherwise their
/// own ticket 404s. Core, test-first (spec §17). Also guards the isolation: a stranger cannot write.
/// </summary>
public class CustomerWriteAccessTests
{
    private sealed class FakeCurrentUser(Guid userId, bool isSuperAdmin, params Guid[] companyIds) : ICurrentUserService
    {
        public Guid? UserId { get; } = userId;
        public bool IsAuthenticated => true;
        public bool IsSuperAdmin { get; } = isSuperAdmin;
        public IReadOnlyCollection<Guid> CompanyIds { get; } = companyIds;
    }

    private sealed class FakePermissionService : IPermissionService
    {
        public Task<IReadOnlySet<string>> GetPermissionsAsync(Guid u, Guid c, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlySet<string>>(new HashSet<string>(StringComparer.Ordinal));
        public Task<bool> HasPermissionAsync(Guid u, Guid c, string k, CancellationToken ct = default) => Task.FromResult(false);
    }

    private sealed class FakeFileStorage : IFileStorage
    {
        public string PresignPut(string key, string contentType, TimeSpan expiry) => "https://storage.test/put";
        public string PresignGet(string key, TimeSpan expiry) => "https://storage.test/get";
        public Task PutAsync(string key, Stream content, string contentType, CancellationToken ct = default) => Task.CompletedTask;
        public Task<Stream> GetAsync(string key, CancellationToken ct = default) => Task.FromResult<Stream>(new MemoryStream());
    }

    private sealed class FixedClock : IClock
    {
        public DateTime UtcNow => new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    }

    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid CustomerId = Guid.NewGuid();
    private static readonly Guid OpenStatusId = Guid.NewGuid();
    private static readonly Guid CancelledStatusId = Guid.NewGuid();

    private static DbContextOptions<CrmDbContext> Store() =>
        new DbContextOptionsBuilder<CrmDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString(),
                new Microsoft.EntityFrameworkCore.Storage.InMemoryDatabaseRoot()).Options;

    private static async Task<Guid> SeedTicketAsync(DbContextOptions<CrmDbContext> options)
    {
        await using var db = new CrmDbContext(options, new FakeCurrentUser(Guid.NewGuid(), isSuperAdmin: true));
        db.TicketStatuses.Add(new TicketStatus("Açık", StatusCategory.Open, "#000", 1, isTerminal: false, id: OpenStatusId));
        db.TicketStatuses.Add(new TicketStatus("İptal", StatusCategory.Cancelled, "#999", 9, isTerminal: true, id: CancelledStatusId));
        var ticket = new Ticket(CompanyId, "ACME-1", CustomerId, OpenStatusId, "Talep", "gövde");
        db.Tickets.Add(ticket);
        await db.SaveChangesAsync();
        return ticket.Id;
    }

    private static (CommentService Comments, TicketCommandService Commands) ServicesFor(
        DbContextOptions<CrmDbContext> options, ICurrentUserService user)
    {
        var db = new CrmDbContext(options, user);
        var authz = new TicketAuthorizationService(user, new FakePermissionService(), db);
        var attachments = new AttachmentService(db, new FakeFileStorage(), authz, new FixedClock(),
            Options.Create(new Application.Files.FileOptions()));
        var comments = new CommentService(db, authz, attachments, new FixedClock());
        var commands = new TicketCommandService(db, authz, user, new FixedClock(), Options.Create(new TicketOptions()));
        return (comments, commands);
    }

    private static TicketQueryService QueriesFor(DbContextOptions<CrmDbContext> options, ICurrentUserService user)
    {
        var db = new CrmDbContext(options, user);
        var authz = new TicketAuthorizationService(user, new FakePermissionService(), db);
        return new TicketQueryService(db, user, authz, Options.Create(new TicketOptions()));
    }

    [Fact]
    public async Task Customer_can_comment_on_their_own_ticket()
    {
        var options = Store();
        var ticketId = await SeedTicketAsync(options);
        var customer = new FakeCurrentUser(CustomerId, isSuperAdmin: false); // no company scope

        var commentId = await ServicesFor(options, customer).Comments
            .AddAsync(ticketId, new AddCommentRequest("Fiyat listesini bekliyorum.", IsInternal: false));

        await using var read = new CrmDbContext(options, new FakeCurrentUser(Guid.NewGuid(), true));
        var comment = await read.Comments.IgnoreQueryFilters().SingleAsync(c => c.Id == commentId);
        comment.AuthorId.Should().Be(CustomerId);
        comment.IsInternal.Should().BeFalse();
    }

    [Fact]
    public async Task Customer_can_cancel_their_own_ticket()
    {
        var options = Store();
        var ticketId = await SeedTicketAsync(options);
        var customer = new FakeCurrentUser(CustomerId, isSuperAdmin: false);

        await ServicesFor(options, customer).Commands
            .ChangeStatusAsync(ticketId, new ChangeStatusRequest(CancelledStatusId));

        await using var read = new CrmDbContext(options, new FakeCurrentUser(Guid.NewGuid(), true));
        (await read.Tickets.IgnoreQueryFilters().SingleAsync()).StatusId.Should().Be(CancelledStatusId);
    }

    [Fact]
    public async Task Customer_can_open_a_follow_up_to_a_company_they_already_contacted()
    {
        var options = Store();
        Guid companyId;
        await using (var seed = new CrmDbContext(options, new FakeCurrentUser(Guid.NewGuid(), isSuperAdmin: true)))
        {
            seed.TicketStatuses.Add(new TicketStatus("Açık", StatusCategory.Open, "#000", 1, isTerminal: false, id: OpenStatusId));
            var company = new Company("Acme", "acme", Guid.NewGuid());
            seed.Companies.Add(company);
            // First contact already happened (via the public form): the customer has a ticket here.
            seed.Tickets.Add(new Ticket(company.Id, "ACME-1", CustomerId, OpenStatusId, "İlk talep", "gövde"));
            await seed.SaveChangesAsync();
            companyId = company.Id;
        }
        var customer = new FakeCurrentUser(CustomerId, isSuperAdmin: false); // not a member, but related to Acme

        var ticketId = await ServicesFor(options, customer).Commands
            .CreateAsCustomerAsync(new CustomerCreateTicketRequest(companyId, "Fiyat listesi", "Toptan fiyat rica ederim."));

        await using var read = new CrmDbContext(options, new FakeCurrentUser(Guid.NewGuid(), isSuperAdmin: true));
        var ticket = await read.Tickets.IgnoreQueryFilters().SingleAsync(t => t.Id == ticketId);
        ticket.OpenedById.Should().Be(CustomerId);
        ticket.CompanyId.Should().Be(companyId);
        ticket.ApprovalState.Should().Be(TicketApprovalState.Approved, "a verified customer's ticket enters the pool directly");
    }

    [Fact]
    public async Task Customer_cannot_open_a_ticket_to_a_company_they_never_contacted()
    {
        var options = Store();
        Guid companyId;
        await using (var seed = new CrmDbContext(options, new FakeCurrentUser(Guid.NewGuid(), isSuperAdmin: true)))
        {
            seed.TicketStatuses.Add(new TicketStatus("Açık", StatusCategory.Open, "#000", 1, isTerminal: false, id: OpenStatusId));
            var company = new Company("Acme", "acme", Guid.NewGuid());
            seed.Companies.Add(company);
            await seed.SaveChangesAsync();
            companyId = company.Id;
        }
        var customer = new FakeCurrentUser(CustomerId, isSuperAdmin: false); // no relationship with Acme

        var act = () => ServicesFor(options, customer).Commands
            .CreateAsCustomerAsync(new CustomerCreateTicketRequest(companyId, "Merhaba", "İlk kez yazıyorum."));

        await act.Should().ThrowAsync<ForbiddenException>("first contact must go through the company's public form, not the portal");
    }

    [Fact]
    public async Task My_companies_lists_only_companies_the_customer_has_contacted()
    {
        var options = Store();
        Guid acmeId, betaId;
        await using (var seed = new CrmDbContext(options, new FakeCurrentUser(Guid.NewGuid(), isSuperAdmin: true)))
        {
            seed.TicketStatuses.Add(new TicketStatus("Açık", StatusCategory.Open, "#000", 1, isTerminal: false, id: OpenStatusId));
            var acme = new Company("Acme", "acme", Guid.NewGuid());
            var beta = new Company("Beta", "beta", Guid.NewGuid());
            seed.Companies.AddRange(acme, beta);
            seed.Tickets.Add(new Ticket(acme.Id, "ACME-1", CustomerId, OpenStatusId, "Talep", "x"));      // customer ↔ Acme
            seed.Tickets.Add(new Ticket(beta.Id, "BETA-1", Guid.NewGuid(), OpenStatusId, "Başka", "y"));  // someone else ↔ Beta
            await seed.SaveChangesAsync();
            acmeId = acme.Id; betaId = beta.Id;
        }
        var customer = new FakeCurrentUser(CustomerId, isSuperAdmin: false);

        var companies = await QueriesFor(options, customer).ListMyCompaniesAsync();

        companies.Should().ContainSingle(c => c.Id == acmeId);
        companies.Should().NotContain(c => c.Id == betaId);
    }

    [Fact]
    public async Task A_stranger_cannot_comment_on_a_ticket_they_did_not_open()
    {
        var options = Store();
        var ticketId = await SeedTicketAsync(options);
        var stranger = new FakeCurrentUser(Guid.NewGuid(), isSuperAdmin: false); // not opener, not a member

        var act = () => ServicesFor(options, stranger).Comments
            .AddAsync(ticketId, new AddCommentRequest("sızmaya çalışıyorum", IsInternal: false));

        await act.Should().ThrowAsync<ForbiddenException>();
    }
}
