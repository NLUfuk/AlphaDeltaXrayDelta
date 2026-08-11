using System.Globalization;
using System.Text.Json;
using CrmKanban.Application.Abstractions;
using CrmKanban.Application.Common;
using CrmKanban.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CrmKanban.Application.Settings;

/// <summary>
/// Reads and edits business parameters in the DB Settings store (spec §13). This is the single
/// read/write path for UI-editable params; secrets/infra stay in file/env and never pass through here.
/// <para>Every consumer reads through the typed getters below, so a value the super admin edits takes
/// effect on the next request. Each getter takes a fallback: a missing or unparseable row must never
/// take a feature down, and <see cref="UpdateAsync"/> validates on write so a fallback is the rare
/// path, not the normal one.</para>
/// </summary>
public sealed class SettingsService(IAppDbContext db, ICurrentUserService currentUser)
{
    /// <summary>
    /// Per-request snapshot of the whole store. The table is ~11 global rows and a single request can
    /// read half of them (the public form config reads four), so one scan per scope beats N keyed
    /// lookups — and unlike a process-wide cache it has no invalidation and no multi-instance staleness
    /// problem at all: the scope ends with the request. Settings are global (§13), so sharing the
    /// snapshot across tenants inside a request is correct, not a leak.
    /// </summary>
    private Dictionary<string, string>? _snapshot;

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

    /// <summary>The public brand triple (spec §13 "marka"). No auth gate — see <see cref="BrandDto"/>.
    /// The fallbacks live here rather than at each caller so the sign-in screen, the app shell and the
    /// public form can never disagree about what an empty logo or a missing name means.</summary>
    public async Task<BrandDto> GetBrandAsync(CancellationToken ct = default) =>
        new(await GetValueAsync("brand.system_name", ct) is { Length: > 0 } name ? name : "Kanby",
            await GetValueAsync("brand.primary_color", ct) is { Length: > 0 } color ? color : "#1f3bb3",
            await GetValueAsync("brand.logo_url", ct) is { Length: > 0 } logo ? logo : null);

    /// <summary>The stored value for a key, or null if the key is not configured.</summary>
    public async Task<string?> GetValueAsync(string key, CancellationToken ct = default) =>
        (await SnapshotAsync(ct)).GetValueOrDefault(key);

    /// <summary>A numeric business parameter. Non-positive is treated as unset: every int setting in the
    /// v1 catalog is a limit or a window, and a silent 0 would disable the feature rather than tune it.</summary>
    public async Task<int> GetIntAsync(string key, int fallback, CancellationToken ct = default) =>
        ParseInt(await GetValueAsync(key, ct)) ?? fallback;

    /// <summary>An enum-valued business parameter (e.g. the default ticket priority), case-insensitive.</summary>
    public async Task<T> GetEnumAsync<T>(string key, T fallback, CancellationToken ct = default)
        where T : struct, Enum =>
        Enum.TryParse<T>(await GetValueAsync(key, ct), ignoreCase: true, out var parsed) ? parsed : fallback;

    /// <summary>A JSON string-array business parameter (e.g. the allowed upload content types).</summary>
    public async Task<string[]> GetStringArrayAsync(string key, string[] fallback, CancellationToken ct = default) =>
        ParseStringArray(await GetValueAsync(key, ct)) ?? fallback;

    /// <summary>The priority a new ticket starts at when the opener did not pick one (spec §13). Named
    /// rather than left as a literal at each call site because both intake paths — staff create and the
    /// customer/public form — must resolve it identically; a typo in one key string would silently pin
    /// that path to Normal forever.</summary>
    public Task<Domain.Enums.Priority> DefaultTicketPriorityAsync(CancellationToken ct = default) =>
        GetEnumAsync("ticket.default_priority", Domain.Enums.Priority.Normal, ct);

    /// <summary>Updates a seeded setting's value. 404 if the key is unknown (v1 keys ship as seed rows,
    /// so this never creates arbitrary keys — a spam/typo guard, §18.3). The value is validated against
    /// the row's declared Type: the settings screen is a free-text box, and an unparseable value would
    /// otherwise be accepted, stored, and then silently ignored by the consumer. Audited (spec §11).</summary>
    public async Task UpdateAsync(string key, string value, CancellationToken ct = default)
    {
        RequireSuperAdmin();
        var actorId = currentUser.UserId
            ?? throw new UnauthorizedException("auth.required", "Authentication required.");

        var setting = await db.Settings.FirstOrDefaultAsync(s => s.Key == key, ct)
            ?? throw new NotFoundException("setting.unknown", $"Unknown setting '{key}'.");

        value = value.Trim();
        EnsureValid(setting.Type, value);

        setting.Update(value, actorId);
        db.AuditLogs.Add(new AuditLog(actorId, "settings.update", $"{key} = {value}"));
        await db.SaveChangesAsync(ct);
        _snapshot = null; // a reader later in this same request must see the new value
    }

    private async Task<Dictionary<string, string>> SnapshotAsync(CancellationToken ct) =>
        _snapshot ??= await db.Settings.ToDictionaryAsync(s => s.Key, s => s.Value, ct);

    /// <summary>Reject a value the declared Type cannot hold. Only the types that carry a machine-read
    /// format are checked — free text (string/html) has no format to violate, and an empty logo url or
    /// KVKK text is a meaningful "none", not an error. One code per format rather than a single
    /// setting.invalid_value: the SPA's catalog is what turns a code into user-facing Turkish, so a
    /// shared code could only ever say "invalid" and the operator would be left guessing which shape
    /// the box wanted.</summary>
    private static void EnsureValid(string type, string value)
    {
        switch (type)
        {
            case "int" when ParseInt(value) is null:
                throw new BadRequestException("setting.invalid_int", "Value must be a positive integer.");
            // Every json setting in the v1 catalog is a string array (file.allowed_types).
            case "json" when ParseStringArray(value) is null:
                throw new BadRequestException("setting.invalid_json", "Value must be a JSON array of strings.");
            case "color" when !IsHexColor(value):
                throw new BadRequestException("setting.invalid_color", "Value must be a #rrggbb colour.");
        }
    }

    private static int? ParseInt(string? value) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) && parsed > 0
            ? parsed : null;

    private static string[]? ParseStringArray(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        try
        {
            return JsonSerializer.Deserialize<string[]>(value) is { Length: > 0 } items
                && Array.TrueForAll(items, i => !string.IsNullOrWhiteSpace(i))
                ? items : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool IsHexColor(string value) =>
        value.Length is 4 or 7 && value[0] == '#' && value.Skip(1).All(char.IsAsciiHexDigit);

    // v1 settings are global (spec §13 list) → SuperAdmin only (spec §8). The settings.manage permission
    // key exists for a future per-company settings surface (§9 seam), not wired in v1.
    private void RequireSuperAdmin()
    {
        if (!currentUser.IsSuperAdmin)
            throw new ForbiddenException("settings.forbidden", "Only a super admin manages global settings.");
    }
}
