using CrmKanban.Application.Abstractions;
using CrmKanban.Application.Common;
using CrmKanban.Application.Reports;
using CrmKanban.Domain.Authorization;
using CrmKanban.Domain.Common;
using CrmKanban.Domain.Entities;
using CrmKanban.Domain.Enums;
using CrmKanban.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace CrmKanban.Application.Tests.Reports;

/// <summary>
/// The money view of the report (Faz 39). Two invariants carry the weight here.
/// <para>
/// <b>Access:</b> commercial value is a second secret on top of the ticket. Seeing that ten requests
/// closed is not the same as seeing that they were worth two million, so <c>ticket.value</c> gates the
/// figures — and it gates them server-side. A test that only checked the UI would pass while the JSON
/// still carried the numbers.
/// </para>
/// <para>
/// <b>Classification:</b> won/lost is read off <see cref="StatusCategory"/>, never the status name.
/// Companies rename their columns ("Tamamlandı" → "Teslim edildi"); if totals keyed on the name they
/// would quietly go to zero the day someone edits a column (spec §4.3).
/// </para>
/// </summary>
public class RevenueReportTests
{
    private sealed class FakeUser(bool isSuperAdmin, Guid? userId, params Guid[] companyIds) : ICurrentUserService
    {
        public Guid? UserId { get; } = userId;
        public bool IsAuthenticated => true;
        public bool IsSuperAdmin { get; } = isSuperAdmin;
        public IReadOnlyCollection<Guid> CompanyIds { get; } = companyIds;
    }

    private sealed class FakePermissions(Guid company, params string[] granted) : IPermissionService
    {
        public Task<IReadOnlySet<string>> GetPermissionsAsync(Guid userId, Guid companyId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlySet<string>>(companyId == company ? granted.ToHashSet() : new HashSet<string>());
        public async Task<bool> HasPermissionAsync(Guid userId, Guid companyId, string key, CancellationToken ct = default) =>
            (await GetPermissionsAsync(userId, companyId, ct)).Contains(key);
    }

    private static readonly Guid CompanyA = Guid.NewGuid();
    private static readonly Guid CompanyB = Guid.NewGuid();
    private static readonly Guid OpenStatus = Guid.NewGuid();
    private static readonly Guid WonStatus = Guid.NewGuid();
    private static readonly Guid LostStatus = Guid.NewGuid();

    private static DbContextOptions<CrmDbContext> Store() =>
        new DbContextOptionsBuilder<CrmDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString(),
                new Microsoft.EntityFrameworkCore.Storage.InMemoryDatabaseRoot()).Options;

    private static async Task SeedStatusesAsync(CrmDbContext db)
    {
        // Names deliberately NOT "Closed"/"Cancelled": the classification must follow the category.
        db.TicketStatuses.Add(new TicketStatus("Görüşülüyor", StatusCategory.Open, "#000", 1, false, null, OpenStatus));
        db.TicketStatuses.Add(new TicketStatus("Teslim edildi", StatusCategory.Closed, "#0a0", 2, true, null, WonStatus));
        db.TicketStatuses.Add(new TicketStatus("Vazgeçildi", StatusCategory.Cancelled, "#a00", 3, true, null, LostStatus));
        await db.SaveChangesAsync();
    }

    private static Ticket Priced(Guid company, string number, Guid status, decimal? estimated, decimal? actual = null)
    {
        var t = new Ticket(company, number, Guid.NewGuid(), status, "t", "b");
        t.SetValue(estimated, actual);
        return t;
    }

    private static ReportService Service(CrmDbContext db, ICurrentUserService user, IPermissionService perms) =>
        new(db, user, perms, new Application.Settings.SettingsService(db, user));

    private static IPermissionService FullAccess =>
        new FakePermissions(CompanyA, PermissionKeys.ReportCompany, PermissionKeys.TicketValue);

    // ---- access ----

    [Fact]
    public async Task Revenue_is_withheld_from_a_user_without_ticket_value()
    {
        var options = Store();
        var user = new FakeUser(false, Guid.NewGuid(), CompanyA);
        await using (var db = new CrmDbContext(options, new FakeUser(true, null)))
        {
            await SeedStatusesAsync(db);
            db.Tickets.Add(Priced(CompanyA, "A-1", WonStatus, 100_000m));
            await db.SaveChangesAsync();
        }

        await using var read = new CrmDbContext(options, user);
        // report.company but NOT ticket.value: they may see the report, not the money.
        var report = await Service(read, user, new FakePermissions(CompanyA, PermissionKeys.ReportCompany))
            .CompanyReportAsync(CompanyA, null, null);

        report.TotalTickets.Should().Be(1, "the ticket itself is still visible");
        report.Revenue.Should().BeNull("the figures are not computed, let alone serialised");
    }

    [Fact]
    public async Task Revenue_is_returned_with_ticket_value()
    {
        var options = Store();
        var user = new FakeUser(false, Guid.NewGuid(), CompanyA);
        await using (var db = new CrmDbContext(options, new FakeUser(true, null)))
        {
            await SeedStatusesAsync(db);
            db.Tickets.Add(Priced(CompanyA, "A-1", WonStatus, 100_000m));
            await db.SaveChangesAsync();
        }

        await using var read = new CrmDbContext(options, user);
        var report = await Service(read, user, FullAccess).CompanyReportAsync(CompanyA, null, null);

        report.Revenue.Should().NotBeNull();
        report.Revenue!.WonTotal.Should().Be(100_000m);
    }

    [Fact]
    public async Task Totals_never_cross_the_tenant_boundary()
    {
        var options = Store();
        var user = new FakeUser(false, Guid.NewGuid(), CompanyA);
        await using (var db = new CrmDbContext(options, new FakeUser(true, null)))
        {
            await SeedStatusesAsync(db);
            db.Tickets.Add(Priced(CompanyA, "A-1", WonStatus, 100_000m));
            db.Tickets.Add(Priced(CompanyB, "B-1", WonStatus, 999_000m)); // another company's order book
            await db.SaveChangesAsync();
        }

        await using var read = new CrmDbContext(options, user);
        var report = await Service(read, user, FullAccess).CompanyReportAsync(CompanyA, null, null);

        report.Revenue!.WonTotal.Should().Be(100_000m, "company B is behind the tenant filter");
    }

    // ---- classification ----

    [Fact]
    public async Task Won_and_lost_follow_the_category_not_the_status_name()
    {
        var options = Store();
        var user = new FakeUser(false, Guid.NewGuid(), CompanyA);
        await using (var db = new CrmDbContext(options, new FakeUser(true, null)))
        {
            await SeedStatusesAsync(db);
            db.Tickets.Add(Priced(CompanyA, "A-1", WonStatus, 60_000m));   // "Teslim edildi" → Closed
            db.Tickets.Add(Priced(CompanyA, "A-2", LostStatus, 25_000m));  // "Vazgeçildi"   → Cancelled
            db.Tickets.Add(Priced(CompanyA, "A-3", OpenStatus, 40_000m));  // "Görüşülüyor"  → still open
            await db.SaveChangesAsync();
        }

        await using var read = new CrmDbContext(options, user);
        var r = (await Service(read, user, FullAccess).CompanyReportAsync(CompanyA, null, null)).Revenue!;

        r.WonTotal.Should().Be(60_000m);
        r.LostTotal.Should().Be(25_000m);
        r.OpenTotal.Should().Be(40_000m);
        r.WonCount.Should().Be(1);
        r.LostCount.Should().Be(1);
        r.OpenCount.Should().Be(1);
    }

    [Fact]
    public async Task An_unpriced_ticket_is_counted_apart_not_as_zero()
    {
        var options = Store();
        var user = new FakeUser(false, Guid.NewGuid(), CompanyA);
        await using (var db = new CrmDbContext(options, new FakeUser(true, null)))
        {
            await SeedStatusesAsync(db);
            db.Tickets.Add(Priced(CompanyA, "A-1", WonStatus, 80_000m));
            db.Tickets.Add(new Ticket(CompanyA, "A-2", Guid.NewGuid(), WonStatus, "t", "b")); // never priced
            await db.SaveChangesAsync();
        }

        await using var read = new CrmDbContext(options, user);
        var r = (await Service(read, user, FullAccess).CompanyReportAsync(CompanyA, null, null)).Revenue!;

        r.UnpricedCount.Should().Be(1);
        r.WonCount.Should().Be(1, "an unpriced ticket is not a zero-value win");
        r.WonTotal.Should().Be(80_000m);
    }

    [Fact]
    public async Task Actual_value_wins_over_the_estimate()
    {
        var options = Store();
        var user = new FakeUser(false, Guid.NewGuid(), CompanyA);
        await using (var db = new CrmDbContext(options, new FakeUser(true, null)))
        {
            await SeedStatusesAsync(db);
            db.Tickets.Add(Priced(CompanyA, "A-1", WonStatus, estimated: 100_000m, actual: 92_500m));
            await db.SaveChangesAsync();
        }

        await using var read = new CrmDbContext(options, user);
        var r = (await Service(read, user, FullAccess).CompanyReportAsync(CompanyA, null, null)).Revenue!;

        r.WonTotal.Should().Be(92_500m, "what it actually came to, not what we hoped");
        r.ForecastAccuracy.Should().Be(0.925m);
    }

    [Fact]
    public async Task Forecast_accuracy_ignores_won_tickets_with_no_actual_figure()
    {
        var options = Store();
        var user = new FakeUser(false, Guid.NewGuid(), CompanyA);
        await using (var db = new CrmDbContext(options, new FakeUser(true, null)))
        {
            await SeedStatusesAsync(db);
            db.Tickets.Add(Priced(CompanyA, "A-1", WonStatus, estimated: 100_000m, actual: 50_000m));
            // Estimate only: counting it would inject a flattering 1.0 into the average.
            db.Tickets.Add(Priced(CompanyA, "A-2", WonStatus, estimated: 100_000m));
            await db.SaveChangesAsync();
        }

        await using var read = new CrmDbContext(options, user);
        var r = (await Service(read, user, FullAccess).CompanyReportAsync(CompanyA, null, null)).Revenue!;

        r.ForecastAccuracy.Should().Be(0.5m);
    }

    [Fact]
    public async Task Win_rate_is_null_before_anything_closes()
    {
        var options = Store();
        var user = new FakeUser(false, Guid.NewGuid(), CompanyA);
        await using (var db = new CrmDbContext(options, new FakeUser(true, null)))
        {
            await SeedStatusesAsync(db);
            db.Tickets.Add(Priced(CompanyA, "A-1", OpenStatus, 40_000m));
            await db.SaveChangesAsync();
        }

        await using var read = new CrmDbContext(options, user);
        var r = (await Service(read, user, FullAccess).CompanyReportAsync(CompanyA, null, null)).Revenue!;

        r.WinRateByCount.Should().BeNull("0/0 is undefined, not a 0% win rate");
        r.WinRateByValue.Should().BeNull();
    }

    [Fact]
    public async Task Win_rate_by_count_and_by_value_can_disagree()
    {
        var options = Store();
        var user = new FakeUser(false, Guid.NewGuid(), CompanyA);
        await using (var db = new CrmDbContext(options, new FakeUser(true, null)))
        {
            await SeedStatusesAsync(db);
            // Three small wins, one big loss: good month by count, bad month by money.
            db.Tickets.Add(Priced(CompanyA, "A-1", WonStatus, 10_000m));
            db.Tickets.Add(Priced(CompanyA, "A-2", WonStatus, 10_000m));
            db.Tickets.Add(Priced(CompanyA, "A-3", WonStatus, 10_000m));
            db.Tickets.Add(Priced(CompanyA, "A-4", LostStatus, 300_000m));
            await db.SaveChangesAsync();
        }

        await using var read = new CrmDbContext(options, user);
        var r = (await Service(read, user, FullAccess).CompanyReportAsync(CompanyA, null, null)).Revenue!;

        r.WinRateByCount.Should().Be(0.75);
        r.WinRateByValue!.Value.Should().BeApproximately(0.0909, 0.0001);
    }

    // ---- domain guard ----

    [Theory]
    [InlineData(-1)]
    [InlineData(-0.01)]
    public void A_negative_amount_is_rejected(decimal amount)
    {
        var ticket = new Ticket(CompanyA, "A-1", Guid.NewGuid(), OpenStatus, "t", "b");

        // A loss is its value with a LOST outcome, never a negative amount — allowing both would
        // subtract the same loss twice, once by sign and once by classification.
        var act = () => ticket.SetValue(amount, null);

        act.Should().Throw<DomainException>().Where(e => e.Code == "ticket.value.negative");
    }
}
