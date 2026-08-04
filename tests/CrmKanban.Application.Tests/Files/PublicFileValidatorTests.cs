using System.Text;
using CrmKanban.Application.Common;
using CrmKanban.Application.Files;
using FluentAssertions;

namespace CrmKanban.Application.Tests.Files;

/// <summary>
/// Zero-trust public upload validation (spec §10): a customer may submit ONLY pdf/txt/doc/docx and
/// png/jpg/webp images, and the
/// real bytes must back the extension. Pure, so tested directly. This is a trust boundary — every case
/// here is a way a malicious client tries to smuggle a disallowed file.
/// </summary>
public class PublicFileValidatorTests
{
    private const long Max = 10 * 1024 * 1024;

    private static readonly byte[] Pdf = "%PDF-1.7\n%âãÏÓ\n"u8.ToArray();
    private static readonly byte[] Docx = [0x50, 0x4B, 0x03, 0x04, 0x14, 0x00];       // ZIP local header
    private static readonly byte[] Doc = [0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1]; // OLE2
    private static readonly byte[] Text = "Merhaba, bu bir metin dosyası.\n"u8.ToArray();
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D];
    private static readonly byte[] Jpg = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46];
    private static readonly byte[] Webp = [0x52, 0x49, 0x46, 0x46, 0x24, 0x00, 0x00, 0x00, 0x57, 0x45, 0x42, 0x50];

    [Theory]
    [InlineData("teklif.pdf", "application/pdf")]
    [InlineData("sozlesme.docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document")]
    [InlineData("eski.doc", "application/msword")]
    [InlineData("not.txt", "text/plain")]
    public void Accepts_real_documents(string name, string contentType)
    {
        var bytes = name.EndsWith(".pdf", StringComparison.Ordinal) ? Pdf
            : name.EndsWith(".docx", StringComparison.Ordinal) ? Docx
            : name.EndsWith(".doc", StringComparison.Ordinal) ? Doc : Text;
        var act = () => PublicFileValidator.Validate(name, contentType, bytes, bytes.Length, Max);
        act.Should().NotThrow();
    }

    [Theory]
    [InlineData("ariza.png", "image/png")]
    [InlineData("urun.jpg", "image/jpeg")]
    [InlineData("urun.jpeg", "image/jpeg")]
    [InlineData("etiket.webp", "image/webp")]
    public void Accepts_real_images(string name, string contentType)
    {
        var bytes = name.EndsWith(".png", StringComparison.Ordinal) ? Png
            : name.EndsWith(".webp", StringComparison.Ordinal) ? Webp : Jpg;
        var act = () => PublicFileValidator.Validate(name, contentType, bytes, bytes.Length, Max);
        act.Should().NotThrow();
    }

    [Fact]
    public void Rejects_a_disallowed_extension()
    {
        // SVG is an image to a user and a script host to a browser — it stays out of the allow-list.
        var svg = "<svg xmlns=\"http://www.w3.org/2000/svg\"><script/></svg>"u8.ToArray();
        var act = () => PublicFileValidator.Validate("logo.svg", "image/svg+xml", svg, svg.Length, Max);
        act.Should().Throw<BadRequestException>().Which.Code.Should().Be("attachment.type_not_allowed");
    }

    [Fact]
    public void Rejects_an_executable_renamed_to_png()
    {
        var exe = new byte[] { 0x4D, 0x5A, 0x90, 0x00 }; // "MZ" — Windows PE
        var act = () => PublicFileValidator.Validate("photo.png", "image/png", exe, exe.Length, Max);
        act.Should().Throw<BadRequestException>().Which.Code.Should().Be("attachment.content_mismatch");
    }

    [Fact]
    public void Rejects_a_file_whose_bytes_do_not_match_its_pdf_extension()
    {
        var notPdf = Encoding.UTF8.GetBytes("this is not really a pdf");
        var act = () => PublicFileValidator.Validate("evil.pdf", "application/pdf", notPdf, notPdf.Length, Max);
        act.Should().Throw<BadRequestException>().Which.Code.Should().Be("attachment.content_mismatch");
    }

    [Fact]
    public void Rejects_a_binary_masquerading_as_txt()
    {
        var binary = new byte[] { (byte)'M', (byte)'Z', 0x00, 0x01, 0x02 }; // NUL byte → not text
        var act = () => PublicFileValidator.Validate("payload.txt", "text/plain", binary, binary.Length, Max);
        act.Should().Throw<BadRequestException>().Which.Code.Should().Be("attachment.content_mismatch");
    }

    [Fact]
    public void Rejects_a_content_type_that_contradicts_the_extension()
    {
        var act = () => PublicFileValidator.Validate("doc.pdf", "text/plain", Pdf, Pdf.Length, Max);
        act.Should().Throw<BadRequestException>().Which.Code.Should().Be("attachment.type_mismatch");
    }

    [Fact]
    public void Rejects_an_oversized_file()
    {
        var act = () => PublicFileValidator.Validate("big.pdf", "application/pdf", Pdf, Max + 1, Max);
        act.Should().Throw<BadRequestException>().Which.Code.Should().Be("attachment.too_large");
    }

    [Fact]
    public void Rejects_an_empty_file()
    {
        var act = () => PublicFileValidator.Validate("empty.pdf", "application/pdf", Pdf, 0, Max);
        act.Should().Throw<BadRequestException>().Which.Code.Should().Be("attachment.too_large");
    }
}
