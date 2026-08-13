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
        // File. The list must stay in step with FileOptions.AllowedContentTypes, which is the fallback
        // when the row is missing — a narrower row silently rejects file types the app otherwise accepts.
        Make("file.max_size_mb", "10", "int", "file"),
        Make("file.max_per_comment", "5", "int", "file"),
        Make("file.allowed_types",
            "[\"image/png\",\"image/jpeg\",\"image/gif\",\"image/webp\",\"application/pdf\",\"text/plain\",\"application/msword\"," +
            "\"application/vnd.openxmlformats-officedocument.wordprocessingml.document\"," +
            "\"application/vnd.ms-excel\",\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet\"]",
            "json", "file"),
        // Form
        Make("form.kvkk_text",
            "Bu form aracılığıyla ilettiğiniz kişisel verileriniz, talebinizin değerlendirilmesi amacıyla 6698 sayılı KVKK kapsamında işlenmektedir.",
            "html", "form"),
        // Brand. Seeding only fills MISSING keys (DatabaseSeeder), so changing a default here never
        // overwrites what an operator edited — an existing database keeps its own name and logo.
        Make("brand.system_name", "Kanby", "string", "brand"),
        Make("brand.primary_color", "#2563eb", "color", "brand"),
        Make("brand.logo_url", "/kanby-logo.svg", "string", "brand"),
        // Finance (Faz 39). Single currency on purpose: multi-currency needs a historical rate table,
        // and converting at today's rate would silently rewrite last year's revenue.
        Make("finance.currency", "TRY", "string", "finance"),
        // System
        Make("system.timezone", "Europe/Istanbul", "string", "system"),
    ];

    /// <summary>
    /// Keys that were once seeded and must now be REMOVED from existing databases. Dropping a key from
    /// <see cref="All"/> is not enough: seeding only fills missing keys, so an old row would survive and
    /// keep showing an editable control that drives nothing.
    /// <para>A setting whose feature does not exist is worse than no setting: the super admin changes it,
    /// nothing happens, and the whole screen loses credibility. Each key below is retired rather than
    /// wired because wiring it means building the missing feature, not reading a row — see PROGRESS
    /// (teknik borç) for the feature each one is waiting on. They come back with their feature.</para>
    /// </summary>
    public static readonly string[] RetiredKeys =
    [
        // The limiter reads a constant in Program.cs. Honouring the row would be worse than removing it:
        // a rate limit is abuse protection, i.e. infra, and §13 keeps infra out of the runtime-editable
        // store precisely so nobody can widen it to 100000 from a web form.
        "form.rate_limit_per_minute",
        // Debounce is per-tick coalescing of identical (recipient, ticket, event) in NotificationService,
        // not a configurable time window. A real window needs a "recently sent" lookup per recipient.
        "notification.debounce_seconds",
        // No CAPTCHA provider is wired in v1 and the validator fails closed, so turning this on from the
        // web form would take every public intake form down. It returns when a provider does.
        "form.captcha_enabled",
        // No retention purge job exists; the number described a policy nothing enforced.
        "kvkk.retention_days",
        // The UI is Turkish-only (no i18n resources, FluentValidation culture pinned to "tr").
        "system.language",
    ];

    private static Setting Make(string key, string value, string type, string group) =>
        new(key, value, type, group, id: DeterministicId(key));

    // Deterministic Id from the key so re-seeding upserts by identity. SHA256 (not MD5/SHA1) keeps the
    // analyzer quiet; this is an identity derivation, not a security hash. First 16 bytes → a GUID.
    private static Guid DeterministicId(string key) =>
        new(SHA256.HashData(Encoding.UTF8.GetBytes("setting:" + key))[..16]);
}
