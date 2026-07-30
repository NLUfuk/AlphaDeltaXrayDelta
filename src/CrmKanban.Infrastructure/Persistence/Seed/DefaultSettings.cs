using System.Security.Cryptography;
using System.Text;
using CrmKanban.Domain.Entities;

namespace CrmKanban.Infrastructure.Persistence.Seed;

/// <summary>
/// The v1 business-parameter set (spec §13 table). Seeded idempotently — the store is generic, so a new
/// setting is just another row here, not a schema/code change (§18.3). Ids are derived from the Key so
/// re-seeding upserts by identity without duplicating. Values are strings; complex ones are JSON.
/// Infra/secrets (DB/S3/SMTP/JWT) are deliberately absent — they live in file/env only (§13).
/// </summary>
public static class DefaultSettings
{
    public static IEnumerable<Setting> All =>
    [
        // Ticket
        Make("ticket.reopen_window_days", "7", "int", "ticket"),
        Make("ticket.default_priority", "Normal", "string", "ticket"),
        // Notification
        Make("notification.debounce_seconds", "60", "int", "notification"),
        // File
        Make("file.max_size_mb", "10", "int", "file"),
        Make("file.max_per_comment", "5", "int", "file"),
        Make("file.allowed_types",
            "[\"image/png\",\"image/jpeg\",\"image/gif\",\"application/pdf\",\"application/msword\",\"application/vnd.openxmlformats-officedocument.wordprocessingml.document\"]",
            "json", "file"),
        // Form
        Make("form.captcha_enabled", "false", "bool", "form"),
        Make("form.rate_limit_per_minute", "5", "int", "form"),
        Make("form.kvkk_text",
            "Bu form aracılığıyla ilettiğiniz kişisel verileriniz, talebinizin değerlendirilmesi amacıyla 6698 sayılı KVKK kapsamında işlenmektedir.",
            "html", "form"),
        // Brand
        Make("brand.system_name", "CRM Kanban", "string", "brand"),
        Make("brand.primary_color", "#2563eb", "color", "brand"),
        Make("brand.logo_url", "", "string", "brand"),
        // System
        Make("system.timezone", "Europe/Istanbul", "string", "system"),
        Make("system.language", "tr", "string", "system"),
        // KVKK
        Make("kvkk.retention_days", "365", "int", "kvkk"),
    ];

    private static Setting Make(string key, string value, string type, string group) =>
        new(key, value, type, group, id: DeterministicId(key));

    // Deterministic Id from the key so re-seeding upserts by identity. SHA256 (not MD5/SHA1) keeps the
    // analyzer quiet; this is an identity derivation, not a security hash. First 16 bytes → a GUID.
    private static Guid DeterministicId(string key) =>
        new(SHA256.HashData(Encoding.UTF8.GetBytes("setting:" + key))[..16]);
}
