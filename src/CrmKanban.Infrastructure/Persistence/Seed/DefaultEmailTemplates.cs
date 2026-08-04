using CrmKanban.Domain.Entities;

namespace CrmKanban.Infrastructure.Persistence.Seed;

/// <summary>
/// v1 default email templates (spec §14). Keys match the notification matrix; bodies use
/// {{placeholder}} tokens filled from the queued message payload. Available tokens:
///  - ticket events: ticketNumber, title, newValue, oldValue, change (what happened, Turkish), link
///  - account emails: name, companyName, link (one-time activation/reset link), code + minutes (sign-in code)
/// Ticket mails come in two voices: the customer-worded ones below and the single staff-worded
/// ticket_staff_update; NotificationService picks per recipient (member of the company or not).
/// Only these render — any other {{token}} is left literal, so never introduce one that isn't in the
/// payload. Super-admin editable (Faz 15 /admin/templates); seeded insert-if-missing by Key, so edits
/// are never overwritten on restart.
/// </summary>
public static class DefaultEmailTemplates
{
    public static IReadOnlyList<EmailTemplate> All { get; } =
    [
        T("11111111-0000-0000-0000-000000000001", "ticket_created",
            "Talebiniz alındı — {{ticketNumber}}",
            Compose("<p>Merhaba,</p><p><b>{{title}}</b> başlıklı talebinizi aldık; kaydınız <b>{{ticketNumber}}</b> numarasıyla oluşturuldu. Ekibimiz en kısa sürede ilgilenecek.</p>",
                "Talebi görüntüle")),

        T("11111111-0000-0000-0000-000000000002", "ticket_status_changed",
            "{{ticketNumber}} numaralı talebiniz: {{newValue}}",
            Compose("<p>Merhaba,</p><p><b>{{ticketNumber}}</b> numaralı <b>{{title}}</b> başlıklı talebiniz <b>{{newValue}}</b> durumuna alınmıştır.</p>",
                "Talebi görüntüle")),

        T("11111111-0000-0000-0000-000000000003", "ticket_reopened",
            "Talebiniz yeniden açıldı — {{ticketNumber}}",
            Compose("<p>Merhaba,</p><p><b>{{ticketNumber}}</b> — <b>{{title}}</b> talebiniz yeniden açıldı ve tekrar işleme alındı.</p>",
                "Talebi görüntüle")),

        T("11111111-0000-0000-0000-000000000004", "ticket_comment_added",
            "Talebinize yeni bir yanıt var — {{ticketNumber}}",
            Compose("<p>Merhaba,</p><p><b>{{ticketNumber}}</b> — <b>{{title}}</b> talebinize yeni bir yanıt yazıldı.</p>",
                "Yanıtı görüntüle")),

        T("11111111-0000-0000-0000-000000000005", "ticket_internal_note_added",
            "İç not eklendi — {{ticketNumber}}",
            Compose("<p>Merhaba,</p><p><b>{{ticketNumber}}</b> — <b>{{title}}</b> talebine bir <b>iç not</b> eklendi. (Bu not yalnızca ekip tarafından görülür.)</p>",
                "Talebi görüntüle")),

        T("11111111-0000-0000-0000-000000000006", "ticket_assigned",
            "Bir talep size atandı — {{ticketNumber}}",
            Compose("<p>Merhaba,</p><p><b>{{ticketNumber}}</b> — <b>{{title}}</b> talebi size atandı. İlgilenmeniz bekleniyor.</p>",
                "Talebi görüntüle")),

        T("11111111-0000-0000-0000-000000000011", "ticket_approved",
            "Talebiniz işleme alındı — {{ticketNumber}}",
            Compose("<p>Merhaba,</p><p><b>{{ticketNumber}}</b> — <b>{{title}}</b> talebiniz onaylandı ve işleme alındı. En kısa sürede sizinle ilgileneceğiz.</p>",
                "Talebi görüntüle")),

        T("11111111-0000-0000-0000-000000000012", "ticket_rejected",
            "Talebiniz hakkında — {{ticketNumber}}",
            Compose("<p>Merhaba,</p><p>Maalesef <b>{{ticketNumber}}</b> — <b>{{title}}</b> talebinizi bu aşamada işleme alamadık. Sorularınız için bize ulaşabilir veya yeni bir talep oluşturabilirsiniz.</p>",
                "Talebi görüntüle")),

        T("11111111-0000-0000-0000-000000000013", "ticket_attachment_added",
            "Talebinize yeni bir dosya eklendi — {{ticketNumber}}",
            Compose("<p>Merhaba,</p><p><b>{{ticketNumber}}</b> — <b>{{title}}</b> talebine yeni bir dosya eklendi.</p>",
                "Dosyayı görüntüle")),

        T("11111111-0000-0000-0000-000000000014", "ticket_edited",
            "Talebiniz güncellendi — {{ticketNumber}}",
            Compose("<p>Merhaba,</p><p><b>{{ticketNumber}}</b> — <b>{{title}}</b> talebinin başlığı veya içeriği güncellendi.</p>",
                "Talebi görüntüle")),

        // Staff-facing counterpart of every ticket mail above: one generic "something changed here"
        // notice for people who work at the company (the customer-worded templates would read wrong).
        T("11111111-0000-0000-0000-000000000016", "ticket_staff_update",
            "{{ticketNumber}} — {{change}}",
            Compose("<p>Merhaba,</p><p><b>{{ticketNumber}}</b> numaralı <b>{{title}}</b> kaydınızda güncelleme var: <b>{{change}}</b>. Lütfen göz atın.</p>",
                "Kaydı görüntüle")),

        // --- Account lifecycle (one-time link) ---

        // First-time customer who submitted the public form (spec §9): clicking the link verifies the
        // address and lets them set a password.
        T("11111111-0000-0000-0000-000000000007", "account_invite",
            "{{companyName}} — hesabınızı etkinleştirin",
            Account("<p>Merhaba {{name}},</p><p><b>{{companyName}}</b> firmasına gönderdiğiniz talep alındı. Taleplerinizi takip edip yanıtlayabilmeniz için hesabınızı etkinleştirin ve bir parola belirleyin.</p>",
                "Hesabımı etkinleştir")),

        // Self-service registration (spec §18.5).
        T("11111111-0000-0000-0000-000000000009", "account_verify",
            "Hesabınızı etkinleştirin",
            Account("<p>Merhaba {{name}},</p><p>Kaydınızı tamamlamak için e-posta adresinizi doğrulayın ve bir parola belirleyin.</p>",
                "Hesabımı etkinleştir")),

        // Password reset (spec §1.12) — same set-password page; accepting also revokes existing sessions.
        T("11111111-0000-0000-0000-000000000010", "password_reset",
            "Parolanızı sıfırlayın",
            Account("<p>Merhaba {{name}},</p><p>Parolanızı sıfırlamak için aşağıdaki butonu kullanın. Bu isteği siz yapmadıysanız bu e-postayı yok sayabilirsiniz; parolanız değişmez.</p>",
                "Parolamı sıfırla")),

        // Customer sign-up on a company's sign-in page (/c/{slug}): a typed code, not a link.
        T("11111111-0000-0000-0000-000000000015", "account_code",
            "{{companyName}} — doğrulama kodunuz: {{code}}",
            "<p>Merhaba {{name}},</p><p><b>{{companyName}}</b> için kaydınızı tamamlamak üzere aşağıdaki doğrulama kodunu giriş ekranına yazın.</p>" +
            "<p style=\"margin:24px 0;font-size:32px;font-weight:700;letter-spacing:8px;color:#1f3bb3\">{{code}}</p>" +
            "<p style=\"color:#94a3b8;font-size:12px\">Kod {{minutes}} dakika geçerlidir. Bu isteği siz yapmadıysanız bu e-postayı yok sayabilirsiniz.</p>" +
            Footer),

        // Staff invitation (spec §9).
        T("11111111-0000-0000-0000-000000000008", "staff_invite",
            "{{companyName}} ekibine davet edildiniz",
            Account("<p>Merhaba {{name}},</p><p><b>{{companyName}}</b> ekibine davet edildiniz. Hesabınızı etkinleştirip parolanızı belirlemek için aşağıdaki butonu kullanın.</p>",
                "Daveti kabul et")),
    ];

    private const string Footer =
        "<p style=\"margin-top:24px;color:#94a3b8;font-size:12px\">Bu otomatik bir bildirimdir; lütfen bu e-postayı yanıtlamayın.</p>";

    private static string Btn(string text) =>
        $"<p style=\"margin:20px 0\"><a href=\"{{{{link}}}}\" style=\"display:inline-block;background:#4f46e5;color:#ffffff;text-decoration:none;padding:10px 18px;border-radius:8px;font-weight:600\">{text}</a></p>";

    /// <summary>Ticket/notification body: content + a "view" button (deep link) + footer.</summary>
    private static string Compose(string inner, string buttonText) =>
        inner + Btn(buttonText) + Footer;

    /// <summary>Account-action body: content + action button + a plain-text fallback link (these are the
    /// critical one-time links, so give a copy-paste fallback for clients that strip the button).</summary>
    private static string Account(string inner, string buttonText) =>
        inner + Btn(buttonText) +
        "<p style=\"color:#94a3b8;font-size:12px\">Buton çalışmazsa bu bağlantıyı tarayıcınıza yapıştırın:<br>{{link}}</p>" +
        Footer;

    private static EmailTemplate T(string id, string key, string subject, string body) =>
        new(key, subject, body, isActive: true, id: Guid.Parse(id));
}
