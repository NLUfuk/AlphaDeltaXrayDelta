using CrmKanban.Application.Abstractions;
using CrmKanban.Application.Common;
using CrmKanban.Application.Files;
using CrmKanban.Application.Settings;
using CrmKanban.Application.Tickets;
using CrmKanban.Domain.Common;
using CrmKanban.Domain.Entities;
using CrmKanban.Domain.Enums;
using CrmKanban.Infrastructure.Persistence;
using CrmKanban.Infrastructure.Persistence.Seed;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Options;

namespace CrmKanban.Application.Tests.Settings;

/// <summary>
/// The settings screen must not lie (teknik borç #56): a value the super admin edits has to change what
/// the server does. These tests are the executable form of that promise — for every business parameter
/// still in the catalog, a seeded row overrides the shipped default, and a missing or unusable row falls
/// back instead of taking the feature down. A regression here is invisible in the UI (the field still
/// saves, the behaviour just stops following it), which is exactly why it is tested rather than eyeballed.
/// </summary>
public class SettingsDriveBehaviourTests
{
    private sealed class SuperAdmin : ICurrentUserService
    {
        public Guid? UserId { get; } = Guid.NewGuid();
        public bool IsAuthenticated => true;
        public bool IsSuperAdmin => true;
        public IReadOnlyCollection<Guid> CompanyIds => [];
    }

    private sealed class Perms : IPermissionService
    {
        public Task<IReadOnlySet<string>> GetPermissionsAsync(Guid u, Guid c, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlySet<string>>(new HashSet<string>());
        public Task<bool> HasPermissionAsync(Guid u, Guid c, string k, CancellationToken ct = default) => Task.FromResult(true);
    }

    /// <summary>The reopen window is a span, so the clock has to move; FixedClock cannot express "3 days later".</summary>
    private sealed class MovableClock : IClock
    {
        public DateTime UtcNow { get; set; } = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    }

    private sealed class FakeStorage : IFileStorage
    {
        public string PresignPut(string key, string contentType, TimeSpan expiry) => $"https://s/{key}";
        public string PresignGet(string key, TimeSpan expiry) => $"https://s/{key}";
        public Task PutAsync(string key, Stream content, string contentType, CancellationToken ct = default) => Task.CompletedTask;
        public Task<Stream> GetAsync(string key, CancellationToken ct = default) => Task.FromResult<Stream>(new MemoryStream());
    }

    private static DbContextOptions<CrmDbContext> Store() =>
        new DbContextOptionsBuilder<CrmDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString(), new InMemoryDatabaseRoot()).Options;

    /// <summary>Company + the global status set + transition graph, plus whatever settings the test needs.</summary>
    private static async Task<Guid> SeedAsync(DbContextOptions<CrmDbContext> options, ICurrentUserService user,
        params Setting[] settings)
    {
        await using var db = new CrmDbContext(options, user);
        foreach (var s in DefaultStatuses.All)
            db.TicketStatuses.Add(new TicketStatus(s.Name, s.Category, s.Color, s.Order, s.IsTerminal, companyId: null, id: s.Id));
        foreach (var (from, to) in DefaultStatuses.Transitions())
            db.StatusTransitions.Add(new StatusTransition(from, to, Domain.Authorization.PermissionKeys.TicketStatusChange));
        var company = new Company("Acme", "acme", Guid.NewGuid());
        db.Companies.Add(company);
        db.Settings.AddRange(settings);
        await db.SaveChangesAsync();
        return company.Id;
    }

    private static Setting Row(string key, string value, string type, string group) => new(key, value, type, group);

    // ---- ticket.reopen_window_days ------------------------------------------------------------

    [Theory]
    [InlineData("1", false)]  // window shorter than the gap → the reopen must be refused
    [InlineData("30", true)]  // window longer than the shipped 7 → the reopen must be allowed
    public async Task Reopen_window_follows_the_stored_setting(string windowDays, bool expectReopened)
    {
        var options = Store();
        var user = new SuperAdmin();
        var companyId = await SeedAsync(options, user,
            Row("ticket.reopen_window_days", windowDays, "int", "ticket"));

        var clock = new MovableClock();
        await using var db = new CrmDbContext(options, user);
        var commands = new TicketCommandService(db, new TicketAuthorizationService(user, new Perms(), db), user, clock,
            new SettingsService(db, user));

        var ticket = new Ticket(companyId, "ACME-1", user.UserId!.Value, DefaultStatuses.New.Id, "t", "b");
        db.Tickets.Add(ticket);
        await db.SaveChangesAsync();

        // Close it, then come back three days later — inside the 30-day window, outside the 1-day one.
        await commands.ChangeStatusAsync(ticket.Id, new ChangeStatusRequest(DefaultStatuses.Completed.Id));
        clock.UtcNow = clock.UtcNow.AddDays(3);

        var reopen = () => commands.ReopenAsync(ticket.Id, new ChangeStatusRequest(DefaultStatuses.InProgress.Id));

        if (expectReopened)
        {
            await reopen();
            (await db.Tickets.SingleAsync(t => t.Id == ticket.Id)).StatusId.Should().Be(DefaultStatuses.InProgress.Id);
        }
        else
        {
            (await reopen.Should().ThrowAsync<DomainException>()).Which.Code.Should().Be("ticket.reopen.window_expired");
        }
    }

    // ---- ticket.default_priority --------------------------------------------------------------

    [Fact]
    public async Task A_new_ticket_starts_at_the_stored_default_priority()
    {
        var options = Store();
        var user = new SuperAdmin();
        var companyId = await SeedAsync(options, user,
            Row("ticket.default_priority", "Urgent", "string", "ticket"));

        await using var db = new CrmDbContext(options, user);
        var commands = new TicketCommandService(db, new TicketAuthorizationService(user, new Perms(), db), user,
            new MovableClock(), new SettingsService(db, user));

        // Priority omitted — the caller did not choose, so the setting decides (not the enum's zero value).
        var id = await commands.CreateAsync(new CreateTicketRequest(companyId, "Sunucu down", "acil"));

        (await db.Tickets.SingleAsync(t => t.Id == id)).Priority.Should().Be(Priority.Urgent);
    }

    [Fact]
    public async Task An_explicit_priority_still_wins_over_the_default()
    {
        var options = Store();
        var user = new SuperAdmin();
        var companyId = await SeedAsync(options, user,
            Row("ticket.default_priority", "Urgent", "string", "ticket"));

        await using var db = new CrmDbContext(options, user);
        var commands = new TicketCommandService(db, new TicketAuthorizationService(user, new Perms(), db), user,
            new MovableClock(), new SettingsService(db, user));

        var id = await commands.CreateAsync(new CreateTicketRequest(companyId, "Yazım hatası", "ufak", Priority.Low));

        (await db.Tickets.SingleAsync(t => t.Id == id)).Priority.Should().Be(Priority.Low);
    }

    [Fact]
    public async Task An_unparseable_priority_falls_back_instead_of_failing_the_create()
    {
        var options = Store();
        var user = new SuperAdmin();
        var companyId = await SeedAsync(options, user,
            Row("ticket.default_priority", "cok-acil", "string", "ticket")); // predates validation / hand-edited

        await using var db = new CrmDbContext(options, user);
        var commands = new TicketCommandService(db, new TicketAuthorizationService(user, new Perms(), db), user,
            new MovableClock(), new SettingsService(db, user));

        var id = await commands.CreateAsync(new CreateTicketRequest(companyId, "t", "b"));

        (await db.Tickets.SingleAsync(t => t.Id == id)).Priority.Should().Be(Priority.Normal);
    }

    // ---- file.* ------------------------------------------------------------------------------

    private static AttachmentService FilesFor(DbContextOptions<CrmDbContext> options, ICurrentUserService user, out CrmDbContext db)
    {
        db = new CrmDbContext(options, user);
        return new AttachmentService(db, new FakeStorage(),
            new TicketAuthorizationService(user, new Perms(), db), new MovableClock(),
            new SettingsService(db, user), Options.Create(new Application.Files.FileOptions()));
    }

    private static List<AttachmentDescriptor> Files(int count, string contentType = "image/png", long size = 1024) =>
        Enumerable.Range(0, count).Select(i => new AttachmentDescriptor($"k{i}", $"f{i}.png", contentType, size)).ToList();

    [Fact]
    public async Task Attachment_count_limit_follows_the_stored_setting()
    {
        var options = Store();
        var user = new SuperAdmin();
        await SeedAsync(options, user, Row("file.max_per_comment", "2", "int", "file"));
        var svc = FilesFor(options, user, out var db);
        using var _ = db;

        // Two is fine under the stored limit; three is not — even though the shipped fallback allows five.
        await svc.BuildAttachmentsAsync(Guid.NewGuid(), Guid.NewGuid(), null, Files(2), Guid.NewGuid());

        var act = () => svc.BuildAttachmentsAsync(Guid.NewGuid(), Guid.NewGuid(), null, Files(3), Guid.NewGuid());
        (await act.Should().ThrowAsync<BadRequestException>()).Which.Code.Should().Be("attachment.too_many");
    }

    [Fact]
    public async Task Attachment_size_limit_follows_the_stored_setting()
    {
        var options = Store();
        var user = new SuperAdmin();
        await SeedAsync(options, user, Row("file.max_size_mb", "1", "int", "file"));
        var svc = FilesFor(options, user, out var db);
        using var _ = db;

        // 2 MB: under the 10 MB fallback, over the stored 1 MB.
        var act = () => svc.BuildAttachmentsAsync(Guid.NewGuid(), Guid.NewGuid(), null,
            Files(1, size: 2 * 1024 * 1024), Guid.NewGuid());

        (await act.Should().ThrowAsync<BadRequestException>()).Which.Code.Should().Be("attachment.too_large");
    }

    [Fact]
    public async Task Allowed_content_types_follow_the_stored_setting()
    {
        var options = Store();
        var user = new SuperAdmin();
        await SeedAsync(options, user, Row("file.allowed_types", "[\"application/pdf\"]", "json", "file"));
        var svc = FilesFor(options, user, out var db);
        using var _ = db;

        // PNG is on the shipped fallback list but not on the stored one.
        var act = () => svc.BuildAttachmentsAsync(Guid.NewGuid(), Guid.NewGuid(), null, Files(1), Guid.NewGuid());

        (await act.Should().ThrowAsync<BadRequestException>()).Which.Code.Should().Be("attachment.type_not_allowed");
    }

    [Fact]
    public async Task A_malformed_row_falls_back_to_the_shipped_limits()
    {
        var options = Store();
        var user = new SuperAdmin();
        await SeedAsync(options, user,
            Row("file.max_per_comment", "bes tane", "int", "file"),
            Row("file.allowed_types", "{ not an array }", "json", "file"));
        var svc = FilesFor(options, user, out var db);
        using var _ = db;

        // A broken settings table must not block uploads: FileOptions' defaults (5 files, png allowed) apply.
        var built = await svc.BuildAttachmentsAsync(Guid.NewGuid(), Guid.NewGuid(), null, Files(5), Guid.NewGuid());

        built.Should().HaveCount(5);
    }

    // ---- write-side validation ----------------------------------------------------------------

    [Theory]
    [InlineData("file.max_size_mb", "int", "10 MB", "setting.invalid_int")]   // unit typed in with the number
    [InlineData("file.max_size_mb", "int", "0", "setting.invalid_int")]       // zero would disable uploads, not tune them
    [InlineData("file.allowed_types", "json", "image/png,application/pdf", "setting.invalid_json")] // CSV, not JSON
    [InlineData("brand.primary_color", "color", "mavi", "setting.invalid_color")]
    public async Task An_unusable_value_is_rejected_at_write_time(string key, string type, string value, string code)
    {
        var options = Store();
        var user = new SuperAdmin();
        await SeedAsync(options, user, Row(key, "1", type, "x"));
        await using var db = new CrmDbContext(options, user);
        var svc = new SettingsService(db, user);

        var act = () => svc.UpdateAsync(key, value);

        // Storing it would be worse than refusing: the screen would show the new value while the server
        // silently kept using the fallback — the exact failure this whole change exists to remove.
        (await act.Should().ThrowAsync<BadRequestException>()).Which.Code.Should().Be(code);
        (await db.Settings.SingleAsync(s => s.Key == key)).Value.Should().Be("1");
    }

    [Fact]
    public async Task A_reader_later_in_the_same_request_sees_the_updated_value()
    {
        var options = Store();
        var user = new SuperAdmin();
        await SeedAsync(options, user, Row("file.max_size_mb", "10", "int", "file"));
        await using var db = new CrmDbContext(options, user);
        var svc = new SettingsService(db, user);

        (await svc.GetIntAsync("file.max_size_mb", 1)).Should().Be(10); // primes the per-request snapshot
        await svc.UpdateAsync("file.max_size_mb", "25");

        (await svc.GetIntAsync("file.max_size_mb", 1)).Should().Be(25, "the write must invalidate the snapshot");
    }
}
