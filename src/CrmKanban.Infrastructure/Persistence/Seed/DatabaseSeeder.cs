using CrmKanban.Domain.Entities;
using CrmKanban.Domain.Enums;
using CrmKanban.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CrmKanban.Infrastructure.Persistence.Seed;

public sealed record SeederOptions(string? SuperAdminEmail, string? SuperAdminPassword);

/// <summary>
/// Idempotent seed (spec §17 Faz 1): permissions, the role→permission matrix, the global default
/// status set + transitions, and the first super admin. Safe to run on every startup — every step
/// upserts by a stable key/Id. Uses a SuperAdmin-scoped context so the tenant filter never hides
/// global rows during existence checks.
/// </summary>
public sealed class DatabaseSeeder(
    DbContextOptions<CrmDbContext> options,
    PasswordHasher<User> passwordHasher,
    SeederOptions seederOptions,
    ILogger<DatabaseSeeder> logger)
{
    public async Task SeedAsync(CancellationToken ct = default)
    {
        await using var db = new CrmDbContext(options, SystemCurrentUserService.System);

        await SeedPermissionsAsync(db, ct);
        await SeedRolePermissionsAsync(db, ct);
        await SeedStatusesAsync(db, ct);
        await SeedTransitionsAsync(db, ct);
        await SeedEmailTemplatesAsync(db, ct);
        await SeedSettingsAsync(db, ct);
        await SeedSuperAdminAsync(db, ct);

        await db.SaveChangesAsync(ct);
    }

    private static async Task SeedPermissionsAsync(CrmDbContext db, CancellationToken ct)
    {
        var existing = await db.Permissions.Select(p => p.Key).ToListAsync(ct);
        foreach (var key in Domain.Authorization.PermissionKeys.All.Except(existing))
            db.Permissions.Add(new Permission(key));
    }

    private static async Task SeedRolePermissionsAsync(CrmDbContext db, CancellationToken ct)
    {
        // Needs permission Ids; flush pending permission inserts first.
        await db.SaveChangesAsync(ct);
        var permByKey = await db.Permissions.ToDictionaryAsync(p => p.Key, p => p.Id, ct);
        var existing = await db.RolePermissions
            .Select(rp => new { rp.Role, rp.PermissionId })
            .ToListAsync(ct);
        var have = existing.Select(x => (x.Role, x.PermissionId)).ToHashSet();

        foreach (var (role, keys) in RolePermissionMatrix.Defaults)
            foreach (var key in keys)
                if (permByKey.TryGetValue(key, out var pid) && have.Add((role, pid)))
                    db.RolePermissions.Add(new RolePermission(role, pid));
    }

    private static async Task SeedStatusesAsync(CrmDbContext db, CancellationToken ct)
    {
        var existingIds = await db.TicketStatuses.Select(s => s.Id).ToListAsync(ct);
        var have = existingIds.ToHashSet();
        foreach (var s in DefaultStatuses.All.Where(s => !have.Contains(s.Id)))
            db.TicketStatuses.Add(new TicketStatus(s.Name, s.Category, s.Color, s.Order, s.IsTerminal, companyId: null, id: s.Id));
    }

    private static async Task SeedTransitionsAsync(CrmDbContext db, CancellationToken ct)
    {
        await db.SaveChangesAsync(ct);
        var existing = await db.StatusTransitions
            .Select(t => new { t.FromStatusId, t.ToStatusId })
            .ToListAsync(ct);
        var have = existing.Select(x => (x.FromStatusId, x.ToStatusId)).ToHashSet();

        foreach (var (from, to) in DefaultStatuses.Transitions())
            if (have.Add((from, to)))
                db.StatusTransitions.Add(new StatusTransition(from, to, Domain.Authorization.PermissionKeys.TicketStatusChange));
    }

    private static async Task SeedEmailTemplatesAsync(CrmDbContext db, CancellationToken ct)
    {
        var existingKeys = await db.EmailTemplates.Select(t => t.Key).ToListAsync(ct);
        var have = existingKeys.ToHashSet();
        foreach (var t in DefaultEmailTemplates.All.Where(t => !have.Contains(t.Key)))
            db.EmailTemplates.Add(t);
    }

    private static async Task SeedSettingsAsync(CrmDbContext db, CancellationToken ct)
    {
        var existingKeys = await db.Settings.Select(s => s.Key).ToListAsync(ct);
        var have = existingKeys.ToHashSet();
        foreach (var s in DefaultSettings.All.Where(s => !have.Contains(s.Key)))
            db.Settings.Add(s);
    }

    private async Task SeedSuperAdminAsync(CrmDbContext db, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(seederOptions.SuperAdminEmail) ||
            string.IsNullOrWhiteSpace(seederOptions.SuperAdminPassword))
        {
            logger.LogWarning("Super admin seed skipped: SuperAdmin email/password not configured (set via env/user-secrets).");
            return;
        }

        var email = seederOptions.SuperAdminEmail.Trim().ToLowerInvariant();
        var existing = await db.Users.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.Email == email, ct);
        if (existing is not null)
        {
            // Idempotent upgrade: ensure the flag is set even if the row predates the IsSuperAdmin column.
            if (!existing.IsSuperAdmin) existing.PromoteToSuperAdmin();
            return;
        }

        var admin = new User(email, "Super", "Admin");
        admin.PromoteToSuperAdmin();
        admin.SetPasswordHash(passwordHasher.HashPassword(admin, seederOptions.SuperAdminPassword), mustChange: true);
        db.Users.Add(admin);
        logger.LogInformation("Seeded initial super admin {Email} (must change password on first login).", email);
    }
}
