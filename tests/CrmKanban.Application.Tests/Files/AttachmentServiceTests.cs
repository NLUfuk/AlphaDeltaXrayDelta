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

namespace CrmKanban.Application.Tests.Files;

/// <summary>
/// File attachment gates (spec §12): server-side type/size/count validation (the client is never
/// trusted) and the download rule that a customer never receives a file on an internal note — the
/// same privacy invariant as internal-note text (spec §14/§20).
/// </summary>
public class AttachmentServiceTests
{
    private sealed class FakeUser(Guid userId, bool isSuperAdmin, params Guid[] companyIds) : ICurrentUserService
    {
        public Guid? UserId { get; } = userId;
        public bool IsAuthenticated => true;
        public bool IsSuperAdmin { get; } = isSuperAdmin;
        public IReadOnlyCollection<Guid> CompanyIds { get; } = companyIds;
    }

    private sealed class FakePermissionService : IPermissionService
    {
        public Task<IReadOnlySet<string>> GetPermissionsAsync(Guid u, Guid c, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlySet<string>>(new HashSet<string>());
        public Task<bool> HasPermissionAsync(Guid u, Guid c, string k, CancellationToken ct = default) => Task.FromResult(false);
    }

    private sealed class FakeFileStorage : IFileStorage
    {
        public readonly Dictionary<string, byte[]> Stored = new();
        public string PresignPut(string key, string contentType, TimeSpan expiry) => $"https://storage.test/put/{key}";
        public string PresignGet(string key, TimeSpan expiry) => $"https://storage.test/get/{key}";
        public async Task PutAsync(string key, Stream content, string contentType, CancellationToken ct = default)
        {
            using var ms = new MemoryStream();
            await content.CopyToAsync(ms, ct);
            Stored[key] = ms.ToArray();
        }
        public Task<Stream> GetAsync(string key, CancellationToken ct = default) =>
            Task.FromResult<Stream>(new MemoryStream(Stored.TryGetValue(key, out var b) ? b : []));
    }

    private sealed class FixedClock : IClock
    {
        public DateTime UtcNow => new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    }

    private static readonly Application.Files.FileOptions Opt = new(); // 10MB, 5 files, image/pdf/office

    // No Settings rows seeded here on purpose: these tests assert the FALLBACK path, i.e. that an empty
    // or unreachable settings store still enforces the shipped limits. DbSettingsDriveFileLimitsTests
    // covers the other half — a seeded row overriding them.
    private static Application.Settings.SettingsService Settings(CrmDbContext db) =>
        new(db, new FakeUser(Guid.NewGuid(), true));

    private static AttachmentService ServiceFor(CrmDbContext db, ICurrentUserService user)
    {
        var authz = new TicketAuthorizationService(user, new FakePermissionService(), db);
        return new AttachmentService(db, new FakeFileStorage(), authz, new FixedClock(), Settings(db), Options.Create(Opt));
    }

    private static CrmDbContext NewDb(ICurrentUserService user) =>
        new(new DbContextOptionsBuilder<CrmDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options, user);

    [Fact]
    public async Task Public_upload_inspects_bytes_and_stores_a_valid_pdf()
    {
        using var db = NewDb(new FakeUser(Guid.NewGuid(), true));
        var storage = new FakeFileStorage();
        var authz = new TicketAuthorizationService(new FakeUser(Guid.NewGuid(), true), new FakePermissionService(), db);
        var svc = new AttachmentService(db, storage, authz, new FixedClock(), Settings(db), Options.Create(Opt));

        var pdf = "%PDF-1.7\nhello"u8.ToArray();
        var descriptor = await svc.StorePublicUploadAsync("public/x", "teklif.pdf", "application/pdf", new MemoryStream(pdf));

        descriptor.Size.Should().Be(pdf.Length, "size is measured server-side, not trusted from the client");
        descriptor.ContentType.Should().Be("application/pdf");
        storage.Stored.Should().ContainKey(descriptor.Key);
        storage.Stored[descriptor.Key].Should().Equal(pdf);
    }

    [Fact]
    public async Task Public_upload_rejects_a_file_that_is_not_a_real_document()
    {
        using var db = NewDb(new FakeUser(Guid.NewGuid(), true));
        var svc = new AttachmentService(db, new FakeFileStorage(),
            new TicketAuthorizationService(new FakeUser(Guid.NewGuid(), true), new FakePermissionService(), db),
            new FixedClock(), Settings(db), Options.Create(Opt));

        // SVG carries script — an image to the user, an XSS vector in a browser. Not on the allow-list.
        var svg = "<svg xmlns=\"http://www.w3.org/2000/svg\"/>"u8.ToArray();
        var act = () => svc.StorePublicUploadAsync("public/x", "logo.svg", "image/svg+xml", new MemoryStream(svg));

        (await act.Should().ThrowAsync<BadRequestException>()).Which.Code.Should().Be("attachment.type_not_allowed");
    }

    [Fact]
    public async Task Disallowed_content_type_is_rejected()
    {
        using var db = NewDb(new FakeUser(Guid.NewGuid(), true));
        var svc = ServiceFor(db, new FakeUser(Guid.NewGuid(), true));

        var act = () => svc.CreateUploadUrlAsync("public/x", new UploadUrlRequest("evil.exe", "application/x-msdownload", 1000));

        (await act.Should().ThrowAsync<BadRequestException>()).Which.Code.Should().Be("attachment.type_not_allowed");
    }

    [Fact]
    public async Task Oversize_file_is_rejected()
    {
        using var db = NewDb(new FakeUser(Guid.NewGuid(), true));
        var svc = ServiceFor(db, new FakeUser(Guid.NewGuid(), true));

        var act = () => svc.CreateUploadUrlAsync("public/x", new UploadUrlRequest("big.pdf", "application/pdf", Opt.MaxSizeBytes + 1));

        (await act.Should().ThrowAsync<BadRequestException>()).Which.Code.Should().Be("attachment.too_large");
    }

    [Fact]
    public async Task Allowed_file_gets_a_presigned_put_url_and_a_scoped_key()
    {
        using var db = NewDb(new FakeUser(Guid.NewGuid(), true));
        var svc = ServiceFor(db, new FakeUser(Guid.NewGuid(), true));

        var result = await svc.CreateUploadUrlAsync("public/acme", new UploadUrlRequest("shot.png", "image/png", 2048));

        result.Key.Should().StartWith("public/acme/");
        result.Url.Should().Contain(result.Key);
    }

    [Fact]
    public async Task Linking_more_than_the_limit_is_rejected()
    {
        using var db = NewDb(new FakeUser(Guid.NewGuid(), true));
        var svc = ServiceFor(db, new FakeUser(Guid.NewGuid(), true));

        var files = Enumerable.Range(0, Opt.MaxPerAttachTarget + 1)
            .Select(i => new AttachmentDescriptor($"k{i}", $"f{i}.png", "image/png", 10)).ToList();

        var act = () => svc.BuildAttachmentsAsync(Guid.NewGuid(), Guid.NewGuid(), null, files, Guid.NewGuid());

        (await act.Should().ThrowAsync<BadRequestException>()).Which.Code.Should().Be("attachment.too_many");
    }

    [Fact]
    public async Task Customer_cannot_download_a_file_on_an_internal_note()
    {
        var companyId = Guid.NewGuid();
        var customerId = Guid.NewGuid();

        // Seed a ticket opened by the customer, an internal comment, and an attachment on that comment.
        Guid attachmentId;
        await using (var seed = NewDbShared(out var options, new FakeUser(Guid.NewGuid(), true)))
        {
            var ticket = new Ticket(companyId, "ACME-1", customerId, Guid.NewGuid(), "t", "b");
            seed.Tickets.Add(ticket);
            var comment = new Comment(companyId, ticket.Id, Guid.NewGuid(), "secret", isInternal: true);
            seed.Comments.Add(comment);
            var att = new Attachment(companyId, ticket.Id, comment.Id, "k/secret.pdf", "secret.pdf", "application/pdf", 100, Guid.NewGuid());
            seed.Attachments.Add(att);
            await seed.SaveChangesAsync();
            attachmentId = att.Id;

            // Customer (opener, no company scope) is denied.
            var customerSvc = ServiceFor(new CrmDbContext(options, new FakeUser(customerId, false)), new FakeUser(customerId, false));
            var act = () => customerSvc.OpenAttachmentAsync(attachmentId);
            await act.Should().ThrowAsync<ForbiddenException>().Where(e => e.Code == "attachment.internal_forbidden");
        }
    }

    [Fact]
    public async Task Ticket_upload_measures_real_size_and_links_the_row()
    {
        var companyId = Guid.NewGuid();
        var openerId = Guid.NewGuid();

        await using var seed = NewDbShared(out var options, new FakeUser(Guid.NewGuid(), true));
        var ticket = new Ticket(companyId, "ACME-1", openerId, Guid.NewGuid(), "t", "b");
        seed.Tickets.Add(ticket);
        await seed.SaveChangesAsync();

        // The customer who opened the ticket attaches a file (authorized as the opener).
        var storage = new FakeFileStorage();
        var db = new CrmDbContext(options, new FakeUser(openerId, false));
        var authz = new TicketAuthorizationService(new FakeUser(openerId, false), new FakePermissionService(), db);
        var svc = new AttachmentService(db, storage, authz, new FixedClock(), Settings(db), Options.Create(Opt));

        var pdf = "%PDF-1.7 body"u8.ToArray();
        var dto = await svc.StoreTicketUploadAsync(ticket.Id, "teklif.pdf", "application/pdf", new MemoryStream(pdf));

        dto.Size.Should().Be(pdf.Length, "size is measured server-side, not trusted from the client");
        dto.Url.Should().Be($"/api/tickets/attachments/{dto.Id}/download", "downloads proxy through the API, same-origin");
        storage.Stored.Values.Should().ContainSingle().Which.Should().Equal(pdf);
        // Count ignoring the tenant filter: the customer actor has no company scope of their own.
        (await db.Attachments.IgnoreQueryFilters().CountAsync(a => a.TicketId == ticket.Id && a.CommentId == null)).Should().Be(1);
        // A file-added event is recorded so the opener/assignee get notified (spec §7).
        (await db.TicketEvents.IgnoreQueryFilters().CountAsync(e => e.TicketId == ticket.Id && e.EventType == TicketEventType.AttachmentAdded))
            .Should().Be(1);
    }

    [Fact]
    public async Task Ticket_upload_falls_back_to_the_extension_when_the_browser_sends_no_mime()
    {
        var companyId = Guid.NewGuid();
        var openerId = Guid.NewGuid();

        await using var seed = NewDbShared(out var options, new FakeUser(Guid.NewGuid(), true));
        var ticket = new Ticket(companyId, "ACME-1", openerId, Guid.NewGuid(), "t", "b");
        seed.Tickets.Add(ticket);
        await seed.SaveChangesAsync();

        var db = new CrmDbContext(options, new FakeUser(openerId, false));
        var authz = new TicketAuthorizationService(new FakeUser(openerId, false), new FakePermissionService(), db);
        var svc = new AttachmentService(db, new FakeFileStorage(), authz, new FixedClock(), Settings(db), Options.Create(Opt));

        // Empty content-type (as some browsers report) must not sink a valid .pdf.
        var dto = await svc.StoreTicketUploadAsync(ticket.Id, "teklif.pdf", "", new MemoryStream("%PDF-1.7"u8.ToArray()));

        dto.ContentType.Should().Be("application/pdf");
    }

    [Fact]
    public async Task A_file_sent_with_a_message_is_attached_to_that_message()
    {
        var companyId = Guid.NewGuid();
        var staffId = Guid.NewGuid();

        await using var seed = NewDbShared(out var options, new FakeUser(Guid.NewGuid(), true));
        var ticket = new Ticket(companyId, "ACME-1", Guid.NewGuid(), Guid.NewGuid(), "t", "b");
        ticket.Assign(staffId); // workspace scope (Faz 55): a personel reaches the tickets that are theirs
        seed.Tickets.Add(ticket);
        seed.Memberships.Add(new Membership(staffId, companyId, RoleType.Personel));
        var comment = new Comment(companyId, ticket.Id, staffId, "ekte gonderiyorum", isInternal: false);
        seed.Comments.Add(comment);
        await seed.SaveChangesAsync();

        var db = new CrmDbContext(options, new FakeUser(staffId, false, companyId));
        var authz = new TicketAuthorizationService(new FakeUser(staffId, false, companyId), new FakePermissionService(), db);
        var svc = new AttachmentService(db, new FakeFileStorage(), authz, new FixedClock(), Settings(db), Options.Create(Opt));

        var dto = await svc.StoreTicketUploadAsync(
            ticket.Id, "teklif.pdf", "application/pdf", new MemoryStream("%PDF-1.7 x"u8.ToArray()), comment.Id);

        // The row — and therefore the ticket screen — knows which message the file belongs to.
        dto.CommentId.Should().Be(comment.Id);
        (await db.Attachments.IgnoreQueryFilters().CountAsync(a => a.CommentId == comment.Id)).Should().Be(1);
        // A public message: the opener still hears about the file (spec §7).
        (await db.TicketEvents.IgnoreQueryFilters()
            .CountAsync(e => e.TicketId == ticket.Id && e.EventType == TicketEventType.AttachmentAdded)).Should().Be(1);
    }

    [Fact]
    public async Task A_file_on_an_internal_note_records_no_event_so_the_customer_never_hears_of_it()
    {
        var companyId = Guid.NewGuid();
        var staffId = Guid.NewGuid();

        await using var seed = NewDbShared(out var options, new FakeUser(Guid.NewGuid(), true));
        var ticket = new Ticket(companyId, "ACME-1", Guid.NewGuid(), Guid.NewGuid(), "t", "b");
        ticket.Assign(staffId); // workspace scope (Faz 55): a personel reaches the tickets that are theirs
        seed.Tickets.Add(ticket);
        seed.Memberships.Add(new Membership(staffId, companyId, RoleType.Personel));
        var note = new Comment(companyId, ticket.Id, staffId, "ic not", isInternal: true);
        seed.Comments.Add(note);
        await seed.SaveChangesAsync();

        var db = new CrmDbContext(options, new FakeUser(staffId, false, companyId));
        var authz = new TicketAuthorizationService(new FakeUser(staffId, false, companyId), new FakePermissionService(), db);
        var svc = new AttachmentService(db, new FakeFileStorage(), authz, new FixedClock(), Settings(db), Options.Create(Opt));

        await svc.StoreTicketUploadAsync(
            ticket.Id, "maliyet.pdf", "application/pdf", new MemoryStream("%PDF-1.7 x"u8.ToArray()), note.Id);

        // AttachmentAdded notifies the opener. On an internal note that would tell the customer a file
        // appeared on a conversation they cannot see (spec §20) — so no event is recorded at all.
        (await db.TicketEvents.IgnoreQueryFilters()
            .CountAsync(e => e.TicketId == ticket.Id && e.EventType == TicketEventType.AttachmentAdded)).Should().Be(0);
        (await db.Attachments.IgnoreQueryFilters().CountAsync(a => a.CommentId == note.Id)).Should().Be(1);
    }

    [Fact]
    public async Task A_file_cannot_be_hung_off_another_tickets_message()
    {
        var companyId = Guid.NewGuid();
        var staffId = Guid.NewGuid();

        await using var seed = NewDbShared(out var options, new FakeUser(Guid.NewGuid(), true));
        var mine = new Ticket(companyId, "ACME-1", Guid.NewGuid(), Guid.NewGuid(), "t", "b");
        mine.Assign(staffId);
        var other = new Ticket(companyId, "ACME-2", Guid.NewGuid(), Guid.NewGuid(), "t2", "b2");
        other.Assign(staffId);
        seed.Tickets.AddRange(mine, other);
        seed.Memberships.Add(new Membership(staffId, companyId, RoleType.Personel));
        var elsewhere = new Comment(companyId, other.Id, staffId, "baska talebin mesaji", isInternal: false);
        seed.Comments.Add(elsewhere);
        await seed.SaveChangesAsync();

        var db = new CrmDbContext(options, new FakeUser(staffId, false, companyId));
        var authz = new TicketAuthorizationService(new FakeUser(staffId, false, companyId), new FakePermissionService(), db);
        var svc = new AttachmentService(db, new FakeFileStorage(), authz, new FixedClock(), Settings(db), Options.Create(Opt));

        var act = () => svc.StoreTicketUploadAsync(
            mine.Id, "teklif.pdf", "application/pdf", new MemoryStream("%PDF-1.7 x"u8.ToArray()), elsewhere.Id);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    // Shared in-memory root so a second context sees seeded rows.
    private static CrmDbContext NewDbShared(out DbContextOptions<CrmDbContext> options, ICurrentUserService user)
    {
        options = new DbContextOptionsBuilder<CrmDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString(), new Microsoft.EntityFrameworkCore.Storage.InMemoryDatabaseRoot()).Options;
        return new CrmDbContext(options, user);
    }
}
