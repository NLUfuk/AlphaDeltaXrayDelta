using CrmKanban.Domain.Entities;

namespace CrmKanban.Infrastructure.Persistence.Seed;

/// <summary>
/// v1 default email templates (spec §14). Keys match the notification matrix; bodies use
/// {{placeholder}} tokens filled from the queued message payload (ticketNumber, title, newValue…).
/// Super-admin editable in Faz 6; seeded idempotently by stable Id.
/// </summary>
public static class DefaultEmailTemplates
{
    public static IReadOnlyList<EmailTemplate> All { get; } =
    [
        T("11111111-0000-0000-0000-000000000001", "ticket_created",
            "Talebiniz alındı: {{ticketNumber}}",
            "<p>Merhaba,</p><p><b>{{title}}</b> başlıklı talebiniz <b>{{ticketNumber}}</b> numarasıyla oluşturuldu.</p>"),

        T("11111111-0000-0000-0000-000000000002", "ticket_status_changed",
            "{{ticketNumber}} durumu güncellendi",
            "<p><b>{{ticketNumber}}</b> ({{title}}) durumu <b>{{newValue}}</b> olarak güncellendi.</p>"),

        T("11111111-0000-0000-0000-000000000003", "ticket_reopened",
            "{{ticketNumber}} yeniden açıldı",
            "<p><b>{{ticketNumber}}</b> ({{title}}) yeniden açıldı: <b>{{newValue}}</b>.</p>"),

        T("11111111-0000-0000-0000-000000000004", "ticket_comment_added",
            "{{ticketNumber}} için yeni yorum",
            "<p><b>{{ticketNumber}}</b> ({{title}}) talebine yeni bir yorum eklendi.</p>"),

        T("11111111-0000-0000-0000-000000000005", "ticket_internal_note_added",
            "{{ticketNumber}} için iç not",
            "<p><b>{{ticketNumber}}</b> ({{title}}) talebine bir iç not eklendi.</p>"),

        T("11111111-0000-0000-0000-000000000006", "ticket_assigned",
            "{{ticketNumber}} size atandı",
            "<p><b>{{ticketNumber}}</b> ({{title}}) talebi size atandı.</p>"),

        // Account activation for a first-time customer who submitted the public form (spec §9). Clicking
        // the link proves they own the address (email verification) and lets them set a password.
        T("11111111-0000-0000-0000-000000000007", "account_invite",
            "{{companyName}} — hesabınızı etkinleştirin",
            "<p>Merhaba {{name}},</p><p><b>{{companyName}}</b> için talebiniz alındı. Taleplerinizi takip edip yanıt yazabilmeniz için hesabınızı etkinleştirin ve bir parola belirleyin:</p><p><a href=\"{{link}}\">Hesabımı etkinleştir</a></p><p style=\"color:#64748b;font-size:12px\">Bağlantı çalışmazsa tarayıcınıza yapıştırın: {{link}}</p>"),

        // Self-service registration: verify the address + set a password via the one-time link (spec §18.5).
        T("11111111-0000-0000-0000-000000000009", "account_verify",
            "Hesabınızı etkinleştirin",
            "<p>Merhaba {{name}},</p><p>Kaydınızı tamamlamak, e-posta adresinizi doğrulamak ve bir parola belirlemek için:</p><p><a href=\"{{link}}\">Hesabımı etkinleştir</a></p><p style=\"color:#64748b;font-size:12px\">Bağlantı çalışmazsa tarayıcınıza yapıştırın: {{link}}</p>"),

        // Staff invitation — same one-time link, activates the account and sets a password (spec §9).
        T("11111111-0000-0000-0000-000000000008", "staff_invite",
            "{{companyName}} ekibine davet edildiniz",
            "<p>Merhaba {{name}},</p><p><b>{{companyName}}</b> ekibine davet edildiniz. Parolanızı belirleyip hesabınızı etkinleştirmek için:</p><p><a href=\"{{link}}\">Daveti kabul et</a></p><p style=\"color:#64748b;font-size:12px\">Bağlantı çalışmazsa tarayıcınıza yapıştırın: {{link}}</p>"),
    ];

    private static EmailTemplate T(string id, string key, string subject, string body) =>
        new(key, subject, body, isActive: true, id: Guid.Parse(id));
}
