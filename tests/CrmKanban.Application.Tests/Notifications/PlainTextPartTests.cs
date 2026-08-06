using CrmKanban.Infrastructure.Email;
using FluentAssertions;

namespace CrmKanban.Application.Tests.Notifications;

/// <summary>
/// Every outbound mail carries a text/plain part alongside the HTML (spec §14 deliverability).
/// HTML-only bodies pick up SpamAssassin's MIME_HTML_ONLY + HTML_IMAGE_ONLY_16 once the relay adds
/// its tracking pixel, which pushes real templates near the spam threshold for every recipient
/// outside the operator's own domain. These assert the flattening keeps the parts a recipient
/// actually needs: the verification code, and where a button link points.
/// </summary>
public sealed class PlainTextPartTests
{
    [Fact]
    public void Flattens_paragraphs_and_keeps_the_code()
    {
        const string html = "<html><head><style>p{color:red}</style></head><body>" +
            "<p>Merhaba Ay&#351;e,</p>" +
            "<p><b>Ege Mermer</b> i&ccedil;in do&#287;rulama kodunuz:</p>" +
            "<p style=\"font-size:32px\">892558</p></body></html>";

        var text = SmtpEmailSender.HtmlToPlainText(html);

        text.Should().Contain("892558");
        text.Should().Contain("Merhaba Ayşe,");          // entities decoded
        text.Should().Contain("Ege Mermer için doğrulama kodunuz:");
        text.Should().NotContain("<");                    // no markup survives
        text.Should().NotContain("color:red");            // <style> content dropped, not flattened
    }

    [Fact]
    public void Keeps_the_destination_of_a_button_link()
    {
        const string html = "<p>Talebiniz güncellendi.</p>" +
            "<p><a href=\"https://crm.example/tickets/42\" style=\"padding:8px\">Talebi aç</a></p>";

        var text = SmtpEmailSender.HtmlToPlainText(html);

        // A text-only reader cannot follow markup — the URL has to survive as readable text.
        text.Should().Contain("https://crm.example/tickets/42");
        text.Should().Contain("Talebi aç");
    }

    [Fact]
    public void Collapses_the_whitespace_a_templated_body_leaves_behind()
    {
        const string html = "<p>Bir</p>\n\n\n   <p>   İki   </p><br><br><br><p>Üç</p>";

        var text = SmtpEmailSender.HtmlToPlainText(html);

        text.Should().Be("Bir\n\nİki\n\nÜç");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Empty_body_stays_empty(string html) =>
        SmtpEmailSender.HtmlToPlainText(html).Should().BeEmpty();
}
