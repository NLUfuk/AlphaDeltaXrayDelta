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
    }

    private sealed class FixedClock : IClock
    {
        public DateTime UtcNow => new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    }

    private static readonly Application.Files.FileOptions Opt = new(); // 10MB, 5 files, image/pdf/office

    private static AttachmentService ServiceFor(CrmDbContext db, ICurrentUserService user)
    {
        var authz = new TicketAuthorizationService(user, new FakePermissionService(), db);
        return new AttachmentService(db, new FakeFileStorage(), authz, new FixedClock(), Options.Create(Opt));
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
        var svc = new AttachmentService(db, storage, authz, new FixedClock(), Options.Create(Opt));

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
            new FixedClock(), Options.Create(Opt));

        var png = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A };
        var act = () => svc.StorePublicUploadAsync("public/x", "photo.png", "image/png", new MemoryStream(png));

        (await act.Should().ThrowAsync<BadRequestException>()).Which.Code.Should().Be("attachment.type_not_allowed");
    }

    [Fact]
    public void Disallowed_content_type_is_rejected()
    {
        using var db = NewDb(new FakeUser(Guid.NewGuid(), true));
        var svc = ServiceFor(db, new FakeUser(Guid.NewGuid(), true));

        var act = () => svc.CreateUploadUrl("public/x", new UploadUrlRequest("evil.exe", "application/x-msdownload", 1000));

        act.Should().Throw<BadRequestException>().Which.Code.Should().Be("attachment.type_not_allowed");
    }

    [Fact]
    public void Oversize_file_is_rejected()
    {
        using var db = NewDb(new FakeUser(Guid.NewGuid(), true));
        var svc = ServiceFor(db, new FakeUser(Guid.NewGuid(), true));

        var act = () => svc.CreateUploadUrl("public/x", new UploadUrlRequest("big.pdf", "application/pdf", Opt.MaxSizeBytes + 1));

        act.Should().Throw<BadRequestException>().Which.Code.Should().Be("attachment.too_large");
    }

    [Fact]
    public void Allowed_file_gets_a_presigned_put_url_and_a_scoped_key()
    {
        using var db = NewDb(new FakeUser(Guid.NewGuid(), true));
        var svc = ServiceFor(db, new FakeUser(Guid.NewGuid(), true));

        var result = svc.CreateUploadUrl("public/acme", new UploadUrlRequest("shot.png", "image/png", 2048));

        result.Key.Should().StartWith("public/acme/");
        result.Url.Should().Contain(result.Key);
    }

    [Fact]
    public void Linking_more_than_the_limit_is_rejected()
    {
        using var db = NewDb(new FakeUser(Guid.NewGuid(), true));
        var svc = ServiceFor(db, new FakeUser(Guid.NewGuid(), true));

        var files = Enumerable.Range(0, Opt.MaxPerAttachTarget + 1)
            .Select(i => new AttachmentDescriptor($"k{i}", $"f{i}.png", "image/png", 10)).ToList();

        var act = () => svc.BuildAttachments(Guid.NewGuid(), Guid.NewGuid(), null, files, Guid.NewGuid());

        act.Should().Throw<BadRequestException>().Which.Code.Should().Be("attachment.too_many");
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
            var act = () => customerSvc.GetDownloadUrlAsync(attachmentId);
            await act.Should().ThrowAsync<ForbiddenException>().Where(e => e.Code == "attachment.internal_forbidden");
        }
    }

    // Shared in-memory root so a second context sees seeded rows.
    private static CrmDbContext NewDbShared(out DbContextOptions<CrmDbContext> options, ICurrentUserService user)
    {
        options = new DbContextOptionsBuilder<CrmDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString(), new Microsoft.EntityFrameworkCore.Storage.InMemoryDatabaseRoot()).Options;
        return new CrmDbContext(options, user);
    }
}
