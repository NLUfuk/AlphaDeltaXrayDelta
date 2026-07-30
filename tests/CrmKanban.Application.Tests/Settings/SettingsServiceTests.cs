using CrmKanban.Application.Abstractions;
using CrmKanban.Application.Common;
using CrmKanban.Application.Settings;
using CrmKanban.Domain.Entities;
using CrmKanban.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace CrmKanban.Application.Tests.Settings;

/// <summary>
/// Settings are the super-admin-managed global params (spec §13). Smoke coverage of the gate (global
/// settings = SuperAdmin only, §8), the "known key" update guard, audit, and the open read path used by
/// public consumers (KVKK text / branding).
/// </summary>
public class SettingsServiceTests
{
    private sealed class FakeUser(bool isSuperAdmin, Guid? userId) : ICurrentUserService
    {
        public Guid? UserId { get; } = userId;
        public bool IsAuthenticated => UserId is not null;
        public bool IsSuperAdmin { get; } = isSuperAdmin;
        public IReadOnlyCollection<Guid> CompanyIds => [];
    }

    private static DbContextOptions<CrmDbContext> Store() =>
        new DbContextOptionsBuilder<CrmDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString(),
                new Microsoft.EntityFrameworkCore.Storage.InMemoryDatabaseRoot()).Options;

    private static async Task SeedAsync(DbContextOptions<CrmDbContext> options)
    {
        await using var db = new CrmDbContext(options, new FakeUser(true, null));
        db.Settings.Add(new Setting("brand.system_name", "CRM Kanban", "string", "brand"));
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Non_super_admin_cannot_list_or_update()
    {
        var options = Store();
        await SeedAsync(options);
        await using var db = new CrmDbContext(options, new FakeUser(false, Guid.NewGuid()));
        var svc = new SettingsService(db, new FakeUser(false, Guid.NewGuid()));

        await ((Func<Task>)(() => svc.ListAsync(null))).Should().ThrowAsync<ForbiddenException>();
        await ((Func<Task>)(() => svc.UpdateAsync("brand.system_name", "x"))).Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task Super_admin_update_persists_the_value_and_audits()
    {
        var options = Store();
        await SeedAsync(options);
        var adminId = Guid.NewGuid();
        await using var db = new CrmDbContext(options, new FakeUser(true, adminId));

        await new SettingsService(db, new FakeUser(true, adminId)).UpdateAsync("brand.system_name", "Acme Desk");

        (await db.Settings.SingleAsync(s => s.Key == "brand.system_name")).Value.Should().Be("Acme Desk");
        (await db.AuditLogs.CountAsync(a => a.Action == "settings.update")).Should().Be(1);
    }

    [Fact]
    public async Task Updating_an_unknown_key_is_not_found()
    {
        var options = Store();
        await SeedAsync(options);
        await using var db = new CrmDbContext(options, new FakeUser(true, Guid.NewGuid()));
        var svc = new SettingsService(db, new FakeUser(true, Guid.NewGuid()));

        await ((Func<Task>)(() => svc.UpdateAsync("does.not.exist", "x"))).Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task GetValue_reads_without_a_super_admin_gate()
    {
        var options = Store();
        await SeedAsync(options);
        await using var db = new CrmDbContext(options, new FakeUser(false, null)); // anonymous public consumer
        var svc = new SettingsService(db, new FakeUser(false, null));

        (await svc.GetValueAsync("brand.system_name")).Should().Be("CRM Kanban");
        (await svc.GetValueAsync("missing")).Should().BeNull();
    }
}
