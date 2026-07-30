using CrmKanban.Application.Abstractions;
using CrmKanban.Application.Common;
using CrmKanban.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CrmKanban.Application.Settings;

/// <summary>
/// Reads and edits business parameters in the DB Settings store (spec §13). This is the single
/// read/write path for UI-editable params; secrets/infra stay in file/env and never pass through here.
/// Reads are unique-key lookups (indexed) — no cache is added until a hot per-request consumer proves
/// the need (SCOPE DISCIPLINE). Multi-instance cache invalidation would be that cache's problem.
/// ponytail: DB read per lookup; add a cache when a hot reader appears.
/// </summary>
public sealed class SettingsService(IAppDbContext db, ICurrentUserService currentUser)
{
    public async Task<IReadOnlyList<SettingDto>> ListAsync(string? group, CancellationToken ct = default)
    {
        RequireSuperAdmin();
        var q = db.Settings.AsQueryable();
        if (!string.IsNullOrWhiteSpace(group))
            q = q.Where(s => s.Group == group);
        return await q.OrderBy(s => s.Group).ThenBy(s => s.Key)
            .Select(s => new SettingDto(s.Key, s.Value, s.Type, s.Group, s.UpdatedAt))
            .ToListAsync(ct);
    }

    /// <summary>The stored value for a key, or null if the key is not configured. The single read path
    /// for consumers (e.g. KVKK text, branding).</summary>
    public async Task<string?> GetValueAsync(string key, CancellationToken ct = default) =>
        await db.Settings.Where(s => s.Key == key).Select(s => s.Value).FirstOrDefaultAsync(ct);

    /// <summary>Updates a seeded setting's value. 404 if the key is unknown (v1 keys ship as seed rows,
    /// so this never creates arbitrary keys — a spam/typo guard, §18.3). Audited (spec §11).</summary>
    public async Task UpdateAsync(string key, string value, CancellationToken ct = default)
    {
        RequireSuperAdmin();
        var actorId = currentUser.UserId
            ?? throw new UnauthorizedException("auth.required", "Authentication required.");

        var setting = await db.Settings.FirstOrDefaultAsync(s => s.Key == key, ct)
            ?? throw new NotFoundException("setting.unknown", $"Unknown setting '{key}'.");

        setting.Update(value, actorId);
        db.AuditLogs.Add(new AuditLog(actorId, "settings.update", $"{key} = {value}"));
        await db.SaveChangesAsync(ct);
    }

    // v1 settings are global (spec §13 list) → SuperAdmin only (spec §8). The settings.manage permission
    // key exists for a future per-company settings surface (§9 seam), not wired in v1.
    private void RequireSuperAdmin()
    {
        if (!currentUser.IsSuperAdmin)
            throw new ForbiddenException("settings.forbidden", "Only a super admin manages global settings.");
    }
}
